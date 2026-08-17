using System;
using Arcomage.Core;
using Arcomage.Gameplay;
using Arcomage.Networking;
using Godot;

namespace Arcomage.UI;

public partial class NetworkSetup
{
   private OptionButton _modeSelect;
   private LineEdit _roomCode;
   private Label _onlineStatus;
   private CheckBox _rankedCheck;
   private bool _usingNakama;
   private int _maxPlayers = 2;
   private MatchMode _mode = MatchMode.OneVsOne;

   private void BuildOnlineUi()
   {
      var box = MultiplayerConfigUi;
      box.AddChild(new HSeparator());

      _modeSelect = new OptionButton { Name = "MatchMode" };
      _modeSelect.AddItem(Tr("MODE_1V1"), (int)MatchMode.OneVsOne);
      _modeSelect.AddItem(Tr("MODE_FFA"), (int)MatchMode.FreeForAll);
      _modeSelect.AddItem(Tr("MODE_2V2"), (int)MatchMode.TwoVsTwo);
      _modeSelect.ItemSelected += OnModeSelected;
      box.AddChild(_modeSelect);

      _rankedCheck = new CheckBox { Text = Tr("RANKED") };
      box.AddChild(_rankedCheck);

      var find = new Button { Text = Tr("FIND_MATCH") };
      find.Pressed += OnFindMatchPressed;
      box.AddChild(find);

      _roomCode = new LineEdit { PlaceholderText = Tr("ROOM_CODE") };
      box.AddChild(_roomCode);

      var roomRow = new HBoxContainer();
      var createRoom = new Button { Text = Tr("CREATE_ROOM"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
      createRoom.Pressed += OnCreateRoomPressed;
      var joinRoom = new Button { Text = Tr("JOIN_ROOM"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
      joinRoom.Pressed += OnJoinRoomPressed;
      roomRow.AddChild(createRoom);
      roomRow.AddChild(joinRoom);
      box.AddChild(roomRow);

      _onlineStatus = new Label
      {
         Text = string.Empty,
         HorizontalAlignment = HorizontalAlignment.Center,
         AutowrapMode = TextServer.AutowrapMode.WordSmart
      };
      box.AddChild(_onlineStatus);

      if (Global.Online != null)
      {
         Global.Online.StatusChanged += OnOnlineStatusChanged;
         Global.Online.MatchReady += OnNakamaMatchReady;
         Global.Online.PeersChanged += OnNakamaPeersChanged;
         Global.Online.MatchLeft += OnNakamaMatchLeft;
      }
   }

   private void OnModeSelected(long index)
   {
      _mode = (MatchMode)_modeSelect.GetItemId((int)index);
      _maxPlayers = MatchModeRules.MaxPlayers(_mode);
      Global.PendingMatchMode = _mode;
   }

   private async void OnFindMatchPressed()
   {
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = _rankedCheck.ButtonPressed;
         _maxPlayers = MatchModeRules.MaxPlayers(_mode);
         if (!await Global.Online.FindMatch(_mode, _rankedCheck.ButtonPressed))
            return;

         CallDeferred(MethodName.ShowOnlineLobby, false);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Find match");
      }
   }

   private async void OnCreateRoomPressed()
   {
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = false;

         var code = await Global.Online.CreateRoom(_mode);
         if (string.IsNullOrEmpty(code))
            return;

         CallDeferred(MethodName.ShowCreatedRoom, code);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Create room");
      }
   }

   private async void OnJoinRoomPressed()
   {
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = false;

         var code = _roomCode.Text;
         if (!await Global.Online.JoinRoom(code, _mode))
            return;

         CallDeferred(MethodName.ShowOnlineLobby, true);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join room");
      }
   }

   private void ShowCreatedRoom(string code)
   {
      if (_roomCode != null && IsInstanceValid(_roomCode))
         _roomCode.Text = code;

      ShowOnlineLobby(true);
   }

   private void ShowOnlineLobby(bool showStart)
   {
      AttachNakamaPeer();
      MultiplayerConfigUi.Hide();
      Lobby.Show();
      StartGameButton.Visible = showStart;
      SyncNakamaPlayers();
      OnOnlineStatusChanged();
   }

   private void OnNakamaPeersChanged()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.OnNakamaPeersChanged);
         return;
      }

      AttachNakamaPeer();
      SyncNakamaPlayers();
   }

   private void OnNakamaMatchReady()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.OnNakamaMatchReady);
         return;
      }

      AttachNakamaPeer();
      SyncNakamaPlayers();
      foreach (var player in Players.Values)
         player.Ready = true;

      if (_rankedCheck.ButtonPressed || Players.Count >= MatchModeRules.MinPlayers(_mode))
         CallDeferred(MethodName.StartGame);
   }

   private void OnOnlineStatusChanged()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.OnOnlineStatusChanged);
         return;
      }

      if (_onlineStatus == null || Global.Online == null || !IsInstanceValid(_onlineStatus))
         return;

      var key = Global.Online.StatusMessage;
      _onlineStatus.Text = string.IsNullOrEmpty(key) ? string.Empty : Tr(key);

      if (!string.IsNullOrEmpty(Global.Online.MatchCode))
         _onlineStatus.Text += $"  {Global.Online.MatchCode}";
   }

   private void OnNakamaMatchLeft()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.OnNakamaMatchLeft);
         return;
      }

      if (!_usingNakama)
         return;

      Players.Clear();
      UpdatePlayersList();
   }

   private void AttachNakamaPeer()
   {
      if (Global.Online?.Peer == null)
         return;

      if (Multiplayer.MultiplayerPeer != Global.Online.Peer)
         Multiplayer.MultiplayerPeer = Global.Online.Peer;

      ((SceneMultiplayer)Multiplayer).ServerRelay = true;
      Global.Online.NotifyGodotPeers();
   }

   private void SyncNakamaPlayers()
   {
      if (Global.Online == null)
         return;

      Players.Clear();
      foreach (var (peerId, name) in Global.Online.ListPeers())
      {
         if (Global.Online.IsDedicated && peerId == 1)
            continue;
         Players[peerId] = new Player
         {
            Id = peerId,
            Name = name,
            Host = peerId == 1,
            Ai = false,
            Ready = true
         };
      }

      if (Players.Count == 0 && Global.Online.Session != null)
      {
         var id = Global.Online.Peer?._GetUniqueId() ?? 1;
         Players[id] = new Player
         {
            Id = id,
            Name = Config.Settings.Nickname,
            Host = id == 1,
            Ready = true
         };
      }

      UpdatePlayersList();
   }

   private void FillCasualWithAi()
   {
      if (Global.PendingRanked)
         return;

      var needed = MatchModeRules.MinPlayers(_mode);
      var nextId = 100;
      while (Players.Count < needed)
      {
         Players[nextId] = new Player
         {
            Id = nextId,
            Name = "COMPUTER",
            Host = false,
            Ai = true,
            Ready = true
         };
         nextId++;
      }
   }
}

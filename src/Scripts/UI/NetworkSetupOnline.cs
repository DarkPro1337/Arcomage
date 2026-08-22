using Arcomage.Networking;

namespace Arcomage.UI;

public partial class NetworkSetup
{
   private bool _usingNakama;
   private bool _startingGame;
   private bool _onlineBusy;
   private int _maxPlayers = 2;
   private MatchMode _mode = MatchMode.OneVsOne;

   private async Task ConnectOnlineSession()
   {
      if (Global.Online == null)
         return;

      SetOnlineBusy(true);
      try
      {
         await Global.Online.EnsureSession();
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Online session");
      }
      finally
      {
         SetOnlineBusy(false);
         ApplyOnlineAvailability();
         OnOnlineStatusChanged();
      }
   }

   private void OnModeSelected(long index)
   {
      _mode = (MatchMode)ModeSelect.GetItemId((int)index);
      _maxPlayers = MatchModeRules.MaxPlayers(_mode);
      Global.PendingMatchMode = _mode;
      UpdateLobbyMeta();
   }

   private void OnFindMatchPressed() => _ = FindMatchAsync();

   private async Task FindMatchAsync()
   {
      if (_onlineBusy || Global.Online == null)
         return;

      SetOnlineBusy(true);
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = RankedCheck.ButtonPressed;
         _maxPlayers = MatchModeRules.MaxPlayers(_mode);
         if (!await Global.Online.FindMatch(_mode, RankedCheck.ButtonPressed))
         {
            _usingNakama = false;
            OnOnlineStatusChanged();
            return;
         }

         CallDeferred(MethodName.ShowOnlineLobby, false);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Find match");
         _usingNakama = false;
         SetUiStatus("ONLINE_UNAVAILABLE");
      }
      finally
      {
         SetOnlineBusy(false);
      }
   }

   private void OnCreateRoomPressed() => _ = CreateRoomAsync();

   private async Task CreateRoomAsync()
   {
      if (_onlineBusy || Global.Online == null)
         return;

      SetOnlineBusy(true);
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = false;

         var code = await Global.Online.CreateRoom(_mode);
         if (string.IsNullOrEmpty(code))
         {
            _usingNakama = false;
            SetUiStatus("ONLINE_UNAVAILABLE");
            return;
         }

         CallDeferred(MethodName.ShowCreatedRoom, code);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Create room");
         _usingNakama = false;
         SetUiStatus("ONLINE_UNAVAILABLE");
      }
      finally
      {
         SetOnlineBusy(false);
      }
   }

   private void OnJoinRoomPressed() => _ = JoinRoomAsync();

   private async Task JoinRoomAsync()
   {
      if (_onlineBusy || Global.Online == null)
         return;

      var code = RoomCodeEdit.Text;
      if (string.IsNullOrWhiteSpace(code))
      {
         SetUiStatus("ONLINE_JOIN_FAILED");
         return;
      }

      SetOnlineBusy(true);
      try
      {
         _usingNakama = true;
         Global.PendingMatchMode = _mode;
         Global.PendingRanked = false;

         if (!await Global.Online.JoinRoom(code, _mode))
         {
            _usingNakama = false;
            SetUiStatus("ONLINE_JOIN_FAILED");
            return;
         }

         CallDeferred(MethodName.ShowOnlineLobby, true);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join room");
         _usingNakama = false;
         SetUiStatus("ONLINE_JOIN_FAILED");
      }
      finally
      {
         SetOnlineBusy(false);
      }
   }

   private void OnRetryOnlinePressed()
   {
      if (_onlineBusy || Global.Online == null)
         return;

      _ = ConnectOnlineSession();
   }

   private void OnCopyRoomCodePressed()
   {
      var code = LobbyRoomCode.Text;
      if (string.IsNullOrWhiteSpace(code))
         return;

      DisplayServer.ClipboardSet(code);
      CopyRoomCodeButton.Text = Tr("COPIED");
      var timer = GetTree().CreateTimer(1.5);
      timer.Timeout += RestoreCopyButtonText;
   }

   private void RestoreCopyButtonText()
   {
      if (IsInstanceValid(CopyRoomCodeButton))
         CopyRoomCodeButton.Text = Tr("COPY");
   }

   private void ShowCreatedRoom(string code)
   {
      LobbyRoomCode.Text = code ?? string.Empty;
      ShowOnlineLobby(true);
   }

   private void ShowOnlineLobby(bool showStart)
   {
      AttachNakamaPeer();
      SetupCenter.Hide();
      LobbyCenter.Show();
      ReadyButton.Hide();
      StartGameButton.Visible = showStart;

      var code = !string.IsNullOrEmpty(LobbyRoomCode.Text)
         ? LobbyRoomCode.Text
         : Global.Online?.MatchCode;

      var hasCode = !string.IsNullOrEmpty(code);
      RoomCodeRow.Visible = hasCode;
      if (hasCode)
         LobbyRoomCode.Text = code;

      CopyRoomCodeButton.Text = Tr("COPY");
      UpdateLobbyMeta();
      SyncNakamaPlayers();
      OnOnlineStatusChanged();
   }

   private void OnNakamaPeersChanged()
   {
      if (DeferIfOffMainThread(MethodName.OnNakamaPeersChanged))
         return;

      AttachNakamaPeer();
      var previous = Players.Count;
      SyncNakamaPlayers();

      if (Lobby.IsVisibleInTree() && Players.Count < previous)
         SetUiStatus("PLAYER_LEFT");
   }

   private void OnNakamaMatchReady()
   {
      if (DeferIfOffMainThread(MethodName.OnNakamaMatchReady))
         return;

      AttachNakamaPeer();
      SyncNakamaPlayers();
      foreach (var player in Players.Values)
         player.Ready = true;

      if (RankedCheck.ButtonPressed || Players.Count >= MatchModeRules.MinPlayers(_mode))
         CallDeferred(MethodName.StartGame);
   }

   private void OnOnlineStatusChanged()
   {
      if (DeferIfOffMainThread(MethodName.OnOnlineStatusChanged))
         return;

      ApplyOnlineAvailability();
      RefreshLobbyStatus();
   }

   private void SetUiStatus(string key)
   {
      var text = string.IsNullOrEmpty(key) ? string.Empty : Tr(key);
      if (IsInstanceValid(OnlineStatus))
         OnlineStatus.Text = text;

      if (IsInstanceValid(LobbyStatus) && Lobby.IsVisibleInTree() && _usingNakama)
         LobbyStatus.Text = text;
   }

   private void RefreshLobbyStatus()
   {
      if (!IsInsideTree() || Global.Online == null)
         return;

      var key = Global.Online.StatusMessage;
      var text = string.IsNullOrEmpty(key) ? string.Empty : Tr(key);
      if (IsInstanceValid(OnlineStatus))
         OnlineStatus.Text = text;

      if (!IsInstanceValid(LobbyStatus) || !Lobby.IsVisibleInTree())
         return;

      if (!_usingNakama)
         return;

      if (!string.IsNullOrEmpty(Global.Online.MatchCode) &&
          Players.Count < MatchModeRules.MinPlayers(_mode) &&
          key is "ONLINE_ROOM" or "ONLINE_CONNECTED")
      {
         LobbyStatus.Text = Tr("WAITING_FOR_PLAYERS");
         return;
      }

      LobbyStatus.Text = text;
   }

   private void ApplyOnlineAvailability()
   {
      if (!IsInsideTree() || DisplayServer.GetName() == "headless")
         return;

      var available = Global.Online?.HasSession == true && !_onlineBusy;
      FindMatchButton.Disabled = !available;
      CreateRoomButton.Disabled = !available;
      JoinRoomButton.Disabled = !available;
      RankedCheck.Disabled = !available;
      RoomCodeEdit.Editable = available;
      RetryOnlineButton.Disabled = available || _onlineBusy;
   }

   private void SetOnlineBusy(bool busy)
   {
      _onlineBusy = busy;
      ApplyOnlineAvailability();
   }

   private void UpdateLobbyMeta()
   {
      if (!IsInstanceValid(LobbyMeta))
         return;

      var mode = _mode switch
      {
         MatchMode.FreeForAll => Tr("MODE_FFA"),
         MatchMode.TwoVsTwo => Tr("MODE_2V2"),
         _ => Tr("MODE_1V1")
      };

      if (_usingNakama && RankedCheck.ButtonPressed)
         mode = $"{mode} · {Tr("RANKED")}";

      LobbyMeta.Text = mode;
   }

   private void OnNakamaMatchLeft()
   {
      if (DeferIfOffMainThread(MethodName.OnNakamaMatchLeft))
         return;

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

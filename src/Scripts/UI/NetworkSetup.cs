using System.Collections.Generic;
using System.Linq;
using Arcomage.Core;
using Arcomage.Gameplay;
using Arcomage.Networking;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.UI;

public partial class NetworkSetup : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("NetworkSetup");

   private const int Port = 8070;

   private Control _setupCenter;
   private Control _lobbyCenter;
   private VBoxContainer _lobby;
   private Tree _playersList;
   private LineEdit _serverIpAddress;
   private Label _lanAddressHint;
   private Button _createServerButton;
   private Button _joinServerButton;
   private Button _cancelButton;
   private Button _readyButton;
   private Button _startGameButton;
   private Button _leaveLobbyButton;
   private Label _lobbyMeta;
   private HBoxContainer _roomCodeRow;
   private Label _lobbyRoomCode;
   private Button _copyRoomCodeButton;
   private Label _lobbyStatus;
   private Node _level;
   private OptionButton _modeSelect;
   private LineEdit _roomCode;
   private Label _onlineStatus;
   private CheckBox _rankedCheck;
   private Button _findMatchButton;
   private Button _createRoomButton;
   private Button _joinRoomButton;
   private Button _retryOnlineButton;
   private TabContainer _tabs;

   private Control SetupCenter => _setupCenter ??= GetNode<Control>("%SetupCenter");
   private Control LobbyCenter => _lobbyCenter ??= GetNode<Control>("%LobbyCenter");
   private VBoxContainer Lobby => _lobby ??= GetNode<VBoxContainer>("%Lobby");
   private Tree PlayersList => _playersList ??= GetNode<Tree>("%PlayersList");
   private LineEdit ServerIpAddress => _serverIpAddress ??= GetNode<LineEdit>("%IpAddress");
   private Label LanAddressHint => _lanAddressHint ??= GetNode<Label>("%LanAddressHint");
   private Button CreateServerButton => _createServerButton ??= GetNode<Button>("%CreateServer");
   private Button JoinServerButton => _joinServerButton ??= GetNode<Button>("%JoinServer");
   private Button CancelButton => _cancelButton ??= GetNode<Button>("%Cancel");
   private Button ReadyButton => _readyButton ??= GetNode<Button>("%Ready");
   private Button StartGameButton => _startGameButton ??= GetNode<Button>("%StartGame");
   private Button LeaveLobbyButton => _leaveLobbyButton ??= GetNode<Button>("%LeaveLobby");
   private Label LobbyMeta => _lobbyMeta ??= GetNode<Label>("%LobbyMeta");
   private HBoxContainer RoomCodeRow => _roomCodeRow ??= GetNode<HBoxContainer>("%RoomCodeRow");
   private Label LobbyRoomCode => _lobbyRoomCode ??= GetNode<Label>("%LobbyRoomCode");
   private Button CopyRoomCodeButton => _copyRoomCodeButton ??= GetNode<Button>("%CopyRoomCode");
   private Label LobbyStatus => _lobbyStatus ??= GetNode<Label>("%LobbyStatus");
   private Node Level => _level ??= GetNode<Node>("%Level");
   private OptionButton ModeSelect => _modeSelect ??= GetNode<OptionButton>("%MatchMode");
   private LineEdit RoomCodeEdit => _roomCode ??= GetNode<LineEdit>("%RoomCode");
   private Label OnlineStatus => _onlineStatus ??= GetNode<Label>("%OnlineStatus");
   private CheckBox RankedCheck => _rankedCheck ??= GetNode<CheckBox>("%Ranked");
   private Button FindMatchButton => _findMatchButton ??= GetNode<Button>("%FindMatch");
   private Button CreateRoomButton => _createRoomButton ??= GetNode<Button>("%CreateRoom");
   private Button JoinRoomButton => _joinRoomButton ??= GetNode<Button>("%JoinRoom");
   private Button RetryOnlineButton => _retryOnlineButton ??= GetNode<Button>("%RetryOnline");
   private TabContainer Tabs => _tabs ??= GetNode<TabContainer>("%Tabs");

   public Dictionary<long, Player> Players { get; } = new();

   public override void _EnterTree()
   {
      base._EnterTree();

      CreateServerButton.Pressed += OnCreateServerPressed;
      JoinServerButton.Pressed += OnConnectPressed;
      CancelButton.Pressed += OnCancelPressed;
      ReadyButton.Toggled += OnReadyPressed;
      StartGameButton.Pressed += OnStartGamePressed;
      LeaveLobbyButton.Pressed += OnLeaveLobbyPressed;
      ModeSelect.ItemSelected += OnModeSelected;
      FindMatchButton.Pressed += OnFindMatchPressed;
      CreateRoomButton.Pressed += OnCreateRoomPressed;
      JoinRoomButton.Pressed += OnJoinRoomPressed;
      RetryOnlineButton.Pressed += OnRetryOnlinePressed;
      CopyRoomCodeButton.Pressed += OnCopyRoomCodePressed;
   }

   public override void _ExitTree()
   {
      base._ExitTree();

      CreateServerButton.Pressed -= OnCreateServerPressed;
      JoinServerButton.Pressed -= OnConnectPressed;
      CancelButton.Pressed -= OnCancelPressed;
      ReadyButton.Toggled -= OnReadyPressed;
      StartGameButton.Pressed -= OnStartGamePressed;
      LeaveLobbyButton.Pressed -= OnLeaveLobbyPressed;
      ModeSelect.ItemSelected -= OnModeSelected;
      FindMatchButton.Pressed -= OnFindMatchPressed;
      CreateRoomButton.Pressed -= OnCreateRoomPressed;
      JoinRoomButton.Pressed -= OnJoinRoomPressed;
      RetryOnlineButton.Pressed -= OnRetryOnlinePressed;
      CopyRoomCodeButton.Pressed -= OnCopyRoomCodePressed;

      UnbindLanPeerSignals();

      Multiplayer.ConnectionFailed -= OnConnectionFailed;
      Multiplayer.ServerDisconnected -= OnServerDisconnected;
      Multiplayer.ConnectedToServer -= OnConnectedToServer;

      if (Global.Online != null)
      {
         Global.Online.StatusChanged -= OnOnlineStatusChanged;
         Global.Online.MatchReady -= OnNakamaMatchReady;
         Global.Online.PeersChanged -= OnNakamaPeersChanged;
         Global.Online.MatchLeft -= OnNakamaMatchLeft;
      }
   }

   public override void _Ready()
   {
      Global.NetworkSetup = this;

      Multiplayer.ConnectionFailed += OnConnectionFailed;
      Multiplayer.ServerDisconnected += OnServerDisconnected;
      Multiplayer.ConnectedToServer += OnConnectedToServer;

      ((SceneMultiplayer)Multiplayer).ServerRelay = false;

      if (DisplayServer.GetName() == "headless")
         return;

      PlayersList.SetColumnTitle(0, Tr("PLAYERS"));
      PlayersList.SetColumnTitle(1, Tr("STATUS"));
      Tabs.SetTabTitle(0, Tr("TAB_ONLINE"));
      Tabs.SetTabTitle(1, Tr("TAB_LAN"));
      FillLanAddress();
      ApplyOnlineAvailability();

      if (Global.Online != null)
      {
         Global.Online.StatusChanged += OnOnlineStatusChanged;
         Global.Online.MatchReady += OnNakamaMatchReady;
         Global.Online.PeersChanged += OnNakamaPeersChanged;
         Global.Online.MatchLeft += OnNakamaMatchLeft;
      }

      _ = ConnectOnlineSession();
   }

   private void FillLanAddress()
   {
      var ips = IP.GetLocalAddresses()
         .Where(ip => ip.Contains('.') && ip != "127.0.0.1" && !ip.StartsWith("169.254."))
         .Distinct();

      var joined = string.Join(", ", ips);
      LanAddressHint.Text = !string.IsNullOrEmpty(joined)
         ? $"{Tr("YOUR_LAN_ADDRESS")}: {joined}"
         : Tr("YOUR_LAN_ADDRESS");
   }

   private void ShowSetupPanel()
   {
      LobbyCenter.Hide();
      SetupCenter.Show();
      RoomCodeRow.Hide();

      LobbyRoomCode.Text = string.Empty;
      CopyRoomCodeButton.Text = Tr("COPY");

      ApplyOnlineAvailability();
      OnOnlineStatusChanged();
   }

   private void ShowLanLobby()
   {
      SetupCenter.Hide();
      LobbyCenter.Show();
      RoomCodeRow.Hide();
      ReadyButton.Show();
      StartGameButton.Hide();

      UpdateLobbyMeta();
      LobbyStatus.Text = LanAddressHint.Text;
   }

   private void OnLeaveLobbyPressed()
   {
      CloseMultiplayerSession();
      ShowSetupPanel();
   }

   private void OnCancelPressed()
   {
      CloseMultiplayerSession();
      ShowSetupPanel();
      Hide();
   }

   private void OnConnectionFailed()
   {
      _logger.Error("Connection failed.");
      CloseMultiplayerSession();
      ShowSetupPanel();
   }

   private void OnServerDisconnected()
   {
      if (Level.GetChildCount() > 0)
         return;

      _logger.Debug("Server disconnected.");
      CloseMultiplayerSession();
      ShowSetupPanel();
   }

   private void OnConnectedToServer()
   {
      if (_usingNakama)
         return;

      _logger.Debug("Connected to server.");
      ShowLanLobby();

      RpcId(1, nameof(RequestNickname));
      RpcId(1, nameof(RequestReadyStatuses));
      UpdatePlayersList();
   }

   private void OnCreateServerPressed()
   {
      CloseMultiplayerSession();

      var peer = new ENetMultiplayerPeer();
      var error = peer.CreateServer(Port, _maxPlayers);
      if (error != Error.Ok)
      {
         _logger.Error("Failed to start multiplayer server: {Error}", error);
         OS.Alert("Failed to start multiplayer server.");
         return;
      }

      if (peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
      {
         OS.Alert("Failed to start multiplayer server.");
         return;
      }

      BindLanPeerSignals();

      _logger.Debug("Server started.");
      Multiplayer.MultiplayerPeer = peer;
      RegisterPlayer(1, Config.Settings.Nickname);
      ShowLanLobby();
      UpdatePlayersList();
   }

   private void OnPeerConnected(long id)
   {
      _logger.Debug($"Peer connected: {id}");
      if (id == 1)
         RegisterPlayer(id, Config.Settings.Nickname);
      else
         RpcId(id, nameof(RequestNickname));

      StartGameButton.Show();
   }

   private void OnPeerDisconnected(long id)
   {
      _logger.Debug($"Peer disconnected: {id}");
      if (!Players.Remove(id))
         return;

      StartGameButton.Hide();
      UpdatePlayersList();
   }

   public void OnConnectPressed()
   {
      var address = ServerIpAddress.Text;
      if (string.IsNullOrWhiteSpace(address))
      {
         _logger.Error("Need a remote to connect to.");
         return;
      }

      CloseMultiplayerSession();

      var peer = new ENetMultiplayerPeer();
      var error = peer.CreateClient(address, Port);
      if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
      {
         _logger.Error("Failed to connect to server: {Error}", error);
         return;
      }

      Multiplayer.MultiplayerPeer = peer;
      SetupCenter.Hide();
   }

   private void OnReadyPressed(bool toggle)
   {
      ReadyButton.Text = Tr(toggle ? "READY" : "NOT_READY");

      var id = Multiplayer.GetUniqueId();
      if (!Players.TryGetValue(id, out var player))
         return;

      player.Ready = toggle;
      UpdatePlayersList();

      if (Multiplayer.MultiplayerPeer == null || Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
         return;

      Rpc(nameof(UpdateReadyStatus), id, toggle);
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   private void UpdateReadyStatus(long id, bool ready)
   {
      if (Players.Count == 0)
         return;

      if (!Players.TryGetValue(id, out var player))
         return;

      player.Ready = ready;
      UpdatePlayersList();
   }

   private void OnStartGamePressed()
   {
      var ready = Players.Values.Count(x => x.Ready);
      if (ready < MatchModeRules.MinPlayers(_mode) && ready < _maxPlayers)
      {
         if (!_usingNakama)
            return;

         FillCasualWithAi();
      }

      Global.PendingMatchMode = _mode;
      if (_usingNakama && Global.Online?.Peer != null)
      {
         AttachNakamaPeer();
         Global.Online.BroadcastStartMatch();
      }

      Rpc(nameof(StartGame));
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
   private void StartGame()
   {
      if (_startingGame || Level.GetChildCount() > 0)
         return;

      _startingGame = true;
      SetupCenter.Hide();
      LobbyCenter.Hide();

      GetTree().Paused = false;
      CallDeferred(MethodName.ChangeLevel, ResourceLoader.Load("res://Scenes/Gameplay/Table.tscn"));
   }

   private void ChangeLevel(PackedScene scene)
   {
      if (scene == null || Level.GetChildCount() > 0)
      {
         _logger.Debug("ChangeLevel skipped");
         return;
      }

      _logger.Debug("Calling ChangeLevel");
      RemoveOldLevel();
      Level.AddChild(scene.Instantiate());
   }

   private void RemoveOldLevel()
   {
      foreach (var child in Level.GetChildren())
      {
         Level.RemoveChild(child);
         child.QueueFree();
      }
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void RequestNickname()
   {
      _logger.Debug("Nickname requested");
      RpcId(Multiplayer.GetRemoteSenderId(), nameof(RespondNickname), Config.Settings.Nickname);
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void RespondNickname(string name)
   {
      _logger.Debug("Nickname received: " + name);
      long id = Multiplayer.GetRemoteSenderId();
      RegisterPlayer(id, name);
   }

   private void RegisterPlayer(long id, string name)
   {
      _logger.Debug($"Registering player with id {id} and name {name}");
      if (Players.ContainsKey(id))
         return;

      var isHost = id == 1;
      Players.Add(id, new Player { Id = id, Name = name, Host = isHost, Ai = false });
      Rpc(nameof(AddRemotePlayer), id, name);
      UpdatePlayersList();
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void AddRemotePlayer(long id, string name)
   {
      _logger.Debug($"Adding remote player with id {id} and name {name}");
      if (Players.ContainsKey(id))
         return;

      var isHost = id == 1;
      Players.Add(id, new Player { Id = id, Name = name, Host = isHost, Ai = false });
      UpdatePlayersList();
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void RequestReadyStatuses()
   {
      long requesterId = Multiplayer.GetRemoteSenderId();
      foreach (var player in Players.Values)
         RpcId(requesterId, nameof(UpdateReadyStatus), player.Id, player.Ready);

      UpdatePlayersList();
   }

   private void UpdatePlayersList()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.UpdatePlayersList);
         return;
      }

      if (!IsInsideTree() || PlayersList == null || !IsInstanceValid(PlayersList))
         return;

      PlayersList.Clear();
      var orderedPlayers = Players.Values.OrderBy(x => !x.Host);
      var players = new Dictionary<string, bool>();
      foreach (var player in orderedPlayers)
      {
         if (string.IsNullOrEmpty(player.Name))
            players.TryAdd($"Player {player.Id}", player.Ready);
         else
            players.TryAdd(player.Name, player.Ready);
      }

      var root = PlayersList.GetRoot() ?? PlayersList.CreateItem();
      if (root == null)
         return;

      foreach (var (name, ready) in players)
      {
         var child = PlayersList.CreateItem(root);
         child.SetText(0, name);
         child.SetText(1, ready ? Tr("READY") : Tr("NOT_READY"));
         child.SetSelectable(0, false);
         child.SetSelectable(1, false);
      }

      if (StartGameButton != null && IsInstanceValid(StartGameButton))
         StartGameButton.Disabled = Players.Values.Count(x => x.Ready) < MatchModeRules.MinPlayers(_mode) && Players.Count < _maxPlayers;

      RefreshLobbyStatus();
   }

   private bool _lanPeerSignalsBound;

   private void BindLanPeerSignals()
   {
      if (_lanPeerSignalsBound)
         return;

      Multiplayer.PeerConnected += OnPeerConnected;
      Multiplayer.PeerDisconnected += OnPeerDisconnected;
      _lanPeerSignalsBound = true;
   }

   private void UnbindLanPeerSignals()
   {
      if (!_lanPeerSignalsBound)
         return;

      Multiplayer.PeerConnected -= OnPeerConnected;
      Multiplayer.PeerDisconnected -= OnPeerDisconnected;
      _lanPeerSignalsBound = false;
   }

   private void CloseMultiplayerSession()
   {
      UnbindLanPeerSignals();

      if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer peer)
      {
         peer.Close();
         Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
      }
      else if (_usingNakama)
      {
         Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
      }

      _usingNakama = false;
      _startingGame = false;
      if (Global.Online != null)
         _ = Global.Online.LeaveMatch();

      Players.Clear();
      PlayersList.Clear();
      ReadyButton.SetPressedNoSignal(false);
      ReadyButton.Text = Tr("NOT_READY");
      StartGameButton.Hide();
      UpdatePlayersList();
   }
}

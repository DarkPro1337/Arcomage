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

   private VBoxContainer MultiplayerConfigUi => GetNode<VBoxContainer>("Container");
   private VBoxContainer Lobby => GetNode<VBoxContainer>("Lobby");
   private Tree PlayersList => GetNode<Tree>("Lobby/PlayersList");
   private LineEdit ServerIpAddress => GetNode<LineEdit>("Container/IpAddress");
   private Label DeviceIpAddress => GetNode<Label>("DeviceIpAddress");
   private Button CreateServerButton => GetNode<Button>("Container/CreateServer");
   private Button JoinServerButton => GetNode<Button>("Container/JoinServer");
   private Button CancelButton => GetNode<Button>("Cancel");
   private Button ReadyButton => GetNode<Button>("Lobby/Ready");
   private Button StartGameButton => GetNode<Button>("Lobby/StartGame");
   private Node Level => GetNode<Node>("Level");
    
   public Dictionary<long, Player> Players { get; } = new();

   public override void _EnterTree()
   {
      base._EnterTree();

      CreateServerButton.Pressed += OnCreateServerPressed;
      JoinServerButton.Pressed += OnConnectPressed;
      CancelButton.Pressed += OnCancelPressed;
      ReadyButton.Toggled += OnReadyPressed;
      StartGameButton.Pressed += OnStartGamePressed;
   }

   public override void _ExitTree()
   {
      base._ExitTree();

      CreateServerButton.Pressed -= OnCreateServerPressed;
      JoinServerButton.Pressed -= OnConnectPressed;
      CancelButton.Pressed -= OnCancelPressed;
      ReadyButton.Toggled -= OnReadyPressed;
      StartGameButton.Pressed -= OnStartGamePressed;

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

      Multiplayer.Set("server_relay", false);

      if (DisplayServer.GetName() == "headless")
         return;

      PlayersList.SetColumnTitle(0, Tr("PLAYERS"));
      PlayersList.SetColumnTitle(1, Tr("STATUS"));
      BuildOnlineUi();

      _ = Global.Online?.EnsureSession();
   }

   private void OnCancelPressed()
   {
      CloseMultiplayerSession();
      if (Global.Online != null)
         _ = Global.Online.LeaveMatch();

      Lobby.Hide();
      MultiplayerConfigUi.Show();
      Hide();
   }
        
   private void OnConnectionFailed()
   {
      _logger.Error("Connection failed.");
      CloseMultiplayerSession();
      Lobby.Hide();
      MultiplayerConfigUi.Show();
   }
        
   private void OnServerDisconnected()
   {
      _logger.Debug("Server disconnected.");
      CloseMultiplayerSession();
      Lobby.Hide();
      MultiplayerConfigUi.Show();

      if (Level.GetChild(0) is { } child && child.Name == "Table")
         child.QueueFree();
   }

   private void OnConnectedToServer()
   {
      _logger.Debug("Connected to server.");
      MultiplayerConfigUi.Hide();
      Lobby.Show();

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

      Multiplayer.PeerConnected -= OnPeerConnected;
      Multiplayer.PeerDisconnected -= OnPeerDisconnected;
      Multiplayer.PeerConnected += OnPeerConnected;
      Multiplayer.PeerDisconnected += OnPeerDisconnected;

      _logger.Debug("Server started.");
      Multiplayer.MultiplayerPeer = peer;
      MultiplayerConfigUi.Hide();
      Lobby.Show();

      RegisterPlayer(1, Config.Settings.Nickname);
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
      var address = ServerIpAddress.Get("text").AsString();
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
      MultiplayerConfigUi.Hide();
   }
    
   private void OnReadyPressed(bool toggle)
   {
      ReadyButton.Text = Tr(toggle ? "READY" : "NOT_READY");

      var id = Multiplayer.GetUniqueId();
      if (!Players.TryGetValue(id, out var player))
         return;
      player.Ready = toggle;
      UpdatePlayersList();

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
      MultiplayerConfigUi.Hide();
      Lobby.Hide();
      GetTree().Paused = false;
      CallDeferred(nameof(ChangeLevel), ResourceLoader.Load("res://Scenes/Gameplay/Table.tscn"));
   }

   private void ChangeLevel(PackedScene scene)
   {
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
         CallDeferred(nameof(UpdatePlayersList));
         return;
      }

      if (!IsInsideTree() || PlayersList == null || !IsInstanceValid(PlayersList))
         return;

      PlayersList.Clear();
      var orderedPlayers = Players.Values.OrderBy(x => !x.Host);
      var players = new Dictionary<string, bool>();
      foreach (var player in orderedPlayers)
         players.TryAdd(string.IsNullOrEmpty(player.Name) ? $"Player {player.Id}" : player.Name, player.Ready);

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
   }

   private void CloseMultiplayerSession()
   {
      Multiplayer.PeerConnected -= OnPeerConnected;
      Multiplayer.PeerDisconnected -= OnPeerDisconnected;

      if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer peer)
      {
         peer.Close();
         Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
      }
      else if (_usingNakama)
      {
         Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
         _usingNakama = false;
         if (Global.Online != null)
            _ = Global.Online.LeaveMatch();
      }

      Players.Clear();
      PlayersList.Clear();
      ReadyButton.SetPressedNoSignal(false);
      ReadyButton.Text = Tr("NOT_READY");
      StartGameButton.Hide();
      UpdatePlayersList();
   }
}

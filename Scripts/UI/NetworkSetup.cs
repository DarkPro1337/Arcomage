using System.Collections.Generic;
using System.Linq;
using Arcomage.Scripts.Core;
using Arcomage.Scripts.Gameplay;
using Arcomage.Scripts.Logging;
using Godot;

namespace Arcomage.Scripts.UI;

public partial class NetworkSetup : Control
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("NetworkSetup");

   private const int Port = 8070;
   private const int MaxPlayers = 2;

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
    
   public List<Player> Players { get; } = new();

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
   }

   public override void _Ready()
   {
      Global.NetworkSetup = this;

      Multiplayer.ConnectionFailed += OnConnectionFailed;
      Multiplayer.ServerDisconnected += OnServerDisconnected;
      Multiplayer.ConnectedToServer += OnConnectedToServer;

      Multiplayer.Set("server_relay", false);

      if (DisplayServer.GetName() == "headless")
         CallDeferred(nameof(OnConnectPressed));

      PlayersList.SetColumnTitle(0, Tr("PLAYERS"));
      PlayersList.SetColumnTitle(1, Tr("STATUS"));
   }

   private void OnCancelPressed()
   {
      Players.Clear();
      PlayersList.Clear();
      Multiplayer.MultiplayerPeer.Close();
      Lobby.Hide();
      MultiplayerConfigUi.Show();
      UpdatePlayersList();
      Hide();
   }
        
   private void OnConnectionFailed()
   {
      _Logger.Error("Connection failed.");

      Lobby.Hide();
      MultiplayerConfigUi.Show();
   }
        
   private void OnServerDisconnected()
   {
      _Logger.Debug("Server disconnected.");

      Lobby.Hide();
      MultiplayerConfigUi.Show();
      PlayersList.Clear();
      Players.Clear();

      if (Level.GetChild(0) is { } child && child.Name == "Table") 
         Level.GetChild(0).QueueFree();
   }

   private void OnConnectedToServer()
   {
      _Logger.Debug("Connected to server.");
      MultiplayerConfigUi.Hide();
      Lobby.Show();

      RpcId(1, nameof(RequestNickname));
      RpcId(1, nameof(RequestReadyStatuses));
      UpdatePlayersList();
   }

   private void OnCreateServerPressed()
   {
      var peer = new ENetMultiplayerPeer();
      var error = peer.CreateServer(Port, MaxPlayers);

      if (error == Error.AlreadyInUse)
      {
         OS.Alert("Multiplayer instance already has an open connection. It'll be closed. Please try again.");
         Multiplayer.MultiplayerPeer.Close();
         return;
      }

      Multiplayer.PeerConnected += OnPeerConnected;
      Multiplayer.PeerDisconnected += OnPeerDisconnected;

      if (peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
      {
         OS.Alert("Failed to start multiplayer server.");
         return;
      }
        
      _Logger.Debug("Server started.");
      Multiplayer.MultiplayerPeer = peer;
      MultiplayerConfigUi.Hide();
      Lobby.Show();

      RegisterPlayer(1, Config.Settings.Nickname);

      UpdatePlayersList();
   }
    
   private void OnPeerConnected(long id)
   {
      _Logger.Debug($"Peer connected: {id}");
      if (id == 1)
         RegisterPlayer(id, Config.Settings.Nickname);
      else
         RpcId(id, nameof(RequestNickname));

      StartGameButton.Show();
   }

   private void OnPeerDisconnected(long id)
   {
      _Logger.Debug($"Peer disconnected: {id}");
      if (Players.All(x => x.Id != id)) return;
      var player = Players.First(x => x.Id == id);
      Players.Remove(player);

      StartGameButton.Hide();
      UpdatePlayersList();
   }

   public void OnConnectPressed()
   {
      var address = ServerIpAddress.Get("text").AsString();
      if (string.IsNullOrWhiteSpace(address))
      {
         _Logger.Error("Need a remote to connect to.");
         return;
      }

      var peer = new ENetMultiplayerPeer();
      peer.CreateClient(address, Port);
      if (peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
      {
         _Logger.Error("Failed to connect to server");
         return;
      }

      Multiplayer.MultiplayerPeer = peer;
      MultiplayerConfigUi.Hide();
   }
    
   private void OnReadyPressed(bool toggle)
   {
      ReadyButton.Text = Tr(toggle ? "READY" : "NOT_READY");

      var id = Multiplayer.GetUniqueId();
      var player = Players.First(x => x.Id == id);
      player.Ready = toggle;
      UpdatePlayersList();

      Rpc(nameof(UpdateReadyStatus), id, toggle);
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   private void UpdateReadyStatus(long id, bool ready)
   {
      if (Players.Count == 0)
         return;
        
      var player = Players.First(x => x.Id == id);
      player.Ready = ready;
      UpdatePlayersList();
   }

   private void OnStartGamePressed()
   {
      if (Players.Count(x => x.Ready) < MaxPlayers)
         return;

      Rpc(nameof(StartGame));
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
   private void StartGame()
   {
      MultiplayerConfigUi.Hide();
      GetTree().Paused = false;
      CallDeferred(nameof(ChangeLevel), ResourceLoader.Load("res://Scenes/Gameplay/Table.tscn"));
   }

   private void ChangeLevel(PackedScene scene)
   {
      _Logger.Debug("Calling ChangeLevel");
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
      _Logger.Debug("Nickname requested");
      RpcId(Multiplayer.GetRemoteSenderId(), nameof(RespondNickname), Config.Settings.Nickname);
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void RespondNickname(string name)
   {
      _Logger.Debug("Nickname received: " + name);
      long id = Multiplayer.GetRemoteSenderId();
      RegisterPlayer(id, name);
   }

   private void RegisterPlayer(long id, string name)
   {
      _Logger.Debug($"Registering player with id {id} and name {name}");
      var isHost = id == 1;
      Players.Add(new Player { Id = id, Name = name, Host = isHost, Ai = false });
      Rpc(nameof(AddRemotePlayer), id, name);
      UpdatePlayersList();
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void AddRemotePlayer(long id, string name)
   {
      _Logger.Debug($"Adding remote player with id {id} and name {name}");
      var isHost = id == 1;
      Players.Add(new Player { Id = id, Name = name, Host = isHost, Ai = false });
      UpdatePlayersList();
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void RequestReadyStatuses()
   {
      long requesterId = Multiplayer.GetRemoteSenderId();
      foreach (var player in Players) 
         RpcId(requesterId, nameof(UpdateReadyStatus), player.Id, player.Ready);
      UpdatePlayersList();
   }

   private void UpdatePlayersList()
   {
      PlayersList.Clear();
      var orderedPlayers = Players.OrderBy(x => !x.Host);
      var players = new Dictionary<string, bool>();
      foreach (var player in orderedPlayers) 
         players.TryAdd(player.Name, player.Ready);

      var root = PlayersList.GetRoot() ?? PlayersList.CreateItem();
      foreach (var (name, ready) in players)
      {
         var child = PlayersList.CreateItem(root);
         child.SetText(0, name);
         child.SetText(1, ready ? Tr("READY") : Tr("NOT_READY"));
         child.SetSelectable(0, false);
         child.SetSelectable(1, false);
      }

      StartGameButton.Disabled = Players.Count(x => x.Ready) < MaxPlayers;
   }
}
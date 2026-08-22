using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arcomage.Core;
using Arcomage.Networking;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Gameplay;

public partial class Table : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Table");

   private const float MaxStructureHeight = 200f;

   private readonly RandomNumberGenerator _rng = new();

   public Dictionary<long, Player> Players { get; private set; } = new();
   private long _turnPlayerId;

   public int Elapsed { get; private set; }
   public string ElapsedString = "00:00";

   public bool IsOffline { get; private set; }

   private bool _gameStarted;
   private bool _gameOver;
   private bool _aiPlayQueued;
   private bool _animating;
   private bool _leavingMatch;
   private long _winnerId;
   private string _winReasonKey = string.Empty;

   [Signal]
   public delegate void GraveyardAnimationEndedEventHandler();

   [Signal]
   public delegate void DeckAnimationEndedEventHandler();

   public override void _Input(InputEvent @event)
   {
      base._Input(@event);

      if (_gameOver && MatchResult.Visible)
      {
         if (IsContinuePressed(@event))
            LeaveMatch();
         return;
      }

      if (_matchChat != null && _matchChat.TryHandleInput(@event))
         return;

      if (!Input.IsActionJustPressed("ui_cancel"))
         return;

      if (InGameMenu.Visible)
      {
         InGameMenu.Close();
         return;
      }

      InGameMenu.Open(pauseTree: IsOffline);
   }

   private int _hostStateRetries;

   private void RequestHostGameState()
   {
      if (IsOffline || Multiplayer.IsServer() || _gameStarted)
         return;

      if (Global.Online is { IsInMatch: true })
      {
         if (_hostStateRetries > 0)
            return;

         _hostStateRetries = 1;
         _ = Global.Online.RequestSnapshot();
         var timer = GetTree().CreateTimer(1.5);
         timer.Timeout += OnSnapshotRetryTimeout;
         return;
      }

      if (Array.IndexOf(Multiplayer.GetPeers(), 1) >= 0)
      {
         RpcId(1, nameof(RequestGameState));
         return;
      }

      if (_hostStateRetries++ < 10)
         CallDeferred(MethodName.RequestHostGameState);
   }

   private void OnSnapshotRetryTimeout()
   {
      if (!IsInsideTree() || _gameStarted || Multiplayer.IsServer())
         return;

      _hostStateRetries = 0;
      RequestHostGameState();
   }

   private void BindOnlineSync()
   {
      if (Global.Online is not { IsInMatch: true })
         return;

      Global.Online.SnapshotReceived += OnNakamaSnapshot;
      Global.Online.SnapshotRequested += OnNakamaSnapshotRequested;
      Global.Online.CardPlayReceived += OnNakamaCardPlay;
      Global.Online.PeersChanged += OnOnlinePeersChanged;
   }

   private void UnbindOnlineSync()
   {
      if (Global.Online == null)
         return;

      Global.Online.SnapshotReceived -= OnNakamaSnapshot;
      Global.Online.SnapshotRequested -= OnNakamaSnapshotRequested;
      Global.Online.CardPlayReceived -= OnNakamaCardPlay;
      Global.Online.PeersChanged -= OnOnlinePeersChanged;
   }

   private void OnOnlinePeersChanged()
   {
      if (!GodotThread.IsMainThread())
      {
         CallDeferred(MethodName.OnOnlinePeersChanged);
         return;
      }

      if (_leavingMatch || _gameOver || Global.Online == null)
         return;

      var present = Global.Online.ListPeers().Select(peer => (long)peer.PeerId).ToHashSet();
      foreach (var id in Players.Keys.Where(id => !Players[id].Ai && !present.Contains(id)).ToList())
         Players.Remove(id);

      TryEndMatchAfterDisconnect();
   }

   private void OnNakamaSnapshot(string json) => ApplyRemoteGameState(json);

   private void OnNakamaSnapshotRequested(int peerId)
   {
      if (!IsAuthority() || !_gameStarted)
         return;

      SendGameState(peerId);
   }

   private void OnNakamaCardPlay(int peerId, int cardIndex, string cardId, bool discarded, long targetId)
   {
      if (!IsAuthority())
         return;

      ResolveCardPlay(peerId, cardIndex, cardId, discarded, targetId);
   }

   private static bool IsContinuePressed(InputEvent @event)
   {
      if (@event.IsEcho())
         return false;

      if (@event.IsActionPressed("ui_select") || @event.IsActionPressed("ui_accept"))
         return true;

      return @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space };
   }

   private async void LeaveMatch()
   {
      if (_leavingMatch)
         return;

      try
      {
         var anim = MatchResult.GetNode<AnimationPlayer>("Anim");
         anim.Play("fade_out");
         await ToSignal(anim, AnimationMixer.SignalName.AnimationFinished);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Leave match");
      }

      await ReturnToMenu();
   }

   public async Task ReturnToMenu()
   {
      _leavingMatch = true;
      if (IsInsideTree())
         GetTree().Paused = false;

      UnbindOnlineSync();
      UnbindDisconnectSignals();
      UnbindPeerConnected();

      if (Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer)
         Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();

      if (Global.Online != null)
         await Global.Online.LeaveMatch();

      if (IsInsideTree())
         GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
   }

   public override void _EnterTree()
   {
      base._EnterTree();
      TimeElapsed.Timeout += OnTimeElapsedTimeout;
   }

   public override void _Ready()
   {
      var args = Global.GetCommandLineArgs();
      if (args.TryGetValue("playerName", out var name))
      {
         _logger.Debug("Player name from command line: " + name);
         Config.Settings.Nickname = name;
      }

      _logger.Debug("Loaded");
      Global.Table = this;
      ConfigureMatchRules();

      ApplyResourcePanelLocale();

      BindOnlineSync();
      BindDisconnectSignals();

      if (Multiplayer.IsServer())
         SpawnLocalPlayer();

      InitializePlayersAndUi();

      if (!Multiplayer.IsServer())
      {
         CallDeferred(MethodName.RequestHostGameState);
         return;
      }

      Multiplayer.PeerConnected += AddPlayer;
      _peerConnectedBound = true;

      SpawnConnectedPlayers();

      _rng.Randomize();
      _turnPlayerId = GetRandomTurnPlayerId();
      AddResources(_turnPlayerId);
      PlaceStartCardsOnDeck();
      _gameStarted = true;
      SetTurn(_turnPlayerId);
      BroadcastGameState();
      TryStartAiTurn();
   }

   public override void _ExitTree()
   {
      TimeElapsed.Timeout -= OnTimeElapsedTimeout;
      if (IsNodeReady())
         ClearSeatBindings();

      UnbindOnlineSync();
      UnbindDisconnectSignals();
      UnbindPeerConnected();
   }

   private string[] BuildRandomHandIds(int count)
   {
      var cards = Global.DeckManager.GetAllCards();
      var hand = new string[count];
      if (cards.Count == 0)
         return hand;

      for (var i = 0; i < count; i++)
      {
         var card = cards[_rng.RandiRange(0, cards.Count - 1)];
         hand[i] = card.Id;
      }

      return hand;
   }

   private void ApplyHand(long playerId, string[] cardIds)
   {
      var deck = GetDeckForPlayer(playerId);
      if (deck == null || cardIds == null)
         return;

      var current = GetHandIds(deck);
      if (current.Length == cardIds.Length)
      {
         var same = !cardIds.Where((t, i) => current[i] != t).Any();
         if (same)
            return;
      }

      ClearDeck(deck);
      foreach (var cardId in cardIds)
      {
         if (string.IsNullOrEmpty(cardId))
            continue;
         deck.AddChild(CreateCard(cardId));
      }
   }

   private void ApplyHiddenHand(long playerId, int count)
   {
      var deck = GetDeckForPlayer(playerId);
      if (deck == null || count <= 0)
         return;

      var current = deck.GetChildren().OfType<CardControl>().Count();
      if (current == count)
      {
         ApplyDeckVisibility(deck, false);
         return;
      }

      var placeholderId = Global.DeckManager.GetAllCards().FirstOrDefault()?.Id;
      if (string.IsNullOrEmpty(placeholderId))
         return;

      ClearDeck(deck);
      for (var i = 0; i < count; i++)
      {
         var card = (CardControl)CreateCard(placeholderId);
         deck.AddChild(card);
         card.SetFaceDown(true);
      }
   }

   private CardControl ReplaceHandCard(HBoxContainer deck, CardControl current, string cardId)
   {
      if (deck == null || current == null)
         return current;

      var index = current.GetIndex();
      var replacement = (CardControl)CreateCard(cardId);
      if (current.GetParent() == deck)
         deck.RemoveChild(current);

      current.QueueFree();
      deck.AddChild(replacement);
      if (index >= 0 && index < deck.GetChildCount())
         deck.MoveChild(replacement, index);

      replacement.SetFaceDown(true);
      return replacement;
   }

   private Control CreateCard(string cardId)
   {
      var card = (PackedScene)ResourceLoader.Load("res://Scenes/Gameplay/Card.tscn");
      var newCard = (CardControl)card.Instantiate();
      newCard.CardId = cardId;
      return newCard;
   }

   private void ClearDeck(HBoxContainer deck)
   {
      foreach (var child in deck.GetChildren())
      {
         deck.RemoveChild(child);
         child.QueueFree();
      }
   }

   private void SpawnConnectedPlayers()
   {
      foreach (var id in Multiplayer.GetPeers())
         AddPlayer(id);
   }

   private void SpawnLocalPlayer()
   {
      if (OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless")
         return;

      AddPlayer(1);
   }

   private void InitializePlayersAndUi()
   {
      if (Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
      {
         IsOffline = true;
         if (!Players.ContainsKey(2))
            Players.Add(2, new Player { Id = 2, Name = "COMPUTER", Host = false, Ai = true });

         AssignSlots();
         UpdateNamePanels();
      }
      else if (Multiplayer.MultiplayerPeer is not null)
      {
         IsOffline = false;
         if (Global.NetworkSetup?.Players is { Count: > 0 })
         {
            Players = new Dictionary<long, Player>(Global.NetworkSetup.Players);
         }
         else if (Global.Online != null)
         {
            Players = new Dictionary<long, Player>();
            foreach (var (id, name) in Global.Online.ListPeers())
            {
               if (Global.Online.IsDedicated && id == 1)
                  continue;

               Players[id] = new Player
               {
                  Id = id,
                  Name = name,
                  Host = id == 1,
                  Ai = false
               };
            }
         }

         AssignSlots();
         UpdateNamePanels();
         SetupMatchChat();
      }
   }

   private void UpdateNamePanels()
   {
      foreach (var (id, hud) in _hudByPlayer)
      {
         if (!Players.TryGetValue(id, out var player))
            continue;

         var display = player.Ai && IsOffline ? Tr(player.Name) : player.Name;
         var named = new Player
         {
            Id = player.Id,
            Name = display,
            Host = player.Host,
            Ai = player.Ai,
            SeatIndex = player.SeatIndex,
            TeamId = player.TeamId,
            Eliminated = player.Eliminated,
            TowerHp = player.TowerHp,
            WallHp = player.WallHp,
            Quarries = player.Quarries,
            Bricks = player.Bricks,
            Magic = player.Magic,
            Gems = player.Gems,
            Dungeons = player.Dungeons,
            Recruits = player.Recruits
         };

         hud.Apply(named);
      }

      HighlightCurrentTurn();
   }

   private void HighlightCurrentTurn()
   {
      foreach (var (id, hud) in _hudByPlayer)
      {
         var onTurn = id == _turnPlayerId;
         if (hud.NameLabel?.GetParent() is CanvasItem panel)
            panel.SelfModulate = onTurn ? Colors.White : new Color(1, 1, 1, 0.45f);

         hud.NameLabel?.Modulate = onTurn ? Colors.White : new Color(1, 1, 1, 0.7f);
      }
   }

   private void AddPlayer(long id)
   {
      _logger.Debug("Adding player with id: " + id);
      if (Players.ContainsKey(id))
      {
         if (_gameStarted && IsAuthority())
            SendGameState(id);

         return;
      }

      if (Global.Online is { IsInMatch: true })
      {
         var name = Config.Settings.Nickname;
         foreach (var (peerId, peerName) in Global.Online.ListPeers())
         {
            if (peerId != id)
               continue;

            name = peerName;
            break;
         }

         RegisterPlayer(id, name);
         if (_gameStarted)
            SendGameState(id);
         return;
      }

      if (id == 1)
         RegisterPlayer(id, Config.Settings.Nickname);
      else
         RpcId(id, nameof(RequestNickname));
   }

   private bool _disconnectSignalsBound;
   private bool _peerConnectedBound;

   private void UnbindPeerConnected()
   {
      if (!_peerConnectedBound)
         return;

      Multiplayer.PeerConnected -= AddPlayer;

      _peerConnectedBound = false;
   }

   private void BindDisconnectSignals()
   {
      if (_disconnectSignalsBound)
         return;

      Multiplayer.PeerDisconnected += OnRemotePlayerLeft;
      Multiplayer.ServerDisconnected += OnMatchServerDisconnected;

      _disconnectSignalsBound = true;
   }

   private void UnbindDisconnectSignals()
   {
      if (!_disconnectSignalsBound)
         return;

      Multiplayer.PeerDisconnected -= OnRemotePlayerLeft;
      Multiplayer.ServerDisconnected -= OnMatchServerDisconnected;

      _disconnectSignalsBound = false;
   }

   private void OnMatchServerDisconnected()
   {
      Players.Remove(1);
      TryEndMatchAfterDisconnect();
   }

   private void OnRemotePlayerLeft(long id)
   {
      Players.Remove(id);
      TryEndMatchAfterDisconnect();
   }

   private void TryEndMatchAfterDisconnect()
   {
      if (_gameOver || _leavingMatch || !_gameStarted)
         return;

      var humans = Players.Values.Count(player => !player.Ai);
      if (humans >= MatchModeRules.MinPlayers(MatchMode) && humans >= 2)
         return;

      var winner = Players.Values.FirstOrDefault(player => !player.Ai);
      if (winner == null)
         return;

      ShowEndGame(winner, "OPPONENT_LEFT_MSG");
   }

   private void AddResources(long playerId)
   {
      if (!Players.TryGetValue(playerId, out var player))
         return;

      player.Bricks += player.Quarries;
      player.Gems += player.Magic;
      player.Recruits += player.Dungeons;
   }

   private void SetTurn(long playerId)
   {
      if (!Players.TryGetValue(playerId, out var player))
         return;

      if (_turnPlayerId != playerId)
         _logger.Debug("Setting turn to {PlayerName}", player.Name);

      _turnPlayerId = playerId;
      RefreshVisibleSeats();
      UpdateDeckVisibility();
      HighlightCurrentTurn();
   }

   private void UpdateDeckVisibility()
   {
      if (Players.Count == 0)
         return;

      PresentTurnHand();

      foreach (var (playerId, deck) in _handByPlayer)
      {
         if (deck == RedDeck || deck == BlueDeck)
            deck.Visible = playerId == _turnPlayerId;

         ApplyDeckVisibility(deck, ShouldShowHandFaces(playerId));
      }

      UpdateCardAffordability();
      UpdateTurnLockUi();
   }

   private bool ShouldShowHandFaces(long playerId)
   {
      var localId = GetLocalHumanId();
      return localId > 0 && playerId == localId;
   }

   private bool IsLocalPlayerHost()
   {
      if (IsOffline || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
         return true;

      return Multiplayer.GetUniqueId() == 1;
   }

   private void ApplyDeckVisibility(HBoxContainer deck, bool showFaces)
   {
      foreach (var child in deck.GetChildren())
      {
         if (child is CardControl card)
            card.SetFaceDown(!showFaces);
      }
   }

   private void ApplyResourcePanelLocale()
   {
      var showPrimary = TranslationServer.GetLocale() == "en";
      LeftSeat.ShowPrimaryResourcePanels(showPrimary);
      RightSeat.ShowPrimaryResourcePanels(showPrimary);
   }

   private void UpdateStatPanelUi() => UpdateNamePanels();

   private static float GetStructureHeight(int hp)
   {
      var maxHp = Mathf.Max(1, Config.Settings.TowerVictory);
      var ratio = Mathf.Clamp(hp / (float)maxHp, 0f, 1f);
      return ratio * MaxStructureHeight;
   }

   private static void SetStructureHeight(Control structure, int hp)
   {
      var size = structure.Size;
      size.Y = GetStructureHeight(hp);
      structure.Size = size;
   }

   [Rpc]
   public void RequestNickname()
   {
      _logger.Debug("Nickname requested");
      RpcId(1, nameof(RespondNickname), Config.Settings.Nickname);
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
      _logger.Debug("Registering player with id: " + id + " and name: " + name);
      if (Players.ContainsKey(id))
         return;

      var isHost = id == 1;
      Players.Add(id, new Player { Id = id, Name = name, Host = isHost, Ai = false });
      AssignSlots();
      UpdateNamePanels();

      if (Global.Online is not { IsInMatch: true })
         Rpc(nameof(AddRemotePlayer), id, name);
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   public void AddRemotePlayer(long id, string name)
   {
      _logger.Debug("Adding remote player with id: " + id + " and name: " + name);
      if (Players.ContainsKey(id))
         return;

      var isHost = id == 1;
      Players.Add(id, new Player { Id = id, Name = name, Host = isHost, Ai = false });
      AssignSlots();
      UpdateNamePanels();
   }

   private void OnTimeElapsedTimeout()
   {
      Elapsed++;
      ElapsedString = $"{Elapsed / 60:D2}:{Elapsed % 60:D2}";
   }

   [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
   public void BroadcastChat(string name, string text)
   {
      _matchChat?.Append(name, text);
   }
}

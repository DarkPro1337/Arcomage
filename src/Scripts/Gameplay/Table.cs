using System.Collections.Generic;
using System.Linq;
using Arcomage.Core;
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

      if (!Input.IsActionJustPressed("ui_cancel"))
         return;

      InGameMenu.Show();
      GetTree().Paused = true;
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

      _leavingMatch = true;
      var anim = MatchResult.GetNode<AnimationPlayer>("Anim");
      anim.Play("fade_out");
      await ToSignal(anim, AnimationMixer.SignalName.AnimationFinished);
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

      LocaleStatPanels();
      SetupMatchChat();
      if (Multiplayer.IsServer())
         SpawnLocalPlayer();

      InitializePlayersAndUi();

      if (!Multiplayer.IsServer())
      {
         RpcId(1, nameof(RequestGameState));
         return;
      }

      Multiplayer.PeerConnected += AddPlayer;
      Multiplayer.PeerDisconnected += RemovePlayer;

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

      if (!Multiplayer.IsServer())
         return;

      Multiplayer.PeerConnected -= AddPlayer;
      Multiplayer.PeerDisconnected -= RemovePlayer;
   }

   public override void _PhysicsProcess(double delta)
   {
      UpdateStatPanelUi();
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

      ClearDeck(deck);
      foreach (var cardId in cardIds)
      {
         if (string.IsNullOrEmpty(cardId))
            continue;
         deck.AddChild(CreateCard(cardId));
      }
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
            Players = Global.NetworkSetup.Players;
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
      }
   }

   private void UpdateNamePanels()
   {
      var english = TranslationServer.GetLocale() == "en";
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

         hud.Apply(named, english);
      }
   }

   private void AddPlayer(long id)
   {
      _logger.Debug("Adding player with id: " + id);
      if (Players.ContainsKey(id))
         return;

      if (id == 1)
         RegisterPlayer(id, Config.Settings.Nickname);
      else
         RpcId(id, nameof(RequestNickname));
   }

   private void RemovePlayer(long id) => Players.Remove(id);

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

      _logger.Debug("Setting turn to {PlayerName}", player.Name);
      _turnPlayerId = playerId;
      UpdateDeckVisibility();
   }

   private void UpdateDeckVisibility()
   {
      if (Players.Count == 0)
         return;

      var localId = GetLocalHumanId();
      RedDeck.Visible = true;
      BlueDeck.Visible = IsOffline;

      foreach (var (playerId, deck) in _handByPlayer)
      {
         var showFaces = IsOffline || playerId == localId;
         ApplyDeckVisibility(deck, showFaces);
      }

      UpdateCardAffordability();
      UpdateTurnLockUi();
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

   private void LocaleStatPanels() => SwitchStatPanel(TranslationServer.GetLocale() == "en");

   private void SwitchStatPanel(bool toggle)
   {
      if (toggle)
      {
         RedBricksPanel.Show();
         RedGemsPanel.Show();
         RedRecruitsPanel.Show();
         BlueBricksPanel.Show();
         BlueGemsPanel.Show();
         BlueRecruitsPanel.Show();

         RedBricksAltPanel.Hide();
         RedGemsAltPanel.Hide();
         RedRecruitsAltPanel.Hide();
         BlueBricksAltPanel.Hide();
         BlueGemsAltPanel.Hide();
         BlueRecruitsAltPanel.Hide();
      }
      else
      {
         RedBricksPanel.Hide();
         RedGemsPanel.Hide();
         RedRecruitsPanel.Hide();
         BlueBricksPanel.Hide();
         BlueGemsPanel.Hide();
         BlueRecruitsPanel.Hide();

         RedBricksAltPanel.Show();
         RedGemsAltPanel.Show();
         RedRecruitsAltPanel.Show();
         BlueBricksAltPanel.Show();
         BlueGemsAltPanel.Show();
         BlueRecruitsAltPanel.Show();
      }
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

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
   private long _redPlayerId = 1;
   private long _bluePlayerId = 2;

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

      LocaleStatPanels();
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

   private void PlaceStartCardsOnDeck()
   {
      SpawnInitialHands(
         BuildRandomHandIds(Config.Settings.CardsInHand),
         BuildRandomHandIds(Config.Settings.CardsInHand));
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

   [Rpc(CallLocal = true)]
   private void SpawnInitialHands(string[] redHand, string[] blueHand)
   {
      ClearDeck(RedDeck);
      ClearDeck(BlueDeck);

      foreach (var cardId in redHand)
      {
         _logger.Debug("Adding card to red deck: " + cardId);
         RedDeck.AddChild(CreateCard(cardId));
      }

      foreach (var cardId in blueHand)
      {
         _logger.Debug("Adding card to blue deck: " + cardId);
         BlueDeck.AddChild(CreateCard(cardId));
      }

      UpdateDeckVisibility();
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
      if (!OS.HasFeature("dedicated_server"))
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
         Players = Global.NetworkSetup.Players;
         AssignSlots();
         UpdateNamePanels();
      }
   }

   private void AssignSlots()
   {
      if (Players.Count == 0)
         return;

      var host = Players.Values.FirstOrDefault(player => player.Host);
      if (host != null)
         _redPlayerId = host.Id;

      var other = Players.Values.FirstOrDefault(player => player.Id != _redPlayerId);
      if (other != null)
         _bluePlayerId = other.Id;
   }

   private void UpdateNamePanels()
   {
      if (!Players.TryGetValue(_redPlayerId, out var red))
         return;

      RedNamePanel.Text = red.Name;

      if (!Players.TryGetValue(_bluePlayerId, out var blue))
         return;

      BlueNamePanel.Text = blue.Ai && IsOffline ? Tr(blue.Name) : blue.Name;
   }

   private long GetRandomTurnPlayerId()
   {
      if (!Players.ContainsKey(_redPlayerId) || !Players.ContainsKey(_bluePlayerId))
         return _redPlayerId;

      return _rng.RandiRange(0, 1) == 0 ? _redPlayerId : _bluePlayerId;
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

      var localId = Multiplayer.GetUniqueId();
      var showRed = _turnPlayerId == _redPlayerId;
      var showBlue = _turnPlayerId == _bluePlayerId;

      if (Players.TryGetValue(_turnPlayerId, out var player))
         _logger.Debug("Updating deck visibility for {PlayerName}", player.Name);

      RedDeck.Visible = showRed;
      BlueDeck.Visible = showBlue;

      if (showRed)
         ApplyDeckVisibility(RedDeck, _redPlayerId == localId);

      if (showBlue)
         ApplyDeckVisibility(BlueDeck, _bluePlayerId == localId);

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

   private void UpdateStatPanelUi()
   {
      if (Players.Count == 0)
         return;

      if (!Players.TryGetValue(_redPlayerId, out var red))
         return;

      RedBricksPerTurn.Text = red.Quarries.ToString();
      RedBricksAltPerTurn.Text = red.Quarries.ToString();
      RedBricksTotal.Text = red.Bricks.ToString();
      RedBricksAltTotal.Text = red.Bricks.ToString();

      RedGemsPerTurn.Text = red.Magic.ToString();
      RedGemsAltPerTurn.Text = red.Magic.ToString();
      RedGemsTotal.Text = red.Gems.ToString();
      RedGemsAltTotal.Text = red.Gems.ToString();

      RedRecruitsPerTurn.Text = red.Dungeons.ToString();
      RedRecruitsAltPerTurn.Text = red.Dungeons.ToString();
      RedRecruitsTotal.Text = red.Recruits.ToString();
      RedRecruitsAltTotal.Text = red.Recruits.ToString();

      SetStructureHeight(RedTower, red.TowerHp);
      SetStructureHeight(RedWall, red.WallHp);
      RedTowerHpPanel.Text = red.TowerHp.ToString();
      RedWallHpPanel.Text = red.WallHp.ToString();

      if (!Players.TryGetValue(_bluePlayerId, out var blue))
         return;

      BlueBricksPerTurn.Text = blue.Quarries.ToString();
      BlueBricksAltPerTurn.Text = blue.Quarries.ToString();
      BlueBricksTotal.Text = blue.Bricks.ToString();
      BlueBricksAltTotal.Text = blue.Bricks.ToString();

      BlueGemsPerTurn.Text = blue.Magic.ToString();
      BlueGemsAltPerTurn.Text = blue.Magic.ToString();
      BlueGemsTotal.Text = blue.Gems.ToString();
      BlueGemsAltTotal.Text = blue.Gems.ToString();

      BlueRecruitsPerTurn.Text = blue.Dungeons.ToString();
      BlueRecruitsAltPerTurn.Text = blue.Dungeons.ToString();
      BlueRecruitsTotal.Text = blue.Recruits.ToString();
      BlueRecruitsAltTotal.Text = blue.Recruits.ToString();

      SetStructureHeight(BlueTower, blue.TowerHp);
      SetStructureHeight(BlueWall, blue.WallHp);
      BlueTowerHpPanel.Text = blue.TowerHp.ToString();
      BlueWallHpPanel.Text = blue.WallHp.ToString();
   }

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
}

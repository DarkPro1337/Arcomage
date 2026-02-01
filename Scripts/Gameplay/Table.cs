using System;
using System.Collections.Generic;
using System.Linq;
using Arcomage.Core;
using Arcomage.Data;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Gameplay;

public class Player
{
   public long Id { get; init; }
   public string Name { get; init; }
   public bool Host { get; init; }
   public bool Ai { get; set; }
   public bool Ready { get; set; }

   public bool PlayAgain { get; set; } = false;
   public bool Discarding { get; set; } = false;
   public bool DrawCard { get; set; } = false;

   public int TowerHp { get; set; } = Config.Settings.TowerLevels;
   public int WallHp { get; set; } = Config.Settings.WallLevels;

   public int Quarries { get; set; } = Config.Settings.QuarryLevels;
   public int Bricks { get; set; } = Config.Settings.BrickQuantity;
   public int Magic { get; set; } = Config.Settings.MagicLevels;
   public int Gems { get; set; } = Config.Settings.GemQuantity;
   public int Dungeons { get; set; } = Config.Settings.DungeonLevels;
   public int Recruits { get; set; } = Config.Settings.RecruitQuantity;

   public override string ToString() => $"{Name} ({Id})";
}

public partial class Table : Control
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("Table");

   #region Controls

   private Control Particles => GetNode<Control>("Particles");
   private TextureRect GraveyardCardBack => GetNode<TextureRect>("Graveyard/CardBack");
   private GridContainer Graveyard => GetNode<GridContainer>("Graveyard");
   private Label DrawCardLabel => GetNode<Label>("DrawCardLabel");
   private Control EndGameScreen => GetNode<Control>("EndGame");
   private Timer TimeElapsed => GetNode<Timer>("TimeElapsed");

   private HBoxContainer RedDeck => GetNode<HBoxContainer>("RedDeck");
   private HBoxContainer BlueDeck => GetNode<HBoxContainer>("BlueDeck");
   private Label RedNamePanel => GetNode<Label>("RedPanel/Name");
   private Label BlueNamePanel => GetNode<Label>("BluePanel/Name");
   private ColorRect DeckLocker => GetNode<ColorRect>("DeckLocker");

   private Panel RedBricksPanel => GetNode<Panel>("RedBricksPanel");
   private Label RedBricksPerTurn => GetNode<Label>("RedBricksPanel/PerTurn");
   private Label RedBricksTotal => GetNode<Label>("RedBricksPanel/Total");
   private Panel RedGemsPanel => GetNode<Panel>("RedGemsPanel");
   private Label RedGemsPerTurn => GetNode<Label>("RedGemsPanel/PerTurn");
   private Label RedGemsTotal => GetNode<Label>("RedGemsPanel/Total");
   private Panel RedRecruitsPanel => GetNode<Panel>("RedRecruitsPanel");
   private Label RedRecruitsPerTurn => GetNode<Label>("RedRecruitsPanel/PerTurn");
   private Label RedRecruitsTotal => GetNode<Label>("RedRecruitsPanel/Total");

   private Panel BlueBricksPanel => GetNode<Panel>("BlueBricksPanel");
   private Label BlueBricksPerTurn => GetNode<Label>("BlueBricksPanel/PerTurn");
   private Label BlueBricksTotal => GetNode<Label>("BlueBricksPanel/Total");
   private Panel BlueGemsPanel => GetNode<Panel>("BlueGemsPanel");
   private Label BlueGemsPerTurn => GetNode<Label>("BlueGemsPanel/PerTurn");
   private Label BlueGemsTotal => GetNode<Label>("BlueGemsPanel/Total");
   private Panel BlueRecruitsPanel => GetNode<Panel>("BlueRecruitsPanel");
   private Label BlueRecruitsPerTurn => GetNode<Label>("BlueRecruitsPanel/PerTurn");
   private Label BlueRecruitsTotal => GetNode<Label>("BlueRecruitsPanel/Total");

   private Panel RedBricksAltPanel => GetNode<Panel>("RedBricksPanelAlt");
   private Label RedBricksAltPerTurn => GetNode<Label>("RedBricksPanelAlt/PerTurn");
   private Label RedBricksAltTotal => GetNode<Label>("RedBricksPanelAlt/Total");
   private Panel RedGemsAltPanel => GetNode<Panel>("RedGemsPanelAlt");
   private Label RedGemsAltPerTurn => GetNode<Label>("RedGemsPanelAlt/PerTurn");
   private Label RedGemsAltTotal => GetNode<Label>("RedGemsPanelAlt/Total");
   private Panel RedRecruitsAltPanel => GetNode<Panel>("RedRecruitsPanelAlt");
   private Label RedRecruitsAltPerTurn => GetNode<Label>("RedRecruitsPanelAlt/PerTurn");
   private Label RedRecruitsAltTotal => GetNode<Label>("RedRecruitsPanelAlt/Total");

   private Panel BlueBricksAltPanel => GetNode<Panel>("BlueBricksPanelAlt");
   private Label BlueBricksAltPerTurn => GetNode<Label>("BlueBricksPanelAlt/PerTurn");
   private Label BlueBricksAltTotal => GetNode<Label>("BlueBricksPanelAlt/Total");
   private Panel BlueGemsAltPanel => GetNode<Panel>("BlueGemsPanelAlt");
   private Label BlueGemsAltPerTurn => GetNode<Label>("BlueGemsPanelAlt/PerTurn");
   private Label BlueGemsAltTotal => GetNode<Label>("BlueGemsPanelAlt/Total");
   private Panel BlueRecruitsAltPanel => GetNode<Panel>("BlueRecruitsPanelAlt");
   private Label BlueRecruitsAltPerTurn => GetNode<Label>("BlueRecruitsPanelAlt/PerTurn");
   private Label BlueRecruitsAltTotal => GetNode<Label>("BlueRecruitsPanelAlt/Total");
   private Label RedTowerHpPanel => GetNode<Label>("RedTowerPanel/Hp");
   private Label RedWallHpPanel => GetNode<Label>("RedWallPanel/Hp");
   private Label BlueTowerHpPanel => GetNode<Label>("BlueTowerPanel/Hp");
   private Label BlueWallHpPanel => GetNode<Label>("BlueWallPanel/Hp");

   private Control InGameMenu => GetNode<Control>("InGameMenu");

   #endregion

   private readonly RandomNumberGenerator _rng = new();

   public Dictionary<long, Player> Players { get; private set; } = new();
   private long _turnPlayerId;
   private long _redPlayerId = 1;
   private long _bluePlayerId = 2;

   public int Elapsed { get; private set; }
   public string ElapsedString = "00:00";

   public bool IsOffline { get; private set; }

   [Signal]
   public delegate void GraveyardAnimationEndedEventHandler();

   [Signal]
   public delegate void DeckAnimationEndedEventHandler();

   public override void _Input(InputEvent @event)
   {
      base._Input(@event);
      if (!Input.IsActionJustPressed("ui_cancel")) return;
      InGameMenu.Show();
      GetTree().Paused = true;
   }

   public override void _Ready()
   {
      var args = Global.GetCommandLineArgs();
      if (args.TryGetValue("playerName", out var name))
      {
         _Logger.Debug("Player name from command line: " + name);
         Config.Settings.Nickname = name;
      }

      _Logger.Debug("Loaded");
      Global.Table = this;

      LocaleStatPanels();
      if (Multiplayer.IsServer())
         SpawnLocalPlayer();

      InitializePlayersAndUi();

      if (!Multiplayer.IsServer())
         return;

      Multiplayer.PeerConnected += AddPlayer;
      Multiplayer.PeerDisconnected += RemovePlayer;

      SpawnConnectedPlayers();

      _rng.Randomize();
      _turnPlayerId = GetRandomTurnPlayerId();
      AddResources(_turnPlayerId);
      PlaceStartCardsOnDeck();
      if (IsOffline)
         SetTurn(_turnPlayerId);
      else
         Rpc(nameof(SetTurn), _turnPlayerId);
   }

   public override void _ExitTree()
   {
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
      if (IsOffline)
      {
         SpawnInitialHands(
            BuildRandomHandIds(Config.Settings.CardsInHand),
            BuildRandomHandIds(Config.Settings.CardsInHand));
         return;
      }

      if (!Multiplayer.IsServer())
         return;

      var redHand = BuildRandomHandIds(Config.Settings.CardsInHand);
      var blueHand = BuildRandomHandIds(Config.Settings.CardsInHand);
      Rpc(nameof(SpawnInitialHands), redHand, blueHand);
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
         _Logger.Debug("Adding card to red deck: " + cardId);
         RedDeck.AddChild(CreateCard(cardId));
      }

      foreach (var cardId in blueHand)
      {
         _Logger.Debug("Adding card to blue deck: " + cardId);
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
      _Logger.Debug("Adding player with id: " + id);
      if (Players.ContainsKey(id))
         return;
      if (id == 1)
         RegisterPlayer(id, Config.Settings.Nickname);
      else
         RpcId(id, nameof(RequestNickname));
   }

   private void RemovePlayer(long id)
   {
      if (!Players.ContainsKey(id))
         return;

      Players.Remove(id);
   }

   private void AddResources(long playerId)
   {
      if (!Players.TryGetValue(playerId, out var player))
         return;
      player.Bricks += player.Quarries;
      player.Gems += player.Magic;
      player.Recruits += player.Dungeons;
   }

   [Rpc(CallLocal = true)]
   private void SetTurn(long playerId)
   {
      if (!Players.TryGetValue(playerId, out var player))
         return;
      _Logger.Debug("Setting turn to {PlayerName}", player.Name);
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
         _Logger.Debug("Updating deck visibility for {PlayerName}", player.Name);

      RedDeck.Visible = showRed;
      BlueDeck.Visible = showBlue;

      if (showRed)
         ApplyDeckVisibility(RedDeck, _redPlayerId == localId);

      if (showBlue)
         ApplyDeckVisibility(BlueDeck, _bluePlayerId == localId);
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

   private void LocaleStatPanels() =>
      SwitchStatPanel(TranslationServer.GetLocale() == "en");

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

      BlueTowerHpPanel.Text = blue.TowerHp.ToString();
      BlueWallHpPanel.Text = blue.WallHp.ToString();
   }

   public Player GetCurrentPlayer()
   {
      Players.TryGetValue(_turnPlayerId, out var player);
      return player;
   }

   [Rpc]
   public void RequestNickname()
   {
      _Logger.Debug("Nickname requested");
      RpcId(1, nameof(RespondNickname), Config.Settings.Nickname);
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
      _Logger.Debug("Registering player with id: " + id + " and name: " + name);
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
      _Logger.Debug("Adding remote player with id: " + id + " and name: " + name);
      if (Players.ContainsKey(id))
         return;

      var isHost = id == 1;
      Players.Add(id, new Player { Id = id, Name = name, Host = isHost, Ai = false });
      AssignSlots();
      UpdateNamePanels();
   }

   public int GetValue(Player player, ResourceTypes resourceType)
   {
      return resourceType switch
      {
         ResourceTypes.Tower => player.TowerHp,
         ResourceTypes.Wall => player.WallHp,
         ResourceTypes.Quarry => player.Quarries,
         ResourceTypes.Magic => player.Magic,
         ResourceTypes.Dungeon => player.Dungeons,
         ResourceTypes.Bricks => player.Bricks,
         ResourceTypes.Gems => player.Gems,
         ResourceTypes.Recruits => player.Recruits,
         _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Invalid resource type")
      };
   }

   public void GainValue(Player targetPlayer, ResourceTypes resource, int amount)
   {
      switch (resource)
      {
         case ResourceTypes.Tower:
            targetPlayer.TowerHp += amount;
            break;
         case ResourceTypes.Wall:
            targetPlayer.WallHp += amount;
            break;
         case ResourceTypes.Quarry:
            targetPlayer.Quarries += amount;
            break;
         case ResourceTypes.Magic:
            targetPlayer.Magic += amount;
            break;
         case ResourceTypes.Dungeon:
            targetPlayer.Dungeons += amount;
            break;
         case ResourceTypes.Bricks:
            targetPlayer.Bricks += amount;
            break;
         case ResourceTypes.Gems:
            targetPlayer.Gems += amount;
            break;
         case ResourceTypes.Recruits:
            targetPlayer.Recruits += amount;
            break;
         default:
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type");
      }
   }

   public void SetValue(Player targetPlayer, ResourceTypes resource, int amount)
   {
      switch (resource)
      {
         case ResourceTypes.Tower:
            targetPlayer.TowerHp = amount;
            break;
         case ResourceTypes.Wall:
            targetPlayer.WallHp = amount;
            break;
         case ResourceTypes.Quarry:
            targetPlayer.Quarries = amount;
            break;
         case ResourceTypes.Magic:
            targetPlayer.Magic = amount;
            break;
         case ResourceTypes.Dungeon:
            targetPlayer.Dungeons = amount;
            break;
         case ResourceTypes.Bricks:
            targetPlayer.Bricks = amount;
            break;
         case ResourceTypes.Gems:
            targetPlayer.Gems = amount;
            break;
         case ResourceTypes.Recruits:
            targetPlayer.Recruits = amount;
            break;
         default:
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type");
      }
   }

   public Player[] GetTargetPlayer(Player self, TargetType target)
   {
      var players = Players.Values;
      return target switch
      {
         TargetType.Self => [self],
         TargetType.Opponent => [players.FirstOrDefault(player => player.Id != self.Id)],
         TargetType.All => players.ToArray(),
         TargetType.AllExceptSelf => players.Where(player => player.Id != self.Id).ToArray(),
         TargetType.LowestWall => [players.OrderBy(player => GetValue(player, ResourceTypes.Wall)).FirstOrDefault()],
         TargetType.HighestWall => [players.OrderByDescending(player => GetValue(player, ResourceTypes.Wall)).FirstOrDefault()],
         TargetType.LowestTower => [players.OrderBy(player => GetValue(player, ResourceTypes.Tower)).FirstOrDefault()],
         TargetType.HighestTower => [players.OrderByDescending(player => GetValue(player, ResourceTypes.Tower)).FirstOrDefault()],
         _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
      };
   }

   public void Damage(Player target, int amount)
   {
      if (target.WallHp > 0)
         target.WallHp -= amount;
      else
         target.TowerHp -= amount;
   }
}

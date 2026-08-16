using System.Collections.Generic;
using System.Linq;
using Arcomage.Core;
using Arcomage.Networking;
using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   public MatchMode MatchMode { get; private set; } = MatchMode.OneVsOne;
   public bool Ranked { get; private set; }

   private readonly List<long> _seatOrder = [];
   private readonly Dictionary<long, HBoxContainer> _handByPlayer = new();
   private readonly Dictionary<long, SeatHud> _hudByPlayer = new();
   private Node _handsRoot;
   private Control _extraSeatsRoot;
   private long _selectedTargetId;

   public IReadOnlyList<long> SeatOrder => _seatOrder;

   public long GetLocalHumanId()
   {
      if (IsOffline || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
         return 1;

      if (OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless")
         return -1;

      return Multiplayer.GetUniqueId();
   }

   public IEnumerable<Player> LivingPlayers() => Players.Values.Where(player => !player.Eliminated);

   public IEnumerable<Player> EnemiesOf(Player self)
   {
      if (self == null)
         return [];

      return LivingPlayers().Where(player => player.Id != self.Id && AreEnemies(self, player));
   }

   public IEnumerable<Player> AlliesOf(Player self, bool includeSelf = false)
   {
      if (self == null)
         return [];

      return LivingPlayers().Where(player =>
         (includeSelf || player.Id != self.Id) && !AreEnemies(self, player) &&
         (MatchMode != MatchMode.TwoVsTwo || player.TeamId == self.TeamId));
   }

   public bool AreEnemies(Player left, Player right)
   {
      if (left == null || right == null || left.Id == right.Id)
         return false;

      if (MatchMode == MatchMode.TwoVsTwo)
         return left.TeamId != right.TeamId;

      return true;
   }

   public long GetDefaultEnemyId(Player self)
   {
      var enemies = EnemiesOf(self).OrderBy(player => player.TowerHp).ToList();
      if (enemies.Count == 0)
         return 0;

      if (self.SelectedTargetId != 0 && enemies.Any(player => player.Id == self.SelectedTargetId))
         return self.SelectedTargetId;

      return enemies[0].Id;
   }

   public bool IsValidEnemyTarget(Player self, long targetId) => EnemiesOf(self).Any(player => player.Id == targetId);

   private void ConfigureMatchRules()
   {
      MatchMode = Global.PendingMatchMode;
      Ranked = Global.PendingRanked;
      if (IsOffline)
      {
         MatchMode = MatchMode.OneVsOne;
         Ranked = false;
      }
   }

   private void AssignSlots()
   {
      _seatOrder.Clear();
      if (Players.Count == 0)
         return;

      foreach (var player in Players.Values.OrderBy(player => player.Host ? 0 : 1).ThenBy(player => player.Id))
         _seatOrder.Add(player.Id);

      for (var i = 0; i < _seatOrder.Count; i++)
      {
         if (!Players.TryGetValue(_seatOrder[i], out var player))
            continue;

         player.SeatIndex = i;
         player.TeamId = MatchMode == MatchMode.TwoVsTwo ? i % 2 + 1 : 0;
      }

      EnsureHandContainers();
      BindSeatHuds();
   }

   private void EnsureHandsRoot()
   {
      _handsRoot ??= GetNodeOrNull<Node>("Hands") ?? CreateHiddenHandsRoot();
      _extraSeatsRoot ??= GetNodeOrNull<Control>("ExtraSeats") ?? CreateExtraSeatsRoot();
   }

   private Node CreateHiddenHandsRoot()
   {
      var root = new Control
      {
         Name = "Hands",
         Visible = false,
         MouseFilter = MouseFilterEnum.Ignore
      };

      AddChild(root);
      return root;
   }

   private Control CreateExtraSeatsRoot()
   {
      var root = new Control
      {
         Name = "ExtraSeats",
         MouseFilter = MouseFilterEnum.Ignore
      };

      root.SetAnchorsPreset(LayoutPreset.FullRect);
      AddChild(root);
      MoveChild(root, 0);
      return root;
   }

   private void EnsureHandContainers()
   {
      EnsureHandsRoot();
      var localId = GetLocalHumanId();
      foreach (var playerId in _seatOrder)
      {
         if (_handByPlayer.ContainsKey(playerId))
            continue;

         if (playerId == localId || (IsOffline && playerId == _seatOrder[0]))
         {
            _handByPlayer[playerId] = RedDeck;
            continue;
         }

         if (IsOffline && _seatOrder.Count > 1 && playerId == _seatOrder[1])
         {
            _handByPlayer[playerId] = BlueDeck;
            continue;
         }

         var hidden = new HBoxContainer
         {
            Name = $"Hand_{playerId}",
            Visible = false
         };

         _handsRoot.AddChild(hidden);
         _handByPlayer[playerId] = hidden;
      }
   }

   private void BindSeatHuds()
   {
      EnsureHandsRoot();
      foreach (var hud in _hudByPlayer.Values)
         hud.QueueFree();

      _hudByPlayer.Clear();

      var localId = GetLocalHumanId();
      var others = _seatOrder.Where(id => id != localId).ToList();
      if (localId < 0)
         others = [.. _seatOrder];

      if (localId > 0 && Players.ContainsKey(localId))
         _hudByPlayer[localId] = SeatHud.FromExisting(this, true);

      if (others.Count > 0)
         _hudByPlayer[others[0]] = SeatHud.FromExisting(this, false);

      for (var i = 1; i < others.Count; i++)
      {
         var hud = SeatHud.CreateExtra(_extraSeatsRoot, i - 1);
         _hudByPlayer[others[i]] = hud;
      }

      foreach (var (id, hud) in _hudByPlayer)
      {
         hud.PlayerId = id;
         if (id != localId)
            hud.Clicked += OnSeatHudClicked;
      }

      if (others.Count > 0)
         ConnectExistingSeatClick(BlueTower, others[0]);

      UpdateNamePanels();
      HighlightSelectedTarget();
   }

   private void ConnectExistingSeatClick(Control control, long playerId)
   {
      if (control == null)
         return;

      if (control.HasMeta("seat_click"))
         return;

      control.SetMeta("seat_click", playerId);
      control.GuiInput += @event =>
      {
         if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            OnSeatHudClicked(playerId);
      };
   }

   private void OnSeatHudClicked(long playerId)
   {
      var local = GetLocalHumanPlayer();
      if (local == null || !IsValidEnemyTarget(local, playerId))
         return;

      _selectedTargetId = playerId;
      local.SelectedTargetId = playerId;
      HighlightSelectedTarget();
   }

   private Player GetLocalHumanPlayer()
   {
      var id = GetLocalHumanId();
      return id > 0 && Players.TryGetValue(id, out var player) ? player : null;
   }

   private void HighlightSelectedTarget()
   {
      foreach (var (id, hud) in _hudByPlayer)
         hud.SetSelected(id == _selectedTargetId);
   }

   private void PlaceStartCardsOnDeck()
   {
      EnsureHandContainers();
      foreach (var playerId in _seatOrder)
      {
         var deck = GetDeckForPlayer(playerId);
         if (deck == null)
            continue;

         ClearDeck(deck);
         foreach (var cardId in BuildRandomHandIds(Config.Settings.CardsInHand))
            deck.AddChild(CreateCard(cardId));
      }

      UpdateDeckVisibility();
   }

   private long GetRandomTurnPlayerId()
   {
      var living = _seatOrder.Where(id => Players.TryGetValue(id, out var player) && !player.Eliminated).ToList();
      if (living.Count == 0)
         return _seatOrder.FirstOrDefault();

      return living[_rng.RandiRange(0, living.Count - 1)];
   }

   private long GetNextLivingPlayerId(long playerId)
   {
      if (_seatOrder.Count == 0)
         return playerId;

      var start = _seatOrder.IndexOf(playerId);
      if (start < 0)
         start = 0;

      for (var step = 1; step <= _seatOrder.Count; step++)
      {
         var id = _seatOrder[(start + step) % _seatOrder.Count];
         if (Players.TryGetValue(id, out var player) && !player.Eliminated)
            return id;
      }

      return playerId;
   }

   private Vector2 GetSeatPlayOrigin(long playerId)
   {
      if (_hudByPlayer.TryGetValue(playerId, out var hud))
         return hud.GetPlayOrigin();

      return GetPlayCenterPosition();
   }
}

public sealed class SeatHud
{
   public long PlayerId { get; set; }
   public Control Tower { get; init; }
   public Control Wall { get; init; }
   public Label NameLabel { get; init; }
   public Label TowerHp { get; init; }
   public Label WallHp { get; init; }
   public Label BricksPerTurn { get; init; }
   public Label BricksTotal { get; init; }
   public Label BricksAltPerTurn { get; init; }
   public Label BricksAltTotal { get; init; }
   public Label GemsPerTurn { get; init; }
   public Label GemsTotal { get; init; }
   public Label GemsAltPerTurn { get; init; }
   public Label GemsAltTotal { get; init; }
   public Label RecruitsPerTurn { get; init; }
   public Label RecruitsTotal { get; init; }
   public Label RecruitsAltPerTurn { get; init; }
   public Label RecruitsAltTotal { get; init; }
   public Control Root { get; init; }
   public bool Local { get; init; }

   public event System.Action<long> Clicked;

   public static SeatHud FromExisting(Table table, bool local)
   {
      return new SeatHud
      {
         Local = local,
         Tower = table.GetSeatControl(local ? "RedTower" : "BlueTower"),
         Wall = table.GetSeatControl(local ? "RedWall" : "BlueWall"),
         NameLabel = table.GetSeatLabel(local ? "RedPanel/Name" : "BluePanel/Name"),
         TowerHp = table.GetSeatLabel(local ? "RedTowerPanel/Hp" : "BlueTowerPanel/Hp"),
         WallHp = table.GetSeatLabel(local ? "RedWallPanel/Hp" : "BlueWallPanel/Hp"),
         BricksPerTurn = table.GetSeatLabel(local ? "RedBricksPanel/PerTurn" : "BlueBricksPanel/PerTurn"),
         BricksTotal = table.GetSeatLabel(local ? "RedBricksPanel/Total" : "BlueBricksPanel/Total"),
         BricksAltPerTurn = table.GetSeatLabel(local ? "RedBricksPanelAlt/PerTurn" : "BlueBricksPanelAlt/PerTurn"),
         BricksAltTotal = table.GetSeatLabel(local ? "RedBricksPanelAlt/Total" : "BlueBricksPanelAlt/Total"),
         GemsPerTurn = table.GetSeatLabel(local ? "RedGemsPanel/PerTurn" : "BlueGemsPanel/PerTurn"),
         GemsTotal = table.GetSeatLabel(local ? "RedGemsPanel/Total" : "BlueGemsPanel/Total"),
         GemsAltPerTurn = table.GetSeatLabel(local ? "RedGemsPanelAlt/PerTurn" : "BlueGemsPanelAlt/PerTurn"),
         GemsAltTotal = table.GetSeatLabel(local ? "RedGemsPanelAlt/Total" : "BlueGemsPanelAlt/Total"),
         RecruitsPerTurn = table.GetSeatLabel(local ? "RedRecruitsPanel/PerTurn" : "BlueRecruitsPanel/PerTurn"),
         RecruitsTotal = table.GetSeatLabel(local ? "RedRecruitsPanel/Total" : "BlueRecruitsPanel/Total"),
         RecruitsAltPerTurn = table.GetSeatLabel(local ? "RedRecruitsPanelAlt/PerTurn" : "BlueRecruitsPanelAlt/PerTurn"),
         RecruitsAltTotal = table.GetSeatLabel(local ? "RedRecruitsPanelAlt/Total" : "BlueRecruitsPanelAlt/Total")
      };
   }

   public static SeatHud CreateExtra(Control parent, int extraIndex)
   {
      var panel = new Panel
      {
         Name = $"ExtraSeat_{extraIndex}",
         CustomMinimumSize = new Vector2(220, 96)
      };

      panel.SetAnchorsPreset(extraIndex == 0 ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.TopRight);
      panel.OffsetLeft = extraIndex == 0 ? 8 : -228;
      panel.OffsetTop = 8;
      panel.OffsetRight = extraIndex == 0 ? 228 : -8;
      panel.OffsetBottom = 104;

      var name = new Label { Name = "Name", Text = "Player", HorizontalAlignment = HorizontalAlignment.Center };
      name.SetAnchorsPreset(Control.LayoutPreset.TopWide);
      name.OffsetBottom = 22;

      var stats = new Label { Name = "Stats", Text = "T 0  W 0", HorizontalAlignment = HorizontalAlignment.Center };
      stats.SetAnchorsPreset(Control.LayoutPreset.Center);
      stats.OffsetTop = -10;
      stats.OffsetBottom = 12;

      var resources = new Label { Name = "Resources", Text = "", HorizontalAlignment = HorizontalAlignment.Center };
      resources.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
      resources.OffsetTop = -28;

      panel.AddChild(name);
      panel.AddChild(stats);
      panel.AddChild(resources);
      parent.AddChild(panel);

      var hud = new SeatHud
      {
         Local = false,
         Root = panel,
         NameLabel = name,
         TowerHp = stats,
         WallHp = stats,
         BricksTotal = resources,
         GemsTotal = resources,
         RecruitsTotal = resources
      };
      panel.GuiInput += @event =>
      {
         if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            hud.Clicked?.Invoke(hud.PlayerId);
      };
      return hud;
   }

   public void QueueFree()
   {
      Root?.QueueFree();
   }

   public void SetSelected(bool selected)
   {
      if (Root != null)
         Root.Modulate = selected ? new Color(1.2f, 1.1f, 0.6f) : Colors.White;
      else
         NameLabel?.Modulate = selected ? new Color(1f, 0.85f, 0.3f) : Colors.White;
   }

   public void Apply(Player player, bool english)
   {
      if (player == null)
         return;

      NameLabel?.Text = player.Name + (player.Eliminated ? " ✕" : string.Empty) + (player.TeamId > 0 ? $"  [{player.TeamId}]" : string.Empty);

      if (TowerHp != null && WallHp != null && TowerHp == WallHp && Root != null)
      {
         TowerHp.Text = $"T {player.TowerHp}   W {player.WallHp}";
         BricksTotal?.Text = $"{player.Bricks}/{player.Quarries}  {player.Gems}/{player.Magic}  {player.Recruits}/{player.Dungeons}";
         Root.Modulate = player.Eliminated ? new Color(1, 1, 1, 0.45f) : Root.Modulate;
         return;
      }

      TowerHp?.Text = player.TowerHp.ToString();
      WallHp?.Text = player.WallHp.ToString();

      SetPair(BricksPerTurn, BricksAltPerTurn, player.Quarries.ToString(), english);
      SetPair(BricksTotal, BricksAltTotal, player.Bricks.ToString(), english);
      SetPair(GemsPerTurn, GemsAltPerTurn, player.Magic.ToString(), english);
      SetPair(GemsTotal, GemsAltTotal, player.Gems.ToString(), english);
      SetPair(RecruitsPerTurn, RecruitsAltPerTurn, player.Dungeons.ToString(), english);
      SetPair(RecruitsTotal, RecruitsAltTotal, player.Recruits.ToString(), english);

      if (Tower != null)
         Table.SetStructureHeightPublic(Tower, player.TowerHp);

      if (Wall != null)
         Table.SetStructureHeightPublic(Wall, player.WallHp);
   }

   public Vector2 GetPlayOrigin()
   {
      if (Tower != null)
         return Tower.GlobalPosition;

      if (Root != null)
         return Root.GlobalPosition;

      return Vector2.Zero;
   }

   public Control GetResourceControl(Data.ResourceTypes resource, bool english)
   {
      return resource switch
      {
         Data.ResourceTypes.Tower => Tower,
         Data.ResourceTypes.Wall => Wall,
         Data.ResourceTypes.Quarry => english ? BricksPerTurn : BricksAltPerTurn,
         Data.ResourceTypes.Bricks => english ? BricksTotal : BricksAltTotal,
         Data.ResourceTypes.Magic => english ? GemsPerTurn : GemsAltPerTurn,
         Data.ResourceTypes.Gems => english ? GemsTotal : GemsAltTotal,
         Data.ResourceTypes.Dungeon => english ? RecruitsPerTurn : RecruitsAltPerTurn,
         Data.ResourceTypes.Recruits => english ? RecruitsTotal : RecruitsAltTotal,
         _ => null
      };
   }

   private static void SetPair(Label primary, Label alt, string text, bool english)
   {
      primary?.Text = text;
      alt?.Text = text;
   }
}

public partial class Table
{
   public Control GetSeatControl(string path) => GetNode<Control>(path);
   public Label GetSeatLabel(string path) => GetNode<Label>(path);

   public static void SetStructureHeightPublic(Control structure, int hp) => SetStructureHeight(structure, hp);
}

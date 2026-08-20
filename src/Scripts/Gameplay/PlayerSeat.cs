using System;
using Arcomage.Data;
using Godot;

namespace Arcomage.Gameplay;

/// <summary>
/// Left/right table seat coordinator: exported node refs, identity color, and HUD apply.
/// </summary>
[GlobalClass]
public partial class PlayerSeat : Node
{
   public static readonly Color[] SeatColors =
   [
      new(0.90f, 0.24f, 0.18f),
      new(0.42f, 0.48f, 0.98f),
      new(0.95f, 0.78f, 0.22f),
      new(0.30f, 0.78f, 0.40f)
   ];

   private static readonly string[] _towerTopPaths =
   [
      "res://Sprites/TowerTopRed.png",
      "res://Sprites/TowerTopBlue.png",
      "res://Sprites/TowerTopYellow.png",
      "res://Sprites/TowerTopGreen.png"
   ];

   [Export] public Control Tower { get; set; }
   [Export] public TextureRect TowerHead { get; set; }
   [Export] public Control Wall { get; set; }
   [Export] public Label NameLabel { get; set; }
   [Export] public Label TowerHp { get; set; }
   [Export] public Label WallHp { get; set; }
   [Export] public Panel BricksPanel { get; set; }
   [Export] public Label BricksPerTurn { get; set; }
   [Export] public Label BricksTotal { get; set; }
   [Export] public Panel BricksAltPanel { get; set; }
   [Export] public Label BricksAltPerTurn { get; set; }
   [Export] public Label BricksAltTotal { get; set; }
   [Export] public Panel GemsPanel { get; set; }
   [Export] public Label GemsPerTurn { get; set; }
   [Export] public Label GemsTotal { get; set; }
   [Export] public Panel GemsAltPanel { get; set; }
   [Export] public Label GemsAltPerTurn { get; set; }
   [Export] public Label GemsAltTotal { get; set; }
   [Export] public Panel RecruitsPanel { get; set; }
   [Export] public Label RecruitsPerTurn { get; set; }
   [Export] public Label RecruitsTotal { get; set; }
   [Export] public Panel RecruitsAltPanel { get; set; }
   [Export] public Label RecruitsAltPerTurn { get; set; }
   [Export] public Label RecruitsAltTotal { get; set; }

   public long PlayerId { get; set; }
   public Color TowerColor { get; private set; } = Colors.White;
   public Control Root { get; private set; }

   public event Action<long> Clicked;

   private Control.GuiInputEventHandler _guiInputHandler;
   private bool _towerClickBound;

   public override void _Ready()
   {
      EnsureWired();
      BindTowerClick();
   }

   public void EnsureWired()
   {
      if (Tower != null || GetParent() == null || Name.ToString().StartsWith("Extra"))
         return;

      var left = Name == "LeftSeat";
      var prefix = left ? "Red" : "Blue";
      var table = GetParent();
      Tower = table.GetNodeOrNull<Control>($"{prefix}Tower");
      TowerHead = table.GetNodeOrNull<TextureRect>($"{prefix}Tower/TowerHead");
      Wall = table.GetNodeOrNull<Control>($"{prefix}Wall");
      NameLabel = table.GetNodeOrNull<Label>($"{prefix}Panel/Name");
      TowerHp = table.GetNodeOrNull<Label>($"{prefix}TowerPanel/Hp");
      WallHp = table.GetNodeOrNull<Label>($"{prefix}WallPanel/Hp");
      BricksPanel = table.GetNodeOrNull<Panel>($"{prefix}BricksPanel");
      BricksPerTurn = table.GetNodeOrNull<Label>($"{prefix}BricksPanel/PerTurn");
      BricksTotal = table.GetNodeOrNull<Label>($"{prefix}BricksPanel/Total");
      BricksAltPanel = table.GetNodeOrNull<Panel>($"{prefix}BricksPanelAlt");
      BricksAltPerTurn = table.GetNodeOrNull<Label>($"{prefix}BricksPanelAlt/PerTurn");
      BricksAltTotal = table.GetNodeOrNull<Label>($"{prefix}BricksPanelAlt/Total");
      GemsPanel = table.GetNodeOrNull<Panel>($"{prefix}GemsPanel");
      GemsPerTurn = table.GetNodeOrNull<Label>($"{prefix}GemsPanel/PerTurn");
      GemsTotal = table.GetNodeOrNull<Label>($"{prefix}GemsPanel/Total");
      GemsAltPanel = table.GetNodeOrNull<Panel>($"{prefix}GemsPanelAlt");
      GemsAltPerTurn = table.GetNodeOrNull<Label>($"{prefix}GemsPanelAlt/PerTurn");
      GemsAltTotal = table.GetNodeOrNull<Label>($"{prefix}GemsPanelAlt/Total");
      RecruitsPanel = table.GetNodeOrNull<Panel>($"{prefix}RecruitsPanel");
      RecruitsPerTurn = table.GetNodeOrNull<Label>($"{prefix}RecruitsPanel/PerTurn");
      RecruitsTotal = table.GetNodeOrNull<Label>($"{prefix}RecruitsPanel/Total");
      RecruitsAltPanel = table.GetNodeOrNull<Panel>($"{prefix}RecruitsPanelAlt");
      RecruitsAltPerTurn = table.GetNodeOrNull<Label>($"{prefix}RecruitsPanelAlt/PerTurn");
      RecruitsAltTotal = table.GetNodeOrNull<Label>($"{prefix}RecruitsPanelAlt/Total");
   }

   public static Color ColorForSeat(int seatIndex)
   {
      if (seatIndex < 0)
         return Colors.White;

      return SeatColors[seatIndex % SeatColors.Length];
   }

   public void ApplyIdentity(int seatIndex)
   {
      EnsureWired();
      TowerColor = ColorForSeat(seatIndex);
      if (TowerHead == null)
         return;

      var path = _towerTopPaths[seatIndex % _towerTopPaths.Length];
      if (!ResourceLoader.Exists(path))
         return;

      TowerHead.Texture = ResourceLoader.Load<Texture2D>(path);
   }

   public void ShowPrimaryResourcePanels(bool showPrimary)
   {
      EnsureWired();
      SetPairVisible(BricksPanel, BricksAltPanel, showPrimary);
      SetPairVisible(GemsPanel, GemsAltPanel, showPrimary);
      SetPairVisible(RecruitsPanel, RecruitsAltPanel, showPrimary);
   }

   public static PlayerSeat CreateExtra(Control parent, int extraIndex)
   {
      var seat = new PlayerSeat { Name = $"ExtraSeat_{extraIndex}" };
      seat.BuildExtraPanel(parent, extraIndex);
      return seat;
   }

   public void BindClick()
   {
      BindTowerClick();
      if (Root == null || _guiInputHandler != null)
         return;

      _guiInputHandler = OnRootGuiInput;
      Root.GuiInput += _guiInputHandler;
   }

   public void UnbindClick()
   {
      Clicked = null;
      if (Root == null || _guiInputHandler == null || !IsInstanceValid(Root))
         return;

      Root.GuiInput -= _guiInputHandler;
      _guiInputHandler = null;
   }

   public void ReleaseExtra()
   {
      UnbindClick();
      Root?.QueueFree();
      QueueFree();
   }

   public void SetSelected(bool selected)
   {
      if (Root != null)
         Root.Modulate = selected ? new Color(1.2f, 1.1f, 0.6f) : Colors.White;
      else
         NameLabel?.Modulate = selected ? new Color(1f, 0.85f, 0.3f) : Colors.White;
   }

   public void Apply(Player player)
   {
      if (player == null)
         return;

      EnsureWired();

      NameLabel?.Text = player.Name + (player.Eliminated ? " ✕" : string.Empty) + (player.TeamId > 0 ? $"  [{player.TeamId}]" : string.Empty);
      NameLabel?.AddThemeColorOverride("font_color", TowerColor);

      if (TowerHp != null && WallHp != null && TowerHp == WallHp && Root != null)
      {
         TowerHp.Text = $"T {player.TowerHp}   W {player.WallHp}";
         BricksTotal?.Text = $"{player.Bricks}/{player.Quarries}  {player.Gems}/{player.Magic}  {player.Recruits}/{player.Dungeons}";
         Root.Modulate = player.Eliminated ? new Color(1, 1, 1, 0.45f) : Root.Modulate;
         return;
      }

      TowerHp?.Text = player.TowerHp.ToString();
      WallHp?.Text = player.WallHp.ToString();

      SetPair(BricksPerTurn, BricksAltPerTurn, player.Quarries.ToString());
      SetPair(BricksTotal, BricksAltTotal, player.Bricks.ToString());
      SetPair(GemsPerTurn, GemsAltPerTurn, player.Magic.ToString());
      SetPair(GemsTotal, GemsAltTotal, player.Gems.ToString());
      SetPair(RecruitsPerTurn, RecruitsAltPerTurn, player.Dungeons.ToString());
      SetPair(RecruitsTotal, RecruitsAltTotal, player.Recruits.ToString());

      if (Tower != null)
         Table.SetStructureHeightPublic(Tower, player.TowerHp);

      if (Wall != null)
         Table.SetStructureHeightPublic(Wall, player.WallHp);
   }

   public Vector2 GetPlayOrigin()
   {
      if (Tower != null)
         return Tower.GlobalPosition;

      return Root?.GlobalPosition ?? Vector2.Zero;
   }

   public Control GetFeedbackControl(ResourceTypes resource)
   {
      if (Root != null && Tower == null)
         return Root;

      return resource switch
      {
         ResourceTypes.Tower => TowerHead ?? Tower,
         ResourceTypes.Wall => Wall,
         ResourceTypes.Quarry => VisiblePanel(BricksPerTurn, BricksAltPerTurn),
         ResourceTypes.Bricks => VisiblePanel(BricksTotal, BricksAltTotal),
         ResourceTypes.Magic => VisiblePanel(GemsPerTurn, GemsAltPerTurn),
         ResourceTypes.Gems => VisiblePanel(GemsTotal, GemsAltTotal),
         ResourceTypes.Dungeon => VisiblePanel(RecruitsPerTurn, RecruitsAltPerTurn),
         ResourceTypes.Recruits => VisiblePanel(RecruitsTotal, RecruitsAltTotal),
         _ => null
      };
   }

   private void BindTowerClick()
   {
      if (_towerClickBound || Tower == null)
         return;

      _towerClickBound = true;
      Tower.GuiInput += OnTowerGuiInput;
   }

   private void BuildExtraPanel(Control parent, int extraIndex)
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
      parent.AddChild(this);

      Root = panel;
      NameLabel = name;
      TowerHp = stats;
      WallHp = stats;
      BricksTotal = resources;
      GemsTotal = resources;
      RecruitsTotal = resources;
      BindClick();
   }

   private void OnTowerGuiInput(InputEvent @event)
   {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
         Clicked?.Invoke(PlayerId);
   }

   private void OnRootGuiInput(InputEvent @event)
   {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
         Clicked?.Invoke(PlayerId);
   }

   private static void SetPair(Label primary, Label alt, string text)
   {
      primary?.Text = text;
      alt?.Text = text;
   }

   private static void SetPairVisible(CanvasItem primary, CanvasItem alt, bool showPrimary)
   {
      if (showPrimary)
      {
         primary?.Show();
         alt?.Hide();
      }
      else
      {
         primary?.Hide();
         alt?.Show();
      }
   }

   private static Control VisiblePanel(Control primary, Control alt)
   {
      if (primary != null && primary.IsVisibleInTree())
         return primary;

      if (alt != null && alt.IsVisibleInTree())
         return alt;

      return primary ?? alt;
   }
}

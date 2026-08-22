namespace Arcomage.Gameplay;

/// <summary>
/// Left/right table seat coordinator: exported node refs, identity color, and HUD apply.
/// </summary>
[GlobalClass]
public partial class PlayerSeat : Node
{
   private static readonly Color[] _seatColors =
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
   private bool _compactHud;
   private ResourceHud _bricksHud;
   private ResourceHud _gemsHud;
   private ResourceHud _recruitsHud;

   public override void _Ready()
   {
      EnsureWired();
      PackResourceHuds();
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
      PackResourceHuds();
   }

   private void PackResourceHuds()
   {
      _bricksHud = new ResourceHud
      {
         Panel = BricksPanel,
         PerTurn = BricksPerTurn,
         Total = BricksTotal,
         AltPanel = BricksAltPanel,
         AltPerTurn = BricksAltPerTurn,
         AltTotal = BricksAltTotal
      };
      _gemsHud = new ResourceHud
      {
         Panel = GemsPanel,
         PerTurn = GemsPerTurn,
         Total = GemsTotal,
         AltPanel = GemsAltPanel,
         AltPerTurn = GemsAltPerTurn,
         AltTotal = GemsAltTotal
      };
      _recruitsHud = new ResourceHud
      {
         Panel = RecruitsPanel,
         PerTurn = RecruitsPerTurn,
         Total = RecruitsTotal,
         AltPanel = RecruitsAltPanel,
         AltPerTurn = RecruitsAltPerTurn,
         AltTotal = RecruitsAltTotal
      };
   }

   public static Color ColorForSeat(int seatIndex)
   {
      if (seatIndex < 0)
         return Colors.White;

      return _seatColors[seatIndex % _seatColors.Length];
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
      PackResourceHuds();

      _bricksHud.ShowPrimary(showPrimary);
      _gemsHud.ShowPrimary(showPrimary);
      _recruitsHud.ShowPrimary(showPrimary);
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

   public void Apply(Player player, string displayName = null)
   {
      if (player == null)
         return;

      EnsureWired();

      var name = displayName ?? player.Name;
      NameLabel?.Text = name + (player.Eliminated ? " ✕" : string.Empty) + (player.TeamId > 0 ? $"  [{player.TeamId}]" : string.Empty);
      NameLabel?.AddThemeColorOverride("font_color", TowerColor);

      if (_compactHud)
      {
         TowerHp.Text = $"T {player.TowerHp}   W {player.WallHp}";
         BricksTotal?.Text = $"{player.Bricks}/{player.Quarries}  {player.Gems}/{player.Magic}  {player.Recruits}/{player.Dungeons}";
         Root.Modulate = player.Eliminated ? new Color(1, 1, 1, 0.45f) : Root.Modulate;
         return;
      }

      PackResourceHuds();

      TowerHp?.Text = player.TowerHp.ToString();
      WallHp?.Text = player.WallHp.ToString();

      _bricksHud.Set(player.Quarries.ToString(), player.Bricks.ToString());
      _gemsHud.Set(player.Magic.ToString(), player.Gems.ToString());
      _recruitsHud.Set(player.Dungeons.ToString(), player.Recruits.ToString());

      if (Tower != null)
         Table.SetStructureHeight(Tower, player.TowerHp);

      if (Wall != null)
         Table.SetStructureHeight(Wall, player.WallHp);
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

      PackResourceHuds();
      return resource switch
      {
         ResourceTypes.Tower => TowerHead ?? Tower,
         ResourceTypes.Wall => Wall,
         ResourceTypes.Quarry => _bricksHud?.VisiblePerTurn,
         ResourceTypes.Bricks => _bricksHud?.VisibleTotal,
         ResourceTypes.Magic => _gemsHud?.VisiblePerTurn,
         ResourceTypes.Gems => _gemsHud?.VisibleTotal,
         ResourceTypes.Dungeon => _recruitsHud?.VisiblePerTurn,
         ResourceTypes.Recruits => _recruitsHud?.VisibleTotal,
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
      _compactHud = true;

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
}

namespace Arcomage.Gameplay;

public partial class CardControl : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("CardControl");
   private static readonly Color _unaffordableModulate = new(0.5f, 0.5f, 0.5f);

   private static Texture2D _redLayout;
   private static Texture2D _blueLayout;
   private static Texture2D _greenLayout;
   private static Texture2D _nullLayout;

   private Panel _selector;
   private TextureRect _cardBack;
   private Label _nameLabel;
   private TextureRect _art;
   private Label _description;
   private Label _cost;
   private TextureRect _layout;
   private Label _discarded;

   private Panel Selector => _selector ??= GetNode<Panel>("Selector");
   private TextureRect CardBack => _cardBack ??= GetNode<TextureRect>("CardBack");
   private Label NameLabel => _nameLabel ??= GetNode<Label>("Name");
   private TextureRect Art => _art ??= GetNode<TextureRect>("Art");
   private Label Description => _description ??= GetNode<Label>("Description");
   private Label Cost => _cost ??= GetNode<Label>("Cost");
   private TextureRect Layout => _layout ??= GetNode<TextureRect>("Layout");
   private Label Discarded => _discarded ??= GetNode<Label>("Discarded");

   private readonly RandomNumberGenerator _rng = new();
   private bool _faceDown;

   public int CardIdx { get; set; } = -1;
   public string CardId { get; set; }
   public string CardName { get; set; }
   public string CardDescription { get; set; }
   public int CardCost { get; set; }
   public CardType CardLayout { get; set; }
   public string CardArt { get; set; }
   public List<CardFeature> CardFeatures { get; set; }
   public List<ActionBase> CardActions { get; set; }

   public bool Preview { get; set; }
   public bool Discardable { get; set; } = true;
   public bool Usable { get; set; } = true;
   public bool BotUsable { get; set; } = true;
   public bool Used { get; set; }
   public bool UiCardUppercaseText { get; set; }

   public override void _Ready()
   {
      GuiInput += OnGuiInput;
      MouseEntered += OnMouseEntered;
      MouseExited += OnMouseExited;

      _rng.Randomize();

      var cards = Global.DeckManager.GetAllCards();
      Card selectedCard = null;

      if (!string.IsNullOrWhiteSpace(CardId))
         selectedCard = cards.FirstOrDefault(card => card.Id == CardId);

      if (selectedCard == null && CardIdx >= 0 && CardIdx < cards.Count)
         selectedCard = cards[CardIdx];

      if (selectedCard == null && cards.Count > 0)
         selectedCard = cards[_rng.RandiRange(0, cards.Count - 1)];

      if (selectedCard == null)
      {
         _logger.Warn("No cards available to initialize card control.");
         return;
      }

      CardId = selectedCard.Id;
      CardName = selectedCard.Id.ToUpper();
      CardArt = selectedCard.Pic.Replace("../", "res://");
      CardDescription = $"{selectedCard.Id.ToUpper()}_DESC";
      CardCost = selectedCard.Cost;
      CardLayout = selectedCard.Type;
      CardActions = selectedCard.Actions;
      CardFeatures = selectedCard.Features;

      NameLabel.Text = CardName;
      Art.Texture = LoadCardArtTexture(CardArt);
      Description.Text = CardDescription;
      Cost.Text = CardCost.ToString();
      NameLabel.Uppercase = UiCardUppercaseText;
      Name = CardId;

      if (CardFeatures != null && CardFeatures.Contains(CardFeature.NotDiscardable))
         Discardable = false;

      Layout.Texture = LoadLayoutTexture(CardLayout);
      ApplyAffordabilityVisual();
   }

   public void SetFaceDown(bool faceDown)
   {
      _faceDown = faceDown;
      CardBack.Visible = faceDown;
      Layout.Visible = !faceDown;
      Art.Visible = !faceDown;
      NameLabel.Visible = !faceDown;
      Description.Visible = !faceDown;
      Cost.Visible = !faceDown;
      Discarded.Visible = !faceDown && Discarded.Visible;
      MouseFilter = faceDown ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

      ApplyAffordabilityVisual();
   }

   /// <summary>
   /// Flips the card face-up, disables input, and optionally shows the discarded overlay
   /// before it flies to the center or graveyard.
   /// </summary>
   public void BeginPlayAnimation(bool discarded)
   {
      Usable = false;
      Used = true;
      Preview = true;
      Selector.Hide();
      SetFaceDown(false);
      MouseFilter = MouseFilterEnum.Ignore;
      Discarded.Visible = discarded;
      ApplyAffordabilityVisual();
   }

   /// <summary>
   /// Dims the card and sets the cursor when it cannot be played, matching the original
   /// GDScript affordability highlight without polling <c>_PhysicsProcess</c>.
   /// </summary>
   public void ApplyAffordabilityVisual()
   {
      if (Preview || _faceDown || Used)
      {
         Modulate = Colors.White;
         MouseDefaultCursorShape = CursorShape.Arrow;
         return;
      }

      if (Usable)
      {
         Modulate = Colors.White;
         MouseDefaultCursorShape = CursorShape.PointingHand;
         return;
      }

      Modulate = _unaffordableModulate;
      MouseDefaultCursorShape = CursorShape.Forbidden;
   }

   private void OnMouseEntered()
   {
      if (_faceDown)
         return;

      Selector.SelfModulate = Usable ? new Color(1, 1, 1) : new Color(1, 0, 0);
      Selector.Show();
   }

   private void OnMouseExited()
   {
      if (_faceDown)
         return;

      Selector.Hide();
      Selector.SelfModulate = new Color(1, 1, 1);
   }

   private void OnGuiInput(InputEvent @event)
   {
      if (_faceDown || Preview)
         return;

      if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
      {
         _logger.Debug($"LMB pressed on {Name}");
         Global.Table?.OnCardClicked(this, discarded: false);
      }
      else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
      {
         _logger.Debug($"RMB pressed on {Name}");
         Global.Table?.OnCardClicked(this, discarded: true);
      }
   }

   private static Texture2D LoadLayoutTexture(CardType layout)
   {
      return layout switch
      {
         CardType.Brick => _redLayout ??= ResourceLoader.Load<Texture2D>("res://Sprites/RedCardLayout.png"),
         CardType.Gem => _blueLayout ??= ResourceLoader.Load<Texture2D>("res://Sprites/BlueCardLayout.png"),
         CardType.Recruit => _greenLayout ??= ResourceLoader.Load<Texture2D>("res://Sprites/GreenCardLayout.png"),
         _ => LoadNullLayout(layout)
      };
   }

   private static Texture2D LoadNullLayout(CardType layout)
   {
      if (layout != CardType.None)
         _logger.Warn("CardLayout out of range");

      return _nullLayout ??= ResourceLoader.Load<Texture2D>("res://Sprites/NullCardLayout.png");
   }

   /// <summary>
   /// Loads the card art texture from the specified path.
   /// If the texture is imported by Godot's editor, it will be loaded directly using the <see cref="ResourceLoader.Load"/> method.
   /// If the texture is not imported by Godot's editor, it will be imported using the <see cref="ImageTexture.CreateFromImage"/> method.
   /// </summary>
   /// <param name="path">The path to the card art image.</param>
   /// <param name="generateMipmaps">Whether to generate mipmaps for the loaded image.</param>
   /// <returns>The loaded card art as a <see cref="Texture2D"/> object, or a <see cref="PlaceholderTexture2D"/> if the image could not be loaded.</returns>
   private Texture2D LoadCardArtTexture(string path, bool generateMipmaps = true)
   {
      if (ResourceLoader.Exists(path))
         return ResourceLoader.Load<Texture2D>(path);

      var image = new Image();
      if (image.Load(path) != Error.Ok)
      {
         _logger.Warn("Failed to load, path: {Path}", path);
         return new PlaceholderTexture2D();
      }

      _logger.Debug("Loading image that was not imported by editor, path: {Path}", path);
      if (generateMipmaps)
         image.GenerateMipmaps();

      return ImageTexture.CreateFromImage(image);
   }
}

using System.Collections.Generic;
using System.Linq;
using Arcomage.Core;
using Arcomage.Data;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Gameplay;

public partial class CardControl : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("CardControl");

   private Panel Selector => GetNode<Panel>("Selector");
   private TextureRect CardBack => GetNode<TextureRect>("CardBack");
   private Label NameLabel => GetNode<Label>("Name");
   private TextureRect Art => GetNode<TextureRect>("Art");
   private Label Description =>  GetNode<Label>("Description");
   private Label Cost => GetNode<Label>("Cost");
   private TextureRect Layout => GetNode<TextureRect>("Layout");
   private Label Discarded => GetNode<Label>("Discarded");

   private readonly RandomNumberGenerator _rng = new();
   private bool _faceDown;

   public int CardIdx = -1;
   public string CardId;
   public string CardName;
   public string CardDescription;
   public int CardCost;
   public CardType CardLayout;
   public string CardArt;
   public List<CardsUse> CardUses;
   public List<CardFeature> CardFeatures;
   public List<ActionBase> CardActions;

   public bool Preview = false;
   public bool Discardable = true;
   public bool Usable = true;
   public bool BotUsable = true;
   public bool Used = false;
   public bool UiCardUppercaseText = false;

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
      CardUses = selectedCard.Uses;

      NameLabel.Text = CardName;
      Art.Texture = LoadCardArtTexture(CardArt);
      Description.Text = CardDescription;
      Cost.Text = CardCost.ToString();
      NameLabel.Uppercase = UiCardUppercaseText;
      Name = CardId;

      if (CardFeatures != null && CardFeatures.Contains(CardFeature.NotDiscardable))
         Discardable = false;

      switch (CardLayout)
      {
         case CardType.Brick:
            Layout.Texture = GD.Load<Texture2D>("res://Sprites/RedCardLayout.png");
            break;
         case CardType.Gem:
            Layout.Texture = GD.Load<Texture2D>("res://Sprites/BlueCardLayout.png");
            break;
         case CardType.Recruit:
            Layout.Texture = GD.Load<Texture2D>("res://Sprites/GreenCardLayout.png");
            break;
         case CardType.None:
         default:
            _logger.Warn("CardLayout out of range");
            Layout.Texture = GD.Load<Texture2D>("res://Sprites/NullCardLayout.png");
            break;
      }
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
      MouseDefaultCursorShape = CursorShape.Arrow;
      Discarded.Visible = discarded;
   }
    
   private void OnMouseEntered()
   {
      if (_faceDown)
         return;

      if (Usable)
      {
         Selector.SelfModulate = new Color(1, 1, 1);
         Selector.Show();
      }
      else
      {
         Selector.SelfModulate = new Color(1, 0, 0);
         Selector.Show();
      }
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

   public override void _PhysicsProcess(double delta)
   {
      base._PhysicsProcess(delta);
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

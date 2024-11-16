using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace Arcomage.Scripts;

public partial class CardControl : Control
{
    private Panel Selector => GetNode<Panel>("Selector");
    private TextureRect CardBack => GetNode<TextureRect>("CardBack");
    private Label NameLabel => GetNode<Label>("Name");
    private TextureRect Art => GetNode<TextureRect>("Art");
    private Label Description =>  GetNode<Label>("Description");
    private Label Cost => GetNode<Label>("Cost");
    private TextureRect Layout => GetNode<TextureRect>("Layout");
    private Label Discarded => GetNode<Label>("Discarded");

    private readonly RandomNumberGenerator _rng = new();

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

        var cards = Global.CardsList;
        var selectedCard = CardIdx != -1 && CardIdx >= 0 && CardIdx <= cards.Count
            ? cards[CardIdx]
            : cards[_rng.RandiRange(0, cards.Count)];
            
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
        Art.Texture = GD.Load<Texture2D>(CardArt);
        Description.Text = CardDescription;
        Cost.Text = CardCost.ToString();
        NameLabel.Uppercase = UiCardUppercaseText;
        Name = CardId;

        if (CardFeatures != null && CardFeatures.Contains(CardFeature.NotDiscardable)) Discardable = false;

        switch (CardLayout)
        {
            case CardType.Brick:
                Layout.Texture = GD.Load<Texture2D>("res://Sprites/red_card_layout_alt.png");
                break;
            case CardType.Gem:
                Layout.Texture = GD.Load<Texture2D>("res://Sprites/blue_card_layout_alt.png");
                break;
            case CardType.Recruit:
                Layout.Texture = GD.Load<Texture2D>("res://Sprites/green_card_layout_alt.png");
                break;
            case CardType.None:
            default:
                Logger.Error("CardLayout out of range");
                Layout.Texture = GD.Load<Texture2D>("res://Sprites/null_card_layout_alt.png");
                break;
        }
    }
    
    private void OnMouseEntered()
    {
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
        Selector.Hide();
        Selector.SelfModulate = new Color(1, 1, 1);
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
        {
            if (Preview) 
                PrintDebugInfo();
            
            // TODO: implement card usage
        }
        else if (@event is InputEventMouseButton mouseButton2 && mouseButton2.ButtonIndex == MouseButton.Right && mouseButton2.Pressed)
        {
            Logger.Debug($"RMB pressed on {Name}");
            // TODO: implement card discarding
        }
    }

    private void PrintDebugInfo()
    {
        Logger.Debug($"LMB pressed on {Name}");
        var test = CardActions;
        Debugger.Break();
        if (CardActions is { Count: > 0 })
            Logger.Debug("Card actions:\n" + string.Join("\n", CardActions.Select((x, idx) => $"{idx + 1}. {x}")));
        if (CardFeatures is { Count: > 0 })
            Logger.Debug("Card features:\n" + string.Join("\n", CardFeatures.Select(x => x.ToString())));
        if (CardUses is { Count: > 0 })
            Logger.Debug("Card uses:\n" + string.Join("\n", CardUses.Select(x => x.ToString())));
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }
}
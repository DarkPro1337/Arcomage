using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace Arcomage.Scripts;

public enum CardType
{
    Brick,
    Gem,
    Recruits,
    None
}

public enum CardsUse
{
    Attack,
    Defence,
    Resource
}

public enum CardFeature
{
    PlayAgain,
    DrawDiscard,
    NotDiscardable
}

public enum EffectType
{
    Gain,
    Lose,
    Set,
    Damage,
    Swap
}

public enum TargetType
{
    Self,
    Opponent,
    All,
    AllExceptSelf,
    LowestWall,
    HighestWall,
    LowestTower,
    HighestTower
}

public enum ConditionType
{
    LessThan,
    GreaterThan,
    Equals,
    NotEquals,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public enum ResourceTypes
{
    Tower,
    Wall,
    Quarry,
    Magic,
    Dungeon,
    Bricks,
    Gems,
    Recruits,
    OpponentTower,
    OpponentWall,
    OpponentQuarry,
    OpponentMagic,
    OpponentDungeon,
    OpponentBricks,
    OpponentGems,
    OpponentRecruits,
    LowestWall,
    HighestWall,
    HighestTower,
    LowestTower,
    HighestQuarry,
    HighestBricks,
    HighestMagic,
    HighestGems,
    HighestDungeon,
    HighestRecruits,
    LowestQuarry,
    LowestBricks,
    LowestMagic,
    LowestGems,
    LowestDungeon,
    LowestRecruits
}

public enum ActionType
{
    Default,
    Conditional,
    Swap,
}

public class CardAction
{
    public ActionType Type { get; set; }
    public TargetType? Target { get; set; }
    public EffectType? Effect { get; set; }
    public ResourceTypes? Resource { get; set; }
    public object Quantity { get; set; }
    public CardCondition Condition { get; set; }
    public CardAction Then { get; set; }
    public CardAction Else { get; set; }

    public override string ToString()
    {
        var props = GetType().GetProperties().Where(prop => prop.GetValue(this) != null).ToList();
        return string.Join(", ", props.Select(prop => $"{prop.Name}: {prop.GetValue(this)}"));
    }
}

public class CardCondition
{
    public object LeftOperand { get; set; }
    public object Operator { get; set; }
    public object RightOperand { get; set; }
    public CardAction ThenAction { get; set; }
    public CardAction ElseAction { get; set; }

    public override string ToString()
    {
        var props = GetType().GetProperties().Where(prop => prop.GetValue(this) != null).ToList();
        return string.Join(", ", props.Select(prop => $"{prop.Name}: {prop.GetValue(this)}"));
    }
}

public class Cards
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public CardType Type { get; set; }
    public int Cost { get; set; }
    public string Pic { get; set; }
    public List<CardAction> Actions { get; set; }
    public List<CardsUse> Uses { get; set; }
    public List<CardFeature> Features { get; set; }

    public void Load(JsonElement obj)
    {
        Id = obj.GetProperty("id").GetString() ?? throw new InvalidOperationException("Card ID cannot be null");
        Name = obj.GetProperty("name").GetString();
        Description = obj.GetProperty("description").GetString();
        Type = obj.GetProperty("type").GetString() switch
        {
            "brick" => CardType.Brick,
            "gem" => CardType.Gem,
            "recruits" => CardType.Recruits,
            _ => CardType.None
        };
        Cost = obj.GetProperty("cost").GetInt32();
        Pic = obj.GetProperty("pic").GetString();

        DeserializeActions(obj);
        DeserializeFeatures(obj);
        Uses = GenerateUses();
    }
    
    private void DeserializeActions(JsonElement obj)
    {
        if (!obj.TryGetProperty("actions", out var actions)) return;
        var actionStrings = actions.GetString()?.Split(',');

        if (actionStrings == null) return;
        Actions ??= new List<CardAction>();
        foreach (var action in actionStrings)
        {
            if (action.Contains("IF", StringComparison.InvariantCultureIgnoreCase))
            {
                var conditionalAction = ParseConditionalAction(action.Trim());
                if (conditionalAction != null)
                    Actions.Add(conditionalAction);
            }
            else
            {
                var simpleAction = ParseSimpleAction(action.Trim());
                if (simpleAction != null)
                    Actions.Add(simpleAction);
            }
        }
    }
    
    private CardAction ParseSimpleAction(string action)
    {
        var parts = action.Split(':');
        if (parts.Length < 2)
            throw new InvalidOperationException($"Parsing error: invalid action format in action string: {action} with ID: {Id}");

        if (!Enum.TryParse<TargetType>(parts[0], true, out var target))
            throw new InvalidOperationException($"Parsing error: invalid target type in action string: {action} with ID: {Id}");

        var effectPart = parts[1].Split('=');
        if (!Enum.TryParse<EffectType>(effectPart[0], true, out var effect))
            throw new InvalidOperationException($"Parsing error: invalid effect type in action string {action} with ID: {Id}");

        ResourceTypes? resourceType = null;
        object quantity = null;

        if (effectPart.Length > 1)
        {
            if (!TryParseOperand(effectPart[1], out quantity))
                throw new InvalidOperationException($"Parsing error: invalid quantity in effect part: {effectPart[1]} with ID: {Id}");
        }

        if (parts.Length > 2)
        {
            var resourceQuantity = parts[2].Split('=');
            if (!Enum.TryParse<ResourceTypes>(resourceQuantity[0], true, out var resource))
                throw new InvalidOperationException($"Parsing error: invalid resource type in action string: {action} with ID: {Id}");

            if (!TryParseOperand(resourceQuantity[1], out quantity))
                throw new InvalidOperationException($"Parsing error: invalid quantity in action string: {action} with ID: {Id}");

            resourceType = resource;
        }

        return new CardAction
        {
            Target = target,
            Effect = effect,
            Resource = resourceType,
            Quantity = quantity
        };
    }


    private CardAction ParseConditionalAction(string action)
    {
        var regex = new Regex(@"IF\s+(.+?)\s+THEN\s+(.+?)(?:\s+ELSE\s+(.+))?$", RegexOptions.IgnoreCase);
        var match = regex.Match(action);

        if (!match.Success)
            throw new InvalidOperationException($"Parsing error: conditional action string format is invalid: {action}");

        var conditionPart = match.Groups[1].Value.Trim();
        var thenPart = match.Groups[2].Value.Trim();
        var elsePart = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

        var conditionParts = conditionPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (conditionParts.Length != 3)
            throw new InvalidOperationException($"Parsing error: condition format is invalid in conditional action string: {action}");

        if (!TryParseOperand(conditionParts[0], out var leftOperand) ||
            !Enum.TryParse<ConditionType>(conditionParts[1].Replace("_", ""), true, out var conditionType) ||
            !TryParseOperand(conditionParts[2], out var rightOperand))
        {
            throw new InvalidOperationException($"Parsing error: failed to parse condition in conditional action string: {action}");
        }

        var thenAction = !string.IsNullOrEmpty(thenPart) ? ParseSimpleAction(thenPart) : null;
        var elseAction = !string.IsNullOrEmpty(elsePart) ? ParseSimpleAction(elsePart) : null;

        return new CardAction
        {
            Type = ActionType.Conditional,
            Condition = new CardCondition
            {
                LeftOperand = leftOperand,
                Operator = conditionType,
                RightOperand = rightOperand
            },
            Then = thenAction,
            Else = elseAction
        };
    }

    private bool TryParseOperand(string operand, out object result)
    {
        if (int.TryParse(operand, out var intValue)) {
            result = intValue;
            return true;
        }

        if (Enum.TryParse<ResourceTypes>(operand, true, out var resourceType)) {
            result = resourceType;
            return true;
        }

        result = null;
        return false;
    }


    private void DeserializeFeatures(JsonElement obj)
    {
        if (!obj.TryGetProperty("features", out var features)) return;

        Features = features.EnumerateArray()
            .Select(f => GetEnumValue<CardFeature>(f.GetString()))
            .ToList();
    }
    
    private TEnum GetEnumValue<TEnum>(string value, TEnum defaultValue = default) where TEnum : struct => 
        Enum.TryParse<TEnum>(value, true, out var result) ? result : defaultValue;

    private List<CardsUse> GenerateUses()
    {
        var uses = new HashSet<CardsUse>();

        if (Actions != null)
        {
            foreach (var action in Actions)
            {
                if (action.Effect is EffectType.Damage or EffectType.Lose || action.Target is TargetType.Opponent or TargetType.All)
                    uses.Add(CardsUse.Attack);

                if (action.Resource is ResourceTypes.Tower or ResourceTypes.Wall && action.Effect is EffectType.Set or EffectType.Gain)
                    uses.Add(CardsUse.Defence);

                if (action.Effect is EffectType.Gain && action.Resource is ResourceTypes.Bricks or ResourceTypes.Gems
                        or ResourceTypes.Recruits or ResourceTypes.Quarry or ResourceTypes.Magic or ResourceTypes.Dungeon)
                    uses.Add(CardsUse.Resource);
            }
        }
        
        if (Features != null)
            uses.UnionWith(Features.Where(feature => feature is CardFeature.PlayAgain or CardFeature.DrawDiscard).Select(_ => CardsUse.Defence));

        return uses.ToList();
    }
}
    
public partial class Card : Control
{
    private Panel Selector => GetNode<Panel>("Selector");
    private TextureRect CardBack => GetNode<TextureRect>("CardBack");
    private Label NameLabel => GetNode<Label>("Name");
    private TextureRect Art => GetNode<TextureRect>("Art");
    private Label Description =>  GetNode<Label>("Description");
    private Label Cost => GetNode<Label>("Cost");
    private TextureRect Layout => GetNode<TextureRect>("Layout");
    private Label Discarded => GetNode<Label>("Discarded");

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    public readonly List<Cards> CardsList = new();

    public int CardIdx = -1;
    public string CardId;
    public string CardName;
    public string CardDescription;
    public int CardCost;
    public CardType CardLayout;
    public string CardArt;
    public List<CardsUse> CardUses;
    public List<CardFeature> CardFeatures;
    public List<CardAction> CardActions;

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

        var jsonFile = FileAccess.Open("res://Db/base.json", FileAccess.ModeFlags.Read);
        var json = jsonFile.GetAsText();
        jsonFile.Close();
        
        using (var document = JsonDocument.Parse(json)) 
            LoadCards(document);
        
        var selectedCard = CardIdx != -1 && CardIdx >= 0 && CardIdx <= CardsList.Count
            ? CardsList[CardIdx]
            : CardsList[_rng.RandiRange(0, CardsList.Count)];
            
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
            case CardType.Recruits:
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
            Logger.Debug("Card actions:\n" + string.Join("\n", CardsList[CardIdx].Actions.Select((x, idx) => $"{idx + 1}. {x}")));
        if (CardFeatures is { Count: > 0 })
            Logger.Debug("Card features:\n" + string.Join("\n", CardFeatures.Select(x => x.ToString())));
        if (CardUses is { Count: > 0 })
            Logger.Debug("Card uses:\n" + string.Join("\n", CardUses.Select(x => x.ToString())));
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }
    
    private void LoadCards(JsonDocument document)
    {
        var root = document.RootElement;
        if(root.ValueKind != JsonValueKind.Array) return;
        foreach (var card in root.EnumerateArray())
        {
            var newCard = new Cards();
            newCard.Load(card);
            CardsList.Add(newCard);
        }
    }
}
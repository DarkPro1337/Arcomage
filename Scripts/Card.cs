using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcomage.Scripts;

public partial class Card
{
    private static readonly char[] Separator = [' '];
    
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public CardType Type { get; set; }
    public int Cost { get; set; }
    public string Pic { get; set; }
    public List<ICardAction> Actions { get; set; }
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

        Actions = DeserializeActions(obj);
        Features = DeserializeFeatures(obj);
        Uses = GenerateUses();
    }
    
    private List<ICardAction> DeserializeActions(JsonElement obj)
    {
        if (!obj.TryGetProperty("actions", out var actionsElement)) return [];

        var actionStrings = actionsElement.GetString()?.Split(',') ?? [];
        return actionStrings
            .Select(action => action.Trim())
            .Select(ParseAction)
            .ToList();
    }
    
    private ICardAction ParseAction(string action)
    {
        return action.StartsWith("IF", StringComparison.InvariantCultureIgnoreCase) 
            ? ParseConditionalAction(action) 
            : ParseSimpleAction(action);
    }
    
    private ICardAction ParseSimpleAction(string action)
    {
        var parts = action.Split(':');
        if (parts.Length is < 2 or > 3)
            throw new InvalidOperationException($"Invalid action format: {action}");

        var target = ParseEnum<TargetType>(parts[0]);

        if (parts.Length == 2 && parts[1].Contains('='))
        {
            var effectAndAmount = parts[1].Split('=');
            if (effectAndAmount.Length != 2)
                throw new InvalidOperationException($"Invalid effect/amount format: {parts[1]}");

            var effect = ParseEnum<EffectType>(effectAndAmount[0]);

            if (!int.TryParse(effectAndAmount[1], out var amount))
                throw new InvalidOperationException($"Invalid amount: {effectAndAmount[1]}");

            return effect switch
            {
                EffectType.Damage => new DamageAction(target, amount),
                _ => throw new InvalidOperationException($"Effect type {effect} does not support non-resource actions")
            };
        }

        if (parts.Length == 3)
        {
            var effect = ParseEnum<EffectType>(parts[1]);
            var resourceAndAmount = parts[2].Split('=');
            if (resourceAndAmount.Length != 2)
                throw new InvalidOperationException($"Invalid resource/amount format: {parts[2]}");

            var resource = ParseEnum<ResourceTypes>(resourceAndAmount[0]);

            if (int.TryParse(resourceAndAmount[1], out var amount))
            {
                return effect switch
                {
                    EffectType.Gain => new GainAction(target, resource, amount),
                    EffectType.Lose => new LoseAction(target, resource, amount),
                    EffectType.Set => new SetAction(target, resource, amount),
                    _ => throw new InvalidOperationException($"Unknown effect type: {effect}")
                };
            }

            if (effect == EffectType.Swap && Enum.TryParse(resourceAndAmount[1], true, out ResourceTypes targetResource))
                return new SwapAction(target, resource, targetResource);

            if (effect == EffectType.Set && Enum.TryParse(resourceAndAmount[1], true, out targetResource))
                return new SetAction(target, resource, targetResource);

            throw new InvalidOperationException($"Invalid resource or amount: {resourceAndAmount[1]}");
        }

        throw new InvalidOperationException($"Invalid action format: {action}");
    }

    private ConditionalAction ParseConditionalAction(string action)
    {
        var match = CardActionRegex().Match(action);
        if (!match.Success) throw new InvalidOperationException($"Invalid conditional action format: {action}");

        var condition = ParseCondition(match.Groups[1].Value.Trim());
        var thenAction = ParseAction(match.Groups[2].Value.Trim());
        var elseAction = match.Groups[3].Success ? ParseAction(match.Groups[3].Value.Trim()) : null;

        return new ConditionalAction(condition, thenAction, elseAction);
    }

    private Func<Player, Player, Table, bool> ParseCondition(string conditionPart)
    {
        var conditionParts = conditionPart.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (conditionParts.Length != 3) throw new InvalidOperationException($"Invalid condition format: {conditionPart}");

        var leftOperand = ParseOperand(conditionParts[0]);
        var conditionType = ParseEnum<ConditionType>(conditionParts[1].Replace("_", ""));
        var rightOperand = ParseOperand(conditionParts[2]);

        return (self, opponent, context) =>
        {
            var leftValue = GetOperandValue(leftOperand, self, context);
            var rightValue = GetOperandValue(rightOperand, opponent, context);

            return CompareOperands(leftValue, rightValue, conditionType);
        };
    }
    
    private bool CompareOperands(int left, int right, ConditionType conditionType) => conditionType switch
    {
        ConditionType.LessThan => left < right,
        ConditionType.GreaterThan => left > right,
        ConditionType.Equals => left == right,
        ConditionType.NotEquals => left != right,
        ConditionType.GreaterThanOrEqual => left >= right,
        ConditionType.LessThanOrEqual => left <= right,
        _ => throw new InvalidOperationException($"Unknown condition type: {conditionType}")
    };

    private List<CardFeature> DeserializeFeatures(JsonElement obj) =>
        !obj.TryGetProperty("features", out var featuresElement) 
            ? []
            : featuresElement.EnumerateArray().Select(f => ParseEnum<CardFeature>(f.GetString())).ToList();

    private object ParseOperand(string operand)
    {
        if (Enum.TryParse(operand, true, out ResourceTypes resourceType))
            return resourceType;

        if (int.TryParse(operand, out var intValue))
            return intValue;

        throw new InvalidOperationException($"Invalid operand: {operand}");
    }

    private int GetOperandValue(object operand, Player player, Table context)
    {
        return operand switch
        {
            ResourceTypes resource => context.GetValue(player, resource),
            int value => value,
            _ => throw new InvalidOperationException($"Unknown operand type: {operand}")
        };
    }

    private TEnum ParseEnum<TEnum>(string value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : throw new InvalidOperationException($"Invalid enum value: {value}");

    private List<CardsUse> GenerateUses()
    {
        var uses = new HashSet<CardsUse>();
        if (Actions != null)
        {
            foreach (var action in Actions)
            {
                if (action is GainAction gainAction)
                {
                    if (gainAction.Resource is ResourceTypes.Tower or ResourceTypes.Wall)
                        uses.Add(CardsUse.Defence);
                    
                    else if (gainAction.Resource is ResourceTypes.Bricks or ResourceTypes.Gems or ResourceTypes.Recruits or ResourceTypes.Quarry or ResourceTypes.Magic or ResourceTypes.Dungeon) 
                        uses.Add(CardsUse.Resource);
                }
                else if (action is LoseAction or DamageAction)
                {
                    uses.Add(CardsUse.Attack);
                }
                else if (action is SwapAction swapAction)
                {
                    if (swapAction.Resource is ResourceTypes.Wall || swapAction.ResourceToSwap is ResourceTypes.Wall)
                        uses.Add(CardsUse.Defence);
                    
                    if (swapAction.Resource is ResourceTypes.OpponentWall or ResourceTypes.OpponentTower) 
                        uses.Add(CardsUse.Attack);
                }
            }
        }

        if (Features == null) 
            return uses.ToList();
        
        if (Features.Contains(CardFeature.PlayAgain) || Features.Contains(CardFeature.DrawDiscard)) 
            uses.Add(CardsUse.Defence);

        return uses.ToList();
    }

    [GeneratedRegex(@"IF\s+(.+?)\s+THEN\s+(.+?)(?:\s+ELSE\s+(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex CardActionRegex();
}
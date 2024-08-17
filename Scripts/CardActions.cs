using System;

namespace Arcomage.Scripts;

public interface ICardAction
{
    void Execute(Player player, Player opponent, Table table);
}

public class GainAction : ICardAction
{
    public readonly TargetType Target;
    public readonly ResourceTypes Resource;
    public readonly int Amount;

    public GainAction(TargetType target, ResourceTypes resource, int amount)
    {
        Target = target;
        Resource = resource;
        Amount = amount;
    }

    public void Execute(Player player, Player opponent, Table table)
    {
        table.GainValue(player, Resource, Amount);
    }
}

public class LoseAction : ICardAction
{
    public readonly TargetType Target;
    public readonly ResourceTypes Resource;
    public readonly int Amount;

    public LoseAction(TargetType result, ResourceTypes resource, int amount)
    {
        Target = result;
        Resource = resource;
        Amount = amount;
    }

    public void Execute(Player player, Player opponent, Table table)
    {
        table.GetValue(player, Resource);
    }
}

public class SetAction : ICardAction
{
    public readonly ResourceTypes Resource;
    public readonly int Amount;
    public readonly TargetType Target;
    public readonly ResourceTypes TargetResource;

    public SetAction(TargetType result, ResourceTypes resource, int amount)
    {
        Resource = resource;
        Amount = amount;
    }

    public SetAction(TargetType target, ResourceTypes resource, ResourceTypes targetResource)
    {
        Target = target;
        Resource = resource;
        TargetResource = targetResource;
    }

    public void Execute(Player player, Player opponent, Table table)
    {
        table.SetValue(player, Resource, Amount);
    }
}

public class DamageAction : ICardAction
{
    public TargetType Target;
    public int Amount;

    public DamageAction(TargetType target, int amount)
    {
        Target = target;
        Amount = amount;
    }

    public void Execute(Player player, Player opponent, Table table)
    {
        var targetPlayers = table.GetTargetPlayer(player, Target);
        foreach (var target in targetPlayers) 
            table.Damage(target, Amount);
    }
}

public class SwapAction : ICardAction
{
    private readonly TargetType _target;
    
    public readonly ResourceTypes Resource;
    public readonly ResourceTypes ResourceToSwap;

    public SwapAction(TargetType target, ResourceTypes resource, ResourceTypes resourceToSwap)
    {
        _target = target;
        Resource = resource;
        ResourceToSwap = resourceToSwap;
    }

    public void Execute(Player player, Player opponent, Table context)
    {
        var selfValue = context.GetValue(player, Resource);
        var opponentValue = context.GetValue(opponent, ResourceToSwap);

        context.SetValue(player, Resource, opponentValue);
        context.SetValue(opponent, ResourceToSwap, selfValue);
    }
}

public class ConditionalAction : ICardAction
{
    public readonly Func<Player, Player, Table, bool> Condition;
    public readonly ICardAction ThenAction;
    public readonly ICardAction ElseAction;

    public ConditionalAction(Func<Player, Player, Table, bool> condition,
        ICardAction thenAction,
        ICardAction elseAction = null)
    {
        Condition = condition;
        ThenAction = thenAction;
        ElseAction = elseAction;
    }

    public void Execute(Player player, Player opponent, Table context)
    {
        if (Condition(player, opponent, context))
            ThenAction.Execute(player, opponent, context);
        else
            ElseAction?.Execute(player, opponent, context);
    }
}
using System;

namespace Arcomage.Scripts;

public interface ICardAction
{
    void Execute(Player self, Player opponent, Table table);
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

    public void Execute(Player self, Player opponent, Table table)
    {
        var value = table.GetValue(self, Resource);
        table.SetValue(self, Resource, value + Amount);
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

    public void Execute(Player self, Player opponent, Table table)
    {
        table.GetValue(self, Resource);
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

    public void Execute(Player self, Player opponent, Table table)
    {
        // TODO: Implement execute
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

    public void Execute(Player self, Player opponent, Table table)
    {
        var targetPlayer = table.GetTargetPlayer(self, Target);
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

    public void Execute(Player self, Player opponent, Table context)
    {
        var selfValue = context.GetValue(self, Resource);
        var opponentValue = context.GetValue(opponent, ResourceToSwap);

        context.SetValue(self, Resource, opponentValue);
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

    public void Execute(Player self, Player opponent, Table context)
    {
        if (Condition(self, opponent, context))
            ThenAction.Execute(self, opponent, context);
        else
            ElseAction?.Execute(self, opponent, context);
    }
}
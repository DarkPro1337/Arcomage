using System.Collections.Generic;
using System.Linq;
using Arcomage.Gameplay;

namespace Arcomage.Data;

public abstract class ActionBase
{
   public abstract void Execute(Table gameState);
}

public class MethodCallAction : ActionBase
{
   public TargetType Target { get; init; }
   public ResourceTypes? Resource { get; init; }
   public EffectType Method { get; init; }
   public List<Expression> Arguments { get; init; } = [];

   public override void Execute(Table gameState)
   {
      var self = gameState.GetCurrentPlayer();
      if (self == null)
         return;

      var targets = gameState.GetTargetPlayer(self, Target)
         .Where(player => player != null)
         .ToArray();

      if (targets.Length == 0)
         return;

      if (Method == EffectType.Damage)
      {
         var amount = GetArgumentValue(gameState, 0);
         foreach (var target in targets)
            gameState.Damage(target, amount);
         return;
      }

      if (!Resource.HasValue)
         return;

      var value = GetArgumentValue(gameState, 0);
      switch (Method)
      {
         case EffectType.Gain:
            foreach (var target in targets)
               gameState.GainValue(target, Resource.Value, value);
            break;
         case EffectType.Lose:
            foreach (var target in targets)
               gameState.GainValue(target, Resource.Value, -value);
            break;
         case EffectType.Set:
            foreach (var target in targets)
               gameState.SetValue(target, Resource.Value, value);
            break;
         case EffectType.Swap:
            var swapTarget = ResolveSwapTarget(gameState, self);
            if (swapTarget == null)
               return;
            var left = targets.FirstOrDefault();
            if (left == null)
               return;
            var leftValue = gameState.GetValue(left, Resource.Value);
            var rightValue = gameState.GetValue(swapTarget.Value.Player, swapTarget.Value.Resource);
            gameState.SetValue(left, Resource.Value, rightValue);
            gameState.SetValue(swapTarget.Value.Player, swapTarget.Value.Resource, leftValue);
            break;
      }
   }

   private int GetArgumentValue(Table gameState, int index)
   {
      if (Arguments == null || index < 0 || index >= Arguments.Count)
         return 0;
      return Arguments[index].Evaluate(gameState);
   }

   private (Player Player, ResourceTypes Resource)? ResolveSwapTarget(Table gameState, Player self)
   {
      if (Arguments == null || Arguments.Count == 0)
         return null;
      if (Arguments[0] is not VariableExpression variable)
         return null;

      var target = gameState.GetTargetPlayer(self, variable.Target)
         .FirstOrDefault(player => player != null);
      if (target == null)
         return null;

      return (target, variable.Resource);
   }

}

public class ConditionalAction : ActionBase
{
   public Expression Condition { get; set; }
   public List<ActionBase> ThenActions { get; set; } = [];
   public List<ActionBase> ElseActions { get; set; } = [];

   public override void Execute(Table gameState)
   {
      if (Condition == null)
         return;

      var result = Condition.Evaluate(gameState) != 0;
      var actions = result ? ThenActions : ElseActions;
      foreach (var action in actions)
         action.Execute(gameState);
   }
}

public abstract class Expression
{
   public abstract int Evaluate(Table gameState);
}

public class NumberExpression : Expression
{
   public int Value { get; init; }

   public override int Evaluate(Table gameState) => Value;
}

public class VariableExpression : Expression
{
   public TargetType Target { get; init; }
   public ResourceTypes Resource { get; init; }

   public override int Evaluate(Table gameState)
   {
      var self = gameState.GetCurrentPlayer();
      if (self == null)
         return 0;

      var targetPlayer = gameState.GetTargetPlayer(self, Target)
         .FirstOrDefault(player => player != null);
      if (targetPlayer == null)
         return 0;

      return gameState.GetValue(targetPlayer, Resource);
   }
}

public class AggregateExpression : Expression
{
   public ResourceTypes Aggregate { get; set; }

   public override int Evaluate(Table gameState)
   {
      return ActionEnumHelpers.GetAggregateValue(gameState, Aggregate);
   }
}

public class BinaryExpression : Expression
{
   public Expression Left { get; init; }
   public Expression Right { get; init; }
   public string Operator { get; init; }

   public override int Evaluate(Table gameState)
   {
      var left = Left?.Evaluate(gameState) ?? 0;
      var right = Right?.Evaluate(gameState) ?? 0;

      return Operator switch
      {
         "+" => left + right,
         "-" => left - right,
         "*" => left * right,
         "/" => right == 0 ? 0 : left / right,
         "==" => left == right ? 1 : 0,
         "!=" => left != right ? 1 : 0,
         "<" => left < right ? 1 : 0,
         ">" => left > right ? 1 : 0,
         "<=" => left <= right ? 1 : 0,
         ">=" => left >= right ? 1 : 0,
         _ => 0
      };
   }
}

internal static class ActionEnumHelpers
{
   public static bool IsAggregateResource(ResourceTypes resource)
   {
      return resource switch
      {
         ResourceTypes.HighestWall => true,
         ResourceTypes.LowestWall => true,
         ResourceTypes.HighestTower => true,
         ResourceTypes.LowestTower => true,
         ResourceTypes.HighestQuarry => true,
         ResourceTypes.LowestQuarry => true,
         ResourceTypes.HighestBricks => true,
         ResourceTypes.LowestBricks => true,
         ResourceTypes.HighestMagic => true,
         ResourceTypes.LowestMagic => true,
         ResourceTypes.HighestGems => true,
         ResourceTypes.LowestGems => true,
         ResourceTypes.HighestDungeon => true,
         ResourceTypes.LowestDungeon => true,
         ResourceTypes.HighestRecruits => true,
         ResourceTypes.LowestRecruits => true,
         _ => false
      };
   }

   public static int GetAggregateValue(Table gameState, ResourceTypes resource)
   {
      var players = gameState.Players.Values;
      if (players.Count == 0)
         return 0;

      return resource switch
      {
         ResourceTypes.HighestWall => players.Max(player => player.WallHp),
         ResourceTypes.LowestWall => players.Min(player => player.WallHp),
         ResourceTypes.HighestTower => players.Max(player => player.TowerHp),
         ResourceTypes.LowestTower => players.Min(player => player.TowerHp),
         ResourceTypes.HighestQuarry => players.Max(player => player.Quarries),
         ResourceTypes.LowestQuarry => players.Min(player => player.Quarries),
         ResourceTypes.HighestBricks => players.Max(player => player.Bricks),
         ResourceTypes.LowestBricks => players.Min(player => player.Bricks),
         ResourceTypes.HighestMagic => players.Max(player => player.Magic),
         ResourceTypes.LowestMagic => players.Min(player => player.Magic),
         ResourceTypes.HighestGems => players.Max(player => player.Gems),
         ResourceTypes.LowestGems => players.Min(player => player.Gems),
         ResourceTypes.HighestDungeon => players.Max(player => player.Dungeons),
         ResourceTypes.LowestDungeon => players.Min(player => player.Dungeons),
         ResourceTypes.HighestRecruits => players.Max(player => player.Recruits),
         ResourceTypes.LowestRecruits => players.Min(player => player.Recruits),
         _ => 0
      };
   }
}

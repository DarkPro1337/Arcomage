using System.Collections.Generic;
using Arcomage.Scripts.Gameplay;

namespace Arcomage.Scripts.Data;

public abstract class ActionBase
{
   public abstract void Execute(Table gameState);
}

public class MethodCallAction : ActionBase
{
   public string Receiver { get; set; }
   public string Method { get; set; }
   public List<Expression> Arguments { get; set; } = [];

   public override void Execute(Table gameState)
   {
      // TODO: Implementation for executing the action
   }
}

public class ConditionalAction : ActionBase
{
   public Expression Condition { get; set; }
   public List<ActionBase> ThenActions { get; set; } = [];
   public List<ActionBase> ElseActions { get; set; } = [];

   public override void Execute(Table gameState)
   {
      // TODO: Implementation for executing the action
   }
}

public abstract class Expression
{
   public abstract int Evaluate(Table gameState);
}

public class NumberExpression : Expression
{
   public int Value { get; set; }

   public override int Evaluate(Table gameState) => Value;
}

public class VariableExpression : Expression
{
   public string VariableName { get; set; }

   public override int Evaluate(Table gameState)
   {
      return 0; // TODO: Implementation for evaluating the variable
   }
}

public class BinaryExpression : Expression
{
   public Expression Left { get; set; }
   public Expression Right { get; set; }
   public string Operator { get; set; }

   public override int Evaluate(Table gameState)
   {
      return 0; // TODO: Implementation for evaluating the binary expression
   }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace Arcomage.Scripts;

public static class ActionParser
{
   // Helper method to create a failing parser
   private static Parser<T> Fail<T>(string message)
   {
      return input => Result.Failure<T>(input, message, Array.Empty<string>());
   }

   // Parser for simple identifiers (e.g., self, Tower)
   private static readonly Parser<string> Identifier =
      from first in Parse.Letter.Or(Parse.Char('_'))
      from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
      select new string(new[] { first }.Concat(rest).ToArray());

   // Parser for numbers (e.g., 10, 5)
   private static readonly Parser<Expression> Number =
      Parse.Number.Token().Select(n => new NumberExpression { Value = int.Parse(n) });

   // Parser for variable expressions (e.g., self.Tower)
   private static readonly Parser<Expression> Variable =
      Identifier.DelimitedBy(Parse.Char('.'))
         .Select(ids => string.Join(".", ids))
         .Select(id => new VariableExpression { VariableName = id });

   // Forward declaration for recursive expressions
   public static readonly Parser<Expression> ExpressionParser = Parse.Ref(() => Expression);

   // Parser for parentheses
   private static readonly Parser<Expression> ParenthesizedExpression =
      from lparen in Parse.Char('(').Token()
      from expr in ExpressionParser
      from rparen in Parse.Char(')').Token()
      select expr;

   // Parser for basic expressions (number, variable, or parenthesized expression)
   private static readonly Parser<Expression> Operand =
      Number.Or(Variable).Or(ParenthesizedExpression);

   // Parser for multiplicative expressions (*, /)
   private static readonly Parser<Expression> MultiplicativeExpression =
      Parse.ChainOperator(
         Parse.Char('*').Token().Select(c => c.ToString()),
         Operand,
         (op, left, right) => new BinaryExpression
         {
            Left = left,
            Right = right,
            Operator = op
         });

   // Parser for additive expressions (+, -)
   private static readonly Parser<Expression> AdditiveExpression =
      Parse.ChainOperator(
         Parse.Char('+').Token().Select(c => c.ToString()),
         MultiplicativeExpression,
         (op, left, right) => new BinaryExpression
         {
            Left = left,
            Right = right,
            Operator = op
         });

   // Parser for comparison expressions (==, !=, <, >, <=, >=)
   private static readonly Parser<Expression> ComparisonExpression =
      from left in AdditiveExpression
      from op in Parse.String("==").Text().Token()
         .Or(Parse.String("!=").Text().Token())
         .Or(Parse.String("<=").Text().Token())
         .Or(Parse.String(">=").Text().Token())
         .Or(Parse.Char('<').Token().Select(c => c.ToString()))
         .Or(Parse.Char('>').Token().Select(c => c.ToString()))
      from right in AdditiveExpression
      select new BinaryExpression
      {
         Left = left,
         Right = right,
         Operator = op
      };

   // The main expression parser
   private static readonly Parser<Expression> Expression =
      ComparisonExpression.Or(AdditiveExpression);

   // Parser for method call arguments
   private static readonly Parser<List<Expression>> Arguments =
      from args in Expression.DelimitedBy(Parse.Char(',').Token()).Optional()
      select args.GetOrElse([]).ToList();

   // Parser to split the receiver and method from a qualified identifier
   private static readonly Parser<(string Receiver, string Method)> ReceiverAndMethod =
      Identifier.DelimitedBy(Parse.Char('.'))
         .Select(ids => ids.ToList())
         .Then(idList =>
         {
            if (idList.Count < 2)
               return Fail<(string Receiver, string Method)>("Expected at least two identifiers for method call");
            var receiver = string.Join(".", idList.Take(idList.Count - 1));
            var method = idList.Last();
            return Parse.Return((Receiver: receiver, Method: method));

         });

   // Parser for method calls (e.g., self.Tower.Gain(10))
   private static readonly Parser<MethodCallAction> MethodCall =
      from rm in ReceiverAndMethod
      from lparen in Parse.Char('(').Token()
      from args in Arguments
      from rparen in Parse.Char(')').Token()
      select new MethodCallAction
      {
         Receiver = rm.Receiver,
         Method = rm.Method,
         Arguments = args
      };

   // Parser for conditional actions
   private static readonly Parser<ConditionalAction> Conditional =
      from ifKeyword in Parse.String("if").Token()
      from condition in Expression
      from thenKeyword in Parse.String("then").Token()
      from thenActions in ActionList
      from elseActions in (
         from elseKeyword in Parse.String("else").Token()
         from elseActs in ActionList
         select elseActs).Optional()
      select new ConditionalAction
      {
         Condition = condition,
         ThenActions = thenActions,
         ElseActions = elseActions.GetOrElse([])
      };

   // Parser for a single action
   public static readonly Parser<ActionBase> Action =
      Conditional.Select(ActionBase (a) => a)
         .Or(MethodCall.Select(ActionBase (a) => a));

   // Parser for a list of actions
   private static readonly Parser<List<ActionBase>> ActionList =
      from lbracket in Parse.Char('[').Token().Optional()
      from actions in Action.DelimitedBy(Parse.Char(',').Token())
      from rbracket in Parse.Char(']').Token().Optional()
      select actions.ToList();
}
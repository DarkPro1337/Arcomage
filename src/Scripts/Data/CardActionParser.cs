using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace Arcomage.Data;

public static class ActionParser
{
   // Helper method to create a failing parser
   private static Parser<T> Fail<T>(string message) => input => Result.Failure<T>(input, message, []);

   // Parser for simple identifiers (e.g., self, Tower)
   private static readonly Parser<string> _identifier =
      from first in Parse.Letter.Or(Parse.Char('_'))
      from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
      select new string(new[] { first }.Concat(rest).ToArray());

   // Parser for numbers (e.g., 10, 5)
   private static readonly Parser<Expression> _number =
      Parse.Number.Token().Select(n => new NumberExpression { Value = int.Parse(n) });

   // Parser for variable expressions (e.g., self.Tower or highestMagic)
   private static readonly Parser<Expression> _variable =
      _identifier.DelimitedBy(Parse.Char('.'))
         .Select(ids => ids.ToList())
         .Then(ParseVariable);

   // Helper method to parse variables from a list of identifiers
   private static Parser<Expression> ParseVariable(List<string> parts)
   {
      return parts.Count switch
      {
         2 => ParseTargetResourceVariable(parts),
         1 => ParseAggregateVariable(parts[0]),
         _ => Fail<Expression>("Expected variable in form target.resource or aggregateResource")
      };
   }

   // Helper method to parse variables in the form target.resource
   private static Parser<Expression> ParseTargetResourceVariable(List<string> parts)
   {
      if (!EnumTokenParser.TryParseToken(parts[0], out TargetType target))
         return Fail<Expression>($"Unknown target '{parts[0]}'");

      if (!EnumTokenParser.TryParseToken(parts[1], out ResourceTypes resource))
         return Fail<Expression>($"Unknown resource '{parts[1]}'");

      return Parse.Return<Expression>(new VariableExpression { Target = target, Resource = resource });
   }

   // Helper method to parse variables in the form aggregateResource
   private static Parser<Expression> ParseAggregateVariable(string token)
   {
      if (!EnumTokenParser.TryParseToken(token, out ResourceTypes resource))
         return Fail<Expression>($"Unknown resource '{token}'");

      if (!ActionEnumHelpers.IsAggregateResource(resource))
         return Fail<Expression>($"Expected aggregate resource, got '{token}'");

      return Parse.Return<Expression>(new AggregateExpression { Aggregate = resource });
   }

   // Forward declaration for recursive expressions
   public static readonly Parser<Expression> ExpressionParser = Parse.Ref(() => _expression);

   // Parser for parentheses
   private static readonly Parser<Expression> _parenthesizedExpression =
      from lparen in Parse.Char('(').Token()
      from expr in ExpressionParser
      from rparen in Parse.Char(')').Token()
      select expr;

   // Parser for basic expressions (number, variable, or parenthesized expression)
   private static readonly Parser<Expression> _operand =
      _number.Or(_variable).Or(_parenthesizedExpression);

   // Parser for multiplicative expressions (*, /)
   private static readonly Parser<Expression> _multiplicativeExpression =
      Parse.ChainOperator(
         Parse.Char('*').Token().Select(c => c.ToString()),
         _operand,
         (op, left, right) => new BinaryExpression
         {
            Left = left,
            Right = right,
            Operator = op
         });

   // Parser for additive expressions (+, -)
   private static readonly Parser<Expression> _additiveExpression =
      Parse.ChainOperator(
         Parse.Char('+').Token().Select(c => c.ToString()),
         _multiplicativeExpression,
         (op, left, right) => new BinaryExpression
         {
            Left = left,
            Right = right,
            Operator = op
         });

   // Parser for comparison expressions (==, !=, <, >, <=, >=)
   private static readonly Parser<Expression> _comparisonExpression =
      from left in _additiveExpression
      from op in Parse.String("==").Text().Token()
         .Or(Parse.String("!=").Text().Token())
         .Or(Parse.String("<=").Text().Token())
         .Or(Parse.String(">=").Text().Token())
         .Or(Parse.Char('<').Token().Select(c => c.ToString()))
         .Or(Parse.Char('>').Token().Select(c => c.ToString()))
      from right in _additiveExpression
      select new BinaryExpression
      {
         Left = left,
         Right = right,
         Operator = op
      };

   // The main expression parser
   private static readonly Parser<Expression> _expression =
      _comparisonExpression.Or(_additiveExpression);

   // Parser for method call arguments
   private static readonly Parser<List<Expression>> _arguments =
      from args in _expression.DelimitedBy(Parse.Char(',').Token()).Optional()
      select args.GetOrElse([]).ToList();

   // Parser to split the receiver and method from a qualified identifier
   private static readonly Parser<(TargetType Target, ResourceTypes? Resource, EffectType Method)> _receiverAndMethod =
      _identifier.DelimitedBy(Parse.Char('.'))
         .Select(ids => ids.ToList())
         .Then(ParseReceiverAndMethod);

   // Helper method to parse the receiver and method from a qualified identifier
   private static Parser<(TargetType Target, ResourceTypes? Resource, EffectType Method)> ParseReceiverAndMethod(List<string> idList)
   {
      if (idList.Count < 2)
         return Fail<(TargetType Target, ResourceTypes? Resource, EffectType Method)>(
            "Expected at least two identifiers for method call");

      var methodToken = idList[^1];
      var receiverParts = idList.Take(idList.Count - 1).ToList();

      if (!TryParseReceiver(receiverParts, out var target, out var resource, out var receiverError))
         return Fail<(TargetType Target, ResourceTypes? Resource, EffectType Method)>(receiverError);

      if (!EnumTokenParser.TryParseToken(methodToken, out EffectType effect))
         return Fail<(TargetType Target, ResourceTypes? Resource, EffectType Method)>($"Unknown effect '{methodToken}'");

      return Parse.Return((Target: target, Resource: resource, Method: effect));
   }

   // Helper method to parse the receiver of a method call
   private static bool TryParseReceiver(
      List<string> receiverParts,
      out TargetType target,
      out ResourceTypes? resource,
      out string error)
   {
      target = default;
      resource = null;
      error = string.Empty;

      if (receiverParts.Count is 0 or > 2)
      {
         error = "Expected receiver in form target or target.resource";
         return false;
      }

      if (!EnumTokenParser.TryParseToken(receiverParts[0], out target))
      {
         error = $"Unknown target '{receiverParts[0]}'";
         return false;
      }

      if (receiverParts.Count == 1)
         return true;

      if (!EnumTokenParser.TryParseToken(receiverParts[1], out ResourceTypes resourceValue))
      {
         error = $"Unknown resource '{receiverParts[1]}'";
         return false;
      }

      resource = resourceValue;
      return true;
   }

   // Parser for method calls (e.g., self.Tower.Gain(10))
   private static readonly Parser<MethodCallAction> _methodCall =
      from rm in _receiverAndMethod
      from lparen in Parse.Char('(').Token()
      from args in _arguments
      from rparen in Parse.Char(')').Token()
      select new MethodCallAction
      {
         Target = rm.Target,
         Resource = rm.Resource,
         Method = rm.Method,
         Arguments = args
      };

   // Parser for conditional actions
   private static readonly Parser<ConditionalAction> _conditional =
      from ifKeyword in Parse.String("if").Token()
      from condition in _expression
      from thenKeyword in Parse.String("then").Token()
      from thenActions in _actionList
      from elseActions in (
         from elseKeyword in Parse.String("else").Token()
         from elseActs in _actionList
         select elseActs).Optional()
      select new ConditionalAction
      {
         Condition = condition,
         ThenActions = thenActions,
         ElseActions = elseActions.GetOrElse([])
      };

   // Parser for a single action
   public static readonly Parser<ActionBase> Action =
      _conditional.Select(ActionBase (a) => a)
         .Or(_methodCall.Select(ActionBase (a) => a));

   // Parser for a list of actions
   private static readonly Parser<List<ActionBase>> _actionList =
      from lbracket in Parse.Char('[').Token().Optional()
      from actions in Action.DelimitedBy(Parse.Char(',').Token())
      from rbracket in Parse.Char(']').Token().Optional()
      select actions.ToList();
}

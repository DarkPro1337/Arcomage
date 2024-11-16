using System;
using System.Collections.Generic;
using Sprache;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Arcomage.Scripts;

public class ActionTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => typeof(ActionBase).IsAssignableFrom(type);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is Scalar scalar)
        {
            var actionString = scalar.Value;
            parser.MoveNext();

            var parsedAction = ParseActionString(actionString);
            return parsedAction;
        }

        if (parser.Current is MappingStart)
        {
            var conditionalAction = ReadConditionalAction(parser, rootDeserializer);
            return conditionalAction;
        }

        throw new InvalidOperationException("Unsupported action format.");
    }

    public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException("Writing actions to YAML is not supported. Why do you need this? :)");
    }

    private ActionBase ParseActionString(string actionString)
    {
        var result = ActionParser.Action.TryParse(actionString);
        if (result.WasSuccessful)
            return result.Value;

        throw new InvalidOperationException($"Failed to parse action: {actionString}\nError: {result.Message}");
    }

    private Expression ParseExpressionString(string expressionString)
    {
        var result = ActionParser.ExpressionParser.TryParse(expressionString);
        if (result.WasSuccessful)
            return result.Value;

        throw new InvalidOperationException($"Failed to parse expression: {expressionString}\nError: {result.Message}");
    }

    private ConditionalAction ReadConditionalAction(IParser parser, ObjectDeserializer rootDeserializer)
    {
        var conditionalAction = new ConditionalAction();

        parser.MoveNext();

        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is Scalar scalarKey)
            {
                var key = scalarKey.Value;
                parser.MoveNext();

                if (key == "if")
                {
                    if (parser.Current is Scalar conditionScalar)
                    {
                        var conditionString = conditionScalar.Value;
                        parser.MoveNext();

                        var condition = ParseExpressionString(conditionString);
                        conditionalAction.Condition = condition;
                    }
                    else
                    {
                        throw new InvalidOperationException("Expected scalar value for 'if' condition.");
                    }
                }
                else if (key == "then")
                {
                    var thenActions = ReadActionList(parser, rootDeserializer);
                    conditionalAction.ThenActions = thenActions;
                }
                else if (key == "else")
                {
                    var elseActions = ReadActionList(parser, rootDeserializer);
                    conditionalAction.ElseActions = elseActions;
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected key '{key}' in conditional action.");
                }
            }
            else
            {
                throw new InvalidOperationException("Expected scalar key in mapping.");
            }
        }

        parser.MoveNext();
        return conditionalAction;
    }

    private List<ActionBase> ReadActionList(IParser parser, ObjectDeserializer rootDeserializer)
    {
        var actions = new List<ActionBase>();

        if (parser.Current is SequenceStart)
        {
            parser.MoveNext();
            while (parser.Current is not SequenceEnd)
            {
                var action = (ActionBase)rootDeserializer(typeof(ActionBase));
                if (action != null)
                    actions.Add(action);
                else
                    throw new InvalidOperationException("Failed to parse action in action list.");
            }

            parser.MoveNext();
        }
        else
        {
            var action = (ActionBase)rootDeserializer(typeof(ActionBase));
            if (action != null)
                actions.Add(action);
            else
                throw new InvalidOperationException("Failed to parse action in action list.");
        }

        return actions;
    }
}
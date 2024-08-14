using System;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace Arcomage.Scripts;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageTemplateFormatMethodAttribute(string parameterName) : Attribute
{
    public string ParameterName { get; } = parameterName;
}

public partial class Logger : Node
{
    private static Logger _instance;

    public override void _EnterTree()
    {
        if (_instance != null) return;
        _instance = this;
        Debug("Logger initialized");
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }
    
    private static void Log(string message, LogLevel level, Exception ex = null)
    {
        var log = $"[{DateTime.Now:HH:mm:ss}]  {level}  {message}";

        if (ex != null)
        {
            log += $"\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";
            if (ex.InnerException != null) 
                log += $"\nInner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\nInner Stack Trace: {ex.InnerException.StackTrace}";
        }

        if (level == LogLevel.Error)
            GD.PrintErr(log);
        else
            GD.Print(log);
    }
    
    [MessageTemplateFormatMethod("message")]
    public static void Error(Exception ex, string message, params object[] args)
    {
        var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
        Log(formattedMessage, LogLevel.Error, ex);
    }

    private static string FormatMessageWithNamedPlaceholders(string message, params object[] args)
    {
        var placeholderPattern = PlaceholderPattern();
        
        var matchIndex = 0;
        message = placeholderPattern.Replace(message, match => matchIndex < args.Length ? ConvertToString(args[matchIndex++]) : match.Value);

        return message;
    }

    private static string ConvertToString(object arg)
    {
        switch (arg)
        {
            case null:
                return "null";
            case System.Collections.IEnumerable enumerable and not string:
                var count = enumerable.Cast<object>().Count();
                return count.ToString();
            default:
                return arg.ToString();
        }
    }


    public static void Error(string message) => Log(message, LogLevel.Error);

    public static void Error(Exception ex, string message) => Log(message, LogLevel.Error, ex);

    public static void Debug(string message) => Log(message, LogLevel.Debug);
    public static void Debug(Exception ex, string message) => Log(message, LogLevel.Debug, ex);
    
    [MessageTemplateFormatMethod("message")]
    public static void Debug(string message, params object[] args)
    {
        var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
        Log(formattedMessage, LogLevel.Debug);
    }

    public static void Info(string message) => Log(message, LogLevel.Info);
    public static void Info(Exception ex, string message) => Log(message, LogLevel.Info, ex);
    
    [MessageTemplateFormatMethod("message")]
    public static void Info(string message, params object[] args)
    {
        var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
        Log(formattedMessage, LogLevel.Info);
    }

    public static void Warn(string message) => Log(message, LogLevel.Warn);
    public static void Warn(Exception ex, string message) => Log(message, LogLevel.Warn, ex);
    
    [MessageTemplateFormatMethod("message")]
    public static void Warn(string message, params object[] args)
    {
        var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
        Log(formattedMessage, LogLevel.Warn);
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderPattern();
}
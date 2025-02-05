using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace Arcomage.Scripts.Logging;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageTemplateFormatMethodAttribute(string parameterName) : Attribute
{
   public string ParameterName { get; } = parameterName;
}

public class Logger
{
   private static readonly Dictionary<string, Logger> _Loggers = new();
   public static event Action<string> NewLogAdded;

   private readonly string _name;
   private Logger(string name) => _name = name;

   public static Logger GetOrCreateLogger(string name)
   {
      if (_Loggers.TryGetValue(name, out var logger))
         return logger;

      logger = new Logger(name);
      _Loggers[name] = logger;
      return logger;
   }

   public enum LogLevel
   {
      Debug,
      Info,
      Warn,
      Error
   }

   private void Log(string message, LogLevel level, Exception ex = null)
   {
      var now = DateTime.Now;
      var formattedMessage = $"[{now:HH:mm:ss}] {level} {_name} {message}";
      if (ex is not null)
      {
         formattedMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";
         if (ex.InnerException is not null)
            formattedMessage += $"\nInner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\nInner Stack Trace: {ex.InnerException.StackTrace}";
      }

      NewLogAdded?.Invoke(message);

      if (level == LogLevel.Error)
         GD.PrintErr(formattedMessage);
      else
         GD.Print(formattedMessage);
   }

   [MessageTemplateFormatMethod("message")]
   public void Error(Exception ex, string message, params object[] args)
   {
      var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
      Log(formattedMessage, LogLevel.Error, ex);
   }

   private static string FormatMessageWithNamedPlaceholders(string message, params object[] args)
   {
      var placeholderPattern = new Regex(@"\{(\w+)\}");
      var matchIndex = 0;
      message = placeholderPattern.Replace(message, match => matchIndex < args.Length ? ConvertToString(args[matchIndex++]) : match.Value);

      return message;
   }

   private static string ConvertToString(object arg)
   {
      return arg switch
      {
         null => "null",
         IEnumerable enumerable and not string => enumerable.Cast<object>().Count().ToString(),
         _ => arg.ToString()
      };
   }

   public void Error(string message) => Log(message, LogLevel.Error);

   public void Error(Exception ex, string message) => Log(message, LogLevel.Error, ex);

   [MessageTemplateFormatMethod("message")]
   public void Error(string message, params object[] args)
   {
      var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
      Log(formattedMessage, LogLevel.Error);
   }

   public void Debug(string message) => Log(message, LogLevel.Debug);

   public void Debug(Exception ex, string message) => Log(message, LogLevel.Debug, ex);

   [MessageTemplateFormatMethod("message")]
   public void Debug(string message, params object[] args)
   {
      var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
      Log(formattedMessage, LogLevel.Debug);
   }

   public void Info(string message) => Log(message, LogLevel.Info);

   public void Info(Exception ex, string message) => Log(message, LogLevel.Info, ex);

   [MessageTemplateFormatMethod("message")]
   public void Info(string message, params object[] args)
   {
      var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
      Log(formattedMessage, LogLevel.Info);
   }

   public void Warn(string message) => Log(message, LogLevel.Warn);

   public void Warn(Exception ex, string message) => Log(message, LogLevel.Warn, ex);

   [MessageTemplateFormatMethod("message")]
   public void Warn(string message, params object[] args)
   {
      var formattedMessage = FormatMessageWithNamedPlaceholders(message, args);
      Log(formattedMessage, LogLevel.Warn);
   }
}
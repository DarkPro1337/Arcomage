using System.Collections;
using System.Text.RegularExpressions;

namespace Arcomage.Logging;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageTemplateFormatMethodAttribute(string parameterName) : Attribute
{
   public string ParameterName { get; } = parameterName;
}

public partial class Logger
{
   private static readonly Dictionary<string, Logger> _loggers = new();
   private static readonly Regex _placeholderPattern = PlaceholderRegex();

   private static LogLevel? _minimumLevel;

   public static event Action<string> NewLogAdded;

   public static LogLevel MinimumLevel
   {
      get => _minimumLevel ??= OS.IsDebugBuild() ? LogLevel.Debug : LogLevel.Info;
      set => _minimumLevel = value;
   }

   private readonly string _name;
   private Logger(string name) => _name = name;

   public static Logger GetOrCreateLogger(string name)
   {
      if (_loggers.TryGetValue(name, out var logger))
         return logger;

      logger = new Logger(name);
      _loggers[name] = logger;
      return logger;
   }

   public enum LogLevel
   {
      Debug,
      Info,
      Warn,
      Error
   }

   private static bool ShouldLog(LogLevel level) => level >= MinimumLevel;

   private void Log(string message, LogLevel level, Exception ex = null)
   {
      if (!ShouldLog(level))
         return;

      var now = DateTime.Now;
      var formattedMessage = $"[{now:HH:mm:ss}] {level} {_name} {message}";
      if (ex is not null)
      {
         formattedMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";
         if (ex.InnerException is not null)
            formattedMessage += $"\nInner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\nInner Stack Trace: {ex.InnerException.StackTrace}";
      }

      NewLogAdded?.Invoke(formattedMessage);

      switch (level)
      {
         case LogLevel.Error:
            GD.PrintErr(formattedMessage);
            break;
         case LogLevel.Warn:
            GD.PushWarning(formattedMessage);
            break;
         default:
            GD.Print(formattedMessage);
            break;
      }
   }

   private void Write(LogLevel level, string message, object[] args, Exception ex = null)
   {
      if (!ShouldLog(level))
         return;

      Log(FormatMessageWithNamedPlaceholders(message, args), level, ex);
   }

   private static string FormatMessageWithNamedPlaceholders(string message, params object[] args)
   {
      var matchIndex = 0;
      return _placeholderPattern.Replace(message, match =>
         matchIndex < args.Length ? ConvertToString(args[matchIndex++]) : match.Value);
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
   public void Error(string message, params object[] args) => Write(LogLevel.Error, message, args);
   [MessageTemplateFormatMethod("message")]
   public void Error(Exception ex, string message, params object[] args) => Write(LogLevel.Error, message, args, ex);

   public void Debug(string message) => Log(message, LogLevel.Debug);
   public void Debug(Exception ex, string message) => Log(message, LogLevel.Debug, ex);
   [MessageTemplateFormatMethod("message")]
   public void Debug(string message, params object[] args) => Write(LogLevel.Debug, message, args);
   [MessageTemplateFormatMethod("message")]
   public void Debug(Exception ex, string message, params object[] args) => Write(LogLevel.Debug, message, args, ex);

   public void Info(string message) => Log(message, LogLevel.Info);
   public void Info(Exception ex, string message) => Log(message, LogLevel.Info, ex);
   [MessageTemplateFormatMethod("message")]
   public void Info(string message, params object[] args) => Write(LogLevel.Info, message, args);
   [MessageTemplateFormatMethod("message")]
   public void Info(Exception ex, string message, params object[] args) => Write(LogLevel.Info, message, args, ex);

   public void Warn(string message) => Log(message, LogLevel.Warn);
   public void Warn(Exception ex, string message) => Log(message, LogLevel.Warn, ex);
   [MessageTemplateFormatMethod("message")]
   public void Warn(string message, params object[] args) => Write(LogLevel.Warn, message, args);
   [MessageTemplateFormatMethod("message")]
   public void Warn(Exception ex, string message, params object[] args) => Write(LogLevel.Warn, message, args, ex);

   [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
   private static partial Regex PlaceholderRegex();
}

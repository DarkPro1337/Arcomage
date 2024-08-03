using System;
using Godot;

namespace Arcomage.Scripts;

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
            log += $"\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";

        GD.Print(log);
    }
    
    public static void Debug(string message) => Log(message, LogLevel.Debug);
    public static void Debug(Exception ex, string message) => Log(message, LogLevel.Debug, ex);
    
    public static void Info(string message) => Log(message, LogLevel.Info);
    public static void Info(Exception ex, string message) => Log(message, LogLevel.Info, ex);
    
    public static void Warn(string message) => Log(message, LogLevel.Warn);
    public static void Warn(Exception ex, string message) => Log(message, LogLevel.Warn, ex);
    
    public static void Error(string message) => Log(message, LogLevel.Error);
    public static void Error(Exception ex, string message) => Log(message, LogLevel.Error, ex);
}
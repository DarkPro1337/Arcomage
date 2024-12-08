using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Arcomage.Scripts.Gameplay;
using Arcomage.Scripts.Logging;
using Arcomage.Scripts.Managers;
using Arcomage.Scripts.UI;
using Godot;

namespace Arcomage.Scripts.Core;

public partial class Global : Node
{
   public static readonly Logger Logger = Logger.GetOrCreateLogger("Main");

   public static Table Table { get; set; }
   public static NetworkSetup NetworkSetup { get; set; }
   public static string BuildNumber { get; private set; } = GetBuildTimestamp();
   public static ModManager ModManager { get; } = new();
   public static DeckManager DeckManager { get; } = new();
   public static TavernManager TavernManager { get; } = new();

   public override void _Ready()
   {
      OS.LowProcessorUsageMode = true;
   }

   public static Dictionary<string, string> GetCommandLineArgs()
   {
      return new Dictionary<string, string>(OS.GetCmdlineArgs()
         .Where(arg => arg.StartsWith("--"))
         .Select(arg => arg[2..].Split("=", 2))
         .Where(parts => parts.Length == 2)
         .ToDictionary(parts => parts[0], parts => parts[1]));
   }

   private static string GetTime() => DateTime.Now.ToString("HH:mm:ss");

   private static string GetBuildTimestamp(string format = "ddMMyyyyHHmmss")
   {
      const string buildPrefix = "+build";
      var assembly = Assembly.GetExecutingAssembly();
      var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
      if (attribute?.InformationalVersion != null)
      {
         var value = attribute.InformationalVersion;
         var index = value.IndexOf(buildPrefix, StringComparison.Ordinal);
         if (index > 0)
         {
            value = value[(index + buildPrefix.Length)..];
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
               return result.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);
         }
      }

      Logger.Error("Build timestamp missing");
      return "UNKNOWN";
   }
}
using System.Reflection;
using Arcomage.Managers;
using Arcomage.Networking;
using Arcomage.UI;

namespace Arcomage.Core;

public partial class Global : Node
{
   public static readonly Logger Logger = Logger.GetOrCreateLogger("Main");

   public static Table Table { get; set; }
   public static NetworkSetup NetworkSetup { get; set; }
   public static OnlineService Online { get; set; }
   public static MatchMode PendingMatchMode { get; set; } = MatchMode.OneVsOne;
   public static bool PendingRanked { get; set; }
   public static string BuildNumber { get; private set; } = GetBuildTimestamp();
   public static ModManager ModManager { get; set; }
   public static DeckManager DeckManager { get; } = new();
   public static TavernManager TavernManager { get; } = new();
   public static TranslationManager TranslationManager { get; } = new();

   public static Dictionary<string, string> GetCommandLineArgs()
   {
      return new Dictionary<string, string>(OS.GetCmdlineArgs()
         .Where(arg => arg.StartsWith("--"))
         .Select(arg => arg[2..].Split("=", 2))
         .Where(parts => parts.Length == 2)
         .ToDictionary(parts => parts[0], parts => parts[1]));
   }

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
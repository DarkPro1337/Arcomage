using Arcomage.Networking;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Core;

public partial class Boot : Node
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Bootstrap");

   public override void _EnterTree()
   {
      var version = ProjectSettings.GetSetting("application/config/version").ToString();
      _logger.Debug("Arcomage {Version} loaded", version);
      _logger.Debug("Build number: {BuildNumber}", Global.BuildNumber);

      ApplyNakamaCli();

      if (DedicatedServer.ShouldRun())
      {
         _logger.Debug("Loading dedicated ranked host...");
         var host = new DedicatedServer { Name = "DedicatedServer" };
         GetTree().Root.CallDeferred(Node.MethodName.AddChild, host);
         return;
      }

      if (!Config.Settings.IntroSkip)
      {
         _logger.Debug("Loading from Boot to Intro...");
         GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Main/Intro.tscn");
      }
      else
      {
         _logger.Debug("Loading from Boot to Main menu...");
         GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Main/MainMenu.tscn");
      }
   }

   private static void ApplyNakamaCli()
   {
      var args = Global.GetCommandLineArgs();
      if (args.TryGetValue("nakamaHost", out var host))
         Config.Settings.NakamaHost = host;
      if (args.TryGetValue("nakamaPort", out var port) && int.TryParse(port, out var parsed))
         Config.Settings.NakamaPort = parsed;
      if (args.ContainsKey("nakamaSsl"))
         Config.Settings.NakamaUseSsl = true;
   }
}
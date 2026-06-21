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
}
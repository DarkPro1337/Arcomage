using Godot;

namespace Arcomage.Scripts;

public partial class Boot : Node
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("Bootstrap");

   public override void _EnterTree()
   {
      var version = ProjectSettings.GetSetting("application/config/version").ToString();
      _Logger.Debug("Arcomage {Version} loaded", version);
      _Logger.Debug("Build number: {BuildNumber}", Global.BuildNumber);

      if (!Config.Settings.IntroSkip)
      {
         _Logger.Debug("Loading from Boot to Intro...");
         GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Intro.tscn");
      }
      else
      {
         _Logger.Debug("Loading from Boot to Main menu...");
         GetTree().CallDeferred("change_scene_to_file", "res://Scenes/MainMenu.tscn");
      }
   }
}
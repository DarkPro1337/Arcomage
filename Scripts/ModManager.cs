using Godot;

namespace Arcomage.Scripts;

public class ModManager
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("ModManager");

   public ModManager()
   {
      if (OS.HasFeature("editor"))
         _Logger.Warn("Mods in editor override 'res://' filesystem completely because of the Godot bug. Please test mods in game builds.");

      var modsDir = DirAccess.Open("user://mods");
      foreach (var mod in modsDir.GetFiles())
      {
         var success = ProjectSettings.LoadResourcePack("user://mods/" + mod);
         if (success)
         {
            _Logger.Debug("Mod loaded: {Mod}", "user://mods/" + mod);
         }
         else
         {
            _Logger.Warn("Failed to load mod: {Mod}", "user://mods/" + mod);
         }
      }
   }
}
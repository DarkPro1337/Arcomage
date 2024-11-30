using System.Linq;
using Godot;

namespace Arcomage.Scripts;

public class ModManager
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("ModManager");

   private const string ModsDir = "user://mods/";

   public ModManager()
   {
      var modsDirExist = DirAccess.DirExistsAbsolute(ModsDir);
      if (!modsDirExist)
      {
         _Logger.Debug("Mods directory not found: {ModsDir}", ModsDir);
         return;
      }

      if (OS.HasFeature("editor"))
         _Logger.Warn("Mods in editor break 'res://' filesystem completely because of the Godot bug. Please test mods in game builds.");

      var modsDir = DirAccess.Open(ModsDir);
      var modFiles = modsDir.GetFiles().Where(x => x.GetExtension() == "pck").ToArray();
      if (modFiles.Length == 0)
      {
         _Logger.Debug("No mods found in {ModsDir}", ModsDir);
         return;
      }

      _Logger.Debug("Found {ModsCount} mods in {ModsDir}", modFiles.Length, ModsDir);
      foreach (var mod in modFiles)
      {
         var success = ProjectSettings.LoadResourcePack(ModsDir + mod);
         if (success)
         {
            _Logger.Debug("Mod loaded: {Mod}", ModsDir + mod);
         }
         else
         {
            _Logger.Warn("Failed to load mod: {Mod}", ModsDir + mod);
         }
      }
   }
}
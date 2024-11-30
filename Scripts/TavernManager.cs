using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Arcomage.Scripts;

public class TavernPack
{
   public string Name { get; set; }
   public string Description { get; set; }
   public List<Tavern> Taverns { get; set; }
}

public class Tavern
{
   [YamlIgnore] public int Index { get; set; }
   public string Id { get; set; }
   public string Name { get; set; }
   public int StartingTower { get; set; }
   public int StartingWall { get; set; }
   public int StartingQuarry { get; set; }
   public int StartingMagic { get; set; }
   public int StartingDungeon { get; set; }
   public int StartingBricks { get; set; }
   public int StartingGems { get; set; }
   public int StartingBeasts { get; set; }
   public int WinningTower { get; set; }
   public int WinningResources { get; set; }
}

public class TavernManager
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("TavernManager");

   private const string TavernsDir = "res://Taverns/";

   public List<TavernPack> TavernPacks { get; } = [];

   public TavernManager() => InitializeTaverns();

   private void InitializeTaverns()
   {
      var tavernsDir = DirAccess.Open(TavernsDir);
      if (tavernsDir == null)
      {
         _Logger.Warn("Taverns directory failed to open: {TavernsDir}", TavernsDir);
         return;
      }
      
      var files = tavernsDir.GetFiles();
      foreach (var tavernFile in files)
      {
         var pack = LoadTavernPackFromFile(TavernsDir + tavernFile);
         if (pack != null)
            TavernPacks.Add(pack);
      }
   }

   public Tavern GetTavernByIndex(int idx)
   {
      if (idx <= 0)
         return null;

      foreach (var pack in TavernPacks)
      {
         foreach (var tavern in pack.Taverns)
         {
            if (tavern.Index == idx)
               return tavern;
         }
      }

      return null;
   }

   private TavernPack LoadTavernPackFromFile(string filePath)
   {
      if (filePath.GetExtension() != "yaml" && filePath.GetExtension() != "yml")
      {
         _Logger.Warn("Only YAML file formats (.yaml or .yml) are supported for taverns.");
         return null;
      }

      if (!FileAccess.FileExists(filePath))
      {
         _Logger.Warn("Tavern file not found: {FilePath}", filePath);
         return null;
      }

      try
      {
         var yamlFile = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
         var yaml = yamlFile.GetAsText();
         yamlFile.Close();

         var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new ActionTypeConverter())
            .Build();

         var pack = deserializer.Deserialize<TavernPack>(yaml);

         if (pack.Taverns == null)
         {
            _Logger.Warn($"Root element in {filePath} is not a valid tavern pack.");
            return null;
         }

         _Logger.Debug("Loaded {Count} taverns from {Name} pack ({Path})", pack.Taverns.Count, pack.Name, filePath);
         return pack;
      }
      catch (Exception ex)
      {
         _Logger.Error(ex, "Unexpected error occurred while loading tavern pack from file {Path}", filePath);
         return null;
      }
   }
}
namespace Arcomage.Managers;

public class TavernManager
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("TavernManager");

   private const string TavernsDir = "res://Taverns/";

   public List<TavernPack> TavernPacks { get; } = [];

   public TavernManager() => InitializeTaverns();

   private void InitializeTaverns()
   {
      var tavernsDir = DirAccess.Open(TavernsDir);
      if (tavernsDir == null)
      {
         _logger.Warn("Taverns directory failed to open: {TavernsDir}", TavernsDir);
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
      return idx <= 0 ? null : TavernPacks.SelectMany(pack => pack.Taverns).FirstOrDefault(tavern => tavern.Index == idx);
   }

   public TavernPack LoadTavernPackFromFile(string filePath)
   {
      if (filePath.GetExtension() != "yaml" && filePath.GetExtension() != "yml")
      {
         _logger.Warn("Only YAML file formats (.yaml or .yml) are supported for taverns.");
         return null;
      }

      if (!FileAccess.FileExists(filePath))
      {
         _logger.Warn("Tavern file not found: {FilePath}", filePath);
         return null;
      }

      try
      {
         var yaml = YamlLoader.ReadFile(filePath);
         var pack = YamlLoader.Create().Deserialize<TavernPack>(yaml);

         if (pack.Taverns == null)
         {
            _logger.Warn($"Root element in {filePath} is not a valid tavern pack.");
            return null;
         }

         _logger.Debug("Loaded {Count} taverns from {Name} pack ({Path})", pack.Taverns.Count, pack.Name, filePath);
         return pack;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Unexpected error occurred while loading tavern pack from file {Path}", filePath);
         return null;
      }
   }

   public TavernPack LoadTavernPackFromYamlText(string yaml)
   {
      try
      {
         var pack = YamlLoader.Create().Deserialize<TavernPack>(yaml);

         if (pack?.Taverns is null)
         {
            _logger.Warn("YAML text did not contain a valid tavern pack.");
            return null;
         }

         _logger.Debug("Loaded {Count} taverns from {Name} pack", pack.Taverns.Count, pack.Name);
         return pack;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Unexpected error occurred while loading tavern pack from YAML text");
         return null;
      }
   }
}
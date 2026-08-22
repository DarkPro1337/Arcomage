using System.IO;
using System.IO.Compression;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Arcomage.Managers;

public record Mod(ModMetadata Metadata, Store Store, Instance Instance)
{
   public bool IsEnabled { get; set; } = true;
   public string AbsolutePath { get; set; }
}

public partial class ModManager : Node
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("ModManager");

   private readonly Dictionary<string, Mod> _mods = new();
   private readonly Wasmtime.Engine _engine = new();

   private const string ModsDir = "user://mods/";

   public override void _Ready()
   {
      Global.ModManager = this;
      LoadMods();
   }

   private IEnumerable<string> GetArcpakFilePaths(string path)
   {
      var results = new List<string>();
      var dir = DirAccess.Open(path);
      if (dir is null)
         return results;

      foreach (var file in dir.GetFiles())
      {
         if (Path.GetExtension(file).Equals(".arcpak", StringComparison.OrdinalIgnoreCase))
            results.Add(path.TrimEnd('/') + "/" + file);
      }

      foreach (var sub in dir.GetDirectories())
      {
         if (sub is "." or "..")
            continue;

         var subDir = path.TrimEnd('/') + "/" + sub;
         results.AddRange(GetArcpakFilePaths(subDir));
      }

      return results;
   }

   private void LoadMods()
   {
      if (!DirAccess.DirExistsAbsolute(ModsDir))
         DirAccess.MakeDirAbsolute(ModsDir);

      var modFilePaths = GetArcpakFilePaths(ModsDir).ToArray();
      if (modFilePaths.Length == 0)
      {
         _logger.Debug("No mods found in {ModsDir}", ModsDir);
         return;
      }

      _logger.Debug("Found {ModsCount} mods in {ModsDir}", modFilePaths.Length, ModsDir);
      foreach (var modPath in modFilePaths)
      {
         try
         {
            var arcpakPath = ProjectSettings.GlobalizePath(modPath);
            using var archive = ZipFile.OpenRead(arcpakPath);
            var metadataEntry = archive.GetEntry("metadata.yaml");
            if (metadataEntry is null)
            {
               _logger.Warn("Metadata file missing in {Mod}, mod loading skipped", modPath);
               continue;
            }

            using var reader = new StreamReader(metadataEntry.Open());
            var metadata = YamlLoader.Create(withActionConverter: false)
               .Deserialize<ModMetadata>(reader.ReadToEnd());

            _logger.Debug("Loading mod {ModName} v{ModVersion} by {ModAuthor} ({Path})", metadata.Name, metadata.Version, metadata.Author, modPath);

            if (metadata.Resources is not null)
            {
               foreach (var resource in metadata.Resources)
               {
                  var resourceEntry = archive.GetEntry(resource);
                  if (resourceEntry is null)
                  {
                     _logger.Warn("Resource file {Resource} missing in {Mod}", resource, modPath);
                     continue;
                  }

                  if (Path.GetExtension(resourceEntry.Name) == ".pck")
                  {
                     var localPath = $"user://mods/{metadata.Name}/{resource}";
                     var globalPath = ProjectSettings.GlobalizePath($"user://mods/{metadata.Name}/{resource}");
                     var resourceDir = Path.GetDirectoryName(globalPath);
                     if (!Directory.Exists(resourceDir))
                        Directory.CreateDirectory(resourceDir);

                     var resourceStream = resourceEntry.Open();
                     var fileStream = new FileStream(globalPath, FileMode.Create);
                     resourceStream.CopyTo(fileStream);
                     resourceStream.Close();
                     fileStream.Close();

                     if (ProjectSettings.LoadResourcePack(localPath))
                        _logger.Debug("Loaded resource pack {Resource} in {Mod}", resource, modPath);
                     else
                        _logger.Warn("Failed to load resource pack {Resource} in {Mod}", resource, modPath);
                  }

                  if (Path.GetExtension(resourceEntry.Name) == ".yaml")
                  {
                     using var stream = resourceEntry.Open();
                     using var yamlReader = new StreamReader(stream);
                     var yamlText = yamlReader.ReadToEnd();
                     var deserializer = YamlLoader.Create(withActionConverter: false);

                     var doc = deserializer.Deserialize<Dictionary<string, object>>(yamlText);
                     if (doc is null)
                        continue;

                     if (doc.TryGetValue("cards", out _))
                     {
                        _logger.Debug("Detected deck pack YAML file: {YamlFile}", resource);
                        var deck = Global.DeckManager.LoadDeckFromYamlText(yamlText);
                        if (deck is not null)
                           Global.DeckManager.Decks.Add(deck);
                     }
                     else if (doc.TryGetValue("taverns", out _))
                     {
                        _logger.Debug("Detected tavern pack YAML file: {YamlFile}", resource);
                        var tavernPack = Global.TavernManager.LoadTavernPackFromYamlText(yamlText);
                        if (tavernPack is not null)
                           Global.TavernManager.TavernPacks.Add(tavernPack);
                     }
                     else
                     {
                        _logger.Warn("YAML file {YamlFile} did not contain a recognized pack type", resource);
                     }
                  }

                  if (Path.GetExtension(resourceEntry.Name) == ".csv")
                  {
                     using var stream = resourceEntry.Open();
                     using var csvReader = new StreamReader(stream);
                     var csvText = csvReader.ReadToEnd();
                     var translations = TranslationManager.LoadTranslationsFromCsv(csvText);
                     foreach (var translation in translations)
                        TranslationServer.AddTranslation(translation);

                     var localesString = string.Join(",", translations.Select(x => x.Locale));
                     var stringsCount = translations.Select(x => x.Messages).Count();
                     Global.TranslationManager.UpdateLoadedLocales();
                     _logger.Debug("Loaded {Count} translations for {Locales} from {CsvFile}", stringsCount, localesString, resource);
                  }
               }
            }

            if (metadata.EntryPoint is not null)
            {
               var wasmEntry = archive.GetEntry(metadata.EntryPoint);
               if (wasmEntry is null)
               {
                  _logger.Warn("WASM file missing but entrypoint is defined in metadata in {Mod}", modPath);
                  continue;
               }

               byte[] wasmBytes;
               using (var ms = new MemoryStream())
               {
                  wasmEntry.Open().CopyTo(ms);
                  wasmBytes = ms.ToArray();
               }

               var module = Module.FromBytes(_engine, metadata.Name, wasmBytes);
               var store = new Store(_engine);
               var linker = new Linker(_engine);

               linker.Define("env", "host_log", Function.FromCallback(store, (Caller caller, int ptr, int len) =>
               {
                  var memory = caller.GetMemory("memory");
                  if (memory != null)
                     _logger.Info("{ModName} :: {Message}", metadata.Name, memory.ReadString(ptr, len));
               }));

               linker.Define("env", "abort", Function.FromCallback(store, (int msg, int file, int line, int column) =>
               {
                  _logger.Error("Abort called in {ModName} at {File}:{Line}:{Column}",
                     metadata.Name, file, line, column);
               }));

               var instance = linker.Instantiate(store, module);
               instance.GetFunction("init")?.Invoke();
               var loadedMod = new Mod(metadata, store, instance) { AbsolutePath = arcpakPath };
               _mods[metadata.Name] = loadedMod;
            }
         }
         catch (Exception ex)
         {
            _logger.Error(ex, "Error loading mod {ModName}: {Error}", modPath, ex.Message);
         }
      }
   }

   public override void _Process(double delta)
   {
      foreach (var mod in _mods.Values)
         mod.Instance.GetFunction("process")?.Invoke(delta);
   }

   public void DisableMod(string modName)
   {
      if (_mods.TryGetValue(modName, out var mod))
         mod.Instance.GetFunction("exit")?.Invoke();
   }
}
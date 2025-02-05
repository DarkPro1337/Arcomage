using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Arcomage.Scripts.Data;
using Arcomage.Scripts.Logging;
using Godot;
using Wasmtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Global = Arcomage.Scripts.Core.Global;
using Module = Wasmtime.Module;

namespace Arcomage.Scripts.Managers;

public record Mod(ModMetadata Metadata, Store Store, Instance Instance)
{
   public bool IsEnabled { get; set; } = true;
   public string AbsolutePath { get; set; }
}

public partial class ModManager : Node
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("ModManager");

   private readonly Dictionary<string, Mod> _mods = new();
   private readonly Wasmtime.Engine _engine = new();

   private const string ModsDir = "user://mods/";

   public override void _Ready()
   {
      Global.ModManager = this;
      LoadMods();
   }

   private void LoadMods()
   {
      if (!DirAccess.DirExistsAbsolute(ModsDir))
         DirAccess.MakeDirAbsolute(ModsDir);

      var modsDir = DirAccess.Open(ModsDir);
      var modFiles = modsDir.GetFiles().Where(x => x.GetExtension() == "arcpak").ToArray();
      if (modFiles.Length == 0)
      {
         _Logger.Debug("No mods found in {ModsDir}", ModsDir);
         return;
      }

      _Logger.Debug("Found {ModsCount} mods in {ModsDir}", modFiles.Length, ModsDir);
      foreach (var mod in modFiles)
      {
         try
         {
            var arcpakPath = ProjectSettings.GlobalizePath(ModsDir + mod);
            using var archive = ZipFile.OpenRead(arcpakPath);
            var metadataEntry = archive.GetEntry("metadata.yaml");
            if (metadataEntry is null)
            {
               _Logger.Warn("Metadata file missing in {Mod}, mod loading skipped", mod);
               continue;
            }

            ModMetadata metadata;
            using (var reader = new StreamReader(metadataEntry.Open()))
               metadata = new DeserializerBuilder()
                  .WithNamingConvention(CamelCaseNamingConvention.Instance)
                  .Build()
                  .Deserialize<ModMetadata>(reader.ReadToEnd());

            _Logger.Debug("Loading mod {ModName} v{ModVersion} by {ModAuthor}",
               metadata.Name, metadata.Version, metadata.Author);

            if (metadata.EntryPoint is not null)
            {
               var wasmEntry = archive.GetEntry(metadata.EntryPoint);
               if (wasmEntry is null)
               {
                  _Logger.Warn("WASM file missing but entrypoint is defined in metadata in {Mod}", mod);
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
                  {
                     var span = memory.GetSpan<byte>(0);
                     var bytes = span.Slice(ptr, len).ToArray();
                     _Logger.Info("{ModName} :: {Message}", metadata.Name, Encoding.UTF8.GetString(bytes));
                  }
               }));

               linker.Define("env", "abort", Function.FromCallback(store, (int msg, int file, int line, int column) =>
               {
                  _Logger.Error("Abort called in {ModName} at {File}:{Line}:{Column}",
                     metadata.Name, file, line, column);
               }));

               var instance = linker.Instantiate(store, module);
               instance.GetFunction("init")?.Invoke();
               var loadedMod = new Mod(metadata, store, instance);
               loadedMod.AbsolutePath = arcpakPath;
               _mods[metadata.Name] = loadedMod;
            }
         }
         catch (Exception ex)
         {
            _Logger.Error("Error loading mod {ModName}: {Error}", mod, ex);
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
using System.Collections.Generic;
using YamlDotNet.Serialization;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Arcomage.Scripts.Data;

public record ModMetadata
{
   [YamlMember(Alias = "name")] public string Name { get; set; }
   [YamlMember(Alias = "version")] public string Version { get; set; }
   [YamlMember(Alias = "author")] public string Author { get; set; }
   [YamlMember(Alias = "description")] public string Description { get; set; }
   [YamlMember(Alias = "pic")] public string Pic { get; set; }
   [YamlMember(Alias = "entrypoint")] public string EntryPoint { get; set; }
   [YamlMember(Alias = "dependencies")] public List<string> Dependencies { get; set; } = [];
   [YamlMember(Alias = "resources")] public List<string> Resources { get; set; } = [];
}
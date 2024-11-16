using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FileAccess = Godot.FileAccess;

namespace Arcomage.Scripts;

public partial class Global : Node
{
    private const string BaseCardsFilePath = "res://Db/MM8.yaml"; // TODO: Implement dynamic card loading from directory

    public static Table Table { get; set; }
    public static Settings Settings { get; set; }
    public static NetworkSetup NetworkSetup { get; set; }
    public static string BuildNumber { get; private set; }
    public static List<Card> CardsList { get; } = []; // TODO: Rework into DeckManager and support mods later

    public override void _Ready()
    {
        OS.LowProcessorUsageMode = true;
        BuildNumber = LoadBuildNumber();
        LoadCardsFromFile(BaseCardsFilePath);
    }

    public static Dictionary<string, string> GetCommandLineArgs()
    {
        return new Dictionary<string, string>(OS.GetCmdlineArgs()
            .Where(arg => arg.StartsWith("--"))
            .Select(arg => arg[2..].Split("=", 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]));
    }

    private static string GetTime() => DateTime.Now.ToString("HH:mm:ss");

    private static string LoadBuildNumber()
    {
        const string fileName = "res://Build.txt";
        if (FileAccess.FileExists(fileName))
        {
            using var file = FileAccess.Open(fileName, FileAccess.ModeFlags.Read);
            var content = file.GetAsText();
            file.Close();
            return content.Trim();
        }

        Logger.Error("Build number file missing");
        return "UNKNOWN";
    }

    /// <summary>
    /// Loads cards from a specified YAML file.
    /// </summary>
    /// <param name="filePath">The path to the YAML file.</param>
    public static void LoadCardsFromFile(string filePath)
    {
        if (filePath.GetExtension() != "yaml" && filePath.GetExtension() != "yml")
        {
            Logger.Warn("Only YAML file formats (.yaml or .yml) are supported for cards.");
            return;
        }
    
        if (!FileAccess.FileExists(filePath))
        {
            Logger.Warn("File {Path} does not exist.", filePath);
            return;
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

            var deck = deserializer.Deserialize<Deck>(yaml);

            if (deck?.Cards == null)
            {
                Logger.Warn($"Root element in {filePath} is not a valid deck.");
                return;
            }

            CardsList.AddRange(deck.Cards);
            Logger.Debug("Loaded {Count} cards from {Path} ({Path})", deck.Cards.Count, deck.Name, filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error occurred while loading cards from file {Path}", filePath);
        }
    }
}
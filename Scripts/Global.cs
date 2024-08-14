using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Arcomage.Scripts;

public partial class Global : Node
{
    public static Table Table { get; set; }
    public static Settings Settings { get; set; }
    public static NetworkSetup NetworkSetup { get; set; }
    public static string BuildNumber { get; private set; }
    public static List<Card> CardsList { get; } = [];

    public override void _Ready()
    {
        OS.LowProcessorUsageMode = true;
        BuildNumber = LoadBuildNumber();
        LoadCardsFromFile("res://Db/base.json");
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
        const string fileName = "res://build.tres";
        if (FileAccess.FileExists(fileName))
        {
            using var file = FileAccess.Open(fileName, FileAccess.ModeFlags.Read);
            var content = file.GetAsText();
            file.Close();
            return content.Trim();
        }

        Logger.Error("Build.tres file missing");
        return "UNKNOWN";
    }
    
    public static void LoadCardsFromFile(string filePath)
    {
        try
        {
            var jsonFile = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            var json = jsonFile.GetAsText();
            jsonFile.Close();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
        
            if (root.ValueKind != JsonValueKind.Array)
            {
                Logger.Warn($"Root element in {filePath} is not an array.");
                return;
            }

            foreach (var cardElement in root.EnumerateArray())
            {
                try
                {
                    var newCard = new Card();
                    newCard.Load(cardElement);
                    CardsList.Add(newCard);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to load a card from JSON. Skipping to the next card.");
                }
            }

            Logger.Debug("Loaded {Count} cards from {Path}", CardsList.Count, filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error occurred while loading cards from file {Path}", filePath);
        }
    }
}
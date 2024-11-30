using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Arcomage.Scripts;

public class DeckManager
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("DeckManager");

   private const string DecksDir = "res://Decks/";

   public List<Deck> Decks { get; } = [];

   public DeckManager() => InitializeDecks();

   private void InitializeDecks()
   {
      var decksDir = DirAccess.Open(DecksDir);
      if (decksDir == null)
      {
         _Logger.Warn("Decks directory failed to open: {DecksDir}", DecksDir);
         return;
      }
      
      var files = decksDir.GetFiles();
      foreach (var deckFile in files)
      {
         var deck = LoadDeckFromFile(DecksDir + deckFile);
         if (deck != null)
            Decks.Add(deck);
      }
   }

   public int GetAllCardsCount() => Decks.Sum(deck => deck.Cards.Count);

   /// <summary>
   /// Retrieves all cards from enabled decks.
   /// </summary>
   /// <returns>A read-only list of cards from enabled decks.</returns>
   public IReadOnlyList<Card> GetAllCards()
   {
      return Decks
         .Where(deck => deck.IsEnabled)
         .SelectMany(deck => deck.Cards)
         .ToList();
   }

   /// <summary>
   /// Loads a deck from a specified YAML file.
   /// </summary>
   /// <param name="filePath">The path to the YAML file.</param>
   /// <returns>The loaded deck, or null if the file is not valid or an error occurs.</returns>
   private Deck LoadDeckFromFile(string filePath)
   {
      if (filePath.GetExtension() != "yaml" && filePath.GetExtension() != "yml")
      {
         _Logger.Warn("Only YAML file formats (.yaml or .yml) are supported for cards.");
         return null;
      }
    
      if (!FileAccess.FileExists(filePath))
      {
         _Logger.Warn("File {Path} does not exist.", filePath);
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

         var deck = deserializer.Deserialize<Deck>(yaml);

         if (deck.Cards == null)
         {
            _Logger.Warn($"Root element in {filePath} is not a valid deck.");
            return null;
         }

         _Logger.Debug("Loaded {Count} cards from {Path} ({Path})", deck.Cards.Count, deck.Name, filePath);
         deck.IsEnabled = true; // TODO: implement deck enabling/disabling in the UI
         return deck;
      }
      catch (Exception ex)
      {
         _Logger.Error(ex, "Unexpected error occurred while loading cards from file {Path}", filePath);
         return null;
      }
   }
}
using FileAccess = Godot.FileAccess;

namespace Arcomage.Managers;

public class DeckManager
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("DeckManager");

   private const string DecksDir = "res://Decks/";

   public List<Deck> Decks { get; } = [];

   public DeckManager() => InitializeDecks();

   private void InitializeDecks()
   {
      var decksDir = DirAccess.Open(DecksDir);
      if (decksDir == null)
      {
         _logger.Warn("Decks directory failed to open: {DecksDir}", DecksDir);
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
   public IReadOnlyList<Card> GetAllCards() =>
   [
      .. Decks
         .Where(deck => deck.IsEnabled)
         .SelectMany(deck => deck.Cards)
   ];

   /// <summary>
   /// Loads a deck from a specified YAML file.
   /// </summary>
   /// <param name="filePath">The path to the YAML file.</param>
   /// <returns>The loaded deck, or null if the file is not valid or an error occurs.</returns>
   public Deck LoadDeckFromFile(string filePath)
   {
      if (filePath.GetExtension() != "yaml" && filePath.GetExtension() != "yml")
      {
         _logger.Warn("Only YAML file formats (.yaml or .yml) are supported for cards.");
         return null;
      }
    
      if (!FileAccess.FileExists(filePath))
      {
         _logger.Warn("File {Path} does not exist", filePath);
         return null;
      }
    
      try
      {
         var yaml = YamlLoader.ReadFile(filePath);
         var deck = YamlLoader.Create().Deserialize<Deck>(yaml);

         if (deck.Cards == null)
         {
            _logger.Warn($"Root element in {filePath} is not a valid deck.");
            return null;
         }

         _logger.Debug("Loaded {Count} cards from {Name} ({Path})", deck.Cards.Count, deck.Name, filePath);
         deck.IsEnabled = true; // TODO: implement deck enabling/disabling in the UI
         return deck;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Unexpected error occurred while loading cards from file {Path}", filePath);
         return null;
      }
   }

   /// <summary>
   /// Loads a deck from a specified YAML text.
   /// </summary>
   /// <param name="yaml">The YAML text to load the deck from.</param>
   /// <returns>The loaded deck, or null if the text is not valid or an error occurs.</returns>
   public Deck LoadDeckFromYamlText(string yaml)
   {
      try
      {
         var deck = YamlLoader.Create().Deserialize<Deck>(yaml);

         if (deck?.Cards is null)
         {
            _logger.Warn("Provided YAML is not a valid deck.");
            return null;
         }

         _logger.Debug("Loaded {Count} cards from deck {Name} from YAML", deck.Cards.Count, deck.Name);
         deck.IsEnabled = true;
         return deck;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Unexpected error occurred while loading deck from YAML text");
         return null;
      }
   }
}
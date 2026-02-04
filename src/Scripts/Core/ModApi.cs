using Arcomage.Data;
using Arcomage.Logging;

namespace Arcomage.Core;

public class ModApi
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("ModAPI");

   public void Log(string message) => Logger.GetOrCreateLogger("Main").Info(message);

   public void LogError(string message) => Logger.GetOrCreateLogger("Main").Error(message);

   public void RegisterDeck(Deck deck)
   {
      Global.DeckManager.Decks.Add(deck);
      _Logger.Debug("Deck {DeckName} registered", deck.Name);
   }

   public void RegisterTavernPack(TavernPack tavernPack)
   {
      Global.TavernManager.TavernPacks.Add(tavernPack);
      _Logger.Debug("Tavern pack {TavernPackName} registered", tavernPack.Name);
   }
}
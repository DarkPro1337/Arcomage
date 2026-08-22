namespace Arcomage.Gameplay;

/// <summary>
/// Hand draw/replace helpers for <see cref="Table"/>.
/// </summary>
public partial class Table
{
   /// <summary>
   /// Removes a spent card and draws a replacement into the same hand slot.
   /// </summary>
   /// <param name="deck">Hand that owns <paramref name="card"/>.</param>
   /// <param name="card">Played or discarded card to replace.</param>
   private void ReplaceCard(HBoxContainer deck, CardControl card)
   {
      var index = card.GetIndex();
      RemoveCard(deck, card);
      DrawRandomCard(deck, index);
   }

   /// <summary>
   /// Removes a card from the hand without drawing a replacement.
   /// </summary>
   private static void RemoveCard(HBoxContainer deck, CardControl card)
   {
      deck.RemoveChild(card);
      card.QueueFree();
   }

   /// <summary>
   /// Adds a random card to <paramref name="deck"/>, optionally inserting it at <paramref name="index"/>.
   /// </summary>
   /// <param name="deck">Hand to draw into.</param>
   /// <param name="index">Slot to fill, or <c>-1</c> to append.</param>
   private void DrawRandomCard(HBoxContainer deck, int index = -1)
   {
      var replacement = CreateCard(PickRandomCardId());
      deck.AddChild(replacement);
      if (index >= 0 && index < deck.GetChildCount() - 1)
         deck.MoveChild(replacement, index);
   }

   /// <summary>
   /// Picks a random card id from the enabled decks, or an empty string if none are loaded.
   /// </summary>
   private string PickRandomCardId()
   {
      var cards = Global.DeckManager.GetAllCards();
      if (cards.Count == 0)
         return string.Empty;

      return cards[_rng.RandiRange(0, cards.Count - 1)].Id;
   }

   /// <summary>
   /// Returns the card ids currently sitting in <paramref name="deck"/>, in hand order.
   /// </summary>
   private string[] GetHandIds(HBoxContainer deck)
   {
      if (deck == null)
         return [];

      return [.. deck.GetChildren().OfType<CardControl>().Select(card => card.CardId ?? string.Empty)];
   }
}

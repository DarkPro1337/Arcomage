namespace Arcomage.Gameplay;

/// <summary>
/// Simple first-affordable-card AI for <see cref="Table"/>.
/// </summary>
public partial class Table
{
   /// <summary>
   /// Queues a short delay, then lets the AI play if it currently owns the turn.
   /// </summary>
   private void TryStartAiTurn()
   {
      if (_gameOver || !IsAuthority() || _aiPlayQueued || _animating)
         return;

      if (!Players.TryGetValue(_turnPlayerId, out var player) || !player.Ai)
         return;

      _aiPlayQueued = true;
      DeckLocker.Show();

      GetTree().CreateTimer(0.75).Timeout += OnAiThinkTimeout;
   }

   /// <summary>
   /// Called after the AI think delay; plays one AI card if the turn is still theirs.
   /// </summary>
   private void OnAiThinkTimeout()
   {
      _aiPlayQueued = false;
      if (!IsInsideTree() || _gameOver || !IsAuthority())
         return;

      if (!Players.TryGetValue(_turnPlayerId, out var player) || !player.Ai)
         return;

      PlayAiCard(player);
   }

   /// <summary>
   /// Picks the first affordable card for the AI, or discards if nothing can be played.
   /// </summary>
   /// <param name="player">The AI player whose hand should be used.</param>
   private void PlayAiCard(Player player)
   {
      var deck = GetDeckForPlayer(player.Id);
      if (deck == null)
         return;

      var cards = deck.GetChildren().OfType<CardControl>().ToList();
      if (cards.Count == 0)
         return;

      if (player.Discarding)
      {
         var discardIndex = FindDiscardIndex(cards);
         ResolveCardPlay(player.Id, discardIndex, cards[discardIndex].CardId, discarded: true, GetDefaultEnemyId(player));
         return;
      }

      player.SelectedTargetId = GetDefaultEnemyId(player);
      for (var i = 0; i < cards.Count; i++)
      {
         if (!CanAfford(player, cards[i]))
            continue;

         ResolveCardPlay(player.Id, i, cards[i].CardId, discarded: false, player.SelectedTargetId);
         return;
      }

      var fallbackIndex = FindDiscardIndex(cards);
      ResolveCardPlay(player.Id, fallbackIndex, cards[fallbackIndex].CardId, discarded: true, player.SelectedTargetId);
   }
}

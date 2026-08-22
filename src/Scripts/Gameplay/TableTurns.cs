using Arcomage.Networking;

namespace Arcomage.Gameplay;

/// <summary>
/// Server-authoritative turn flow for <see cref="Table"/>: playing and discarding cards
/// and <see cref="CardFeature.PlayAgain"/> extra turns.
/// </summary>
public partial class Table
{
   /// <summary>
   /// Handles a left-click play or right-click discard from a card in a player's hand.
   /// </summary>
   /// <param name="card">The card control that received the click.</param>
   /// <param name="discarded"><see langword="true"/> to discard without resolving effects; <see langword="false"/> to play it.</param>
   /// <remarks>
   /// The host and offline match resolve immediately. A client forwards the request to the server,
   /// so both peers apply the same result.
   /// </remarks>
   public void OnCardClicked(CardControl card, bool discarded)
   {
      if (_gameOver || _animating || card == null)
         return;

      var ownerId = GetOwnerId(card);
      if (ownerId < 0)
         return;

      var cardIndex = card.GetIndex();
      var cardId = card.CardId ?? string.Empty;

      if (IsAuthority())
      {
         ResolveCardPlay(ownerId, cardIndex, cardId, discarded, _selectedTargetId);
         return;
      }

      if (ownerId != Multiplayer.GetUniqueId())
         return;

      if (Global.Online is { IsInMatch: true })
      {
         _ = Global.Online.SendCardPlay(cardIndex, cardId, discarded, _selectedTargetId);
         return;
      }

      RpcId(1, nameof(RequestCardPlay), cardIndex, cardId, discarded, _selectedTargetId);
   }

   /// <summary>
   /// Client-to-server request to play or discard the card at <paramref name="cardIndex"/>.
   /// </summary>
   /// <param name="cardIndex">Index of the card in the sender's hand.</param>
   /// <param name="cardId">Expected card id, used to reject a desynced hand.</param>
   /// <param name="discarded"><see langword="true"/> if the sender discarded instead of playing.</param>
   /// <param name="targetId">Enemy seat the sender selected, or <c>0</c> to use the default.</param>
   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   private void RequestCardPlay(int cardIndex, string cardId, bool discarded, long targetId)
   {
      if (!Multiplayer.IsServer())
         return;

      ResolveCardPlay(Multiplayer.GetRemoteSenderId(), cardIndex, cardId, discarded, targetId);
   }

   /// <summary>
   /// Validates and resolves one play or discard on the authority, then advances the turn.
   /// </summary>
   /// <param name="playerId">Peer id of the player who owns the card.</param>
   /// <param name="cardIndex">Index of the card in that player's hand.</param>
   /// <param name="cardId">Expected card id, or empty to skip the id check.</param>
   /// <param name="discarded"><see langword="true"/> to discard; <see langword="false"/> to pay the cost and run effects.</param>
   /// <param name="targetId">Enemy seat to hit, or <c>0</c> to pick the default living enemy.</param>
   /// <remarks>
   /// While <see cref="Player.Discarding"/> is set (DrawDiscard cards), any click discards
   /// and the same player keeps the turn afterward.
   /// </remarks>
   private void ResolveCardPlay(long playerId, int cardIndex, string cardId, bool discarded, long targetId = 0)
   {
      _ = ResolveCardPlayAsync(playerId, cardIndex, cardId, discarded, targetId);
   }

   private async Task ResolveCardPlayAsync(long playerId, int cardIndex, string cardId, bool discarded, long targetId)
   {
      try
      {
         if (_gameOver || !IsAuthority() || _turnPlayerId != playerId || _animating)
            return;

         if (!Players.TryGetValue(playerId, out var player) || player.Eliminated)
            return;

         player.SelectedTargetId = IsValidEnemyTarget(player, targetId) ? targetId : GetDefaultEnemyId(player);

         var deck = GetDeckForPlayer(playerId);
         if (deck == null)
            return;

         var cards = deck.GetChildren().OfType<CardControl>().ToList();
         if (cardIndex < 0 || cardIndex >= cards.Count)
            return;

         var card = cards[cardIndex];
         if (!string.IsNullOrEmpty(cardId) && card.CardId != cardId)
            return;

         var discardedPlay = discarded;
         var playAgain = false;
         var enterDiscarding = false;
         string replacementId;

         if (player.Discarding)
         {
            if (!CanDiscard(card, cards))
               return;

            player.Discarding = false;
            discardedPlay = true;
            playAgain = true;
            replacementId = PickRandomCardId();
         }
         else if (discarded)
         {
            if (!CanDiscard(card, cards))
               return;

            replacementId = PickRandomCardId();
         }
         else
         {
            if (!CanAfford(player, card))
               return;

            PayCost(player, card);
            var before = CaptureStatSnapshots();

            if (card.CardActions != null)
            {
               foreach (var action in card.CardActions)
                  action.Execute(this);
            }

            BindSeatHuds(apply: false);
            PlayStatChangeFeedback(before);

            playAgain = HasFeature(card, CardFeature.PlayAgain);
            replacementId = PickRandomCardId();

            if (HasFeature(card, CardFeature.DrawDiscard))
            {
               enterDiscarding = true;
               playAgain = true;
            }
         }

         player.PlayAgain = playAgain;
         var clearGraveyard = !playAgain && !TryGetMatchResult(player, out _, out _);
         await PlayResolvedCard(player, deck, card, cardIndex, discardedPlay, replacementId, playAgain, clearGraveyard, enterDiscarding);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Failed to resolve card play");
         _animating = false;
         if (IsInsideTree())
            UpdateTurnLockUi();
      }
   }

   /// <summary>
   /// Runs the play animation on every peer, then advances the turn on the authority.
   /// </summary>
   private async Task PlayResolvedCard(
      Player player,
      HBoxContainer deck,
      CardControl card,
      int cardIndex,
      bool discarded,
      string replacementId,
      bool playAgain,
      bool clearGraveyard,
      bool enterDiscarding)
   {
      _animating = true;
      UpdateTurnLockUi();
      AfterStateChanged();

      if (!IsOffline && Multiplayer.IsServer())
      {
         Rpc(nameof(AnimateRemoteCardPlay), SnapshotJson.Serialize(new CardPlayCue
         {
            PlayerId = player.Id,
            TargetId = player.SelectedTargetId,
            CardIndex = cardIndex,
            Discarded = discarded,
            ReplacementId = replacementId,
            ClearGraveyard = clearGraveyard,
            PlayedCardId = card.CardId ?? string.Empty,
            Players = BuildPlayerSnapshots(0)
         }));
      }

      try
      {
         await AnimateCardPlay(deck, card, discarded, replacementId);
         if (!IsInsideTree())
            return;

         if (TryFinishGame(player))
         {
            BroadcastGameState();
            return;
         }

         if (clearGraveyard)
            await ClearGraveyardAnimated();

         if (!IsInsideTree())
            return;

         if (enterDiscarding)
            player.Discarding = true;

         _animating = false;
         AdvanceTurn(player, playAgain);
      }
      finally
      {
         _animating = false;
         if (IsInsideTree())
            UpdateTurnLockUi();
      }
   }

   /// <summary>
   /// Client-side playback of a card the host already resolved: apply stats, fly the card, then wait for the snapshot.
   /// </summary>
   [Rpc]
   private void AnimateRemoteCardPlay(string json)
   {
      _ = AnimateRemoteCardPlayAsync(json);
   }

   private async Task AnimateRemoteCardPlayAsync(string json)
   {
      try
      {
         if (IsAuthority())
            return;

         var cue = SnapshotJson.Deserialize<CardPlayCue>(json);
         if (cue == null)
            return;

         var deck = GetDeckForPlayer(cue.PlayerId);
         var cards = deck?.GetChildren().OfType<CardControl>().ToList();
         Vector2? startPos = null;

         if (cards != null && cue.CardIndex >= 0 && cue.CardIndex < cards.Count)
            startPos = GetHandSlotPosition(cards[cue.CardIndex]);
         else if (deck != null && deck.GetChildCount() > 0)
            startPos = GetHandSlotPosition((Control)deck.GetChild(0));

         var before = CaptureStatSnapshots();
         if (!cue.Discarded)
            ApplyPayCostToSnapshot(before, cue.PlayerId, cue.PlayedCardId);

         ApplyPlayerSnapshots(cue.Players);
         if (Players.TryGetValue(cue.PlayerId, out var actor))
            actor.SelectedTargetId = cue.TargetId;

         _selectedTargetId = cue.TargetId;
         BindSeatHuds(apply: false);

         PlayStatChangeFeedback(before);
         UpdateNamePanels();

         CardControl flying = null;
         if (cards != null && cue.CardIndex >= 0 && cue.CardIndex < cards.Count)
         {
            flying = cards[cue.CardIndex];
            if (!string.IsNullOrEmpty(cue.PlayedCardId) && flying.CardId != cue.PlayedCardId)
               flying = ReplaceHandCard(deck, flying, cue.PlayedCardId);
         }
         else if (!string.IsNullOrEmpty(cue.PlayedCardId))
         {
            flying = (CardControl)CreateCard(cue.PlayedCardId);
            deck?.AddChild(flying);
         }

         if (flying == null)
            return;

         _animating = true;
         UpdateTurnLockUi();

         try
         {
            await AnimateCardPlay(deck ?? RedDeck, flying, cue.Discarded, cue.ReplacementId, startPos);
            if (!IsInsideTree())
               return;

            if (cue.ClearGraveyard)
               await ClearGraveyardAnimated();
         }
         finally
         {
            _animating = false;
            if (IsInsideTree())
               ApplyPendingRemoteState();
         }
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Failed to play remote card animation");
         _animating = false;
         if (IsInsideTree())
            ApplyPendingRemoteState();
      }
   }

   /// <summary>
   /// Either keeps the current player (PlayAgain) or starts the opponent's turn with income.
   /// Victory for the acting player is checked before this is called, so a winning card can stay in the graveyard.
   /// </summary>
   private void AdvanceTurn(Player player, bool playAgain)
   {
      player.PlayAgain = playAgain;

      if (playAgain)
      {
         SetTurn(player.Id);
         BroadcastGameState();
         TryStartAiTurn();
         return;
      }

      player.PlayAgain = false;
      var nextId = GetNextLivingPlayerId(player.Id);

      AddResources(nextId);
      SetTurn(nextId);

      if (Players.TryGetValue(nextId, out var nextPlayer) && TryFinishGame(nextPlayer))
      {
         BroadcastGameState();
         return;
      }

      BroadcastGameState();
      TryStartAiTurn();
   }

   /// <summary>
   /// Rebuilds nameplates, hand visibility, and resource panels after a local state change.
   /// </summary>
   private void AfterStateChanged()
   {
      UpdateNamePanels();
      UpdateDeckVisibility();
   }

   /// <summary>
   /// Marks cards as usable only for the current player's affordable, non-discard-mode hand.
   /// </summary>
   private void UpdateCardAffordability()
   {
      var current = GetCurrentPlayer();
      foreach (var (playerId, deck) in _handByPlayer)
         UpdateDeckAffordability(deck, current, _turnPlayerId == playerId);
   }

   /// <summary>
   /// Updates the <see cref="CardControl.Usable"/> flag for every card in <paramref name="deck"/>.
   /// </summary>
   /// <param name="deck">Red or blue hand container.</param>
   /// <param name="current">Player whose resources are checked.</param>
   /// <param name="isTurnOwner"><see langword="true"/> if this deck belongs to the player whose turn it is.</param>
   private static void UpdateDeckAffordability(HBoxContainer deck, Player current, bool isTurnOwner)
   {
      var cards = deck.GetChildren().OfType<CardControl>().ToList();
      foreach (var card in cards)
      {
         card.Usable = isTurnOwner && current != null && (current.Discarding
            ? CanDiscard(card, cards)
            : CanAfford(current, card));

         card.ApplyAffordabilityVisual();
      }
   }

   /// <summary>
   /// Locks the hand while it is not the local human's turn and shows the discard prompt when needed.
   /// </summary>
   private void UpdateTurnLockUi()
   {
      var current = GetCurrentPlayer();
      var localTurn = IsLocalPlayersTurn();
      DeckLocker.Visible = _gameOver || _animating || !localTurn || current?.Ai == true;
      DrawCardLabel.Visible = localTurn && current?.Discarding == true;
   }

   /// <summary>
   /// Returns the player id that owns <paramref name="card"/> based on which hand it sits in.
   /// </summary>
   /// <returns>Red or blue player id, or <c>-1</c> if the card is not in a hand.</returns>
   private long GetOwnerId(CardControl card)
   {
      foreach (var (playerId, deck) in _handByPlayer)
      {
         if (card.GetParent() == deck)
            return playerId;
      }

      if (card.GetParent() == RedDeck)
         return GetLocalHumanId();
      return -1;
   }

   private HBoxContainer GetDeckForPlayer(long playerId)
   {
      if (_handByPlayer.TryGetValue(playerId, out var deck))
         return deck;

      EnsureHandContainers();
      return _handByPlayer.GetValueOrDefault(playerId);
   }

   /// <summary>
   /// Whether this instance may mutate match state (offline play or the multiplayer host).
   /// </summary>
   private bool IsAuthority() => IsOffline || Multiplayer.IsServer();

   /// <summary>
   /// Whether the local human is the one currently allowed to click cards.
   /// </summary>
   private bool IsLocalPlayersTurn()
   {
      if (IsOffline)
         return Players.TryGetValue(_turnPlayerId, out var player) && !player.Ai;

      return _turnPlayerId == Multiplayer.GetUniqueId();
   }

   /// <summary>
   /// Whether <paramref name="player"/> can pay <paramref name="card"/>'s cost in the matching resource.
   /// </summary>
   private static bool CanAfford(Player player, CardControl card)
   {
      if (player == null || card == null)
         return false;

      return card.CardLayout switch
      {
         CardType.Brick => player.Bricks >= card.CardCost,
         CardType.Gem => player.Gems >= card.CardCost,
         CardType.Recruit => player.Recruits >= card.CardCost,
         _ => false
      };
   }

   /// <summary>
   /// Subtracts the card cost from bricks, gems, or recruits depending on layout.
   /// </summary>
   private static void PayCost(Player player, CardControl card)
   {
      switch (card.CardLayout)
      {
         case CardType.Brick:
            player.Bricks -= card.CardCost;
            break;
         case CardType.Gem:
            player.Gems -= card.CardCost;
            break;
         case CardType.Recruit:
            player.Recruits -= card.CardCost;
            break;
      }
   }

   /// <summary>
   /// Whether <paramref name="card"/> may be discarded from <paramref name="hand"/>.
   /// </summary>
   /// <remarks>
   /// Cards with <see cref="CardFeature.NotDiscardable"/> can only be discarded if every card
   /// in the hand is also undiscardable.
   /// </remarks>
   private static bool CanDiscard(CardControl card, List<CardControl> hand)
   {
      if (card.Discardable)
         return true;

      return hand.All(other => !other.Discardable);
   }

   /// <summary>
   /// Finds the first discardable card index, or <c>0</c> if none are marked discardable.
   /// </summary>
   private static int FindDiscardIndex(List<CardControl> cards)
   {
      var discardable = cards.FindIndex(card => card.Discardable);
      return discardable >= 0 ? discardable : 0;
   }

   /// <summary>
   /// Whether <paramref name="card"/> lists <paramref name="feature"/> in its YAML features.
   /// </summary>
   private static bool HasFeature(CardControl card, CardFeature feature)
   {
      return card.CardFeatures?.Contains(feature) == true;
   }
}

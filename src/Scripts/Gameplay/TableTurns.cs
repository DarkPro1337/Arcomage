using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arcomage.Core;
using Arcomage.Data;
using Godot;

namespace Arcomage.Gameplay;

/// <summary>
/// Server-authoritative turn flow for <see cref="Table"/>: playing and discarding cards,
/// <see cref="CardFeature.PlayAgain"/> extra turns, and replicating match state to clients.
/// </summary>
public partial class Table
{
   /// <summary>
   /// Number of packed integers in <see cref="SerializePlayer"/> / <see cref="ApplyPlayerStats"/>.
   /// Order: tower, wall, quarry, bricks, magic, gems, dungeon, recruits.
   /// </summary>
   private const int PlayerStatCount = 8;

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
         ResolveCardPlay(ownerId, cardIndex, cardId, discarded);
         return;
      }

      if (ownerId != Multiplayer.GetUniqueId())
         return;

      RpcId(1, nameof(RequestCardPlay), cardIndex, cardId, discarded);
   }

   /// <summary>
   /// Client-to-server request to play or discard the card at <paramref name="cardIndex"/>.
   /// </summary>
   /// <param name="cardIndex">Index of the card in the sender's hand.</param>
   /// <param name="cardId">Expected card id, used to reject a desynced hand.</param>
   /// <param name="discarded"><see langword="true"/> if the sender discarded instead of playing.</param>
   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   private void RequestCardPlay(int cardIndex, string cardId, bool discarded)
   {
      if (!Multiplayer.IsServer())
         return;

      ResolveCardPlay(Multiplayer.GetRemoteSenderId(), cardIndex, cardId, discarded);
   }

   /// <summary>
   /// Asks the server to resend the current match snapshot to the requesting peer.
   /// </summary>
   /// <remarks>
   /// Used when a client's <see cref="Table"/> appears after the host already started the match,
   /// so the initial broadcast would otherwise be missed.
   /// </remarks>
   [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
   private void RequestGameState()
   {
      if (!Multiplayer.IsServer() || !_gameStarted)
         return;

      SendGameState(Multiplayer.GetRemoteSenderId());
   }

   /// <summary>
   /// Validates and resolves one play or discard on the authority, then advances the turn.
   /// </summary>
   /// <param name="playerId">Peer id of the player who owns the card.</param>
   /// <param name="cardIndex">Index of the card in that player's hand.</param>
   /// <param name="cardId">Expected card id, or empty to skip the id check.</param>
   /// <param name="discarded"><see langword="true"/> to discard; <see langword="false"/> to pay the cost and run effects.</param>
   /// <remarks>
   /// While <see cref="Player.Discarding"/> is set (DrawDiscard cards), any click discards
   /// and the same player keeps the turn afterward.
   /// </remarks>
   private void ResolveCardPlay(long playerId, int cardIndex, string cardId, bool discarded)
   {
      _ = ResolveCardPlayAsync(playerId, cardIndex, cardId, discarded);
   }

   private async Task ResolveCardPlayAsync(long playerId, int cardIndex, string cardId, bool discarded)
   {
      try
      {
         if (_gameOver || !IsAuthority() || _turnPlayerId != playerId || _animating)
            return;

         if (!Players.TryGetValue(playerId, out var player))
            return;

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

            if (card.CardActions != null)
            {
               foreach (var action in card.CardActions)
                  action.Execute(this);
            }

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
         Rpc(nameof(AnimateRemoteCardPlay),
            player.Id, cardIndex, discarded,
            replacementId, clearGraveyard,
            SerializePlayer(_redPlayerId), SerializePlayer(_bluePlayerId));
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
   private void AnimateRemoteCardPlay(
      long playerId,
      int cardIndex,
      bool discarded,
      string replacementId,
      bool clearGraveyard,
      int[] redStats,
      int[] blueStats)
   {
      _ = AnimateRemoteCardPlayAsync(playerId, cardIndex, discarded, replacementId, clearGraveyard, redStats, blueStats);
   }

   private async Task AnimateRemoteCardPlayAsync(
      long playerId,
      int cardIndex,
      bool discarded,
      string replacementId,
      bool clearGraveyard,
      int[] redStats,
      int[] blueStats)
   {
      try
      {
         if (IsAuthority())
            return;

         ApplyPlayerStats(_redPlayerId, redStats);
         ApplyPlayerStats(_bluePlayerId, blueStats);
         UpdateStatPanelUi();

         var deck = GetDeckForPlayer(playerId);
         var cards = deck?.GetChildren().OfType<CardControl>().ToList();
         if (deck == null || cardIndex < 0 || cardIndex >= cards.Count)
            return;

         _animating = true;
         UpdateTurnLockUi();

         try
         {
            await AnimateCardPlay(deck, cards[cardIndex], discarded, replacementId);
            if (!IsInsideTree())
               return;

            if (clearGraveyard)
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
      var nextId = GetOpponentId(player.Id);
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
         ResolveCardPlay(player.Id, discardIndex, cards[discardIndex].CardId, discarded: true);
         return;
      }

      for (var i = 0; i < cards.Count; i++)
      {
         if (!CanAfford(player, cards[i]))
            continue;

         ResolveCardPlay(player.Id, i, cards[i].CardId, discarded: false);
         return;
      }

      var fallbackIndex = FindDiscardIndex(cards);
      ResolveCardPlay(player.Id, fallbackIndex, cards[fallbackIndex].CardId, discarded: true);
   }

   /// <summary>
   /// Refreshes local UI and, in a networked match, sends the snapshot to every client.
   /// </summary>
   private void BroadcastGameState()
   {
      AfterStateChanged();
      if (IsOffline || !Multiplayer.IsServer())
         return;

      SendGameState();
   }

   /// <summary>
   /// Sends the current match snapshot over RPC.
   /// </summary>
   /// <param name="peerId">Target peer id, or <c>0</c> to broadcast to all clients.</param>
   private void SendGameState(long peerId = 0)
   {
      var discarding = GetCurrentPlayer()?.Discarding == true;
      if (peerId == 0)
      {
         Rpc(nameof(ApplyRemoteGameState),
            _turnPlayerId, _redPlayerId, _bluePlayerId,
            SerializePlayer(_redPlayerId), SerializePlayer(_bluePlayerId),
            GetHandIds(RedDeck), GetHandIds(BlueDeck),
            discarding, _gameOver, _winnerId, _winReasonKey);
         return;
      }

      RpcId(peerId, nameof(ApplyRemoteGameState),
         _turnPlayerId, _redPlayerId, _bluePlayerId,
         SerializePlayer(_redPlayerId), SerializePlayer(_bluePlayerId),
         GetHandIds(RedDeck), GetHandIds(BlueDeck),
         discarding, _gameOver, _winnerId, _winReasonKey);
   }

   /// <summary>
   /// Applies a server snapshot on a client: player stats, hands, current turn, and match result.
   /// </summary>
   [Rpc]
   private void ApplyRemoteGameState(
      long turnPlayerId,
      long redId,
      long blueId,
      int[] redStats,
      int[] blueStats,
      string[] redHand,
      string[] blueHand,
      bool discarding,
      bool gameOver,
      long winnerId,
      string winReason)
   {
      if (_animating)
      {
         _pendingRemoteState = new RemoteGameState(
            turnPlayerId, redId, blueId, redStats, blueStats,
            redHand, blueHand, discarding, gameOver, winnerId, winReason);
         ApplyPlayerStats(_redPlayerId, redStats);
         ApplyPlayerStats(_bluePlayerId, blueStats);
         UpdateStatPanelUi();
         return;
      }

      ApplyRemoteGameStateNow(
         turnPlayerId, redId, blueId, redStats, blueStats,
         redHand, blueHand, discarding, gameOver, winnerId, winReason);
   }

   private void ApplyPendingRemoteState()
   {
      if (_pendingRemoteState == null)
      {
         UpdateTurnLockUi();
         return;
      }

      var pending = _pendingRemoteState;
      _pendingRemoteState = null;
      ApplyRemoteGameStateNow(
         pending.TurnPlayerId, pending.RedId, pending.BlueId,
         pending.RedStats, pending.BlueStats,
         pending.RedHand, pending.BlueHand,
         pending.Discarding, pending.GameOver, pending.WinnerId, pending.WinReason);
   }

   private void ApplyRemoteGameStateNow(
      long turnPlayerId,
      long redId,
      long blueId,
      int[] redStats,
      int[] blueStats,
      string[] redHand,
      string[] blueHand,
      bool discarding,
      bool gameOver,
      long winnerId,
      string winReason)
   {
      _redPlayerId = redId;
      _bluePlayerId = blueId;
      EnsurePlayer(redId);
      EnsurePlayer(blueId);
      ApplyPlayerStats(_redPlayerId, redStats);
      ApplyPlayerStats(_bluePlayerId, blueStats);

      if (Players.TryGetValue(turnPlayerId, out var current))
         current.Discarding = discarding;

      if (!HandsMatch(redHand, blueHand))
         SpawnInitialHands(redHand, blueHand);

      _gameStarted = true;
      _gameOver = gameOver;
      SetTurn(turnPlayerId);

      if (!gameOver || winnerId == 0)
         return;

      if (Players.TryGetValue(winnerId, out var winner))
         ShowEndGame(winner, winReason);
   }

   private bool HandsMatch(string[] redHand, string[] blueHand)
   {
      return redHand != null
         && blueHand != null
         && GetHandIds(RedDeck).SequenceEqual(redHand)
         && GetHandIds(BlueDeck).SequenceEqual(blueHand);
   }

   private RemoteGameState _pendingRemoteState;

   private sealed record RemoteGameState(
      long TurnPlayerId,
      long RedId,
      long BlueId,
      int[] RedStats,
      int[] BlueStats,
      string[] RedHand,
      string[] BlueHand,
      bool Discarding,
      bool GameOver,
      long WinnerId,
      string WinReason);

   /// <summary>
   /// Rebuilds nameplates, hand visibility, and resource panels after a local state change.
   /// </summary>
   private void AfterStateChanged()
   {
      UpdateNamePanels();
      UpdateDeckVisibility();
      UpdateStatPanelUi();
   }

   /// <summary>
   /// Marks cards as usable only for the current player's affordable, non-discard-mode hand.
   /// </summary>
   private void UpdateCardAffordability()
   {
      var current = GetCurrentPlayer();
      UpdateDeckAffordability(RedDeck, current, _turnPlayerId == _redPlayerId);
      UpdateDeckAffordability(BlueDeck, current, _turnPlayerId == _bluePlayerId);
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
   /// Ends the match if a victory condition is met.
   /// </summary>
   /// <param name="actingPlayer">Player who just acted, or who just received turn income.</param>
   /// <returns><see langword="true"/> if the match is over.</returns>
   /// <remarks>
   /// The acting player wins ties: their tower destruction / tower / resource victory is checked
   /// before the opponent's.
   /// </remarks>
   private bool TryFinishGame(Player actingPlayer)
   {
      if (_gameOver)
         return true;

      if (!TryGetMatchResult(actingPlayer, out var winner, out var reasonKey))
         return false;

      return ShowEndGame(winner, reasonKey);
   }

   /// <summary>
   /// Checks victory without showing the match-result overlay.
   /// The acting player wins ties: their tower destruction / tower / resource victory is checked first.
   /// </summary>
   private bool TryGetMatchResult(Player actingPlayer, out Player winner, out string reasonKey)
   {
      winner = null;
      reasonKey = string.Empty;
      if (actingPlayer == null)
         return false;

      var opponent = GetOpponent(actingPlayer);
      if (opponent == null)
         return false;

      if (opponent.TowerHp <= 0)
      {
         winner = actingPlayer;
         reasonKey = "TOWER_DESTROY_MSG";
         return true;
      }

      if (actingPlayer.TowerHp >= Config.Settings.TowerVictory)
      {
         winner = actingPlayer;
         reasonKey = "TOWER_VICTORY_MSG";
         return true;
      }

      if (GetResourceTotal(actingPlayer) >= Config.Settings.ResourceVictory)
      {
         winner = actingPlayer;
         reasonKey = "RESOURCE_VICTORY_MSG";
         return true;
      }

      if (actingPlayer.TowerHp <= 0)
      {
         winner = opponent;
         reasonKey = "TOWER_DESTROY_MSG";
         return true;
      }

      if (opponent.TowerHp >= Config.Settings.TowerVictory)
      {
         winner = opponent;
         reasonKey = "TOWER_VICTORY_MSG";
         return true;
      }

      if (GetResourceTotal(opponent) >= Config.Settings.ResourceVictory)
      {
         winner = opponent;
         reasonKey = "RESOURCE_VICTORY_MSG";
         return true;
      }

      return false;
   }

   /// <summary>
   /// Shows the match-result overlay and stops further plays.
   /// </summary>
   /// <param name="winner">Player who won.</param>
   /// <param name="reasonKey">Locale key for the victory type, for example <c>TOWER_DESTROY_MSG</c>.</param>
   /// <returns>Always <see langword="true"/>, so callers can return the result of a victory check directly.</returns>
   private bool ShowEndGame(Player winner, string reasonKey)
   {
      _gameOver = true;
      _winnerId = winner.Id;
      _winReasonKey = reasonKey;
      MatchResult.GetNode<Label>("WinnerName").Text = winner.Ai && IsOffline ? Tr(winner.Name) : winner.Name;
      MatchResult.GetNode<Label>("ByWhat").Text = Tr(reasonKey);
      MatchResult.GetNode<Label>("Time").Text = $"TIME: {ElapsedString}";
      MatchResult.Show();
      DeckLocker.Show();
      return true;
   }

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
      return deck.GetChildren().OfType<CardControl>().Select(card => card.CardId ?? string.Empty).ToArray();
   }

   /// <summary>
   /// Packs a player's resources into a fixed-length array for RPC.
   /// </summary>
   /// <param name="playerId">Player whose stats should be serialized.</param>
   /// <returns>An array of <see cref="PlayerStatCount"/> integers; zeros if the player is missing.</returns>
   private int[] SerializePlayer(long playerId)
   {
      if (!Players.TryGetValue(playerId, out var player))
         return new int[PlayerStatCount];

      return
      [
         player.TowerHp,
         player.WallHp,
         player.Quarries,
         player.Bricks,
         player.Magic,
         player.Gems,
         player.Dungeons,
         player.Recruits
      ];
   }

   /// <summary>
   /// Creates a placeholder player if a snapshot arrives before lobby registration finished.
   /// </summary>
   /// <param name="id">Multiplayer peer id to ensure exists in <see cref="Players"/>.</param>
   private void EnsurePlayer(long id)
   {
      if (Players.ContainsKey(id))
         return;

      Players[id] = new Player
      {
         Id = id,
         Name = id == 1 ? "Host" : "Player",
         Host = id == 1,
         Ai = false
      };
   }

   /// <summary>
   /// Writes a packed stats array from <see cref="SerializePlayer"/> back onto a player.
   /// </summary>
   /// <param name="playerId">Player to update.</param>
   /// <param name="stats">Packed values; ignored when null or shorter than <see cref="PlayerStatCount"/>.</param>
   private void ApplyPlayerStats(long playerId, int[] stats)
   {
      if (stats == null || stats.Length < PlayerStatCount)
         return;

      if (!Players.TryGetValue(playerId, out var player))
         return;

      player.TowerHp = stats[0];
      player.WallHp = stats[1];
      player.Quarries = stats[2];
      player.Bricks = stats[3];
      player.Magic = stats[4];
      player.Gems = stats[5];
      player.Dungeons = stats[6];
      player.Recruits = stats[7];
   }

   /// <summary>
   /// Returns the player id that owns <paramref name="card"/> based on which hand it sits in.
   /// </summary>
   /// <returns>Red or blue player id, or <c>-1</c> if the card is not in a hand.</returns>
   private long GetOwnerId(CardControl card)
   {
      if (card.GetParent() == RedDeck)
         return _redPlayerId;
      if (card.GetParent() == BlueDeck)
         return _bluePlayerId;
      return -1;
   }

   /// <summary>
   /// Returns the hand container for <paramref name="playerId"/>.
   /// </summary>
   private HBoxContainer GetDeckForPlayer(long playerId) =>
      playerId == _redPlayerId ? RedDeck : BlueDeck;

   /// <summary>
   /// Returns the opposing seat for <paramref name="playerId"/>.
   /// </summary>
   private long GetOpponentId(long playerId) =>
      playerId == _redPlayerId ? _bluePlayerId : _redPlayerId;

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
   private static bool HasFeature(CardControl card, CardFeature feature) =>
      card.CardFeatures?.Contains(feature) == true;

   /// <summary>
   /// Sum of bricks, gems, and recruits used for the resource victory check.
   /// </summary>
   private static int GetResourceTotal(Player player) =>
      player.Bricks + player.Gems + player.Recruits;
}

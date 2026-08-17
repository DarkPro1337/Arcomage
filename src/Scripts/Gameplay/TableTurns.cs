using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arcomage.Core;
using Arcomage.Data;
using Arcomage.Networking;
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

         ApplyPlayerSnapshots(cue.Players);
         UpdateStatPanelUi();

         var deck = GetDeckForPlayer(cue.PlayerId);
         var cards = deck?.GetChildren().OfType<CardControl>().ToList();
         var before = CaptureStatSnapshots();
         if (!cue.Discarded && cards != null && cue.CardIndex >= 0 && cue.CardIndex < cards.Count)
            ApplyPayCostToSnapshot(before, cue.PlayerId, cards[cue.CardIndex]);

         PlayStatChangeFeedback(before);

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
            await AnimateCardPlay(deck ?? RedDeck, flying, cue.Discarded, cue.ReplacementId);
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
      if (Global.Online is { IsInMatch: true })
      {
         SendNakamaSnapshots(peerId);
         return;
      }

      if (peerId != 0)
      {
         if (Array.IndexOf(Multiplayer.GetPeers(), (int)peerId) < 0)
            return;

         RpcId(peerId, nameof(ApplyRemoteGameState), SnapshotJson.Serialize(BuildSnapshot(peerId)));
         return;
      }

      var peers = Multiplayer.GetPeers();
      foreach (var id in peers)
      {
         if (id == Multiplayer.GetUniqueId())
            continue;

         RpcId(id, nameof(ApplyRemoteGameState), SnapshotJson.Serialize(BuildSnapshot(id)));
      }
   }

   private void SendNakamaSnapshots(long peerId)
   {
      if (peerId != 0)
      {
         _ = Global.Online.SendSnapshot((int)peerId, SnapshotJson.Serialize(BuildSnapshot(peerId)));
         return;
      }

      var self = Multiplayer.GetUniqueId();
      foreach (var id in Players.Keys.Where(id => id != self && id is > 0 and < 100))
      {
         _ = Global.Online.SendSnapshot((int)id, SnapshotJson.Serialize(BuildSnapshot(id)));
      }
   }

   [Rpc]
   private void ApplyRemoteGameState(string json)
   {
      var snapshot = SnapshotJson.Deserialize<GameSnapshot>(json);
      if (snapshot == null)
         return;

      if (_animating)
      {
         _pendingRemoteState = snapshot;
         ApplyPlayerSnapshots(snapshot.Players);
         UpdateStatPanelUi();
         return;
      }

      ApplyRemoteGameStateNow(snapshot);
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
      ApplyRemoteGameStateNow(pending);
   }

   private void ApplyRemoteGameStateNow(GameSnapshot snapshot)
   {
      MatchMode = snapshot.Mode;
      Ranked = snapshot.Ranked;
      _seatOrder.Clear();
      _seatOrder.AddRange(snapshot.SeatOrder ?? []);
      foreach (var playerSnap in snapshot.Players)
      {
         EnsurePlayer(playerSnap.Id);
         ApplyPlayerSnapshot(playerSnap);
      }

      EnsureHandContainers();
      if (_hudByPlayer.Count != _seatOrder.Count)
         BindSeatHuds();
      else
         UpdateNamePanels();

      var localId = GetLocalHumanId();
      foreach (var playerSnap in snapshot.Players)
      {
         if (playerSnap.Id == localId && playerSnap.Hand is { Length: > 0 })
            ApplyHand(playerSnap.Id, playerSnap.Hand);
         else if (playerSnap.Id != localId)
            ApplyHiddenHand(playerSnap.Id, playerSnap.HandCount);
      }

      if (Players.TryGetValue(snapshot.TurnPlayerId, out var current))
         current.Discarding = snapshot.Discarding;

      _gameStarted = true;
      _gameOver = snapshot.GameOver;
      SetTurn(snapshot.TurnPlayerId);

      if (!snapshot.GameOver || snapshot.WinnerId == 0)
         return;

      if (Players.TryGetValue(snapshot.WinnerId, out var winner))
         ShowEndGame(winner, snapshot.WinReason);
   }

   private GameSnapshot _pendingRemoteState;

   private GameSnapshot BuildSnapshot(long viewerId)
   {
      var discarding = GetCurrentPlayer()?.Discarding == true;
      return new GameSnapshot
      {
         TurnPlayerId = _turnPlayerId,
         Mode = MatchMode,
         Ranked = Ranked,
         Discarding = discarding,
         GameOver = _gameOver,
         WinnerId = _winnerId,
         WinReason = _winReasonKey,
         SeatOrder = [.. _seatOrder],
         Players = BuildPlayerSnapshots(viewerId)
      };
   }

   private List<PlayerSnapshot> BuildPlayerSnapshots(long viewerId)
   {
      var list = new List<PlayerSnapshot>();
      foreach (var player in Players.Values)
      {
         var hand = GetHandIds(GetDeckForPlayer(player.Id));
         var hide = viewerId != 0 && viewerId != player.Id && !IsOffline;
         list.Add(new PlayerSnapshot
         {
            Id = player.Id,
            Name = player.Name,
            SeatIndex = player.SeatIndex,
            TeamId = player.TeamId,
            Eliminated = player.Eliminated,
            Ai = player.Ai,
            Host = player.Host,
            TowerHp = player.TowerHp,
            WallHp = player.WallHp,
            Quarries = player.Quarries,
            Bricks = player.Bricks,
            Magic = player.Magic,
            Gems = player.Gems,
            Dungeons = player.Dungeons,
            Recruits = player.Recruits,
            Hand = hide ? [] : hand,
            HandCount = hand.Length
         });
      }

      return list;
   }

   private void ApplyPlayerSnapshots(List<PlayerSnapshot> snapshots)
   {
      if (snapshots == null)
         return;

      foreach (var snapshot in snapshots)
      {
         EnsurePlayer(snapshot.Id);
         ApplyPlayerSnapshot(snapshot);
      }
   }

   private void ApplyPlayerSnapshot(PlayerSnapshot snapshot)
   {
      if (!Players.TryGetValue(snapshot.Id, out var player))
         return;

      player.SeatIndex = snapshot.SeatIndex;
      player.TeamId = snapshot.TeamId;
      player.Eliminated = snapshot.Eliminated;
      player.TowerHp = snapshot.TowerHp;
      player.WallHp = snapshot.WallHp;
      player.Quarries = snapshot.Quarries;
      player.Bricks = snapshot.Bricks;
      player.Magic = snapshot.Magic;
      player.Gems = snapshot.Gems;
      player.Dungeons = snapshot.Dungeons;
      player.Recruits = snapshot.Recruits;

      if (!string.IsNullOrEmpty(snapshot.Name))
         player.Name = snapshot.Name;
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

      EliminateDestroyedTowers();

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

      if (MatchMode == MatchMode.TwoVsTwo)
         return TryGetTeamMatchResult(actingPlayer, out winner, out reasonKey);

      var living = LivingPlayers().ToList();
      if (MatchMode == MatchMode.FreeForAll)
      {
         if (living.Count == 1)
         {
            winner = living[0];
            reasonKey = "TOWER_DESTROY_MSG";
            return true;
         }

         return false;
      }

      var opponent = GetOpponent(actingPlayer);
      if (opponent == null)
         return false;

      if (opponent.TowerHp <= 0)
      {
         winner = actingPlayer;
         reasonKey = "TOWER_DESTROY_MSG";
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

   private void EliminateDestroyedTowers()
   {
      if (MatchMode == MatchMode.OneVsOne)
         return;

      foreach (var player in Players.Values)
      {
         if (!player.Eliminated && player.TowerHp <= 0)
            player.Eliminated = true;
      }
   }

   private bool TryGetTeamMatchResult(Player actingPlayer, out Player winner, out string reasonKey)
   {
      winner = null;
      reasonKey = string.Empty;

      var team = actingPlayer.TeamId;
      var enemies = Players.Values.Where(player => player.TeamId != team).ToList();
      if (enemies.Count > 0 && enemies.All(player => player.Eliminated || player.TowerHp <= 0))
      {
         winner = actingPlayer;
         reasonKey = "TOWER_DESTROY_MSG";
         return true;
      }

      var allies = Players.Values.Where(player => player.TeamId == team).ToList();
      if (allies.All(player => player.Eliminated || player.TowerHp <= 0))
      {
         winner = enemies.FirstOrDefault(player => !player.Eliminated) ?? enemies.FirstOrDefault();
         reasonKey = "TOWER_DESTROY_MSG";
         return winner != null;
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
      TimeElapsed.Stop();
      MatchResult.Show();
      MatchResult.GetNode<AnimationPlayer>("Anim").Play("hint_anim");
      DeckLocker.Show();

      if (Ranked && IsAuthority())
         _ = ApplyRankedRatings(winner);

      return true;
   }

   private async Task ApplyRankedRatings(Player winner)
   {
      if (Global.Online == null)
         return;

      try
      {
         var ratings = new Dictionary<long, int>();
         foreach (var player in Players.Values.Where(player => !player.Ai))
            ratings[player.Id] = 1000;

         var local = GetLocalHumanId();
         if (local > 0)
            ratings[local] = await Global.Online.LoadRating();

         var winners = MatchMode == MatchMode.TwoVsTwo
            ? Players.Values.Where(player => player.TeamId == winner.TeamId).Select(player => player.Id).ToHashSet()
            : new HashSet<long> { winner.Id };

         foreach (var player in Players.Values.Where(player => !player.Ai))
         {
            var score = winners.Contains(player.Id) ? 1.0 : 0.0;
            var opponentAvg = Players.Values.Where(other => other.Id != player.Id && !other.Ai)
               .Select(other => ratings.GetValueOrDefault(other.Id, 1000)).DefaultIfEmpty(1000).Average();

            var expected = 1.0 / (1.0 + Math.Pow(10, (opponentAvg - ratings.GetValueOrDefault(player.Id, 1000)) / 400.0));
            var next = (int)Math.Round(ratings.GetValueOrDefault(player.Id, 1000) + 32 * (score - expected));
            if (player.Id == local)
               await Global.Online.SubmitRating(Math.Max(100, next));
         }
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Failed to apply ranked rating");
      }
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
      if (deck == null)
         return [];

      return [.. deck.GetChildren().OfType<CardControl>().Select(card => card.CardId ?? string.Empty)];
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
   private static bool HasFeature(CardControl card, CardFeature feature) => card.CardFeatures?.Contains(feature) == true;

   /// <summary>
   /// Sum of bricks, gems, and recruits used for the resource victory check.
   /// </summary>
   private static int GetResourceTotal(Player player) => player.Bricks + player.Gems + player.Recruits;
}

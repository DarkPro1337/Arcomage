using Arcomage.Networking;

namespace Arcomage.Gameplay;

/// <summary>
/// Victory, elimination, and ranked rating updates for <see cref="Table"/>.
/// </summary>
public partial class Table
{
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

      if (actingPlayer.ResourceTotal >= Config.Settings.ResourceVictory)
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

      if (opponent.ResourceTotal >= Config.Settings.ResourceVictory)
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
}

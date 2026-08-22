using Arcomage.Networking;

namespace Arcomage.Gameplay;

/// <summary>
/// Match snapshot broadcast for <see cref="Table"/> over LAN RPC or Nakama.
/// </summary>
public partial class Table
{
   /// <summary>
   /// Number of packed integers in <see cref="SerializePlayer"/> / <see cref="ApplyPlayerStats"/>.
   /// Order: tower, wall, quarry, bricks, magic, gems, dungeon, recruits.
   /// </summary>
   private const int PlayerStatCount = 8;

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
         UpdateNamePanels();
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
      RefreshVisibleSeats();

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
}

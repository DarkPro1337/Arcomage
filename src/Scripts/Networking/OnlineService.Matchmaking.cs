using Nakama;

namespace Arcomage.Networking;

/// <summary>
/// Matchmaker, rooms, dedicated host, and ratings for <see cref="OnlineService"/>.
/// </summary>
public partial class OnlineService
{
   public async Task<bool> FindMatch(MatchMode mode, bool ranked)
   {
      if (!await EnsureSession())
         return false;

      await CancelMatchmaker();
      Mode = mode;
      Ranked = ranked;
      _forceHost = false;

      if (ranked)
      {
         var dedicated = await TryJoinDedicated();
         if (dedicated)
            return true;
      }

      var version = Global.BuildNumber ?? "dev";
      var min = MatchModeRules.MinPlayers(mode);
      var max = MatchModeRules.MaxPlayers(mode);
      var queue = MatchModeRules.QueueName(mode, ranked);
      var query = $"+properties.queue:{queue} +properties.client_version:{version}";
      var props = new Dictionary<string, string>
      {
         ["queue"] = queue,
         ["client_version"] = version,
         ["mode"] = mode.ToString()
      };

      SetStatus("ONLINE_SEARCHING");
      _ticket = await Socket.AddMatchmakerAsync(query, min, max, props);
      return _ticket != null;
   }

   public async Task CancelMatchmaker()
   {
      var ticket = _ticket;
      _ticket = null;
      if (Socket == null || ticket == null)
         return;

      try
      {
         await Socket.RemoveMatchmakerAsync(ticket);
      }
      catch (Exception ex)
      {
         _logger.Debug("Remove matchmaker skipped: {Message}", ex.Message);
      }
   }

   public async Task<string> CreateRoom(MatchMode mode)
   {
      if (!await EnsureSession())
         return string.Empty;

      await LeaveMatch();
      Mode = mode;
      Ranked = false;
      _forceHost = true;
      MatchCode = GenerateCode();
      try
      {
         Match = await JoinCreatedMatch("room", mode, MatchCode, false);
         await RunOnMainThreadAsync(InitializeAsHost);
         await JoinTableChat();
         SetStatus("ONLINE_ROOM");
         return MatchCode;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Create room");
         MatchCode = string.Empty;
         SetStatus("ONLINE_UNAVAILABLE");
         return string.Empty;
      }
   }

   public async Task<bool> JoinRoom(string code, MatchMode mode)
   {
      if (!await EnsureSession() || string.IsNullOrWhiteSpace(code))
         return false;

      await LeaveMatch();
      Mode = mode;
      Ranked = false;
      _forceHost = false;
      MatchCode = code.Trim().ToUpperInvariant();
      try
      {
         string matchId = null;
         for (var attempt = 0; attempt < 5 && string.IsNullOrEmpty(matchId); attempt++)
         {
            try
            {
               matchId = await RpcMatchIdAsync("find_room", $"{{\"code\":\"{EscapeJson(MatchCode)}\"}}");
            }
            catch (Exception) when (attempt < 4)
            {
               await Task.Delay(250);
            }
         }

         if (string.IsNullOrEmpty(matchId))
            return false;

         Match = await Socket.JoinMatchAsync(matchId);
         await RunOnMainThreadAsync(InitializeAsClient);
         await JoinTableChat();
         SetStatus("ONLINE_ROOM");
         return true;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join room");
         MatchCode = string.Empty;
         SetStatus("ONLINE_UNAVAILABLE");
         return false;
      }
   }

   public async Task StartDedicated(MatchMode mode)
   {
      IsDedicated = true;
      if (!await EnsureSession())
         return;

      Mode = mode;
      Ranked = true;
      _forceHost = true;
      try
      {
         Match = await JoinCreatedMatch("dedicated", mode, string.Empty, true);
         await RunOnMainThreadAsync(InitializeAsHost);
         await JoinTableChat();
         await PublishDedicated();
         SetStatus("ONLINE_DEDICATED");
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Start dedicated");
         SetStatus("ONLINE_UNAVAILABLE");
      }
   }

   public async Task LeaveMatch()
   {
      await CancelMatchmaker();
      await LeaveTableChat();

      if (Socket != null && Match != null)
      {
         try
         {
            await Socket.LeaveMatchAsync(Match);
         }
         catch (Exception ex)
         {
            _logger.Debug(ex, "Leave match");
         }
      }

      if (Peer != null)
      {
         Peer.PacketGenerated -= OnGodotPacketGenerated;
         Peer.SetDisconnected();
      }

      Match = null;
      Peer = null;
      _userToPeer.Clear();
      _peerToUser.Clear();
      _userNames.Clear();
      _presences.Clear();
      _hostUserId = string.Empty;
      MatchCode = string.Empty;
      MatchLeft?.Invoke();

      if (HasSession)
         SetStatus("ONLINE_CONNECTED");
   }
   public async Task SubmitRating(int rating)
   {
      if (Client == null || Session == null)
         return;

      try
      {
         await Client.WriteLeaderboardRecordAsync(Session, "arcomage_rating", rating);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Write rating");
      }
   }

   public async Task<int> LoadRating()
   {
      if (Client == null || Session == null)
         return 1000;

      try
      {
         var record = await Client.ListLeaderboardRecordsAroundOwnerAsync(Session, "arcomage_rating", Session.UserId, 1);
         var own = record.Records.FirstOrDefault(item => item.OwnerId == Session.UserId);
         if (own != null && long.TryParse(own.Score, out var score))
            return (int)score;
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Load rating");
      }

      return 1000;
   }

   public void BroadcastStartMatch()
   {
      if (Peer == null || !Peer.IsHost || Match == null)
         return;

      _ = Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.StartMatch, Array.Empty<byte>());
      MatchReady?.Invoke();
   }
   private async Task<bool> TryJoinDedicated()
   {
      try
      {
         var listed = await Client.ListStorageObjectsAsync(Session, "dedicated_servers", 10, string.Empty);
         var server = listed.Objects.FirstOrDefault();
         if (server == null)
            return false;

         var matchId = ExtractJsonString(server.Value, "matchId");
         if (string.IsNullOrEmpty(matchId))
            return false;

         Match = await Socket.JoinMatchAsync(matchId);
         _forceHost = false;
         InitializeAsClient();
         await JoinTableChat();
         SetStatus("ONLINE_MATCHED");
         return true;
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Join dedicated");
      }

      return false;
   }

   private async Task PublishDedicated()
   {
      if (Client == null || Session == null || Match == null)
         return;

      try
      {
         var json = $"{{\"matchId\":\"{Match.Id}\",\"mode\":\"{Mode}\"}}";
         await Client.WriteStorageObjectsAsync(Session, [
            new WriteStorageObject
            {
               Collection = "dedicated_servers",
               Key = "current",
               Value = json,
               PermissionRead = 2,
               PermissionWrite = 1
            }
         ]);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Publish dedicated");
      }
   }
   private async Task<IMatch> JoinCreatedMatch(string kind, MatchMode mode, string code, bool ranked)
   {
      var rankedJson = ranked ? "true" : "false";
      var payload = $"{{\"kind\":\"{EscapeJson(kind)}\",\"mode\":\"{mode}\",\"code\":\"{EscapeJson(code)}\",\"ranked\":{rankedJson}}}";
      var matchId = await RpcMatchIdAsync("create_match", payload);
      if (string.IsNullOrEmpty(matchId))
         throw new InvalidOperationException("create_match returned an empty match id");

      return await Socket.JoinMatchAsync(matchId);
   }

   private async Task<string> RpcMatchIdAsync(string rpcId, string payload)
   {
      var result = await Client.RpcAsync(Session, rpcId, payload);
      return ExtractJsonString(result.Payload, "match_id");
   }

   private static string GenerateCode()
   {
      const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
      var rng = new Random();
      Span<char> chars = stackalloc char[6];
      for (var i = 0; i < chars.Length; i++)
         chars[i] = alphabet[rng.Next(alphabet.Length)];

      return new string(chars);
   }
   private static string EscapeJson(string text) => text.Replace("\\", @"\\").Replace("\"", "\\\"");

   private static string ExtractJsonString(string json, string key)
   {
      var marker = $"\"{key}\":\"";
      var index = json.IndexOf(marker, StringComparison.Ordinal);
      if (index < 0)
         return string.Empty;

      var start = index + marker.Length;
      var end = json.IndexOf('"', start);
      return end > start ? json[start..end] : string.Empty;
   }
}

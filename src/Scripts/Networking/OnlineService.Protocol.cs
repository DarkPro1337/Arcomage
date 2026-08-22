using Nakama;

namespace Arcomage.Networking;

/// <summary>
/// Match protocol: peer map, snapshots, card plays, and Godot packet bridge.
/// </summary>
public partial class OnlineService
{
   public IEnumerable<(int PeerId, string Name)> ListPeers()
   {
      if (_peerToUser.Count > 0)
      {
         foreach (var (peerId, userId) in _peerToUser.OrderBy(pair => pair.Key))
            yield return (peerId, ResolvePeerName(userId, string.Empty));

         yield break;
      }

      BindMatchNames();
      var seen = new HashSet<string>();
      var id = 1;
      if (Match?.Self != null && seen.Add(Match.Self.UserId))
         yield return (id++, ResolvePeerName(Match.Self.UserId, Match.Self.Username));

      if (Match?.Presences == null)
         yield break;

      foreach (var presence in Match.Presences)
      {
         if (!seen.Add(presence.UserId))
            continue;

         yield return (id++, ResolvePeerName(presence.UserId, presence.Username));
      }
   }

   private string ResolvePeerName(string userId, string fallback)
   {
      if (_userNames.TryGetValue(userId, out var name) && !string.IsNullOrEmpty(name))
         return name;

      if (userId == Session?.UserId)
         return Config.Settings.Nickname;

      return string.IsNullOrEmpty(fallback) ? userId : fallback;
   }

   public async Task SendSnapshot(int peerId, string json)
   {
      if (Socket == null || Match == null || string.IsNullOrEmpty(json))
         return;

      try
      {
         await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.Snapshot, $"{peerId}\n{json}");
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Send snapshot to peer {PeerId}", peerId);
      }
   }

   public async Task RequestSnapshot()
   {
      if (Socket == null || Match == null)
         return;

      try
      {
         await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.RequestSnapshot, Array.Empty<byte>());
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Request snapshot");
      }
   }

   public async Task SendCardPlay(int cardIndex, string cardId, bool discarded, long targetId)
   {
      if (Socket == null || Match == null)
         return;

      var payload = $"{cardIndex}\n{cardId}\n{(discarded ? 1 : 0)}\n{targetId}";
      try
      {
         await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.CardPlay, payload);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Send card play");
      }
   }

   private void RememberPresence(IUserPresence presence)
   {
      if (presence == null || string.IsNullOrEmpty(presence.UserId))
         return;

      _presences[presence.UserId] = presence;
   }

   private void TrackMatchPresences()
   {
      RememberPresence(Match?.Self);
      if (Match?.Presences == null)
         return;

      foreach (var presence in Match.Presences)
         RememberPresence(presence);
   }
   private async void OnMatchmakerMatched(IMatchmakerMatched matched)
   {
      try
      {
         _ticket = null;
         Match = await Socket.JoinMatchAsync(matched);
         TrackMatchPresences();
         foreach (var user in matched.Users)
         {
            if (user.Presence == null)
               continue;

            RememberPresence(user.Presence);
            _userNames[user.Presence.UserId] = !string.IsNullOrEmpty(user.Presence.Username)
               ? user.Presence.Username
               : user.Presence.UserId;
         }

         _userNames[Session.UserId] = Config.Settings.Nickname;
         ElectHostFromUsers(matched.Users.Select(user => user.Presence.UserId).Append(Session.UserId));
         await JoinTableChat();
         SetStatus("ONLINE_MATCHED");
         MatchReady?.Invoke();
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join matched match failed");
         SetStatus("ONLINE_UNAVAILABLE");
      }
   }

   private void ElectHostFromUsers(IEnumerable<string> userIds)
   {
      var ordered = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
      if (ordered.Count == 0)
         return;

      _hostUserId = ordered[0];
      _forceHost = Session.UserId == _hostUserId;
      BuildPeerMap(ordered);
      BindPeer();
   }

   public void NotifyGodotPeers()
   {
      if (Peer == null)
         return;

      foreach (var peerId in _peerToUser.Keys)
         Peer.NotifyPeerConnected(peerId);
   }

   private void InitializeAsHost()
   {
      _hostUserId = Session.UserId;
      _userNames[Session.UserId] = Config.Settings.Nickname;

      TrackMatchPresences();
      BuildPeerMap(new[] { Session.UserId }.Concat(_presences.Keys));
      BindPeer();

      _ = SendHostClaim();
      _ = SendPeerAssignments();
   }

   private void InitializeAsClient()
   {
      _userNames[Session.UserId] = Config.Settings.Nickname;
      if (Peer == null)
      {
         Peer = new NakamaMultiplayerPeer();
         Peer.PacketGenerated += OnGodotPacketGenerated;
      }

      Peer.BeginConnecting();
      BindMatchNames();
   }

   private void BindMatchNames()
   {
      if (Match?.Self != null)
         _userNames[Match.Self.UserId] = string.IsNullOrEmpty(Match.Self.Username) ? Config.Settings.Nickname : Match.Self.Username;

      if (Match?.Presences == null)
         return;

      foreach (var presence in Match.Presences)
         _userNames[presence.UserId] = presence.Username;
   }

   private void BuildPeerMap(IEnumerable<string> userIds)
   {
      var ordered = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
      if (!string.IsNullOrEmpty(_hostUserId))
      {
         ordered.Remove(_hostUserId);
         ordered.Insert(0, _hostUserId);
      }

      _userToPeer.Clear();
      _peerToUser.Clear();
      for (var i = 0; i < ordered.Count; i++)
      {
         var peerId = i + 1;
         _userToPeer[ordered[i]] = peerId;
         _peerToUser[peerId] = ordered[i];
      }

      BindMatchNames();
      PeersChanged?.Invoke();
   }

   private void BindPeer()
   {
      if (!_userToPeer.TryGetValue(Session.UserId, out var selfId))
         return;

      if (Peer == null)
      {
         Peer = new NakamaMultiplayerPeer();
         Peer.PacketGenerated += OnGodotPacketGenerated;
      }

      Peer.Initialize(selfId);
      NotifyGodotPeers();
      PeersChanged?.Invoke();
   }

   private async Task SendHostClaim()
   {
      if (Match == null || Socket == null)
         return;

      var payload = Encoding.UTF8.GetBytes(Session.UserId);
      await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.HostClaim, payload);
   }

   private async Task SendPeerAssignments()
   {
      if (Match == null || Socket == null || !_forceHost)
         return;

      var body = string.Join(",", _peerToUser.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
      await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.AssignPeers, Encoding.UTF8.GetBytes(body));
   }

   private void OnMatchPresence(IMatchPresenceEvent presence)
   {
      BindMatchNames();
      foreach (var joined in presence.Joins)
      {
         RememberPresence(joined);
         _userNames[joined.UserId] = joined.Username;
      }

      NotifyPeerChanges(presence);

      foreach (var left in presence.Leaves)
         _presences.Remove(left.UserId);

      if (_forceHost)
      {
         BuildPeerMap(new[] { Session.UserId }.Concat(_presences.Keys));
         _ = SendPeerAssignments();
      }

      PeersChanged?.Invoke();
   }

   private void NotifyPeerChanges(IMatchPresenceEvent presence)
   {
      if (Peer == null)
         return;

      foreach (var joined in presence.Joins)
      {
         if (_userToPeer.TryGetValue(joined.UserId, out var peerId))
            Peer.NotifyPeerConnected(peerId);
      }

      foreach (var left in presence.Leaves)
      {
         if (_userToPeer.TryGetValue(left.UserId, out var peerId))
            Peer.NotifyPeerDisconnected(peerId);
      }
   }

   private void OnMatchState(IMatchState state)
   {
      switch ((NakamaOp)state.OpCode)
      {
         case NakamaOp.HostClaim:
            _hostUserId = Encoding.UTF8.GetString(state.State);
            _forceHost = Session.UserId == _hostUserId;
            break;
         case NakamaOp.AssignPeers:
            ApplyPeerAssignments(Encoding.UTF8.GetString(state.State));
            break;
         case NakamaOp.GodotPacket:
            DeliverGodotPacket(state.State);
            break;
         case NakamaOp.Chat:
            if (TableChannel != null)
               break;
            var chat = Encoding.UTF8.GetString(state.State);
            var split = chat.Split(':', 2);
            if (split.Length == 2)
               ChatReceived?.Invoke(split[0], split[1]);
            break;
         case NakamaOp.StartMatch:
            MatchReady?.Invoke();
            break;
         case NakamaOp.Snapshot:
            ApplyIncomingSnapshot(Encoding.UTF8.GetString(state.State));
            break;
         case NakamaOp.RequestSnapshot:
            if (Peer is { IsHost: true } && state.UserPresence != null && _userToPeer.TryGetValue(state.UserPresence.UserId, out var requester))
               SnapshotRequested?.Invoke(requester);
            break;
         case NakamaOp.CardPlay:
            if (Peer is not { IsHost: true } || state.UserPresence == null || !_userToPeer.TryGetValue(state.UserPresence.UserId, out var fromPeer))
               break;
            ParseCardPlay(Encoding.UTF8.GetString(state.State), fromPeer);
            break;
      }
   }

   private void ApplyIncomingSnapshot(string raw)
   {
      var json = raw;
      var split = raw.IndexOf('\n');
      if (split > 0 && int.TryParse(raw[..split], out var target) && target > 0)
      {
         var self = 0;
         if (Session != null)
            _userToPeer.TryGetValue(Session.UserId, out self);

         if (self == 0)
            self = Peer?._GetUniqueId() ?? 0;

         if (self != 0 && target != self)
            return;

         json = raw[(split + 1)..];
      }

      SnapshotReceived?.Invoke(json);
   }

   private void ApplyPeerAssignments(string body)
   {
      _userToPeer.Clear();
      _peerToUser.Clear();
      foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries))
      {
         var pair = part.Split(':');
         if (pair.Length != 2 || !int.TryParse(pair[0], out var peerId))
            continue;

         _peerToUser[peerId] = pair[1];
         _userToPeer[pair[1]] = peerId;
      }

      BindPeer();
   }

   private void ParseCardPlay(string body, int fromPeer)
   {
      var parts = body.Split('\n');
      if (parts.Length < 4)
         return;

      if (!int.TryParse(parts[0], out var index))
         return;

      var discarded = parts[2] == "1";
      long.TryParse(parts[3], out var targetId);
      CardPlayReceived?.Invoke(fromPeer, index, parts[1], discarded, targetId);
   }

   private void DeliverGodotPacket(byte[] buffer)
   {
      if (Peer == null || buffer == null || buffer.Length < 8)
         return;

      var from = BitConverter.ToInt32(buffer, 0);
      var to = BitConverter.ToInt32(buffer, 4);
      var payload = buffer[8..];
      var self = Peer._GetUniqueId();
      if (to != 0 && to != self)
         return;

      Peer.DeliverPacket(payload, from);
   }

   private async void OnGodotPacketGenerated(int targetPeer, byte[] payload)
   {
      try
      {
         if (Socket == null || Match == null || Peer == null)
            return;

         var packet = new byte[8 + payload.Length];
         BitConverter.GetBytes(Peer._GetUniqueId()).CopyTo(packet, 0);
         BitConverter.GetBytes(targetPeer).CopyTo(packet, 4);
         payload.CopyTo(packet, 8);
         await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.GodotPacket, packet);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Send Godot packet");
      }
   }

   private void OnSocketMatchmakerMatched(IMatchmakerMatched matched)
   {
      RunOnMainThread(() => OnMatchmakerMatched(matched));
   }

   private void OnSocketMatchState(IMatchState state)
   {
      RunOnMainThread(() => OnMatchState(state));
   }

   private void OnSocketMatchPresence(IMatchPresenceEvent presence)
   {
      RunOnMainThread(() => OnMatchPresence(presence));
   }
}

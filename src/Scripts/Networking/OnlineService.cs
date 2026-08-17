using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arcomage.Core;
using Godot;
using Nakama;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Networking;

public partial class OnlineService : Node
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Online");

   public const int OpAssignPeers = 1;
   public const int OpGodotPacket = 2;
   public const int OpHostClaim = 3;
   public const int OpChat = 4;
   public const int OpStartMatch = 5;
   public const int OpSnapshot = 6;
   public const int OpRequestSnapshot = 7;
   public const int OpCardPlay = 8;

   public static OnlineService Instance { get; private set; }

   public IClient Client { get; private set; }
   public ISession Session { get; private set; }
   public ISocket Socket { get; private set; }
   public IMatch Match { get; private set; }
   public NakamaMultiplayerPeer Peer { get; private set; }
   public IChannel TableChannel { get; private set; }

   public bool HasSession => Session != null && Socket is { IsConnected: true };
   public bool IsInMatch => Match != null;
   public bool IsDedicated { get; private set; }
   public bool Ranked { get; private set; }
   public MatchMode Mode { get; private set; } = MatchMode.OneVsOne;
   public string MatchCode { get; private set; } = string.Empty;
   public string StatusMessage { get; private set; } = string.Empty;

   public event Action StatusChanged;
   public event Action MatchReady;
   public event Action MatchLeft;
   public event Action<string, string> ChatReceived;
   public event Action PeersChanged;
   public event Action<string> SnapshotReceived;
   public event Action<int> SnapshotRequested;
   public event Action<int, int, string, bool, long> CardPlayReceived;

   private readonly Dictionary<string, int> _userToPeer = new();
   private readonly Dictionary<int, string> _peerToUser = new();
   private readonly Dictionary<string, string> _userNames = new();
   private readonly Dictionary<string, IUserPresence> _presences = new();
   private readonly ConcurrentQueue<Action> _mainThread = new();
   private string _hostUserId = string.Empty;
   private IMatchmakerTicket _ticket;
   private bool _forceHost;

   public IReadOnlyDictionary<string, int> UserToPeer => _userToPeer;
   public IReadOnlyDictionary<int, string> PeerToUser => _peerToUser;

   public override void _EnterTree()
   {
      Instance = this;
      Global.Online = this;
   }

   public override void _ExitTree()
   {
      if (Instance == this)
         Instance = null;
   }

   public override void _Process(double delta)
   {
      while (_mainThread.TryDequeue(out var action))
      {
         try
         {
            action();
         }
         catch (Exception ex)
         {
            _logger.Error(ex, "Nakama main-thread callback failed");
         }
      }
   }

   private void RunOnMainThread(Action action)
   {
      if (action == null)
         return;

      if (GodotThread.IsMainThread())
      {
         action();
         return;
      }

      _mainThread.Enqueue(action);
   }

   private Task RunOnMainThreadAsync(Action action)
   {
      if (action == null)
         return Task.CompletedTask;

      if (GodotThread.IsMainThread())
      {
         action();
         return Task.CompletedTask;
      }

      var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      _mainThread.Enqueue(() =>
      {
         try
         {
            action();
            done.TrySetResult();
         }
         catch (Exception ex)
         {
            done.TrySetException(ex);
         }
      });
      return done.Task;
   }

   public async Task<bool> EnsureSession()
   {
      try
      {
         if (HasSession)
            return true;

         var host = Config.Settings.NakamaHost;
         var port = Config.Settings.NakamaPort;
         var scheme = Config.Settings.NakamaUseSsl ? "https" : "http";
         Client = new Client(scheme, host, port, Config.Settings.NakamaServerKey)
         {
            Timeout = 10
         };

         var deviceId = BuildDeviceId();
         var username = SanitizeUsername(Config.Settings.Nickname);
         Session = await Client.AuthenticateDeviceAsync(deviceId, username);
         try
         {
            await Client.UpdateAccountAsync(Session, username, Config.Settings.Nickname);
         }
         catch (Exception ex)
         {
            _logger.Debug("Update account skipped: {Message}", ex.Message);
         }

         Socket = Nakama.Socket.From(Client);
         Socket.ReceivedMatchmakerMatched += matched => RunOnMainThread(() => OnMatchmakerMatched(matched));
         Socket.ReceivedMatchState += state => RunOnMainThread(() => OnMatchState(state));
         Socket.ReceivedMatchPresence += presence => RunOnMainThread(() => OnMatchPresence(presence));
         Socket.ReceivedChannelMessage += message => RunOnMainThread(() => OnChannelMessage(message));
         Socket.Closed += OnSocketClosed;
         await Socket.ConnectAsync(Session, true);

         _logger.Debug("Nakama session {UserId} as {Username} (device {DeviceId})", Session.UserId, username, deviceId);
         SetStatus("ONLINE_CONNECTED");
         return true;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Nakama session failed");
         SetStatus("ONLINE_UNAVAILABLE");
         return false;
      }
   }

   public async Task DisconnectAsync()
   {
      await LeaveMatch();
      if (Socket != null)
      {
         Socket.ReceivedMatchmakerMatched -= OnMatchmakerMatched;
         Socket.ReceivedMatchState -= OnMatchState;
         Socket.ReceivedMatchPresence -= OnMatchPresence;
         Socket.ReceivedChannelMessage -= OnChannelMessage;
         Socket.Closed -= OnSocketClosed;
         try
         {
            await Socket.CloseAsync();
         }
         catch (Exception ex)
         {
            _logger.Debug(ex, "Socket close");
         }
      }

      Socket = null;
      Session = null;
      SetStatus(string.Empty);
   }

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
      Match = await Socket.CreateMatchAsync(MatchCode);
      await RunOnMainThreadAsync(InitializeAsHost);
      SetStatus("ONLINE_ROOM");
      return MatchCode;
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
      Match = await Socket.CreateMatchAsync(MatchCode);
      await RunOnMainThreadAsync(InitializeAsClient);
      SetStatus("ONLINE_ROOM");
      return true;
   }

   public async Task StartDedicated(MatchMode mode)
   {
      IsDedicated = true;
      if (!await EnsureSession())
         return;

      Mode = mode;
      Ranked = true;
      _forceHost = true;
      Match = await Socket.CreateMatchAsync($"dedicated-{Guid.NewGuid():N}");
      await RunOnMainThreadAsync(InitializeAsHost);
      await PublishDedicated();
      SetStatus("ONLINE_DEDICATED");
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
   }

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
         await Socket.SendMatchStateAsync(Match.Id, OpSnapshot, $"{peerId}\n{json}");
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
         await Socket.SendMatchStateAsync(Match.Id, OpRequestSnapshot, Array.Empty<byte>());
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
         await Socket.SendMatchStateAsync(Match.Id, OpCardPlay, payload);
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

   public async Task SendChat(string text)
   {
      if (string.IsNullOrWhiteSpace(text))
         return;

      text = text.Trim();
      if (text.Length > 240)
         text = text[..240];

      if (TableChannel != null)
      {
         await Socket.WriteChatMessageAsync(TableChannel.Id, $"{{\"text\":\"{EscapeJson(text)}\"}}");
         return;
      }

      if (Match != null)
      {
         var payload = Encoding.UTF8.GetBytes($"{Config.Settings.Nickname}:{text}");
         await Socket.SendMatchStateAsync(Match.Id, OpChat, payload);
         ChatReceived?.Invoke(Config.Settings.Nickname, text);
      }
   }

   public async Task JoinTableChat()
   {
      if (Socket == null || Match == null)
         return;

      try
      {
         TableChannel = await Socket.JoinChatAsync($"match_{Match.Id}", ChannelType.Room);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Join table chat");
      }
   }

   public async Task LeaveTableChat()
   {
      if (Socket == null || TableChannel == null)
         return;

      try
      {
         await Socket.LeaveChatAsync(TableChannel);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Leave table chat");
      }

      TableChannel = null;
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

      _ = Socket.SendMatchStateAsync(Match.Id, OpStartMatch, Array.Empty<byte>());
      MatchReady?.Invoke();
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
            _userNames[user.Presence.UserId] = string.IsNullOrEmpty(user.Presence.Username)
               ? user.Presence.UserId
               : user.Presence.Username;
         }

         _userNames[Session.UserId] = Config.Settings.Nickname;
         ElectHostFromUsers(matched.Users.Select(user => user.Presence.UserId).Append(Session.UserId));
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
      await Socket.SendMatchStateAsync(Match.Id, OpHostClaim, payload);
   }

   private async Task SendPeerAssignments()
   {
      if (Match == null || Socket == null || !_forceHost)
         return;

      var body = string.Join(",", _peerToUser.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
      await Socket.SendMatchStateAsync(Match.Id, OpAssignPeers, Encoding.UTF8.GetBytes(body));
   }

   private void OnMatchPresence(IMatchPresenceEvent presence)
   {
      BindMatchNames();
      foreach (var joined in presence.Joins)
      {
         RememberPresence(joined);
         _userNames[joined.UserId] = joined.Username;
      }

      foreach (var left in presence.Leaves)
         _presences.Remove(left.UserId);

      if (_forceHost)
      {
         var ids = new[] { Session.UserId }.Concat(_presences.Keys);
         foreach (var left in presence.Leaves)
            ids = ids.Where(id => id != left.UserId);

         BuildPeerMap(ids);
         NotifyPeerChanges(presence);
         _ = SendPeerAssignments();
         return;
      }

      NotifyPeerChanges(presence);
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
      switch (state.OpCode)
      {
         case OpHostClaim:
            _hostUserId = Encoding.UTF8.GetString(state.State);
            _forceHost = Session.UserId == _hostUserId;
            break;
         case OpAssignPeers:
            ApplyPeerAssignments(Encoding.UTF8.GetString(state.State));
            break;
         case OpGodotPacket:
            DeliverGodotPacket(state.State);
            break;
         case OpChat:
            var chat = Encoding.UTF8.GetString(state.State);
            var split = chat.Split(':', 2);
            if (split.Length == 2)
               ChatReceived?.Invoke(split[0], split[1]);
            break;
         case OpStartMatch:
            MatchReady?.Invoke();
            break;
         case OpSnapshot:
            ApplyIncomingSnapshot(Encoding.UTF8.GetString(state.State));
            break;
         case OpRequestSnapshot:
            if (Peer is { IsHost: true } && state.UserPresence != null && _userToPeer.TryGetValue(state.UserPresence.UserId, out var requester))
               SnapshotRequested?.Invoke(requester);
            break;
         case OpCardPlay:
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
         await Socket.SendMatchStateAsync(Match.Id, OpGodotPacket, packet);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Send Godot packet");
      }
   }

   private void OnChannelMessage(IApiChannelMessage message)
   {
      var username = message.Username ?? "Player";
      var text = message.Content ?? string.Empty;
      var marker = "\"text\":\"";
      var index = text.IndexOf(marker, StringComparison.Ordinal);
      if (index >= 0)
      {
         var start = index + marker.Length;
         var end = text.IndexOf('"', start);
         if (end > start)
            text = text[start..end];
      }

      ChatReceived?.Invoke(username, text);
   }

   private void OnSocketClosed(string reason)
   {
      RunOnMainThread(() =>
      {
         if (!string.IsNullOrEmpty(reason))
            _logger.Debug("Nakama socket closed: {Reason}", reason);

         SetStatus("ONLINE_UNAVAILABLE");
      });
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

   private void SetStatus(string key)
   {
      RunOnMainThread(() =>
      {
         StatusMessage = key;
         StatusChanged?.Invoke();
      });
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

   private static string BuildDeviceId()
   {
      var hardware = OS.GetUniqueId();
      if (string.IsNullOrWhiteSpace(hardware))
         hardware = "godot-device";

      var args = Global.GetCommandLineArgs();
      var suffix = string.Empty;
      if (args.TryGetValue("nakamaDevice", out var device) && !string.IsNullOrWhiteSpace(device))
         suffix = device.Trim();
      else if (args.TryGetValue("playerName", out var playerName) && !string.IsNullOrWhiteSpace(playerName))
         suffix = playerName.Trim();

      var id = string.IsNullOrEmpty(suffix) ? hardware : $"{hardware}:{suffix}";
      if (id.Length < 10)
         id = id.PadRight(10, '0');

      return id.Length <= 128 ? id : id[..128];
   }

   private static string SanitizeUsername(string name)
   {
      if (string.IsNullOrWhiteSpace(name))
         return "Player";

      var trimmed = new string(name.Where(char.IsLetterOrDigit).Take(18).ToArray());
      return string.IsNullOrEmpty(trimmed) ? "Player" : trimmed;
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

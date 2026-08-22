using System.Collections.Concurrent;
using Nakama;

namespace Arcomage.Networking;

public partial class OnlineService : Node
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Online");

   public static OnlineService Instance { get; private set; }

   public IClient Client { get; private set; }
   public ISession Session { get; private set; }
   public ISocket Socket { get; private set; }
   public IMatch Match { get; private set; }
   public NakamaMultiplayerPeer Peer { get; private set; }
   public IChannel TableChannel { get; private set; }

   public const string ConsoleChatRoom = "arcomage";

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
   private IChannel _consoleChannel;
   private bool _forceHost;
   private bool _socketBound;

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
}

using Arcomage.Networking;

namespace Arcomage.Gameplay;

public partial class Table
{
   public MatchMode MatchMode { get; private set; } = MatchMode.OneVsOne;
   public bool Ranked { get; private set; }

   private readonly List<long> _seatOrder = [];
   private readonly Dictionary<long, HBoxContainer> _handByPlayer = new();
   private readonly Dictionary<long, PlayerSeat> _hudByPlayer = new();
   private readonly List<PlayerSeat> _extraSeats = [];

   private Node _handsRoot;
   private Control _extraSeatsRoot;
   private PlayerSeat _leftSeat;
   private PlayerSeat _rightSeat;
   private long _selectedTargetId;

   private PlayerSeat LeftSeat => _leftSeat ??= GetNode<PlayerSeat>("LeftSeat");
   private PlayerSeat RightSeat => _rightSeat ??= GetNode<PlayerSeat>("RightSeat");

   public IReadOnlyList<long> SeatOrder => _seatOrder;

   public long GetLocalHumanId()
   {
      if (IsOffline || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
         return 1;

      if (OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless")
         return -1;

      return Multiplayer.GetUniqueId();
   }

   public IEnumerable<Player> LivingPlayers() => Players.Values.Where(player => !player.Eliminated);

   public IEnumerable<Player> EnemiesOf(Player self)
   {
      if (self == null)
         return [];

      return LivingPlayers().Where(player => player.Id != self.Id && AreEnemies(self, player));
   }

   public IEnumerable<Player> AlliesOf(Player self, bool includeSelf = false)
   {
      if (self == null)
         return [];

      return LivingPlayers().Where(player =>
         (includeSelf || player.Id != self.Id) && !AreEnemies(self, player) &&
         (MatchMode != MatchMode.TwoVsTwo || player.TeamId == self.TeamId));
   }

   public bool AreEnemies(Player left, Player right)
   {
      if (left == null || right == null || left.Id == right.Id)
         return false;

      if (MatchMode == MatchMode.TwoVsTwo)
         return left.TeamId != right.TeamId;

      return true;
   }

   public long GetDefaultEnemyId(Player self)
   {
      var enemies = EnemiesOf(self).OrderBy(player => player.TowerHp).ToList();
      if (enemies.Count == 0)
         return 0;

      if (self.SelectedTargetId != 0 && enemies.Any(player => player.Id == self.SelectedTargetId))
         return self.SelectedTargetId;

      return enemies[0].Id;
   }

   public bool IsValidEnemyTarget(Player self, long targetId) => EnemiesOf(self).Any(player => player.Id == targetId);

   private void ConfigureMatchRules()
   {
      MatchMode = Global.PendingMatchMode;
      Ranked = Global.PendingRanked;
      if (IsOffline)
      {
         MatchMode = MatchMode.OneVsOne;
         Ranked = false;
      }
   }

   private void AssignSlots()
   {
      _seatOrder.Clear();
      if (Players.Count == 0)
         return;

      foreach (var player in Players.Values.OrderBy(player => player.Host ? 0 : 1).ThenBy(player => player.Id))
         _seatOrder.Add(player.Id);

      for (var i = 0; i < _seatOrder.Count; i++)
      {
         if (!Players.TryGetValue(_seatOrder[i], out var player))
            continue;

         player.SeatIndex = i;
         player.TeamId = MatchMode == MatchMode.TwoVsTwo ? i % 2 + 1 : 0;
      }

      EnsureHandContainers();
      BindSeatHuds();
   }

   private void EnsureHandsRoot()
   {
      _handsRoot ??= GetNodeOrNull<Node>("Hands") ?? CreateHiddenHandsRoot();
      _extraSeatsRoot ??= GetNodeOrNull<Control>("ExtraSeats") ?? CreateExtraSeatsRoot();
   }

   private Node CreateHiddenHandsRoot()
   {
      var root = new Control
      {
         Name = "Hands",
         Visible = false,
         MouseFilter = MouseFilterEnum.Ignore
      };

      AddChild(root);
      return root;
   }

   private Control CreateExtraSeatsRoot()
   {
      var root = new Control
      {
         Name = "ExtraSeats",
         MouseFilter = MouseFilterEnum.Ignore
      };

      root.SetAnchorsPreset(LayoutPreset.FullRect);
      AddChild(root);
      MoveChild(root, 0);
      return root;
   }

   private void EnsureHandContainers()
   {
      EnsureHandsRoot();
      var localId = GetLocalHumanId();
      foreach (var playerId in _seatOrder.Where(playerId => !_handByPlayer.ContainsKey(playerId)))
      {
         if (playerId == localId || (localId < 0 && playerId == _seatOrder[0]))
         {
            _handByPlayer[playerId] = RedDeck;
            continue;
         }

         if (!_handByPlayer.ContainsValue(BlueDeck))
         {
            _handByPlayer[playerId] = BlueDeck;
            continue;
         }

         _handByPlayer[playerId] = CreateHiddenHand(playerId);
      }
   }

   private HBoxContainer CreateHiddenHand(long playerId)
   {
      var hidden = new HBoxContainer
      {
         Name = $"Hand_{playerId}",
         Visible = false
      };

      _handsRoot.AddChild(hidden);
      return hidden;
   }

   /// <summary>
   /// Shows the current player's cards on RedDeck (local) or BlueDeck (everyone else).
   /// Extra seats store unused hands off-screen; without this swap, those turns look empty.
   /// </summary>
   private void PresentTurnHand()
   {
      if (_animating || _turnPlayerId == 0)
         return;

      EnsureHandContainers();
      if (!_handByPlayer.TryGetValue(_turnPlayerId, out var turnDeck))
         return;

      if (turnDeck == RedDeck || turnDeck == BlueDeck)
         return;

      var blueOwner = GetBlueDeckOwner();
      if (blueOwner == 0)
      {
         MoveHandChildren(turnDeck, BlueDeck);
         _handByPlayer[_turnPlayerId] = BlueDeck;
         return;
      }

      SwapHandChildren(turnDeck, BlueDeck);
      _handByPlayer[_turnPlayerId] = BlueDeck;
      _handByPlayer[blueOwner] = turnDeck;
   }

   private long GetBlueDeckOwner()
   {
      foreach (var (id, deck) in _handByPlayer)
      {
         if (deck == BlueDeck)
            return id;
      }

      return 0;
   }

   private static void SwapHandChildren(HBoxContainer left, HBoxContainer right)
   {
      if (left == null || right == null || left == right)
         return;

      var fromLeft = DetachHandChildren(left);
      var fromRight = DetachHandChildren(right);
      AttachHandChildren(left, fromRight);
      AttachHandChildren(right, fromLeft);
   }

   private static void MoveHandChildren(HBoxContainer from, HBoxContainer to)
   {
      if (from == null || to == null || from == to)
         return;

      AttachHandChildren(to, DetachHandChildren(from));
   }

   private static List<Node> DetachHandChildren(HBoxContainer deck)
   {
      var children = deck.GetChildren().ToList();
      foreach (var child in children)
         deck.RemoveChild(child);

      return children;
   }

   private static void AttachHandChildren(HBoxContainer deck, List<Node> children)
   {
      foreach (var child in children)
         deck.AddChild(child);
   }

   private void BindSeatHuds(bool apply = true)
   {
      EnsureHandsRoot();
      ClearSeatBindings();

      var (leftId, rightId) = GetVisiblePair();
      BindMainSeat(LeftSeat, leftId);
      BindMainSeat(RightSeat, rightId);

      var extraIndex = 0;
      foreach (var id in _seatOrder)
      {
         if (id == leftId || id == rightId)
            continue;

         var extra = PlayerSeat.CreateExtra(_extraSeatsRoot, extraIndex++);
         extra.PlayerId = id;
         extra.ApplyIdentity(SeatIndexOf(id));
         extra.Clicked += OnSeatHudClicked;
         _extraSeats.Add(extra);
         _hudByPlayer[id] = extra;
      }

      if (!apply)
         return;

      ApplyResourcePanelLocale();
      UpdateNamePanels();
      HighlightSelectedTarget();
   }

   private void RefreshVisibleSeats()
   {
      var (leftId, rightId) = GetVisiblePair();
      var extraCount = _seatOrder.Count(id => id != leftId && id != rightId);
      if (LeftSeat.PlayerId == leftId && RightSeat.PlayerId == rightId &&
          _hudByPlayer.Count == _seatOrder.Count && _extraSeats.Count == extraCount)
      {
         UpdateNamePanels();
         HighlightSelectedTarget();
         return;
      }

      BindSeatHuds();
   }

   private (long LeftId, long RightId) GetVisiblePair()
   {
      if (_seatOrder.Count == 0)
         return (0, 0);

      if (_seatOrder.Count <= 2)
         return (_seatOrder[0], _seatOrder.Count > 1 ? _seatOrder[1] : 0);

      var actorId = _seatOrder.Contains(_turnPlayerId) ? _turnPlayerId : _seatOrder[0];
      Players.TryGetValue(actorId, out var actor);
      var enemyId = actor != null ? GetDefaultEnemyId(actor) : 0;
      if (enemyId == 0)
         enemyId = _seatOrder.FirstOrDefault(id => id != actorId);

      return SeatIndexOf(actorId) <= SeatIndexOf(enemyId)
         ? (actorId, enemyId)
         : (enemyId, actorId);
   }

   private int SeatIndexOf(long playerId)
   {
      if (Players.TryGetValue(playerId, out var player))
         return player.SeatIndex;

      var index = _seatOrder.IndexOf(playerId);
      return index < 0 ? 0 : index;
   }

   private void BindMainSeat(PlayerSeat seat, long playerId)
   {
      if (seat == null)
         return;

      seat.Clicked -= OnSeatHudClicked;
      seat.PlayerId = playerId;
      if (playerId == 0)
         return;

      seat.ApplyIdentity(SeatIndexOf(playerId));
      seat.Clicked += OnSeatHudClicked;
      _hudByPlayer[playerId] = seat;
   }

   private void ClearSeatBindings()
   {
      LeftSeat.Clicked -= OnSeatHudClicked;
      RightSeat.Clicked -= OnSeatHudClicked;
      foreach (var extra in _extraSeats)
      {
         extra.Clicked -= OnSeatHudClicked;
         extra.ReleaseExtra();
      }

      _extraSeats.Clear();
      _hudByPlayer.Clear();
   }

   private void OnSeatHudClicked(long playerId)
   {
      var local = GetLocalHumanPlayer();
      if (local == null || !IsValidEnemyTarget(local, playerId))
         return;

      _selectedTargetId = playerId;
      local.SelectedTargetId = playerId;
      RefreshVisibleSeats();
      HighlightSelectedTarget();
   }

   private Player GetLocalHumanPlayer()
   {
      var id = GetLocalHumanId();
      return id > 0 && Players.TryGetValue(id, out var player) ? player : null;
   }

   private void HighlightSelectedTarget()
   {
      foreach (var (id, hud) in _hudByPlayer)
         hud.SetSelected(id == _selectedTargetId);
   }

   private void PlaceStartCardsOnDeck()
   {
      EnsureHandContainers();
      foreach (var playerId in _seatOrder)
      {
         var deck = GetDeckForPlayer(playerId);
         if (deck == null)
            continue;

         ClearDeck(deck);
         foreach (var cardId in BuildRandomHandIds(Config.Settings.CardsInHand))
            deck.AddChild(CreateCard(cardId));
      }

      UpdateDeckVisibility();
   }

   private long GetRandomTurnPlayerId()
   {
      var living = _seatOrder.Where(id => Players.TryGetValue(id, out var player) && !player.Eliminated).ToList();
      if (living.Count == 0)
         return _seatOrder.FirstOrDefault();

      return living[_rng.RandiRange(0, living.Count - 1)];
   }

   private long GetNextLivingPlayerId(long playerId)
   {
      if (_seatOrder.Count == 0)
         return playerId;

      var start = _seatOrder.IndexOf(playerId);
      if (start < 0)
         start = 0;

      for (var step = 1; step <= _seatOrder.Count; step++)
      {
         var id = _seatOrder[(start + step) % _seatOrder.Count];
         if (Players.TryGetValue(id, out var player) && !player.Eliminated)
            return id;
      }

      return playerId;
   }

   private Vector2 GetSeatPlayOrigin(long playerId)
   {
      if (_hudByPlayer.TryGetValue(playerId, out var hud))
         return hud.GetPlayOrigin();

      return GetPlayCenterPosition();
   }

   public Color GetChatNameColor(string name)
   {
      if (string.IsNullOrEmpty(name))
         return Colors.White;

      foreach (var player in Players.Values)
      {
         if (!NamesMatch(player.Name, name))
            continue;

         return PlayerSeat.ColorForSeat(player.SeatIndex);
      }

      return Colors.White;
   }

   private static bool NamesMatch(string playerName, string chatName)
   {
      if (string.Equals(playerName, chatName, StringComparison.OrdinalIgnoreCase))
         return true;

      var sanitized = new string((playerName ?? string.Empty).Where(char.IsLetterOrDigit).Take(18).ToArray());
      return sanitized.Length > 0 &&
             string.Equals(sanitized, chatName, StringComparison.OrdinalIgnoreCase);
   }
}

namespace Arcomage.Gameplay;

public partial class Table
{
   private MatchChat _matchChat;

   private void SetupMatchChat()
   {
      var chat = GetNodeOrNull<MatchChat>("MatchChat");
      if (chat == null)
         return;

      if (IsOffline || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
      {
         chat.Hide();
         return;
      }

      if (OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless")
      {
         chat.QueueFree();
         return;
      }

      _matchChat = chat;
      _matchChat.Bind(this);
   }
}

public partial class MatchChat : Control
{
   private const int MaxVisibleMessages = 6;
   private const float MessageLifetime = 7f;
   private const float MessageFade = 1.2f;

   private VBoxContainer _log;
   private LineEdit _input;
   private Table _table;
   private bool _suppressFocusExit;

   public bool IsInputOpen => _input is { Visible: true };

   public override void _Ready()
   {
      _log = GetNode<VBoxContainer>("Log/Messages");
      _input = GetNode<LineEdit>("Input");

      _input.TextSubmitted += OnTextSubmitted;
      _input.FocusExited += OnInputFocusExited;
   }

   public void Bind(Table table)
   {
      _table = table;
      if (Global.Online != null)
         Global.Online.ChatReceived += OnOnlineChat;

      if (Global.Online is { IsInMatch: true })
         _ = Global.Online.JoinTableChat();
   }

   public override void _ExitTree()
   {
      if (Global.Online != null)
         Global.Online.ChatReceived -= OnOnlineChat;

      if (_input == null)
         return;

      _input.TextSubmitted -= OnTextSubmitted;
      _input.FocusExited -= OnInputFocusExited;
   }

   public bool TryHandleInput(InputEvent @event)
   {
      if (@event.IsEcho() || GetTree().Paused)
         return false;

      if (@event is not InputEventKey { Pressed: true, Echo: false } key)
         return false;

      if (IsInputOpen && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape))
      {
         CloseInput();
         GetViewport().SetInputAsHandled();
         return true;
      }

      if (IsInputOpen)
         return false;

      if (!@event.IsActionPressed("ui_chat"))
         return false;

      OpenInput();
      GetViewport().SetInputAsHandled();
      return true;
   }

   private void OpenInput()
   {
      if (_input == null)
         return;

      _input.Visible = true;
      _input.CallDeferred(Control.MethodName.GrabFocus);
   }

   private void CloseInput()
   {
      if (_input == null)
         return;

      _suppressFocusExit = true;
      _input.Text = string.Empty;
      if (_input.HasFocus())
         _input.ReleaseFocus();

      _input.Visible = false;
      _suppressFocusExit = false;
   }

   private void OnInputFocusExited()
   {
      if (!_suppressFocusExit)
         CloseInput();
   }

   private void OnOnlineChat(string name, string text) => Append(name, text);

   private void OnTextSubmitted(string text)
   {
      CloseInput();
      if (string.IsNullOrWhiteSpace(text))
         return;

      if (Global.Online is { IsInMatch: true })
      {
         _ = Global.Online.SendChat(text);
         return;
      }

      _table?.Rpc(nameof(Table.BroadcastChat), Config.Settings.Nickname, text);
   }

   public void Append(string name, string text)
   {
      if (_log == null)
         return;

      var line = new RichTextLabel
      {
         FitContent = true,
         ScrollActive = false,
         AutowrapMode = TextServer.AutowrapMode.WordSmart,
         SizeFlagsHorizontal = SizeFlags.ExpandFill,
         MouseFilter = MouseFilterEnum.Ignore,
         FocusMode = FocusModeEnum.None
      };

      line.AddThemeColorOverride("default_color", Colors.White);
      line.AddThemeColorOverride("font_outline_color", Colors.Black);
      line.AddThemeConstantOverride("outline_size", 8);
      line.AddThemeFontSizeOverride("normal_font_size", 18);

      line.PushColor(_table?.GetChatNameColor(name) ?? Colors.White);
      line.AddText(name);
      line.Pop();
      line.AddText($": {text}");
      _log.AddChild(line);

      while (_log.GetChildCount() > MaxVisibleMessages)
         _log.GetChild(0).QueueFree();

      var tween = CreateTween();
      tween.TweenInterval(MessageLifetime);
      tween.TweenProperty(line, "modulate:a", 0f, MessageFade);
      tween.TweenCallback(Callable.From(line.QueueFree));
   }
}

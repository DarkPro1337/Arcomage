using Arcomage.Core;
using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   private MatchChat _matchChat;

   private void SetupMatchChat()
   {
      if (IsOffline || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
         return;

      if (OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless")
         return;

      _matchChat = new MatchChat();
      _matchChat.Name = "MatchChat";
      AddChild(_matchChat);
      var menu = GetNodeOrNull<Node>("InGameMenu");
      if (menu != null)
         MoveChild(_matchChat, menu.GetIndex());

      _matchChat.BuildUi();
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

   public void BuildUi()
   {
      SetAnchorsPreset(LayoutPreset.FullRect);
      MouseFilter = MouseFilterEnum.Ignore;

      _log = new VBoxContainer
      {
         Name = "Log",
         MouseFilter = MouseFilterEnum.Ignore,
         Alignment = BoxContainer.AlignmentMode.End
      };
      _log.SetAnchorsPreset(LayoutPreset.VcenterWide);
      _log.OffsetLeft = 80;
      _log.OffsetRight = -80;
      _log.OffsetTop = -40;
      _log.OffsetBottom = 80;
      AddChild(_log);

      _input = new LineEdit
      {
         Name = "Input",
         Visible = false,
         PlaceholderText = Tr("CHAT_PLACEHOLDER"),
         MaxLength = 120
      };
      _input.SetAnchorsPreset(LayoutPreset.BottomWide);
      _input.OffsetLeft = 120;
      _input.OffsetRight = -120;
      _input.OffsetTop = -236;
      _input.OffsetBottom = -208;
      _input.TextSubmitted += OnTextSubmitted;
      _input.FocusExited += OnInputFocusExited;
      AddChild(_input);
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

      if (!IsChatHotkey(key))
         return false;

      OpenInput();
      GetViewport().SetInputAsHandled();
      return true;
   }

   private static bool IsChatHotkey(InputEventKey key)
   {
      if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed)
         return false;

      return key.PhysicalKeycode == Key.T || key.Keycode == Key.T;
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

      var line = new Label
      {
         Text = $"{name}: {text}",
         HorizontalAlignment = HorizontalAlignment.Center,
         AutowrapMode = TextServer.AutowrapMode.WordSmart,
         MouseFilter = MouseFilterEnum.Ignore
      };

      line.AddThemeColorOverride("font_color", Colors.White);
      line.AddThemeColorOverride("font_outline_color", Colors.Black);
      line.AddThemeConstantOverride("outline_size", 8);
      line.AddThemeFontSizeOverride("font_size", 18);
      _log.AddChild(line);

      while (_log.GetChildCount() > MaxVisibleMessages)
         _log.GetChild(0).QueueFree();

      var tween = CreateTween();
      tween.TweenInterval(MessageLifetime);
      tween.TweenProperty(line, "modulate:a", 0f, MessageFade);
      tween.TweenCallback(Callable.From(line.QueueFree));
   }
}

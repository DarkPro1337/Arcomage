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
      MouseFilter = MouseFilterEnum.Ignore;
      ApplyPreset(this, LayoutPreset.FullRect);

      _log = new VBoxContainer
      {
         Name = "Log",
         MouseFilter = MouseFilterEnum.Ignore,
         Alignment = BoxContainer.AlignmentMode.End
      };
      AddChild(_log);
      ApplyPreset(_log, LayoutPreset.FullRect);
      _log.OffsetLeft = 48;
      _log.OffsetRight = -48;
      _log.OffsetTop = 72;
      _log.OffsetBottom = -248;

      _input = new LineEdit
      {
         Name = "Input",
         Visible = false,
         PlaceholderText = Tr("CHAT_PLACEHOLDER"),
         MaxLength = 120,
         CustomMinimumSize = new Vector2(0, 36)
      };
      AddChild(_input);
      ApplyPreset(_input, LayoutPreset.BottomWide);
      _input.OffsetLeft = 120;
      _input.OffsetRight = -120;
      _input.OffsetTop = -244;
      _input.OffsetBottom = -208;
      _input.AddThemeFontSizeOverride("font_size", 16);
      _input.AddThemeStyleboxOverride("normal", CreateInputStyle());
      _input.AddThemeStyleboxOverride("focus", CreateInputStyle());
      _input.TextSubmitted += OnTextSubmitted;
      _input.FocusExited += OnInputFocusExited;
   }

   private static void ApplyPreset(Control control, LayoutPreset preset)
   {
      control.SetAnchorsPreset(preset);
      control.SetOffsetsPreset(preset);
      control.GrowHorizontal = GrowDirection.Both;
      control.GrowVertical = GrowDirection.Both;
   }

   private static StyleBoxFlat CreateInputStyle()
   {
      return new StyleBoxFlat
      {
         BgColor = new Color(0, 0, 0, 0.78f),
         BorderColor = new Color(1, 1, 1, 0.35f),
         BorderWidthLeft = 1,
         BorderWidthTop = 1,
         BorderWidthRight = 1,
         BorderWidthBottom = 1,
         CornerRadiusTopLeft = 4,
         CornerRadiusTopRight = 4,
         CornerRadiusBottomRight = 4,
         CornerRadiusBottomLeft = 4,
         ContentMarginLeft = 10,
         ContentMarginTop = 6,
         ContentMarginRight = 10,
         ContentMarginBottom = 6
      };
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
         HorizontalAlignment = HorizontalAlignment.Left,
         AutowrapMode = TextServer.AutowrapMode.WordSmart,
         SizeFlagsHorizontal = SizeFlags.ExpandFill,
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

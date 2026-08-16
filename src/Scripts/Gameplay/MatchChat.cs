using Arcomage.Core;
using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   private MatchChat _matchChat;

   private void SetupMatchChat()
   {
      _matchChat = new MatchChat();
      _matchChat.Name = "MatchChat";
      AddChild(_matchChat);
      _matchChat.BuildUi();
      _matchChat.Bind(this);
   }
}

public partial class MatchChat : Control
{
   private TextEdit _log;
   private LineEdit _input;
   private Table _table;

   public void BuildUi()
   {
      SetAnchorsPreset(LayoutPreset.BottomLeft);
      OffsetLeft = 8;
      OffsetTop = -280;
      OffsetRight = 260;
      OffsetBottom = -210;
      MouseFilter = MouseFilterEnum.Stop;

      var panel = new Panel { Name = "Panel" };
      panel.SetAnchorsPreset(LayoutPreset.FullRect);
      AddChild(panel);

      _log = new TextEdit
      {
         Name = "Log",
         Editable = false,
         WrapMode = TextEdit.LineWrappingMode.Boundary,
         ScrollFitContentHeight = true
      };

      _log.SetAnchorsPreset(LayoutPreset.FullRect);
      _log.OffsetBottom = -24;
      panel.AddChild(_log);

      _input = new LineEdit
      {
         Name = "Input",
         PlaceholderText = Tr("CHAT_PLACEHOLDER")
      };

      _input.SetAnchorsPreset(LayoutPreset.BottomWide);
      _input.OffsetTop = -24;
      _input.TextSubmitted += OnTextSubmitted;
      panel.AddChild(_input);
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

      if (_input != null)
         _input.TextSubmitted -= OnTextSubmitted;
   }

   private void OnOnlineChat(string name, string text) => Append(name, text);

   private void OnTextSubmitted(string text)
   {
      if (string.IsNullOrWhiteSpace(text))
         return;

      _input.Text = string.Empty;
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

      _log.Text += $"{name}: {text}\n";
      _log.ScrollVertical = _log.GetLineCount();
   }
}

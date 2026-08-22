using Nakama;

namespace Arcomage.Networking;

/// <summary>
/// Table and console chat for <see cref="OnlineService"/>.
/// </summary>
public partial class OnlineService
{
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
         await WriteConsoleChat(text);
         if (Match != null)
            await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.Chat, Encoding.UTF8.GetBytes($"{Config.Settings.Nickname}:{text}"));
         return;
      }

      if (Match != null)
      {
         var payload = Encoding.UTF8.GetBytes($"{Config.Settings.Nickname}:{text}");
         await Socket.SendMatchStateAsync(Match.Id, (long)NakamaOp.Chat, payload);
         ChatReceived?.Invoke(Config.Settings.Nickname, text);
         await WriteConsoleChat(text);
      }
   }

   public async Task JoinTableChat()
   {
      if (Socket == null || Match == null)
         return;

      try
      {
         TableChannel ??= await Socket.JoinChatAsync(ChatRoomName(), ChannelType.Room, persistence: true);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join table chat");
      }

      try
      {
         _consoleChannel ??= await Socket.JoinChatAsync(ConsoleChatRoom, ChannelType.Room, persistence: true, hidden: true);
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Join console chat room");
      }
   }

   public async Task LeaveTableChat()
   {
      await LeaveChannel(TableChannel);
      TableChannel = null;
      await LeaveChannel(_consoleChannel);
      _consoleChannel = null;
   }

   private async Task LeaveChannel(IChannel channel)
   {
      if (Socket == null || channel == null)
         return;

      try
      {
         await Socket.LeaveChatAsync(channel);
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Leave chat channel");
      }
   }

   private async Task WriteConsoleChat(string text)
   {
      if (Socket == null || _consoleChannel == null)
         return;

      try
      {
         await Socket.WriteChatMessageAsync(_consoleChannel.Id, $"{{\"text\":\"{EscapeJson(text)}\"}}");
      }
      catch (Exception ex)
      {
         _logger.Debug(ex, "Write console chat");
      }
   }

   private string ChatRoomName()
   {
      if (!string.IsNullOrEmpty(MatchCode))
         return $"{ConsoleChatRoom}-{MatchCode}";

      var id = Match?.Id ?? string.Empty;
      var dot = id.IndexOf('.');
      if (dot > 0)
         id = id[..dot];

      return string.IsNullOrEmpty(id) ? ConsoleChatRoom : $"{ConsoleChatRoom}-{id}";
   }
   private void OnChannelMessage(IApiChannelMessage message)
   {
      if (_consoleChannel != null && message.ChannelId == _consoleChannel.Id)
      {
         if (TableChannel == null || message.ChannelId != TableChannel.Id)
            return;
      }

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
   private void OnSocketChannelMessage(IApiChannelMessage message)
   {
      RunOnMainThread(() => OnChannelMessage(message));
   }
}

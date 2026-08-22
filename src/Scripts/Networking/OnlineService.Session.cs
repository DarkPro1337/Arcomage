using Nakama;

namespace Arcomage.Networking;

/// <summary>
/// Nakama session, socket bind, and main-thread dispatch for <see cref="OnlineService"/>.
/// </summary>
public partial class OnlineService
{
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

         DropSession();

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
         BindSocketEvents();
         await Socket.ConnectAsync(Session, true);

         _logger.Debug("Nakama session {UserId} as {Username} (device {DeviceId})", Session.UserId, username, deviceId);
         SetStatus("ONLINE_CONNECTED");
         return true;
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Nakama session failed");
         DropSession();
         SetStatus("ONLINE_UNAVAILABLE");
         return false;
      }
   }

   public async Task DisconnectAsync()
   {
      await LeaveMatch();
      if (Socket != null)
      {
         UnbindSocketEvents();
         try
         {
            await Socket.CloseAsync();
         }
         catch (Exception ex)
         {
            _logger.Debug(ex, "Socket close");
         }
      }

      DropSession();
      SetStatus(string.Empty);
   }

   private void BindSocketEvents()
   {
      if (Socket == null || _socketBound)
         return;

      Socket.ReceivedMatchmakerMatched += OnSocketMatchmakerMatched;
      Socket.ReceivedMatchState += OnSocketMatchState;
      Socket.ReceivedMatchPresence += OnSocketMatchPresence;
      Socket.ReceivedChannelMessage += OnSocketChannelMessage;
      Socket.Closed += OnSocketClosed;

      _socketBound = true;
   }

   private void UnbindSocketEvents()
   {
      if (Socket == null || !_socketBound)
      {
         _socketBound = false;
         return;
      }

      Socket.ReceivedMatchmakerMatched -= OnSocketMatchmakerMatched;
      Socket.ReceivedMatchState -= OnSocketMatchState;
      Socket.ReceivedMatchPresence -= OnSocketMatchPresence;
      Socket.ReceivedChannelMessage -= OnSocketChannelMessage;
      Socket.Closed -= OnSocketClosed;

      _socketBound = false;
   }

   private void DropSession()
   {
      UnbindSocketEvents();
      Socket = null;
      Session = null;
      Client = null;
   }

   private void OnSocketClosed(string reason)
   {
      RunOnMainThread(() =>
      {
         if (!string.IsNullOrEmpty(reason))
            _logger.Debug("Nakama socket closed: {Reason}", reason);

         DropSession();
         SetStatus("ONLINE_UNAVAILABLE");
      });
   }
   private void SetStatus(string key)
   {
      RunOnMainThread(() =>
      {
         StatusMessage = key;
         StatusChanged?.Invoke();
      });
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
}

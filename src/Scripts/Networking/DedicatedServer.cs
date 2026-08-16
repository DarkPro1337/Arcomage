using System.Linq;
using Arcomage.Core;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.Networking;

/// <summary>
/// Headless ranked host: <c>--dedicated</c> or a headless display.
/// Creates a Nakama match and runs <see cref="Table"/> as peer 1.
/// </summary>
public partial class DedicatedServer : Node
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Dedicated");

   public static bool ShouldRun()
   {
      if (DisplayServer.GetName() == "headless")
         return true;

      var args = Global.GetCommandLineArgs();
      return args.ContainsKey("dedicated");
   }

   public override async void _Ready()
   {
      var args = Global.GetCommandLineArgs();
      if (args.TryGetValue("nakamaHost", out var host))
         Config.Settings.NakamaHost = host;

      if (args.TryGetValue("nakamaPort", out var port) && int.TryParse(port, out var parsed))
         Config.Settings.NakamaPort = parsed;

      Global.PendingMatchMode = MatchMode.OneVsOne;
      Global.PendingRanked = true;
      _logger.Debug("Starting dedicated ranked host");

      if (Global.Online == null)
         return;

      await Global.Online.StartDedicated(MatchMode.OneVsOne);
      if (Global.Online.Peer == null)
      {
         _logger.Error("Dedicated Nakama peer was not created");
         return;
      }

      GetTree().GetMultiplayer().MultiplayerPeer = Global.Online.Peer;
      Global.Online.MatchReady += OnMatchReady;
      Global.Online.PeersChanged += OnPeersChanged;
   }

   private void OnPeersChanged()
   {
      if (Global.Online == null)
         return;

      var humans = Global.Online.ListPeers().Count(peer => !Global.Online.IsDedicated || peer.PeerId != 1);
      _logger.Debug("Dedicated lobby size: {Count}", humans);

      if (humans >= 2)
         CallDeferred(nameof(StartDedicatedMatch));
   }

   private void OnMatchReady() => CallDeferred(nameof(StartDedicatedMatch));

   private void StartDedicatedMatch()
   {
      if (GetNodeOrNull("/root/DedicatedTable") != null)
         return;

      var table = ResourceLoader.Load<PackedScene>("res://Scenes/Gameplay/Table.tscn").Instantiate();
      table.Name = "DedicatedTable";
      GetTree().Root.AddChild(table);
   }
}

using System;
using System.Collections.Concurrent;
using Godot;

namespace Arcomage.Networking;

/// <summary>
/// Relays Godot High-Level Multiplayer packets through a Nakama match.
/// Peer 1 is the match host (elected player or dedicated server).
/// </summary>
public partial class NakamaMultiplayerPeer : MultiplayerPeerExtension
{
   public const int HostPeerId = 1;
   private const int MaxPacketBytes = 1 << 24;

   private readonly ConcurrentQueue<IncomingPacket> _incoming = new();
   private ConnectionStatus _status = ConnectionStatus.Disconnected;
   private int _selfId;
   private int _targetPeer;
   private bool _refusingNewConnections;

   public event Action<int, byte[]> PacketGenerated;

   public bool IsHost => _selfId == HostPeerId;

   public override byte[] _GetPacketScript()
   {
      return _incoming.TryDequeue(out var packet) ? packet.Data : [];
   }

   public override Error _PutPacketScript(byte[] pBuffer)
   {
      PacketGenerated?.Invoke(_targetPeer, pBuffer ?? []);
      return Error.Ok;
   }

   public override int _GetAvailablePacketCount() => _incoming.Count;

   public override int _GetMaxPacketSize() => MaxPacketBytes;

   public override TransferModeEnum _GetPacketMode() => TransferModeEnum.Reliable;

   public override int _GetPacketChannel() => 0;

   public override int _GetTransferChannel() => 0;

   public override void _SetTransferChannel(int pChannel)
   {
   }

   public override void _SetTransferMode(TransferModeEnum pMode)
   {
   }

   public override TransferModeEnum _GetTransferMode() => TransferModeEnum.Reliable;

   public override void _SetTargetPeer(int pPeer)
   {
      _targetPeer = pPeer;
   }

   public override int _GetPacketPeer()
   {
      if (_status != ConnectionStatus.Connected || !_incoming.TryPeek(out var packet))
         return HostPeerId;

      return packet.From;
   }

   public override bool _IsServer() => _selfId == HostPeerId;

   public override bool _IsServerRelaySupported() => true;

   public override void _Poll()
   {
   }

   public override int _GetUniqueId() => _selfId;

   public override void _SetRefuseNewConnections(bool pEnable) => _refusingNewConnections = pEnable;

   public override bool _IsRefusingNewConnections() => _refusingNewConnections;

   public override ConnectionStatus _GetConnectionStatus() => _status;

   public override void _Close()
   {
      _status = ConnectionStatus.Disconnected;
      _incoming.Clear();
      _selfId = 0;
   }

   public void BeginConnecting()
   {
      _status = ConnectionStatus.Connecting;
      _incoming.Clear();
   }

   public void Initialize(int selfId)
   {
      _selfId = selfId;
      _status = ConnectionStatus.Connected;
   }

   public void SetDisconnected()
   {
      _status = ConnectionStatus.Disconnected;
   }

   public void DeliverPacket(byte[] data, int fromPeerId)
   {
      _incoming.Enqueue(new IncomingPacket(data ?? [], fromPeerId));
   }

   public void NotifyPeerConnected(int peerId)
   {
      if (peerId == _selfId)
         return;

      EmitSignal(MultiplayerPeer.SignalName.PeerConnected, peerId);
   }

   public void NotifyPeerDisconnected(int peerId)
   {
      if (peerId == _selfId)
         return;

      EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, peerId);
   }

   private sealed class IncomingPacket(byte[] data, int from)
   {
      public byte[] Data { get; } = data;
      public int From { get; } = from;
   }
}

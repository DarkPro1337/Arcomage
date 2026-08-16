using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
   private readonly ConcurrentQueue<int> _pendingConnected = new();
   private readonly ConcurrentQueue<int> _pendingDisconnected = new();

   private readonly HashSet<int> _knownRemotes = [];

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

   private int _lastPacketFrom = HostPeerId;

   public override int _GetPacketPeer()
   {
      if (_incoming.TryPeek(out var packet))
         _lastPacketFrom = packet.From;

      return _lastPacketFrom;
   }

   public override bool _IsServer() => _selfId == HostPeerId;

   public override bool _IsServerRelaySupported() => true;

   public override void _Poll()
   {
      while (_pendingConnected.TryDequeue(out var peerId))
         EmitSignal(MultiplayerPeer.SignalName.PeerConnected, peerId);

      while (_pendingDisconnected.TryDequeue(out var peerId))
         EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, peerId);
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
      _knownRemotes.Clear();
      Drain(_pendingConnected);
      Drain(_pendingDisconnected);
   }

   public void BeginConnecting()
   {
      _status = ConnectionStatus.Connecting;
      _incoming.Clear();
      _knownRemotes.Clear();
      Drain(_pendingConnected);
      Drain(_pendingDisconnected);
   }

   public void Initialize(int selfId)
   {
      _selfId = selfId;
      _status = ConnectionStatus.Connected;
      _knownRemotes.Clear();
      Drain(_pendingConnected);
      Drain(_pendingDisconnected);
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
      if (peerId == _selfId || peerId <= 0)
         return;

      if (!_knownRemotes.Add(peerId))
         return;

      _pendingConnected.Enqueue(peerId);
   }

   public void NotifyPeerDisconnected(int peerId)
   {
      if (peerId == _selfId || peerId <= 0)
         return;

      if (!_knownRemotes.Remove(peerId))
         return;

      _pendingDisconnected.Enqueue(peerId);
   }

   private static void Drain(ConcurrentQueue<int> queue)
   {
      while (queue.TryDequeue(out _))
      {
      }
   }

   private sealed class IncomingPacket(byte[] data, int from)
   {
      public byte[] Data { get; } = data;
      public int From { get; } = from;
   }
}

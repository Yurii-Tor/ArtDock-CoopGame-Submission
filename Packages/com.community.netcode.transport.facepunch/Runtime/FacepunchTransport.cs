// Adapted from Unity's MIT-licensed community Facepunch transport. See LICENSE.md and UPSTREAM.md.
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Transports.Facepunch
{
    /// <summary>Steam Networking Sockets relay transport. SteamClient lifetime belongs to the adapter.</summary>
    public sealed class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private sealed class Peer
        {
            public Connection Connection;
            public ulong SteamId;
            public double LastPing;
            public ulong Rtt;
        }

        // Native close notifications can arrive after a reconnect. Bind callbacks to one session.
        private sealed class CallbackGate : IConnectionManager, ISocketManager
        {
            private readonly FacepunchTransport owner;
            private readonly int generation;
            private bool Current => generation == owner.generation;
            public CallbackGate(FacepunchTransport owner, int generation) { this.owner = owner; this.generation = generation; }
            public void OnConnecting(ConnectionInfo info) { if (Current) ((IConnectionManager)owner).OnConnecting(info); }
            public void OnConnected(ConnectionInfo info) { if (Current) ((IConnectionManager)owner).OnConnected(info); }
            public void OnDisconnected(ConnectionInfo info) { if (Current) ((IConnectionManager)owner).OnDisconnected(info); }
            public void OnMessage(IntPtr data, int size, long number, long time, int channel)
            { if (Current) ((IConnectionManager)owner).OnMessage(data, size, number, time, channel); }
            public void OnConnecting(Connection connection, ConnectionInfo info)
            { if (Current) ((ISocketManager)owner).OnConnecting(connection, info); else connection.Close(); }
            public void OnConnected(Connection connection, ConnectionInfo info)
            { if (Current) ((ISocketManager)owner).OnConnected(connection, info); }
            public void OnDisconnected(Connection connection, ConnectionInfo info)
            { if (Current) ((ISocketManager)owner).OnDisconnected(connection, info); }
            public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long number, long time, int channel)
            { if (Current) ((ISocketManager)owner).OnMessage(connection, identity, data, size, number, time, channel); }
        }

        private const byte DataFrame = 0, PingFrame = 1, PongFrame = 2;
        private const int MaxMessageBytes = 1024 * 1024;
        private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        private ConnectionManager client;
        private SocketManager server;
        private NetworkManager manager;
        private Peer host;
        private double nextPing;
        private int generation;
        public ulong targetSteamId;
        public Func<ulong, bool> AcceptPeer;
        public override ulong ServerClientId => 0;

        public override void Initialize(NetworkManager networkManager = null)
        {
            manager = networkManager;
            ++generation;
            peers.Clear();
            nextPing = 0;
        }

        public bool TryGetSteamId(ulong transportId, out ulong steamId)
        {
            if (peers.TryGetValue(transportId, out var peer))
            {
                steamId = peer.SteamId;
                return true;
            }
            steamId = 0;
            return false;
        }

        public override bool StartClient()
        {
            if (!SteamClient.IsValid || targetSteamId == 0) return false;
            client = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
            client.Interface = new CallbackGate(this, generation);
            host = new Peer { Connection = client.Connection, SteamId = targetSteamId };
            return true;
        }

        public override bool StartServer()
        {
            if (!SteamClient.IsValid) return false;
            server = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            server.Interface = new CallbackGate(this, generation);
            return true;
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            var peer = FindPeer(clientId);
            if (peer == null || data.Array == null) return;
            var packet = new byte[data.Count + 1];
            packet[0] = DataFrame;
            Buffer.BlockCopy(data.Array, data.Offset, packet, 1, data.Count);
            var reliable = delivery != NetworkDelivery.Unreliable && delivery != NetworkDelivery.UnreliableSequenced;
            peer.Connection.SendMessage(packet, reliable ? SendType.Reliable : SendType.Unreliable);
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            if (manager != null && manager.IsHost && clientId == NetworkManager.ServerClientId) return 0;
            var transportId = manager == null ? clientId : manager.GetTransportIdFromClientId(clientId);
            return FindPeer(transportId)?.Rtt ?? 0;
        }

        private Peer FindPeer(ulong transportId) => transportId == ServerClientId ? host :
            peers.TryGetValue(transportId, out var peer) ? peer : null;

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            client?.Receive();
            server?.Receive();
            if (Time.realtimeSinceStartupAsDouble >= nextPing)
            {
                nextPing = Time.realtimeSinceStartupAsDouble + 1;
                if (host != null) SendPing(host);
                foreach (var peer in peers.Values) SendPing(peer);
            }
            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        private static void SendPing(Peer peer)
        {
            peer.LastPing = Time.realtimeSinceStartupAsDouble;
            var packet = new byte[9];
            packet[0] = PingFrame;
            Buffer.BlockCopy(BitConverter.GetBytes(peer.LastPing), 0, packet, 1, 8);
            peer.Connection.SendMessage(packet, SendType.Unreliable);
        }

        private void Receive(ulong transportId, IntPtr data, int size)
        {
            if (size < 1 || size > MaxMessageBytes) return;
            // NGO may retain messages until a later update; never overwrite a shared receive buffer.
            var packet = new byte[size];
            Marshal.Copy(data, packet, 0, size);
            var peer = FindPeer(transportId);
            if (peer == null) return;
            if (packet[0] == DataFrame)
                InvokeOnTransportEvent(NetworkEvent.Data, transportId, new ArraySegment<byte>(packet, 1, size - 1), Time.realtimeSinceStartup);
            else if (size == 9 && packet[0] == PingFrame)
            {
                packet[0] = PongFrame;
                peer.Connection.SendMessage(packet, SendType.Unreliable);
            }
            else if (size == 9 && packet[0] == PongFrame && BitConverter.ToDouble(packet, 1) == peer.LastPing)
                peer.Rtt = (ulong)Math.Max(0, (Time.realtimeSinceStartupAsDouble - peer.LastPing) * 1000);
        }

        public override void DisconnectLocalClient()
        {
            client?.Connection.Flush();
            client?.Connection.Close(true);
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!peers.TryGetValue(clientId, out var peer)) return;
            peer.Connection.Flush();
            peer.Connection.Close(true);
            peers.Remove(clientId);
        }

        public override void Shutdown()
        {
            ++generation;
            // Keep Steam alive in the menu for invitations; its owner handles application shutdown.
            client?.Close();
            server?.Close();
            client = null;
            server = null;
            host = null;
            peers.Clear();
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info) { }
        void IConnectionManager.OnConnected(ConnectionInfo info) =>
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);
        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            host = null;
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);
        }
        void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) =>
            Receive(ServerClientId, data, size);
        void ISocketManager.OnConnecting(Connection connection, ConnectionInfo info)
        {
            if (AcceptPeer != null && AcceptPeer(info.Identity.SteamId)) connection.Accept();
            else connection.Close();
        }
        void ISocketManager.OnConnected(Connection connection, ConnectionInfo info)
        {
            if (peers.ContainsKey(connection.Id)) return;
            peers.Add(connection.Id, new Peer { Connection = connection, SteamId = info.Identity.SteamId });
            InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);
        }
        void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info)
        {
            if (peers.Remove(connection.Id))
                InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);
        }
        void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel) =>
            Receive(connection.Id, data, size);
    }
}
#endif

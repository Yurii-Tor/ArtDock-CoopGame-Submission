using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Coop.Session
{
    public enum SessionPhase : byte
    {
        Lobby, Loading, Playing
    }

    public struct PlayerRecord : INetworkSerializable, IEquatable<PlayerRecord>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerId;
        public FixedString64Bytes Name;
        public bool Ready;
        public uint Rtt;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref Rtt);
        }

        public bool Equals(PlayerRecord other) => ClientId == other.ClientId && PlayerId.Equals(other.PlayerId) &&
            Name.Equals(other.Name) && Ready == other.Ready && Rtt == other.Rtt;
    }

    public sealed class SessionState : NetworkBehaviour
    {
        public static event Action<SessionState> Spawned;
        public readonly NetworkList<PlayerRecord> Players = new NetworkList<PlayerRecord>();
        public readonly NetworkVariable<SessionPhase> Phase = new NetworkVariable<SessionPhase>(SessionPhase.Lobby);
        public readonly NetworkVariable<double> PlayAt = new NetworkVariable<double>();
        public readonly NetworkVariable<int> PlayerLimit = new NetworkVariable<int>(ApprovalPolicy.MaxPlayers);

        public override void OnNetworkSpawn()
        {
            DontDestroyOnLoad(gameObject);
            Spawned?.Invoke(this);
        }

        public int Find(ulong clientId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].ClientId == clientId)
                    return i;
            return -1;
        }

        public bool IsReady(ulong clientId)
        {
            int index = Find(clientId);
            return index >= 0 && Players[index].Ready;
        }
    }
}

using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Coop.Infrastructure
{
    public sealed class LocalPlayerIdentity : IPlayerIdentity
    {
        public string PlayerId { get; }
        public string DisplayName => string.Empty;

        public LocalPlayerIdentity(string profile)
        {
            string key = "Coop.PlayerId." + profile;
            PlayerId = PlayerPrefs.GetString(key, string.Empty);
            if (!Guid.TryParse(PlayerId, out _))
            {
                PlayerId = Guid.NewGuid().ToString("D");
                PlayerPrefs.SetString(key, PlayerId);
                PlayerPrefs.Save();
            }
        }
    }

    public sealed class UtpConnectionTransport : IConnectionTransport
    {
        private readonly NetworkManager manager;
        private readonly UnityTransport transport;

        public UtpConnectionTransport(NetworkManager manager, UnityTransport transport)
        {
            this.manager = manager;
            this.transport = transport;
        }

        public Task PrepareAsync(bool isHost, string address, ushort port, int maxPlayers)
        {
            if (port == 0) throw new ArgumentException("Port must be between 1 and 65535.");
            manager.NetworkConfig.NetworkTransport = transport;
            transport.SetConnectionData(address, port, isHost ? "0.0.0.0" : null);
            return Task.CompletedTask;
        }

        public ulong GetRtt(ulong clientId) => transport.GetCurrentRtt(clientId);
        // A saved GUID distinguishes profiles; it is deliberately not authentication.
        public bool ValidateIdentity(ulong clientId, string playerId) => Guid.TryParse(playerId, out _);
        public void Tick() { }
        public void SetSessionState(bool inGame, int players, int maxPlayers) { }
        public void Shutdown() { }
    }

    public sealed class LoggingGameStats : IGameStats
    {
        private int pendingThrows;
        private int totalThrows;
        public void RecordThrow()
        {
            pendingThrows++;
            totalThrows++;
            Debug.Log($"[Stats] Confirmed throw. Total={totalThrows}");
        }

        public void Flush()
        {
            if (pendingThrows == 0) return;
            Debug.Log($"[Stats] StoreStats stub: throws={totalThrows}, pending={pendingThrows}");
            pendingThrows = 0;
        }
    }
}

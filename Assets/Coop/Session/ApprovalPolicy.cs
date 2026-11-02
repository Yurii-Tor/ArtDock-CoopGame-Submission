using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Session
{
    [Serializable]
    public sealed class JoinPayload
    {
        public string version = string.Empty;
        public string playerId = string.Empty;
        public string name = string.Empty;
    }

    // Pure policy. Reservations are included so simultaneous approvals cannot overbook.
    public static class ApprovalPolicy
    {
        public const string BuildVersion = "1.0.0";
        public const int MaxPlayers = 4;

        public static string RejectReason(JoinPayload payload, int reservedCount, ICollection<string> playerIds, int capacity = MaxPlayers)
        {
            if (capacity < 2 || capacity > MaxPlayers)
                return "Invalid session capacity. Choose 2 to 4 players.";
            if (reservedCount < 0)
                return "Invalid session player count.";
            if (payload == null || string.IsNullOrWhiteSpace(payload.playerId) || payload.playerId.Length > 64 ||
                string.IsNullOrWhiteSpace(payload.name) || payload.name.Length > 24)
                return "Invalid player profile.";
            if (Encoding.UTF8.GetByteCount(payload.playerId) > 60 || Encoding.UTF8.GetByteCount(payload.name) > 60)
                return "Player profile is too long.";
            if (payload.version != BuildVersion)
                return "Different build version. Use version " + BuildVersion + ".";
            if (playerIds.Contains(payload.playerId))
                return "This player is already in the session. Use a different --profile for local testing.";
            if (reservedCount >= capacity)
                return $"Session is full ({capacity} players).";
            return string.Empty;
        }
    }
}

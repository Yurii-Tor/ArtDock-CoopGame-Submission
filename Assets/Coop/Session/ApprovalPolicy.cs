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

        public static string RejectReason(JoinPayload payload, int reservedCount, ICollection<string> playerIds)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.playerId) || payload.playerId.Length > 64 ||
                string.IsNullOrWhiteSpace(payload.name) || payload.name.Length > 24)
                return "Invalid player profile.";
            if (Encoding.UTF8.GetByteCount(payload.playerId) > 60 || Encoding.UTF8.GetByteCount(payload.name) > 60)
                return "Player profile is too long.";
            if (payload.version != BuildVersion)
                return "Different build version. Use version " + BuildVersion + ".";
            if (playerIds.Contains(payload.playerId))
                return "This player is already in the session. Use a different --profile for local testing.";
            if (reservedCount >= MaxPlayers)
                return "Session is full (4 players).";
            return string.Empty;
        }
    }
}

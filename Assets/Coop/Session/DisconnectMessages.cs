using System;

namespace Coop.Session
{
    public static class DisconnectMessages
    {
        // Preserve our actionable application reasons; NGO diagnostic shutdown
        // dumps belong in logs, not in the menu shown to a player.
        public static string ForUser(string reason, bool wasConnecting)
        {
            string[] prefixes = { "Invalid player", "Player profile", "Different build", "This player", "Session is full",
                "You were kicked", "Scene loading timed out", "Connection synchronization timed out" };
            if (!string.IsNullOrEmpty(reason) && reason.Length <= 240)
                foreach (string prefix in prefixes)
                    if (reason.StartsWith(prefix, StringComparison.Ordinal))
                        return reason;
            return wasConnecting ? "Connection timed out. Check host address, port and firewall." :
                "Connection lost or host left the session.";
        }
    }
}

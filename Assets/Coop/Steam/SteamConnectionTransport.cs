#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Coop.Infrastructure;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace Coop.Steam
{
    public sealed class SteamSessionOptions
    {
        public string LobbyName = "Coop Workshop";
        public bool FriendsOnly = true;
    }

    /// <summary>All Steam lobby, identity and relay decisions live outside gameplay and session policy.</summary>
    public sealed class SteamConnectionTransport : IConnectionTransport, IPlayerIdentity, IDisposable
    {
        private const uint AppId = 480;
        private const string GameKey = "artdock-coop-workshop-v1";
        private readonly NetworkManager manager;
        private readonly SteamSessionOptions options;
        private readonly FacepunchTransport transport;
        private Lobby? lobby;
        private ulong originalHost;
        private bool initialized;
        private bool isHost;
        private int generation;
        private string lastPresence;

        public event Action<string> InviteJoinRequested;
        public event Action<string> SessionEnded;
        public string LobbyId => lobby.HasValue ? lobby.Value.Id.ToString() : "";
        public string PlayerId => initialized ? SteamClient.SteamId.ToString() : "";
        public string DisplayName => initialized ? SteamClient.Name : "";

        public SteamConnectionTransport(NetworkManager manager, SteamSessionOptions options)
        {
            this.manager = manager;
            this.options = options;
            transport = manager.GetComponent<FacepunchTransport>() ?? manager.gameObject.AddComponent<FacepunchTransport>();
        }

        public bool TryInitialize(out string error)
        {
            error = "";
            if (initialized)
                return true;
            try
            {
                SteamClient.Init(AppId, false);
                if (!SteamClient.IsValid || !SteamClient.IsLoggedOn)
                    throw new InvalidOperationException("Steam must be running and signed in.");
                SteamNetworkingUtils.InitRelayNetworkAccess();
                SteamNetworkingUtils.ConnectionTimeout = 15;
                SteamNetworkingUtils.Timeout = 15;
                SteamFriends.OnGameLobbyJoinRequested += OnInvite;
                SteamFriends.OnGameRichPresenceJoinRequested += OnPresenceJoin;
                SteamMatchmaking.OnLobbyDataChanged += OnLobbyChanged;
                SteamMatchmaking.OnLobbyMemberLeave += OnMemberLeft;
                SteamMatchmaking.OnLobbyMemberDisconnected += OnMemberLeft;
                initialized = true;
                SteamFriends.SetRichPresence("status", "In menu");
                return true;
            }
            catch (Exception exception)
            {
                if (SteamClient.IsValid)
                    SteamClient.Shutdown();
                error = "Steam unavailable. Start Steam and sign in, or restart with --transport utp. " + exception.Message;
                return false;
            }
        }

        public async Task PrepareAsync(bool hosting, string address, ushort port, int maxPlayers)
        {
            if (!TryInitialize(out var error))
                throw new InvalidOperationException(error);
            Shutdown();
            var operation = ++generation;
            isHost = hosting;
            Task<Lobby?> pending;
            if (hosting)
                pending = SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            else
            {
                if (!ulong.TryParse(address, out var lobbyId) || lobbyId == 0)
                    throw new InvalidOperationException("Enter the numeric Steam lobby ID.");
                pending = SteamMatchmaking.JoinLobbyAsync(lobbyId);
            }
            var completed = await Task.WhenAny(pending, Task.Delay(15000));
            if (completed != pending)
            {
                // Steam cannot cancel these operations; leave a lobby if its reply arrives after timeout.
                LeaveLateLobby(pending);
                throw new TimeoutException("Steam lobby request timed out.");
            }
            var result = await pending;
            if (operation != generation)
            {
                result?.Leave();
                throw new OperationCanceledException("Steam connection cancelled.");
            }
            if (!result.HasValue)
                throw new InvalidOperationException("Steam could not join or create this lobby.");
            lobby = result;
            var current = result.Value;
            if (hosting)
            {
                originalHost = SteamClient.SteamId;
                current.SetJoinable(false);
                current.MaxMembers = maxPlayers;
                if (options.FriendsOnly)
                    current.SetFriendsOnly();
                else
                    current.SetPrivate();
                current.SetData("game", GameKey);
                current.SetData("name", options.LobbyName);
                current.SetData("host", PlayerId);
                current.SetData("closed", "0");
                current.SetData("players", "1");
            }
            else
            {
                if (current.GetData("game") != GameKey || current.GetData("closed") == "1" ||
                    !ulong.TryParse(current.GetData("host"), out originalHost) ||
                    current.Owner.Id.Value != originalHost)
                {
                    Shutdown();
                    throw new InvalidOperationException("This lobby is closed or belongs to another game.");
                }
                transport.targetSteamId = originalHost;
            }
            // Only members of the chosen lobby may enter this private/friends session.
            transport.AcceptPeer = steamId => lobby.HasValue && lobby.Value.Members.Any(member => member.Id.Value == steamId);
            manager.NetworkConfig.NetworkTransport = transport;
            lastPresence = null;
        }

        private static async void LeaveLateLobby(Task<Lobby?> pending)
        {
            try
            {
                (await pending)?.Leave();
            }
            catch (Exception exception) { Debug.LogWarning("Late Steam lobby request failed: " + exception.Message); }
        }

        public void Tick()
        {
            if (initialized && SteamClient.IsValid)
                SteamClient.RunCallbacks();
        }

        public bool ValidateIdentity(ulong clientId, string playerId)
        {
            if (!ulong.TryParse(playerId, out var claimedId))
                return false;
            if (clientId == NetworkManager.ServerClientId)
                return claimedId == SteamClient.SteamId.Value;
            return transport.TryGetSteamId(manager.GetTransportIdFromClientId(clientId), out var actualId) && actualId == claimedId;
        }

        public ulong GetRtt(ulong clientId) => transport.GetCurrentRtt(clientId);

        public void SetSessionState(bool inGame, int players, int maxPlayers)
        {
            if (!initialized || !lobby.HasValue)
                return;
            var status = inGame ? $"In game {players}/{maxPlayers}" : $"In lobby {players}/{maxPlayers}";
            if (status == lastPresence)
                return;
            lastPresence = status;
            SteamFriends.SetRichPresence("status", status);
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + LobbyId);
            SteamFriends.SetRichPresence("steam_player_group", LobbyId);
            SteamFriends.SetRichPresence("steam_player_group_size", players.ToString(CultureInfo.InvariantCulture));
            if (isHost)
            {
                var current = lobby.Value;
                current.SetData("players", players.ToString(CultureInfo.InvariantCulture));
                current.SetData("state", inGame ? "game" : "lobby");
                current.SetJoinable(players < maxPlayers);
            }
        }

        public void OpenInviteOverlay()
        {
            if (initialized && lobby.HasValue)
                SteamFriends.OpenGameInviteOverlay(lobby.Value.Id);
        }

        private void OnInvite(Lobby invited, SteamId inviter) => InviteJoinRequested?.Invoke(invited.Id.ToString());
        private void OnPresenceJoin(Friend friend, string connect)
        {
            const string prefix = "+connect_lobby ";
            if (connect.StartsWith(prefix, StringComparison.Ordinal))
                InviteJoinRequested?.Invoke(connect.Substring(prefix.Length).Trim());
        }

        private void OnMemberLeft(Lobby changed, Friend member)
        {
            if (lobby.HasValue && changed.Id == lobby.Value.Id && member.Id.Value == originalHost)
                OnHostLost();
        }

        private void OnLobbyChanged(Lobby changed)
        {
            if (isHost || !lobby.HasValue || changed.Id != lobby.Value.Id)
                return;
            if (changed.GetData("closed") == "1" || changed.Owner.Id.Value != originalHost)
                OnHostLost();
        }

        private void OnHostLost()
        {
            if (!lobby.HasValue || isHost)
                return;
            Shutdown();
            SessionEnded?.Invoke("The Steam lobby host left. Host migration is not supported.");
        }

        public void Shutdown()
        {
            ++generation;
            if (initialized && lobby.HasValue)
            {
                var current = lobby.Value;
                // Steam transfers lobby ownership automatically; close it even if we became owner after a crash.
                if (current.IsOwnedBy(SteamClient.SteamId))
                {
                    current.SetJoinable(false);
                    current.SetData("closed", "1");
                }
                current.Leave();
            }
            lobby = null;
            originalHost = 0;
            lastPresence = null;
            if (initialized)
                SteamFriends.ClearRichPresence();
        }

        public void Dispose()
        {
            Shutdown();
            if (!initialized)
                return;
            SteamFriends.OnGameLobbyJoinRequested -= OnInvite;
            SteamFriends.OnGameRichPresenceJoinRequested -= OnPresenceJoin;
            SteamMatchmaking.OnLobbyDataChanged -= OnLobbyChanged;
            SteamMatchmaking.OnLobbyMemberLeave -= OnMemberLeft;
            SteamMatchmaking.OnLobbyMemberDisconnected -= OnMemberLeft;
            SteamClient.Shutdown();
            initialized = false;
        }
    }
}
#else
using System;
using System.Threading.Tasks;
using Coop.Infrastructure;
using Unity.Netcode;
using UnityEngine;

namespace Coop.Steam
{
    public sealed class SteamSessionOptions
    {
        public string LobbyName = "Coop Workshop";
        public bool FriendsOnly = true;
    }

    /// <summary>Fallback for platforms without the bundled Windows/macOS Steam bindings.</summary>
    public sealed class SteamConnectionTransport : IConnectionTransport, IPlayerIdentity, IDisposable
    {
        const string Unavailable = "Steam transport is available in the Windows and macOS builds. Restart without --transport steam to use IP/port (UTP).";

        public SteamConnectionTransport(NetworkManager manager, SteamSessionOptions options) { }
        public event Action<string> InviteJoinRequested { add { } remove { } }
        public event Action<string> SessionEnded { add { } remove { } }
        public string LobbyId => "";
        public string PlayerId => "";
        public string DisplayName => "";

        public bool TryInitialize(out string error)
        {
            error = Unavailable;
            return false;
        }

        public Task PrepareAsync(bool isHost, string address, ushort port, int maxPlayers) =>
            Task.FromException(new PlatformNotSupportedException(Unavailable));
        public bool ValidateIdentity(ulong clientId, string playerId) => false;
        public ulong GetRtt(ulong clientId) => 0;
        public void OpenInviteOverlay() => Debug.LogWarning(Unavailable);
        public void Tick() { }
        public void SetSessionState(bool inGame, int players, int maxPlayers) { }
        public void Shutdown() { }
        public void Dispose() { }
    }
}
#endif

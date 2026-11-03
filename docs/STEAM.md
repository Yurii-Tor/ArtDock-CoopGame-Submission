# Steam transport

Steam is optional and bundled for Windows x64 and macOS Universal (Intel and Apple Silicon). The default Unity Transport path supports multiple copies on one PC without Steam. Steam mode needs two different signed-in Steam accounts, internet access and matching game builds. A Windows PC and a Mac can use the same lobby across different networks. A separate Windows VM with its own Steam installation is an unverified alternative; two game windows in one ordinary Steam session do not create two Steam identities. See [environment choices](MANUAL-TESTING.md#two-steam-accounts).

## Launch and join

1. Start Steam and sign in on both PCs. Add the accounts as friends for the friends-only lobby test.
2. Extract the entire build folder on both PCs. Keep `steam_appid.txt` containing `480` next to the executable, and keep the build's native plugin files. This test uses Valve's Spacewar AppID as requested by the assignment; it does not require a production Steam app.
3. Run `Steam.cmd` on Windows or `Steam.command` on Mac; both select `--transport steam`. Choose Host. The default lobby is friends-only, named Dock Co-op, and limited to four members. Use `--lobby-name "Dock demo"` to change the name, `--max-players 2` (2–4 supported) to set capacity, or `--private` for an invitation-only lobby.
4. Choose **Copy lobby ID** in the menu. On the second PC paste that ID in the address/lobby field and choose Join. IP addresses and port forwarding are not used in Steam mode.
5. Alternatively choose **Invite Steam friends** after joining or hosting a lobby, where Steam Overlay is available, or use Steam's Join Game action. The adapter handles lobby and rich-presence join callbacks; startup `+connect_lobby <id>` is also accepted. Leave the current session before accepting another session's invitation.
6. Start the game on the host. Repeat late join, grab/throw, client exit, host exit, and host process termination. Repeat with a private lobby and an invited friend.

If Steam initialization fails, the game presents a readable message. Start and sign in to Steam, then retry, or relaunch with `--transport utp`. No application restart through Steam is forced during development.

### Invitation button and overlay availability

**Invite Steam friends** opens Steam's invitation dialog for the current lobby; it does not send invitations automatically. Both invitation buttons are disabled until connected to a lobby. If Steam reports that its overlay is unavailable, a persistent message explains the lobby-ID alternative. **Copy lobby ID** copies only the numeric ID and works without the overlay. The message clears when the lobby changes. Copying an ID does not grant access to a private, invitation-only lobby.

The direct launchers select Steam networking but do not guarantee Steam Overlay activation. For an overlay check, add `DockCoop.exe` (Windows) or `DockCoop.app` (Mac) through **Steam → Games → Add a Non-Steam Game to My Library**, set its launch options to `--transport steam`, enable Steam Overlay, and launch it from the library. After a few seconds, check **Shift+Tab**, then Host and Invite. Valve also documents additional macOS signing entitlements; this development build does not configure them, so launching from the library alone is not a verified Mac fix. See [Valve's overlay requirements](https://partner.steamgames.com/doc/features/overlay), [non-Steam shortcuts](https://help.steampowered.com/en/faqs/view/4B8B-9697-2338-40EC) and [macOS requirements](https://partner.steamgames.com/doc/store/application/platforms#macos).

The availability check runs on each click because Steam may take time to initialize the overlay. A successful API call means the dialog was requested, not that an invitation was sent or accepted. Lobby-ID joining remains the manually verified connection path.

## Design and authority

`CompositionRoot` selects `SteamConnectionTransport`, which implements the same connection and identity interfaces as UTP. Gameplay does not reference Steam. Preparation creates/joins a lobby before NGO starts, selects its original owner's SteamID, and configures the relay transport. The adapter pumps Steam callbacks even while NGO is stopped, so lobby operations and menu invitations can complete.

The forked community transport calls Facepunch's `CreateRelaySocket` / `ConnectRelay`: these use Steam Networking Sockets, allowing Steam to select direct connectivity or Steam Datagram Relay. Incoming connections must belong to the current lobby. Approval compares the supplied PlayerId with the authenticated transport peer's SteamID; UTP's local GUID has no equivalent authentication guarantee.

Steam's initial and established-connection timeouts are configured as 15000 milliseconds (15 seconds). The adapter logs the values read back from the native API at initialization and the prepared lobby ID, so connection diagnostics distinguish lobby preparation from the subsequent NGO connection.

Lobby metadata has a project-specific game key to avoid joining unrelated Spacewar lobbies. The host publishes name, original host, player count and game/lobby state. A full session is not joinable. Late join remains available while the game is running. Graceful host exit marks the lobby closed; loss of the original owner causes all clients to leave even if Steam transfers lobby ownership. There is no game host migration.

NGO packet data is copied into a separate managed array per received message, avoiding shared-buffer overwrites when NGO defers processing. The local fork adds a one-byte message type and a nine-byte ping/pong once per second for host-observed RTT. This is application RTT including callback scheduling, not a raw link-latency measurement. Session-scoped native callback gates ignore stale disconnects after reconnecting. The fork's wire framing requires matching builds on both sides.

## Presence and statistics

The adapter publishes `status` (In lobby / In game N/4), `connect`, `steam_player_group` and its size. A production app must configure its own localized `steam_display` tokens in Steamworks. The test does not own AppID 480's configuration; submitting these keys does not guarantee the desired localized status appears in Steam's friends UI.

The shared `IGameStats` implementation is deliberately a logging stub, allowed by the assignment. It records a throw only after host validation, on the actor's client. It batches the StoreStats-equivalent log when the session ends or the application quits. It does not write fake achievement names or alter Spacewar's real statistics.

## Verification boundary

The adapter uses matching Facepunch 2.5.2 managed/native dependencies for both platforms. On 3 November 2026, a tester confirmed two distinct Steam accounts joining the same lobby on Windows and macOS Ventura 13.2.1 across different networks, entering the game and exercising the controls. The host log also records remote-client item requests and ownership changes. The Mac was operated through RustDesk. Full evidence and remaining cases are in [VERIFICATION.md](VERIFICATION.md#manual-steam-session--3-november-2026).

This session does not identify whether Steam selected a direct or SDR route, and does not establish overlay invitations, private-lobby access or Join Game behavior. The baseline automated multi-client tests use UTP. Lobby-ID join was verified independently of overlay availability.

Sources: [pinned Unity community transport](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/0fab638470379ace12b0149dfb41c043d23dbce5/Transports/com.community.netcode.transport.facepunch), [Facepunch initialization](https://wiki.facepunch.com/steamworks/SteamClient.Init), [lobby creation](https://wiki.facepunch.com/steamworks/SteamMatchmaking.CreateLobbyAsync), [lobby join requests](https://wiki.facepunch.com/steamworks/SteamFriends.OnGameLobbyJoinRequested), [Valve matchmaking and lobbies](https://partner.steamgames.com/doc/features/multiplayer/matchmaking), [Valve rich presence](https://partner.steamgames.com/doc/features/enhancedrichpresence).

Third-party origins, local modifications and licenses are recorded in `Packages/com.community.netcode.transport.facepunch/UPSTREAM.md`.

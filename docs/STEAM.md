# Steam transport

Steam is optional and bundled for Windows x64 and macOS Universal (Intel and Apple Silicon). The default Unity Transport path supports multiple copies on one PC without Steam. Steam mode needs two different signed-in Steam accounts, internet access and matching game builds. A Windows PC and a Mac can use the same lobby across different networks. A separate Windows VM with its own Steam installation is an unverified alternative; two game windows in one ordinary Steam session do not create two Steam identities. See [environment choices](MANUAL-TESTING.md#two-steam-accounts).

## Launch and join

1. Start Steam and sign in with different accounts on both PCs. Add the accounts as friends. Extract the entire matching build on each PC; keep `steam_appid.txt` containing `480` and all native plugin files. This test uses Valve's Spacewar AppID as requested by the assignment.
2. On each PC choose **Steam → Games → Add a Non-Steam Game to My Library**. Add `DockCoop.exe` on Windows or `DockCoop.app` on Mac. In the shortcut's properties set **Launch Options** to `--transport steam` and **Start In** to the extracted folder containing `steam_appid.txt` and the `.exe` or `.app`.
3. On Mac, if Gatekeeper blocks the app, use its per-app **System Settings → Privacy & Security → Open Anyway** approval. Close the game after approving it, then launch it again from the Steam library.
4. Enable Steam Overlay in Steam's settings and the shortcut's properties, then choose **Play** in the Steam library on both PCs. Wait a few seconds and check **Shift+Tab** on the host. Keep the guest at the game's main menu.
5. On the host choose **Host session**, press **Shift+Tab**, right-click the friend and choose **Invite to Game**. The guest accepts the invitation while its game is running at the main menu; confirm both names in the lobby roster.
6. Choose **Start game** on the host. Check movement and item controls on both PCs. On the host, press **Tab/Esc** to open the player menu and choose **Kick** beside the guest's name.

The default lobby is friends-only, named Dock Co-op, and limited to four members. Use `--lobby-name "Dock demo"` to change its name, `--max-players 2` (2–4 supported) to set capacity, or `--private` for an invitation-only lobby. Leave the current session before accepting another session's invitation. The adapter also implements rich-presence join callbacks and startup `+connect_lobby <id>`; these are separate test cases.

### Lobby-ID alternative

`Steam.cmd` on Windows and `Steam.command` on Mac select Steam networking directly, but do not guarantee overlay activation. Use them when testing by lobby ID: host a session, choose **Copy lobby ID**, then paste it into the guest's **IP address / Steam lobby ID** field and choose **Join session**. No IP address or port forwarding is needed. Copying an ID does not grant access to a private, invitation-only lobby.

If Steam initialization fails, the game presents a readable message. Start and sign in to Steam, then retry, or relaunch with `--transport utp`. No application restart through Steam is forced during development.

### Invitation button and overlay availability

The in-game **Invite Steam friends** button opens Steam's separate invitation dialog for the current lobby; it is an alternative to the friends-menu route above and does not send invitations automatically. Invite and Copy are disabled until connected to a lobby. If Steam reports that its overlay is unavailable, a persistent message explains the lobby-ID alternative. **Copy lobby ID** copies only the numeric ID and works without the overlay. The message clears when the lobby changes.

Use the Steam-library launch above for invitation testing. See [Valve's overlay requirements](https://partner.steamgames.com/doc/features/overlay) and [non-Steam shortcuts](https://help.steampowered.com/en/faqs/view/4B8B-9697-2338-40EC). Valve also documents [macOS signing entitlements](https://partner.steamgames.com/doc/store/application/platforms#macos); this development build does not configure them, and Mac overlay activation has not been checked independently.

The availability check runs on each click because Steam may take time to initialize the overlay. A successful API call means the dialog was requested, not that an invitation was sent or accepted.

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

A follow-up with both Windows and Mac builds confirmed that launching Windows through the Steam library enabled Shift+Tab and the in-game invitation button. An invitation sent through Steam's friends menu connected the other player across platforms, and host Kick worked. This does not establish an independent Mac Shift+Tab/button check, private-lobby access, the separate Join Game action, a direct-versus-SDR route, or forced-crash recovery over Steam. Lobby-ID join was verified separately; the baseline automated multi-client tests use UTP.

Sources: [pinned Unity community transport](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/0fab638470379ace12b0149dfb41c043d23dbce5/Transports/com.community.netcode.transport.facepunch), [Facepunch initialization](https://wiki.facepunch.com/steamworks/SteamClient.Init), [lobby creation](https://wiki.facepunch.com/steamworks/SteamMatchmaking.CreateLobbyAsync), [lobby join requests](https://wiki.facepunch.com/steamworks/SteamFriends.OnGameLobbyJoinRequested), [Valve matchmaking and lobbies](https://partner.steamgames.com/doc/features/multiplayer/matchmaking), [Valve rich presence](https://partner.steamgames.com/doc/features/enhancedrichpresence).

Third-party origins, local modifications and licenses are recorded in `Packages/com.community.netcode.transport.facepunch/UPSTREAM.md`.

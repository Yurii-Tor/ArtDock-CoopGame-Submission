# Vendored Steam transport

Source: https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/0fab638470379ace12b0149dfb41c043d23dbce5/Transports/com.community.netcode.transport.facepunch

Only the permitted transport runtime and Windows x64 dependencies are included. No multiplayer samples or templates are used.

The transport is MIT-licensed (LICENSE.md). Facepunch.Steamworks is MIT-licensed (FACEPUNCH-LICENSE.txt). The Steam native redistributable is Valve's Steamworks runtime, shipped unchanged from the upstream transport package; it is not covered by our code license. Steam and Steamworks are Valve trademarks.

Local transport changes:

- Steam initialization/callback pumping/shutdown belong to the platform adapter, so asynchronous lobby operations work before NGO starts and invites work after leaving.
- Each received payload has its own managed buffer; NGO can defer reading without a later Steam message overwriting it.
- Authenticated peer SteamIDs are exposed for approval and P2P connections require lobby membership.
- A one-byte frame tag distinguishes NGO payload from a 9-byte once-per-second ping/pong. RTT is measured on the host, not reported by clients. This framing requires both peers to use this fork.
- Windows x64 only; removed unavailable NetcodeInterop assembly reference and unsafe memory copies.
- Compile guards exclude the Windows-only Steam bindings in other editors and players; those platforms can use the game's separate UTP adapter.
- Native callback gates are scoped to one network session, preventing a late disconnect from an old connection from terminating a new one. Explicit disconnects linger to flush reliable refusal/kick reasons.

The relay socket APIs are Steam Networking Sockets (CreateRelaySocket / ConnectRelay), not the legacy SteamNetworking P2P API. Steam selects direct connectivity or SDR as appropriate.

Steam end-to-end validation requires two signed-in accounts on two PCs. Compilation and missing-Steam handling do not establish that the live Steam path works.

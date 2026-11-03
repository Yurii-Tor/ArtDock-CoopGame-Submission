# Vendored Steam transport

Source: https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/0fab638470379ace12b0149dfb41c043d23dbce5/Transports/com.community.netcode.transport.facepunch

Only the permitted transport runtime and Windows x64/macOS dependencies are included. No multiplayer samples or templates are used.

The transport is MIT-licensed (LICENSE.md). Facepunch.Steamworks is MIT-licensed (FACEPUNCH-LICENSE.txt). Managed wrappers and native redistributables now come together from the official [Facepunch.Steamworks 2.5.2 release](https://github.com/Facepunch/Facepunch.Steamworks/releases/tag/2.5.2), specifically its `Release/Unity` directory. The Steam native redistributables are Valve's Steamworks runtime, shipped unchanged from that release; they are not covered by our code license. Steam and Steamworks are Valve trademarks.

Local transport changes:

- Steam initialization/callback pumping/shutdown belong to the platform adapter, so asynchronous lobby operations work before NGO starts and invites work after leaving.
- Each received payload has its own managed buffer; NGO can defer reading without a later Steam message overwriting it.
- Authenticated peer SteamIDs are exposed for approval and P2P connections require lobby membership.
- A one-byte frame tag distinguishes NGO payload from a 9-byte once-per-second ping/pong. RTT is measured on the host, not reported by clients. This framing requires both peers to use this fork.
- Windows x64 uses `Facepunch.Steamworks.Win64.dll` and `steam_api64.dll`; macOS uses `Facepunch.Steamworks.Posix.dll` and universal `libsteam_api.dylib`. Platform import metadata prevents Windows binaries loading in the Mac editor/player or vice versa.
- The adapter and transport compile in Windows/macOS editors and players. Other platforms retain the explicit unavailable adapter and can use UTP. No new scripting define is required.
- Removed the unavailable NetcodeInterop assembly reference and unsafe memory copies.
- Native callback gates are scoped to one network session, preventing a late disconnect from an old connection from terminating a new one. Explicit disconnects linger to flush reliable refusal/kick reasons.

The relay socket APIs are Steam Networking Sockets (CreateRelaySocket / ConnectRelay), not the legacy SteamNetworking P2P API. Steam selects direct connectivity or SDR as appropriate.

Steam end-to-end validation requires two signed-in accounts on two PCs. Compilation and missing-Steam handling do not establish that the live Steam path works.

## macOS compatibility evidence

The official 2.5.2 native library has two Mach-O slices. Inspection of its actual load commands gives:

| Architecture | Minimum macOS | Load command |
| --- | --- | --- |
| Intel x86_64 (`0x01000007`) | 10.13.0 | `LC_VERSION_MIN_MACOSX` |
| Apple Silicon ARM64 (`0x0100000c`) | 11.0.0 | `LC_BUILD_VERSION`, platform macOS |

Both native library minimums are below macOS Ventura 13.2.1. The Unity player and Steam desktop client have their own requirements: [Valve's current support notice](https://help.steampowered.com/en/faqs/view/659D-A19E-018A-81A6) requires macOS 12 or newer after support for macOS 11 ended. This confirms binary/OS compatibility, not a completed live Mac session test.

The existing adapter and forked transport compile unchanged against the release's Posix managed API. Its wire framing and lobby metadata are the same on Windows and Mac. Test a matching pair of builds with Steam running on both machines and different accounts; a VPN is not required by the Steam relay transport.

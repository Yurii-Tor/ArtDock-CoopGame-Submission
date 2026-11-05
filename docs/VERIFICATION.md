# Verification record

## v0.1.1 release verification — 5 November 2026

Runnable artifacts are published with [v0.1.1](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/tag/v0.1.1): `DockCoop-Windows-v0.1.1.zip`, `DockCoop-macOS-v0.1.1.zip`, `DockCoop-Demo-v0.1.1.mp4` and `SHA256SUMS.txt`. The ZIPs include platform launchers, documentation, Steam AppID 480 and `BUILD-INFO.txt`; the latter records the exact source commit and Unity version. The macOS build is Universal, requires macOS 12+, and is not notarized. No Unity installation is needed to play.

This release fixes menu validation and feedback: Steam does not validate an unused UDP port, UTP still requires 1–65535, invalid input survives UI refresh, and unavailable Steam initialization is explained on the first menu. Historical r2/r3 results below are not relabelled as tests of the new binaries.

| Release check | Result / evidence |
|---|---|
| Policy tests | PASS, all 32; `dotnet run --project tools/tests/SessionPolicy.Tests.csproj` |
| Fresh Windows and Universal macOS candidate builds | PASS; `COOP_BUILD_OK 158554696`, `COOP_MAC_BUILD_OK 317957132`; no C# compiler errors or warnings |
| UTP approval | PASS; `Artifacts/approval-20261105-205945/` |
| Impaired UTP gameplay | PASS; `Artifacts/gameplay-20261105-210058/`, `GAMEPLAY_SUITE_PASS`; added ~150 ms RTT and 3% loss each direction, held-item late join, forcibly killed holder, fresh join and graceful host leave |
| Forced host-process crash: model and reusable menu state | PASS; `Artifacts/host-crash-20261105-205946/`, `HOST_CRASH_SUITE_PASS`; its own host PID is killed after the client reaches Playing; model and UI state report **Connection lost or host left the session.**, Host/Join are enabled and same-process rehost passes |
| Forced host-process crash: rendered feedback | PASS; inspected `Artifacts/host-crash-20261105-205946/frames-client/frame-00080.png`: the return menu shows the readable host-loss message and Host/Join controls. This is separate visual evidence from the controller log |
| Client disconnect during scene loading | PASS; `Artifacts/loading-disconnect-20261105-210027/`, `LOADING_DISCONNECT_SUITE_PASS`; a test-only gate holds one client's loading completion, the host remains Loading with 2/3 ready, then force-killing that client lets the two survivors reach Playing |
| Menu validation with Steam running | PASS; `Artifacts/ui-20261105-210134/`, `UI_SUITE_PASS`; UTP invalid-port feedback persists and corrected input hosts; Steam rejects a nonnumeric lobby ID and can host despite `--port garbage` |
| Startup with Steam closed | PASS; `Artifacts/ui-20261105-210235/`, `UI_SUITE_PASS`; UTP hosts normally without Steam, and Steam mode shows its unavailable message on the first menu |
| Rendered validation/startup messages | PASS; inspected `frames-steam-unavailable/frame-00007.png` in the closed-Steam run: full action message is readable and the port field is absent; `frames-utp-port/frame-00020.png` in the earlier UI run shows the invalid-port message |

These runs validate the release candidate before integration. The published ZIPs are rebuilt from the final clean `main` commit recorded in their `BUILD-INFO.txt`. Final archive/download checks and smoke results are recorded in the [release notes](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/tag/v0.1.1), so publication evidence can be added without changing the tagged source.

Reproduce the new probes with `tools/verify-host-crash.ps1 -Capture`, `tools/verify-loading-disconnect.ps1` and `tools/verify-ui.ps1 -SteamAvailable -Capture`. Run `tools/verify-ui.ps1 -Capture` with Steam closed for unavailable-Steam startup and normal UTP startup. The scripts use separate profiles, bounded timeouts and clean up only the processes they create. The loading gate is enabled only by the test flags; it is a real three-process disconnect/barrier test, not an observation of a naturally slow disk load.

### Additional tester reports recorded for this release

- **Forced host-process termination:** Yurii reports using PowerShell `Stop-Process -Id <hostPid> -Force`; the client returned to the lobby search/join screen. This is tester-reported. The transport, executable SHA and visible reason were not supplied. The numeric example `1234` is not a reusable PID. This report does not establish a known-build Steam crash test or the wording/visibility of its disconnect reason.
- **Remote Steam RTT:** two players roughly 1,000 km apart observed about 100 ms RTT in one separate manual Steam session. The transport uses application ping/pong, including callback scheduling/processing time. No selected direct/SDR route, jitter series, packet-loss measurement or bandwidth profile was captured.
- The earlier Tailscale attempt was an incomplete private-IP test for UTP: the selected package did not work on Ventura 13.2.1; Dock Co-op itself subsequently worked on that Mac through Steam.

### Remaining verification and delivery scope

- Direct UTP between two physical computers over LAN/public IP remains separate from the verified remote Steam path and local multi-process UTP tests.
- Steam private-lobby permissions, the separate **Join Game** action, an independent Mac Shift+Tab/button check, and forced host termination with a recorded Steam transport/build/reason remain unverified. AppID 480 does not permit configuring this project's localized Rich Presence display.
- A detailed per-control matrix for both roles, physical input directly at the Mac, subjective smoothness under varied connections, route diagnostics and bandwidth profiling remain incomplete. The reported Mac session used RustDesk.
- The available [demo video](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/download/v0.1.1/DockCoop-Demo-v0.1.1.mp4) is an automated UTP recording with host and late-client views, accelerated capture and no audio. It does not cover Steam account/lobby/invitation UI. A complete assignment video still needs a Steam setup/join segment with two accounts, visible player list/RTT and kick, plus clear views of the requested 2–3 participants, late join, grab/throw, client departure and host departure. The existing automated footage must be identified as scripted rather than a manual session.

**Effort:** the author's approximate estimate is **4 hours for implementation and review**, plus **2–3 hours for manual testing and network troubleshooting**, about **6–7 person-hours in total**. This is a self-reported estimate, not a calculation from commit timestamps. The historical 1.5-hour figure below covered only the initial implementation/debugging/automated-checking stage and is not added again to the total.

## Historical baseline — 2 November 2026

Baseline date: 2 November 2026. Windows x64, Unity 6000.3.21f1, NGO 2.7.0, embedded Unity Transport 2.7.4 with the documented failed-receive buffer fix. The baseline tests use separate real player processes on one Windows PC. Later platform updates and the actual two-computer Steam session are recorded in the dated sections below.

### Executed

| Check | Result / evidence |
|---|---|
| Windows development build | PASS; `Artifacts/build-delivery.log`, `COOP_BUILD_OK`, no C# compiler errors or warnings |
| Approval/loading/disconnect policy tests | 32 PASS; `dotnet run --project tools/tests/SessionPolicy.Tests.csproj` |
| Duplicate GUID, incompatible version, fifth player, unreachable host | PASS; `Artifacts/approval-20261102-171540/` |
| Configured two-player limit, duplicate/version/full/timeout reasons in final build | PASS; `Artifacts/approval-20261102-181102/` |
| Initial lobby and shared level start | PASS in separate host/client processes |
| Spawn, ownership transfer on grab, authoritative throw and return to host ownership | PASS on host and clients |
| Impaired connection | PASS with UDP proxy: 75 ms base delay each direction, +/-15 ms jitter, 3% packet loss each direction, seed480 |
| Late join while another player holds an item | PASS; late client3 observes item7 holder2/owner2, then independently grabs/throws item9 |
| Force-kill a holding client | PASS; host logs `Disconnect drop`, item survives and returns to owner0 |
| New connection after the crash | PASS; fresh client4 connects, grabs item11 as owner4, throws it back to owner0 |
| Host exit | PASS; survivor returns to menu with the readable host-loss message |
| Contested grab + forged deletion by loser | PASS from both observers; exactly one holder and ownership matches; `Artifacts/final-race-*.log` |
| Kick, same-profile rejoin, host loss, same-process rehost | PASS; `Artifacts/final-lifecycle-*.log`, including `REHOST_PASS` |
| Rendered menu/game/return-to-menu UI | Inspected actual game frames; demo includes owner movement, replicated colours and player/RTT list |

The final combined crash/latency/late-join run is `Artifacts/gameplay-20261102-181049/`; it ends with `GAMEPLAY_SUITE_PASS`. Earlier failing runs are retained locally for the debugging history. Logs and build outputs are ignored in Git; the source regression scripts are committed. [The debugging note](NETWORK-DEBUGGING.md) explains why the original transport failed and why increasing timeouts was not a fix.

Run the build with `--race` on one host and one client for the contested-item probe. Use `--lifecycle host` / `--lifecycle client` alongside `--host` / `--join` for the reconnect/rehost probe. Always give processes different `--profile` values and use an unused port.

### Historical delivery artifacts

- `Artifacts/DockCoop-Windows.zip`: complete runnable development build, launcher scripts, native Steam dependency and AppID480 file. The optional Burst debug-only directory is excluded.
- `Artifacts/DockCoop-Demo.mp4`: actual host and late-join render captures, encoded in two labelled segments. Automated actions, accelerated capture, no audio, no Steam footage. It is evidence of the scripted test, not a manual play-session recording.
- `Artifacts/SHA256SUMS.txt`: checksums for the archive and video.
- The v0.1.0 GitHub prerelease contains the original Windows delivery. Current downloads are linked in the v0.1.1 section above.

### Gaps at the baseline stage

- Direct UTP between two physical computers over LAN/public IP. The separate Internet Steam path was verified below.
- Private-lobby access, the separate Steam **Join Game** action, Steam-unavailable startup, forced process termination over Steam, and identifying the actual direct/SDR route. Windows overlay/button use, cross-platform **Invite to Game** joining and host kick were subsequently confirmed in the 4 November follow-up below. AppID480 does not permit configuring this project's localized Rich Presence display.
- A detailed per-control matrix for both host/client roles and physical input directly at the Mac. The tester reported exercising all controls, with the remote Mac operated through RustDesk; see the recorded scope below.
- Forced process termination precisely during scene loading. The disconnect-unblocks-barrier policy is tested; the tiny scene transition was not deterministically interrupted in the process tests.
- Bandwidth profiling and subjective movement smoothness under varied network conditions. The configured rates/thresholds are documented, not presented as measured bandwidth.

Approximate AI-assisted initial implementation, debugging and automated verification time: 1.5 hours. This historical partial figure excludes later platform work, manual checks and release preparation.

## macOS build follow-up — 3 November 2026

The original time estimate above covers the initial Windows implementation. The following platform and packaging work is additional.

| Check | Result / evidence |
|---|---|
| macOS Universal development build from Windows | PASS; `Artifacts/build-macos.log`, `COOP_MAC_BUILD_OK 316897441`; no C# errors or warnings |
| Native CPU slices | All six Mach-O binaries contain x86_64 and arm64; `Info.plist` minimum macOS version is 12.0 |
| ZIP and launch scripts | CRC PASS; native executables and `.command` launchers use Unix mode 0755; launchers use LF and pass `bash -n`; Windows Steam binaries and DoNotShip directories excluded |
| Windows rebuild after platform guards | PASS; `Artifacts/build.log`, `COOP_BUILD_OK 158439576`; no C# errors or warnings |
| Session policy checks | All 32 PASS again |
| Windows multiplayer regression | PASS; `Artifacts/gameplay-20261103-185210/`, `GAMEPLAY_SUITE_PASS`; impaired client, held-state late join, killed holder, fresh connection and host-loss handling |

At this initial build stage, the macOS application had not been executed on a Mac. Compilation and structural archive inspection alone did not establish cross-platform runtime behavior. That initial macOS build used UTP only; the subsequent Steam update and manual test are recorded below. The development app has no Developer ID notarization. Follow [MANUAL-TESTING.md](MANUAL-TESTING.md) and the archive's `START-HERE-macOS.txt` for reproducing the test.

The follow-up pair is packaged locally as `Artifacts/DockCoop-macOS.zip` and `Artifacts/DockCoop-Windows-CrossPlatform.zip`, with the source revision in each `BUILD-INFO.txt` and checksums in `Artifacts/CROSS-PLATFORM-SHA256SUMS.txt`. The Windows v0.1.0 release remains a separate recorded build.

## Cross-platform Steam update — 3 November 2026

Both platforms now use the matching Facepunch 2.5.2 wrappers and native Steam runtime. `Steam.cmd` (Windows) and `Steam.command` (macOS) select the same existing lobby/socket adapter and wire framing. The Mac launcher and its AppID 480 file are included in the build. vSync is disabled so rendering follows the configured 60 FPS focused / 30 FPS unfocused cap; NGO tick rate and physics timestep are unchanged.

| Check | Result / evidence |
|---|---|
| Windows build | PASS; `Artifacts/build.log`, `COOP_BUILD_OK 158537272`; no C# errors or warnings |
| Universal macOS build | PASS; `Artifacts/build-macos.log`, `COOP_MAC_BUILD_OK 317939700`; no C# errors or warnings |
| Mac Steam dependencies | Posix managed wrapper and universal `libsteam_api.dylib` included; native minimums are 10.13 Intel / 11.0 ARM64; Unity app minimum remains 12.0 |
| Real Steam host initialization, one account | PASS; Steam running on Windows, game launched with `-batchmode -nographics --transport steam --host`; `Artifacts/steam-host-validation.log` reaches `[Session] Connected.` after Steam lobby preparation and NGO host startup; test process stopped afterward |
| Windows multiplayer regression after FPS/dependency changes | PASS; `Artifacts/gameplay-20261103-203321/`, `GAMEPLAY_SUITE_PASS`; impaired connection, held-state late join, killed holder, fresh connection and host loss; processes run without graphics and are cleaned up by the script |

The one-account result alone did not establish a connection between two Steam users, cross-platform gameplay, invites or overlay behavior. The later manual session is recorded below. The FPS policy is configured in code; headless tests do not measure rendered frame rate or physical focus transitions.

The new matching archives are `Artifacts/DockCoop-Windows-Steam.zip` and `Artifacts/DockCoop-macOS-Steam.zip`; their launch instructions use Steam lobby IDs for Internet play. The earlier UTP archives and v0.1.0 release are separate artifacts.

## Steam timeout units correction — 3 November 2026

Facepunch's `ConnectionTimeout` and `Timeout` properties use milliseconds. The original adapter passed `15`, allowing only 15 ms instead of the intended 15 seconds. Both now use the named constant `SteamTimeoutMilliseconds = 15000`.

- Windows and Universal macOS builds passed without C# errors or warnings (`COOP_BUILD_OK 158537791`, `COOP_MAC_BUILD_OK 317940224`).
- The updated Windows player read back `initial=15000 ms, connected=15000 ms` from the native API, prepared a real Steam lobby and reached `[Session] Connected.`; see `Artifacts/steam-timeout-validation.log`. The test process was stopped afterward.
- The previous Mac player was launched and its Join action exercised. Its supplied log reported `Enter the numeric Steam lobby ID`, a validation rejection before Steam matchmaking. This does not demonstrate a transport timeout or a completed remote connection.

The corrected matching archives use the `Steam-r2` suffix. The native timeout readback is not evidence of a successful remote connection; that was subsequently checked in the manual session below.

## Manual Steam session — 3 November 2026

Executable source revision: `bb3f937c09246a409de4947755978fb6865ba0e8` (Steam r2). Two physical computers on different networks in different countries: Windows host and macOS Ventura 13.2.1 client, with distinct Steam accounts added as friends. The remote Mac was controlled through RustDesk. Tailscale was disconnected and the earlier Cloudflare connector stopped.

| Check | Result / evidence |
|---|---|
| Steam lobby-ID join | PASS, tester-reported: both players appeared in the same lobby |
| Shared game start | PASS, tester-reported: the session entered gameplay |
| Gameplay controls | PASS, tester-reported: all controls were exercised; this is not a separately recorded per-control/per-platform matrix |
| Client rejoin while host holds a cube | PASS, tester-reported: the returning client starts at the default spawn position and the host continues holding the cube; player position is not persisted across connections |
| Client leaves while holding a cube | PASS, tester-reported: the client avatar disappears and the cube falls; host logs corroborate `Disconnect drop` for clients6,8,9 with ownership returned to host0 |
| Host leaves the session | PASS, tester-reported: leaving as host ends the session for everyone; this was a graceful session leave, not a forced host-process termination test |
| Remote item interaction | Host log corroborates remote `sender=1` grab/release requests and item ownership changing to client1 and back to host0 |
| Held-item access check | Host log shows attempts to grab an item already held by client1 being rejected; this does not establish a simultaneous free-item race test |

Local evidence: `Artifacts/steam-manual-host-r2.log`. The record combines the tester's report with host-side logs; the assistant did not independently inspect both screens. It establishes actual cross-platform Steam gameplay, rejoin and graceful session-exit behavior. The direct/SDR route, overlay/invitation/private-lobby flows, simultaneous free-item race and forced process termination over Steam were not established by this manual session. Earlier automated UTP crash tests remain separate evidence.

## Steam invitation feedback — 4 November 2026

The invitation button now checks Steam and overlay availability and displays feedback in the session panel. Invite and Copy lobby ID are disabled before a connection is established. Copying a lobby ID works independently of the overlay; feedback resets on lobby changes. A dialog request is not reported as a sent invitation.

- Windows and Universal macOS builds passed with no C# errors or warnings: `COOP_BUILD_OK 158540291`, `COOP_MAC_BUILD_OK 317942720`.
- Source review covered both platform branches, UI refresh, lobby-change reset and panel layout. This is not a runtime visual check.
- The updated packages use the `Steam-r3` suffix. Earlier two-computer gameplay evidence above refers to r2. This update does not change the wire protocol or gameplay.
- At this build/review stage, opening the Steam invitation dialog and accepting an invitation had not yet been verified. The new availability check distinguishes an unavailable overlay from a dialog request; the subsequent manual result is recorded below.

To verify the UI, launch in Steam mode, confirm both buttons are disabled before Host/Join, then create a lobby. Copy its ID and paste it into a text field; it must match the displayed numeric ID. Click Invite: an unavailable overlay must produce a readable message that survives subsequent UI refreshes. With the overlay available, confirm the invitation dialog opens and a friend can accept it. Leave and create a new lobby to confirm the prior feedback clears. See [STEAM.md](STEAM.md#invitation-button-and-overlay-availability) for overlay launch requirements.

## Steam-library invitation and kick follow-up — 4 November 2026

The tester reported that Windows Shift+Tab and the invitation button did not open an overlay with the direct launcher. After adding the executable to Steam's library, setting `--transport steam` and launching with Steam's Play button, both worked. The same library setup was then applied to the Mac, with a per-app Gatekeeper exception for the unnotarized build.

| Check | Result / evidence |
|---|---|
| Windows overlay and in-game invitation button | PASS, tester-reported after launching from the Steam library |
| Steam invitation followed by cross-platform join | PASS, tester-reported: right-click friend in Steam's overlay/menu, choose **Invite to Game**, and the invited friend joins the session |
| Host kick in the Windows/Mac Steam session | PASS, tester-reported |

These results extend the earlier two-account Windows/Mac gameplay checks. They confirm an invitation-based join and kick, not just that an invitation API was called. The report does not separately establish opening Shift+Tab/the invitation button on Mac, the **Join Game** action, private-lobby permissions, the chosen direct/SDR route, or a forced Steam host-process crash.

The supplied r3 executables were built from `feca9e1deb183411c0f9db8597304501a050a451`. This historical follow-up updated instructions and evidence without rebuilding game code; those archives' `BUILD-INFO.txt` distinguishes executable source from documentation revision. Current release binaries have their own source SHA. Follow [README](../README.md#quick-start-steam-on-two-computers) or [STEAM.md](STEAM.md) to reproduce the tested library-launch and invitation flow.

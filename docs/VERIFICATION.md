# Verification record

Date: 2 November 2026. Windows x64, Unity 6000.3.21f1, NGO 2.7.0, embedded Unity Transport 2.7.4 with the documented failed-receive buffer fix. Tests use separate real player processes on one Windows PC. These are not claims of a two-PC or live Steam run.

## Executed

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

## Delivery artifacts

- `Artifacts/DockCoop-Windows.zip`: complete runnable development build, launcher scripts, native Steam dependency and AppID480 file. The optional Burst debug-only directory is excluded.
- `Artifacts/DockCoop-Demo.mp4`: actual host and late-join render captures, encoded in two labelled segments. Automated actions, accelerated capture, no audio, no Steam footage. It is evidence of the scripted test, not a manual play-session recording.
- `Artifacts/SHA256SUMS.txt`: checksums for the archive and video.
- GitHub prerelease assets provide the same files. The repository is private; reviewers need access.

## Remaining manual verification

- Two physical PCs over LAN/public IP, including actual firewall/NAT conditions.
- Two separate Steam accounts/PCs: real P2P/SDR path, friends/private lobby invitations, Overlay, Join Game and Steam-unavailable startup. Steam code and bundled dependencies compile; no live Steam-session success is claimed. AppID480 does not permit configuring this project's localized Rich Presence display.
- Manual keyboard/mouse and menu-button playthrough. Automated gameplay uses the normal RPC request methods; visual checks do not replace testing physical input. Desktop input automation was attempted but both available runtimes failed to initialize with `failed to write kernel assets: The system cannot find the path specified`.
- Forced process termination precisely during scene loading. The disconnect-unblocks-barrier policy is tested; the tiny scene transition was not deterministically interrupted in the process tests.
- Bandwidth profiling and subjective movement smoothness under varied network conditions. The configured rates/thresholds are documented, not presented as measured bandwidth.

Approximate AI-assisted implementation, debugging and automated verification time: 1.5 hours. The pending manual/two-PC/Steam checks are additional and not included.

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

The initial macOS application had not been executed on a Mac. Cross-platform sessions, physical input, Finder launch and Gatekeeper acceptance remain manual checks; compilation and structural archive inspection do not establish those results. That initial macOS build used UTP only; the subsequent Steam update is recorded below. The development app has no Developer ID notarization. Follow [MANUAL-TESTING.md](MANUAL-TESTING.md) and the archive's `START-HERE-macOS.txt` for the next run.

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

The one-account result does not establish a connection between two Steam users, cross-platform gameplay, invites or overlay behavior. Those checks remain pending. The FPS policy is configured in code; headless tests do not measure rendered frame rate or physical focus transitions.

The new matching archives are `Artifacts/DockCoop-Windows-Steam.zip` and `Artifacts/DockCoop-macOS-Steam.zip`; their launch instructions use Steam lobby IDs for Internet play. The earlier UTP archives and v0.1.0 release are separate artifacts.

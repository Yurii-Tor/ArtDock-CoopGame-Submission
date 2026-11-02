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

# Dock Co-op v0.1.1

Runnable Windows x64 and Universal macOS builds for the ArtDock assignment. Unity is not required. Extract a ZIP and follow its `START-HERE` file or the [README](../README.md).

## Downloads

- [Windows x64](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/download/v0.1.1/DockCoop-Windows-v0.1.1.zip)
- [macOS Universal, macOS 12+](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/download/v0.1.1/DockCoop-macOS-v0.1.1.zip)
- [Scripted UTP demonstration](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/download/v0.1.1/DockCoop-Demo-v0.1.1.mp4)
- [SHA-256 checksums](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/download/v0.1.1/SHA256SUMS.txt)

Each ZIP contains the executable/app, data, native Steam libraries, AppID 480, launchers, current documentation and `BUILD-INFO.txt` with the source SHA and Unity version. macOS binaries and shell launchers retain Unix executable permissions. The macOS development build is not Apple-notarized; follow the per-app approval instructions.

## What changed

- Steam Host/Join no longer validates an unused UDP port; the port field is hidden in Steam mode. UTP retains validation for 1–65535 with persistent feedback.
- A failed Steam initialization is explained on the first menu, with actions to start/sign in to Steam or restart in UTP.
- Reviewer instructions now start with local Windows launchers and then the verified two-account Steam library/invitation flow on Windows/macOS.
- A controlled UTP host-process-crash probe distinguishes forced termination from graceful host leave. See the verification record for executed checks, controller evidence and rendered UI observations.

## Run

For one Windows PC without Steam: `Host-Local.cmd`, `Client-1-Local.cmd`, optionally `Client-2-Local.cmd`, then **Start game** on the host. The launchers use different profiles at `127.0.0.1:7777`. On one Mac use `Host-LAN.command` and `Join-LAN.command`; Enter accepts the local address.

For two computers over Steam: sign in with different accounts, add them as friends, add the game to each Steam library with launch options `--transport steam`, and launch through Steam for the overlay. Host → **Invite to Game** (or **Copy lobby ID** / **Join session**) → **Start game**. No shared LAN, VPN or port forwarding is required.

## Evidence and remaining work

See [VERIFICATION.md](VERIFICATION.md) for release checks and historical tester-reported Windows/Mac Steam results. About 100 ms application RTT was observed between players roughly 1,000 km apart in one session; the selected route and bandwidth were not profiled.

The release candidate passed all 32 policy tests, fresh Windows/macOS builds, UTP approval and impaired gameplay suites, controlled host-crash recovery with rendered menu feedback, a three-process loading-disconnect test, and menu validation with Steam running or closed. Rendered frames confirm the startup error and invalid-port feedback are readable. Final published-build/download verification is recorded on the GitHub release page.

The video is a scripted, accelerated UTP recording with no audio or Steam footage. It is not the complete requested Steam demonstration. The verification record lists missing footage and unperformed checks. The author's approximate effort is 4 hours of implementation/review plus 2–3 hours of manual testing/network troubleshooting: about 6–7 person-hours overall.

v0.1.0 remains unchanged as a historical prerelease. Host migration, regional relays and new infrastructure are not part of this release.

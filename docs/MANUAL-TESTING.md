# Manual multiplayer verification

This is a test procedure, not a record of passed tests. Record the build revision, operating systems, transport, network arrangement, results and logs for each run. Automated results and remaining gaps are in [VERIFICATION.md](VERIFICATION.md).

Use the matching ZIPs from [v0.1.1](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission/releases/tag/v0.1.1); `BUILD-INFO.txt` records each build's source. Extract all files before launching.

## One computer without Steam

On Windows, run `Host-Local.cmd` and `Client-1-Local.cmd`, optionally `Client-2-Local.cmd`. On Mac, run `Host-LAN.command` and `Join-LAN.command`; press Enter at the address prompt to use `127.0.0.1`. The scripts use different profiles, with local UTP port 7777. Confirm the names in the lobby and click **Start game** on the host. Launch another client after the host enters gameplay to test late join. These ordinary launchers do not drive automated input.

## Choose the environment

| Environment | Useful evidence | What it does not establish |
|---|---|---|
| Two player processes on one PC, UTP | Input, menus, replicated gameplay, late join and lifecycle | A second OS, real LAN/firewall conditions or Steam identities |
| Host PC plus a Windows VM | Separate OS, virtual network, guest firewall and potentially a separate Steam client/account | Independent hardware, two home networks or reliable graphics/overlay behavior in every VM |
| Two physical PCs on one LAN | Actual hardware/input, network adapters, firewall and cross-platform behavior | Internet NAT traversal or that Steam selected an SDR route |
| Two PCs on different home networks | Internet connectivity, realistic latency and NAT conditions | The exact relay route unless separately observed in networking diagnostics |

Use the same source revision/protocol on both ends. Run ordinary builds without `--smoke`, `--race` or `--lifecycle` for manual input checks. Those flags drive automated actions.

## Two PCs on one LAN

1. Extract the full matching build on each computer. Connect both to the same normal home network; guest Wi-Fi may isolate devices.
2. On the Windows host, run `ipconfig` and find the IPv4 address of the active Wi-Fi/Ethernet adapter, for example `192.168.1.50`. Ignore disconnected adapters and unrelated VPNs.
3. Start the game, enter a name and port `7777`, then choose **Host session**. Allow this game/inbound UDP port through the host firewall for the intended network. Keep the firewall enabled.
4. On the other computer, start the game, enter a different name, the host's actual IPv4 address and port `7777`, then choose **Join session**. `127.0.0.1` always means the current computer and is only appropriate for same-PC copies.
5. Confirm both names in the lobby, then click **Start game** on the host. Run the checklist below, then swap host/client roles.

Windows PowerShell equivalents, from the extracted directory:

```powershell
.\DockCoop.exe --host --profile lan-host --name Host --port 7777 -logFile host.log
.\DockCoop.exe --join --profile lan-client --name Client --address 192.168.1.50 --port 7777 -logFile client.log
```

Run the second command on the other PC and replace the example address. For the native macOS build, use the same menu fields and UTP transport; the host can be either platform. `Host-LAN.command` hosts, and `Join-LAN.command` prompts for the host's address. Follow `START-HERE-macOS.txt` in its archive. For Steam Internet play and invitations, use the Steam-library procedure below.

For a third/fourth player, extra local copies with distinct `--profile` values are sufficient to test capacity. A VM cloned after a previous launch can inherit a saved local GUID; give the clone a new profile. Display names alone do not change identity.

## Internet and VM alternatives

The bundled Steam transport is the first option for two supported computers on different networks. Sign in with different Steam accounts, add them as friends, and follow the Steam-library invitation procedure below. Steam handles connectivity without configuring a VPN or host port forwarding. Lobby-ID joining is also available.

Direct UTP over the Internet requires reachable host UDP: forward external UDP `7777` to the host's LAN address/port and connect to the public address. Carrier-grade NAT may prevent this. A successful LAN run does not prove this path.

A Windows VM is a practical additional test environment if its graphics driver can run the player. Configure a bridged/external virtual network for straightforward LAN addresses, and connect the guest to the host's reachable IP, never the guest's `127.0.0.1`. Microsoft documents [Hyper-V virtual switches](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/get-started/create-a-virtual-switch-for-hyper-v-virtual-machines). NAT networking can also work for a guest connecting out to the host, but the correct host address/routing depends on the hypervisor. Virtual graphics, shared CPU/GPU and the shared network uplink make a VM an incomplete substitute for a physical-device test.

## Two Steam accounts

The normal Steam client accesses one account at a time on a computer; opening two game copies or changing `--profile` does not create another Steam identity. See [Valve's account-use FAQ](https://help.steampowered.com/en/faqs/view/71EA-CDCE-FB5C-82B3).

The simplest setup is two supported physical PCs, with one signed-in Steam account on each. A separate Windows guest with its own Steam installation/account is an alternative isolation arrangement to try; it has not been validated for this build. Steam, the game, graphics acceleration and overlay must all work inside that guest. Run the game and Steam under the same OS user/privilege context, as required for [Steam API initialization](https://partner.steamgames.com/doc/sdk/api).

### Reviewer invitation flow

1. Extract matching full builds. On each PC choose **Steam → Games → Add a Non-Steam Game to My Library** and add `DockCoop.exe` (Windows) or `DockCoop.app` (Mac). Set shortcut **Launch Options** to `--transport steam` and **Start In** to the folder containing `steam_appid.txt` and the `.exe` or `.app`.
2. On Mac, approve this app through **System Settings → Privacy & Security → Open Anyway** if Gatekeeper blocks it, close it, then restart it from Steam. Enable Steam Overlay in Steam's settings and the shortcut's properties. Use **Play** in the Steam library on both PCs.
3. Keep the guest at the game's main menu. The host chooses **Host session**, opens the overlay with **Shift+Tab**, right-clicks the friend and selects **Invite to Game**. Accept the invitation on the guest and confirm both names in the roster.
4. The host chooses **Start game**. Check controls on both machines using the checklist below. The host presses **Tab/Esc** and chooses **Kick** beside the guest's name; check that the guest returns to the menu.

The game's **Invite Steam friends** button opens Steam's separate invitation dialog, an alternative to the friends-menu route above. For a lobby-ID test, the direct `Steam.cmd` / `Steam.command` launchers are sufficient: host, **Copy lobby ID**, paste it into the guest's **IP address / Steam lobby ID** field, then **Join session**. These launchers do not guarantee overlay activation. See [Steam setup and diagnostics](STEAM.md).

Reported evidence covers Windows Shift+Tab and the invitation button after Steam-library launch, a Steam friends-menu invitation joining Windows and Mac players, and host Kick. The earlier cross-platform run also covered lobby-ID joining and gameplay controls. An independent Mac Shift+Tab/button check, private invited join, the separate Join Game action, and forced-crash recovery over Steam remain separate checks; do not infer them from these results.

Repeat late join, client exit and host loss as applicable. Test the Internet scenario from two different networks when possible. A successful Steam session alone does not identify whether Steam used a direct route or relay. One VM sharing the host's uplink does not recreate two independent home NATs.

A fresh limited Steam account can receive a friend request from the existing account, even when it cannot send requests itself. See [Valve's friends-list guidance](https://help.steampowered.com/en/faqs/view/1902-2D54-E445-59DD).

## Keyboard, mouse and session checklist

Repeat gameplay controls once as host and once as a remote client. Watch the other player's screen or use a screen recording to compare results. Keep both game processes running when switching focus.

| Check | Procedure and expected result |
|---|---|
| Menu input | In UTP, try ports `0`, `65536`, empty and nonnumeric text: each is rejected with a message that survives subsequent refreshes. Valid ports are 1–65535. In Steam, the UDP port is unused; Join requires a numeric lobby ID. Only the host can start. An unreachable host eventually returns a readable failure instead of hanging. |
| Steam unavailable | With Steam closed, launch `--transport steam`. The first menu must explain how to start/sign in to Steam or relaunch with `--transport utp`; wait through several refreshes. A normal UTP launch must work without Steam. |
| Scene start | Begin with both players in the lobby. Both enter the arena and become controllable; nobody remains indefinitely on the loading screen. |
| Movement and look | WASD moves in the viewing direction, mouse rotates the view, Space jumps from the floor. Check walls/steps, diagonal movement and looking up/down. The other screen shows smooth movement without persistent snapping. |
| Cursor and focus | Tab/Escape opens the player menu and releases the pointer. Close it to resume control. Alt-Tab away and back; movement must not remain stuck, clicks on UI must not fire gameplay actions, and the session must survive loss of focus. |
| Spawn and grab | Press F, aim at the resulting cube within reach, press E. Both screens see one held item following the holder. Rapid input must not create duplicates or allow a second held item. |
| Distance and obstruction | Try E on an item beyond reach or behind a wall. It must not become held. Move close with a clear view and retry. |
| Drop and throw | E drops a held item; right mouse throws it. Both screens agree on the resulting body after it settles. Re-grab from the other player. A short input cooldown can reject actions sent too rapidly. |
| Delete | Grab an item and press X. It disappears for everyone. X with nothing held does not delete a nearby/other player's item. |
| Competing grab | Both aim at the same free item and press E together. Only one gets it; the other cannot steal or delete it while held. Repeat several times. |
| Colour and RTT | Press C. The other screen sees the new avatar colour. Tab shows correct names/player count and updating RTT; host RTT can be zero. |
| Late join | Keep a cube held, change colour and add several dropped items. Join from another copy. The newcomer sees current players, colours, items and holder; existing players continue playing. |
| Client disconnect | A client holds a cube and leaves; repeat using Task Manager/Force Quit. The cube survives, drops and can be grabbed by a survivor. A new client can still join and interact. |
| Kick and rejoin | The host kicks a client from the player menu. That client sees a reason and can join again. The surviving session keeps working. |
| Host loss and restart | Host leaves; repeat by killing its process. Clients return to a usable menu with a reason. Start a new session and join again. Host migration is not expected. |
| Loading disconnect | Start with multiple clients in the lobby. Disconnect one while the initial scene is loading; remaining participants must start. Record whether this was an ordinary timing attempt or a deliberately gated harness run. Unit tests of `LoadingBarrier` are separate evidence. |
| Capacity | Set `--max-players 2`, fill both slots, then attempt a third connection. It must be rejected with a readable full-session message. Repeat with four slots if needed. |

For an impaired-network check, run the existing UDP proxy with 75 ms delay each direction, 15 ms jitter and 3% loss as documented in the README. Repeat movement, grabbing, throwing and disconnect handling. Distinguish input/visual quality observations from the already automated state checks.

For each failure, save the steps, expected/actual result, both player logs and a short recording if visual. Do not label any row PASS until someone performs it on the stated environment.

For a reproducible UTP host-crash probe, use `tools/verify-host-crash.ps1` from source. It starts its own processes, waits for the client to play, kills only its recorded host PID and checks recovery within a timeout. A model/controller log is not proof that a human can read the rendered message; inspect that separately and record the result.

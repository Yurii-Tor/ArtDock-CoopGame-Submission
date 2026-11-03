# Manual multiplayer verification

This is a test procedure, not a record of passed tests. Record the build revision, operating systems, transport, network arrangement, results and logs for each run. Automated results and remaining gaps are in [VERIFICATION.md](VERIFICATION.md).

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

Run the second command on the other PC and replace the example address. For the native macOS build, use the same menu fields and UTP transport; the host can be either platform. `Host-LAN.command` hosts, and `Join-LAN.command` prompts for the host's address. Follow `START-HERE-macOS.txt` in its archive. For Steam Internet play, run `Steam.cmd` on Windows and `Steam.command` on Mac, then use a lobby ID instead of an IP address.

For a third/fourth player, extra local copies with distinct `--profile` values are sufficient to test capacity. A VM cloned after a previous launch can inherit a saved local GUID; give the clone a new profile. Display names alone do not change identity.

## Internet and VM alternatives

The bundled Steam transport is the first option for two supported computers on different networks. Sign in with different Steam accounts, add them as friends, launch both Steam scripts and join the host's displayed lobby ID. Steam handles connectivity without configuring a VPN or host port forwarding. Verify this path with the Steam checklist below.

Direct UTP over the Internet requires reachable host UDP: forward external UDP `7777` to the host's LAN address/port and connect to the public address. Carrier-grade NAT may prevent this. A successful LAN run does not prove this path.

An optional alternative is a private VPN such as Tailscale: put both devices on an authorized private network, allow the game port in the relevant access policy/firewall, and join the host's Tailscale IPv4 address. The game still uses UTP. This tests gameplay over that VPN, not direct public UDP or Steam SDR. Tailscale documents [connecting devices by their assigned address](https://tailscale.com/docs/how-to/connect-to-devices); its [Personal plan](https://tailscale.com/pricing) currently has a free tier.

A Windows VM is a practical additional test environment if its graphics driver can run the player. Configure a bridged/external virtual network for straightforward LAN addresses, and connect the guest to the host's reachable IP, never the guest's `127.0.0.1`. Microsoft documents [Hyper-V virtual switches](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/get-started/create-a-virtual-switch-for-hyper-v-virtual-machines). NAT networking can also work for a guest connecting out to the host, but the correct host address/routing depends on the hypervisor. Virtual graphics, shared CPU/GPU and the shared network uplink make a VM an incomplete substitute for a physical-device test.

## Two Steam accounts

The normal Steam client accesses one account at a time on a computer; opening two game copies or changing `--profile` does not create another Steam identity. See [Valve's account-use FAQ](https://help.steampowered.com/en/faqs/view/71EA-CDCE-FB5C-82B3).

The simplest setup is two supported physical PCs, with one signed-in Steam account on each. A separate Windows guest with its own Steam installation/account is an alternative isolation arrangement to try; it has not been validated for this build. Steam, the game, graphics acceleration and overlay must all work inside that guest. Run the game and Steam under the same OS user/privilege context, as required for [Steam API initialization](https://partner.steamgames.com/doc/sdk/api).

Use the [Steam checklist](STEAM.md): lobby ID join, friends-only lobby, invitation overlay, private invited join, Join Game, late join, client exit and host loss. Test the Internet scenario from two different networks when possible. A successful Steam session alone does not identify whether Steam used a direct route or relay. One VM sharing the host's uplink does not recreate two independent home NATs.

A fresh limited Steam account can receive a friend request from the existing account, even when it cannot send requests itself. See [Valve's friends-list guidance](https://help.steampowered.com/en/faqs/view/1902-2D54-E445-59DD).

## Keyboard, mouse and session checklist

Repeat gameplay controls once as host and once as a remote client. Watch the other player's screen or use a screen recording to compare results. Keep both game processes running when switching focus.

| Check | Procedure and expected result |
|---|---|
| Menu input | Edit name/address/port, click Host/Join/Start. Only the host can start. Invalid port shows a readable message; an unreachable host eventually returns a readable failure instead of hanging. |
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
| Capacity | Set `--max-players 2`, fill both slots, then attempt a third connection. It must be rejected with a readable full-session message. Repeat with four slots if needed. |

For an impaired-network check, run the existing UDP proxy with 75 ms delay each direction, 15 ms jitter and 3% loss as documented in the README. Repeat movement, grabbing, throwing and disconnect handling. Distinguish input/visual quality observations from the already automated state checks.

For each failure, save the steps, expected/actual result, both player logs and a short recording if visual. Do not label any row PASS until someone performs it on the stated environment.

# Debugging the Windows UDP disconnect failure

## Observed failure

Four players could join, synchronize held objects, spawn, grab and throw successfully. Forcibly terminating one client then caused the surviving clients to time out too. The host kept updating and retained its objects, but stopped receiving further gameplay requests. A healthy host process therefore did not prove a healthy network session.

The failing run is preserved locally in `Artifacts/gameplay-20261102-175104/`. Those diagnostic logs are excluded from Git.

## Isolation

1. Keep every client connected: the four-player scenario succeeds.
2. Keep the impaired client connected but force-kill a different client: the failure remains.
3. Inspect host logs: later requests never reach the RPC handler; there are transport timeouts rather than gameplay validation failures.
4. Inspect disconnect cleanup: it releases the departing player's item and roster entry; it does not shut down the host transport.
5. Inspect the transport receive loop and compare with the upstream report.

## Root cause and bounded fix

The installed Unity Transport 2.7.4 source acquires receive buffers when scheduling UDP requests. Its failed-completion branch skips normal packet processing without returning the acquired buffer. Windows ICMP errors following an abruptly closed peer can exhaust that pool, affecting every connection sharing the host socket.

A [Unity engineer confirmed the same defect and the proposed fix](https://discussions.unity.com/t/unity-transport-6-6-0-udp-host-stops-receiving-after-abrupt-client-disconnect-on-windows/1736023). The report uses another package/editor combination; we separately inspected the identical defective branch in our installed version.

The narrowly scoped correction returns `bufferIndex` with `ReceiveQueue.ReleaseBuffer(bufferIndex)` before continuing past a failed completion. The project embeds its pinned transport dependency so this change is reproducible. Increasing queue capacity only delays exhaustion; changing the machine's firewall would hide the underlying defect and make the submission environment-dependent.

## Required regression evidence

Run `tools/verify-gameplay.ps1`: terminate a holder process while another client is late-joining, verify the held item becomes free, and confirm surviving clients can still grab and throw. Also test a fresh connection after the crash. Keep the normal latency/loss, approval, and kick/rejoin checks.

The patched Windows build passed this regression. Local evidence is in `Artifacts/gameplay-20261102-175715/`: the host released the forcibly terminated holder's item; the surviving late joiner grabbed and threw successfully; a fresh client joined afterward as client ID 4 and also grabbed and threw; the surviving client then received the expected host-loss message when the host left. The run also included the 150 ms added RTT / 3% packet-loss client. This establishes the local multi-process regression result, not cross-machine or Steam verification.

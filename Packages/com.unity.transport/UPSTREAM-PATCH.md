# Unity Transport 2.7.4: failed UDP receive buffer release

This embedded package contains the installed Unity Transport 2.7.4 runtime/editor and original package metadata, source metadata and license. Upstream package revision: `4ab5f764fea8cb22683a635175bcb13fcd0b23de`; package fingerprint: `98ef8bfec34d7eea791603fc1e45ce86c3484180`. Samples and offline documentation were omitted; no sample is used as the game's foundation. The original optional sample descriptors in package.json are retained, but their sample directories are not included.

There is exactly one runtime-source change, in `Runtime/UDPNetworkInterface.cs`, inside `ReceiveJob`'s failed receive-completion branch:

```diff
 if (results[i].status == Binding.Baselib_RegisteredNetwork_CompletionStatus.Failed)
 {
     failedCount++;
+    ReceiveQueue.ReleaseBuffer(bufferIndex);
     continue;
 }
```

A completed receive request still owns its acquired buffer. On success it enters the receive queue and normal queue cleanup releases it. On failure it bypasses that queue, so this branch must release the buffer itself. On Windows, repeated ICMP errors after an abruptly terminated client can otherwise exhaust the host's entire receive pool and affect surviving clients.

The same defect and this exact fix were [confirmed by Unity's transport maintainer](https://discussions.unity.com/t/unity-transport-6-6-0-udp-host-stops-receiving-after-abrupt-client-disconnect-on-windows/1736023/3) on September 9, 2026. The report concerned a newer Unity/transport combination; the installed 2.7.4 source has the identical faulty branch. This local backport preserves the project's pinned package version rather than implying an official 2.7.4 release contains the fix.

Validation: compare the runtime source against the cached original (one added line); rebuild; run the multi-process regression that force-terminates a holding client and verifies surviving and late-joining clients can still issue RPCs. Local regression evidence is recorded with the project's other test results. No firewall or operating-system setting is changed. Remove this embedded package when upgrading to an upstream version verified to include the fix, and rerun the same regression.

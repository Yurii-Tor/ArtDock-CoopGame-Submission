# Gameplay networking

## Authority and ownership

| State | Writer | Replication |
| --- | --- | --- |
| Character position and yaw | Owning client | `OwnerNetworkTransform`; interpolated on other peers |
| Character look pitch | Owning client | Owner-write `NetworkVariable<float>`, sampled at 10 Hz |
| Character color | Host after an owner request | Server-write `NetworkVariable<int>` |
| Held item identifier | Host | Server-write `NetworkVariable<ulong>` |
| Item holder | Host | Server-write `NetworkVariable<ulong>` |
| Item position and rotation | Host | Standard server-authoritative `NetworkTransform` |
| Item physics | Host | Clients render transforms; their bodies are kinematic |

Ownership is the identity associated with a network object. Authority determines who can write a particular state. They are separate concepts. An item is owned by its holder to meet the ownership requirement, while its transform and physics remain server authoritative. The host separately writes `PhysicsItem.Holder` and `PlayerAvatar.HeldItem`, then calls `NetworkObject.ChangeOwnership(holderClientId)`. On release it clears both values and calls `NetworkObject.RemoveOwnership()` to return ownership to the server.

## Input and dependency injection

`PlayerAvatar` reads the New Input System only for the local owner. A local camera exists only on that owner's player. Its composition notification allows the root to call `Configure(GameplayServices)` before gameplay starts. Dependencies are an item prefab, a session readiness predicate, a feedback callback, and an accepted-throw callback. Gameplay has no Steam or Unity Transport imports.

The readiness predicate is also checked on the server for every mutation. A connected but still-loading client cannot spawn or interact merely by sending an RPC.

## Grab transaction

1. The client raycasts and sends the requested network object ID through its own player object.
2. The host obtains the sender from RPC metadata, verifies that sender owns the player, and checks session readiness and a bounded, finite player pose.
3. The host looks up the item in its own spawned-object table, checks the 3.5 m reach and line of sight, and checks that both item and hands are free.
4. In the same callback, the host sets the item's holder and player's held ID, then changes ownership.

Host callbacks run sequentially. Two grab requests can arrive in either order, but only the first can observe a free item. No client makes an optimistic ownership change, so the loser has nothing to roll back. The two replicated fields need not arrive in the same packet; they are a server invariant, not a distributed transaction. Rendering uses the item transform and holder, while new interaction requests are validated against the server's current pair.

There is no client-selected throw velocity, spawn position, or ownership target. Spawn position derives from the host's player snapshot. The world is limited to 40 items, spawning to one request per second, interactions to 10 per second, and colors to four per second per player.

## Carried and released physics

The host changes a carried body to kinematic and moves it to a host-computed hand point in `FixedUpdate`. A sphere cast against the level shortens the hand distance near walls. The host still resolves contact with free dynamic items.

`PhysicsItem` owns the kinematic policy. NGO's `NetworkRigidbody.AutoUpdateKinematicState` is disabled because its generic ownership callback would otherwise reset a held body's custom kinematic state. `AutoSetKinematicOnDespawn` remains enabled.

On release, the host clears both held fields, restores server ownership, enables dynamic simulation, and assigns a fixed velocity for throws. Remote peers interpolate the same host simulation throughout the flight and landing. There is no independent client projectile simulation to correct at impact. Local hold/throw feedback therefore includes network delay; a cosmetic predicted hand model would be a possible extension.

`DontDestroyWithOwner` preserves an item when its holder disconnects. Session disconnect and player despawn both call an idempotent release path. The item returns to server ownership, becomes dynamic, and falls. Items below the world are despawned by the host.

## Persistent state versus events

Color, holder, and held ID are network variables, and item transforms are replicated state. A late joiner receives current state without replaying every historical grab and color change. The throw statistic is an event: only after acceptance does the host send a targeted RPC to the thrower's client. That client records against its local platform account. Other players do not increment their statistics.

## Tradeoffs and verification

Owner-authoritative movement provides immediate local control with very little code. The host validates interactions against its latest received player pose, not a rewound input timeline. A player moving quickly under delay can have a borderline reach request rejected. A malicious owner can falsify movement inside the world bounds; the pose checks are input hardening, not movement anti-cheat. A competitive game would need server-authoritative movement, prediction, reconciliation, and potentially lag compensation.

Players do not physically push items with their movement collider. Player/player and player/item collisions are disabled consistently; interaction uses the validated grab/throw rules. This avoids a remote movement proxy changing host physics unpredictably.

The runtime logs `[World]` transitions with item ID, owner, holder and throw velocity to make ownership and disconnect behavior inspectable. Acceptance should also cover two clients grabbing the same object, dropping after forced disconnect, late joining while an item is held, and throws under simulated latency/loss. A passing build alone is not proof of those behaviors; see the project's test evidence for checks actually executed.

# Design decisions

- Follow the assignment's listen-server topology: the host player's Unity process simulates the world. A Cloudflare-hosted authoritative server would change that requirement. No paid hosting is needed.
- Unity 6.3 LTS (6000.3.21f1), Netcode for GameObjects 2.7, Unity Transport for reproducible local testing. Optional Steam is an infrastructure adapter.
- Explicit constructor/configuration injection in one composition root; no reflection container is necessary for this small graph.
- Owner-authoritative players keep input responsive. This is a cooperative prototype, not a competitive anti-cheat solution. The host validates item requests against its observed player position.
- Physics items remain server-authoritative even while NGO ownership is assigned to the holder. Ownership is an access/lifetime concept, not the physics simulation authority.
- Scene synchronization and durable replicated state support late join. Initial participants share a network-time release barrier; late joiners do not pause existing players.

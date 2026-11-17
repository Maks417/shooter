# Feature: staged authoritative networking

**First-version scope, owner clarification 2026-11-14:** online/LAN multiplayer and hosted deployment are deferred. The immediate release is one human with local bots in one offline process; no rented server, external server process or online service is required. The staged networking design and historical evidence below remain for future work and architectural regression checks; they are not a first-version online delivery requirement. See [local release scope](../vision.md#first-version-scope-local-bots-only).

Owner WP-08. [Contracts](../contract-details.md) define messages/IDs; [combat](combat.md) stays transport independent. Select transport/framework at M5 through the spike below, without rewriting simulation as network component scripts.

## Phases

| Phase | Deliverable | Gate |
|---|---|---|
| N0 during M0–M4 | Offline authority, command streams, snapshots, headless composition | Server build and offline replay harness |
| N1 M5a | Loopback two peers; dedicated server; manifest handshake | No presentation references in server; reject mismatches |
| N2 M5b | Client motor prediction/reconciliation, remote interpolation | Impaired-network tests and bounded correction |
| N3 M6 | Fire validation, lag-compensated queries, late join, four players + eight bots | B04 and correctness/security gates |
| N4 post-slice | Eight players, deployment/services, auth and matchmaking | Measured capacity and operational design ADR |

## Framework decision spike

Use Unity Transport as initial candidate behind INetworkChannel; test NGO as an adapter if its lifecycle/replication saves work. Do not assume any package's anticipation API implements FPS rollback/replay. Netcode for Entities is a different ECS architecture and requires explicit migration ADR; not a drop-in optimization. Exact package capabilities/version must be verified on pinned toolchain. Spike implements two-player prediction, packet simulator, headless build and codec fuzzing; choose based on correctness, maintenance and budget. Keep custom prediction responsibility explicit whichever adapter is selected. [Official references](../references.md) document these distinctions.

## Authority, clocks and movement

Server 60 Hz authority; client commands at 60 Hz, batch three ticks per 20 Hz send with recent redundant commands in unreliable sequenced delivery. Server caps commands/tick/client and validates ownership, tick window, aim and rate; missing command holds last movement briefly ≤100 ms, clears one-shot buttons, then neutral. Input history ring 256 ticks, server state includes last processed input sequence. Time synchronization estimates offset/drift with bounded smoothing; client cannot select arbitrary rewind tick.

Client predicts only own motor and reversible weapon feedback. On snapshot restore authoritative state then replay unacknowledged commands using same motor and world collision revision. Capsule casts may differ at dynamic contacts; static world is baseline, correction is expected. Snap local simulation immediately; smooth visual error ≤0.5 m over ≤100 ms, teleport larger errors. Remote actors interpolate with initial 100 ms buffer, adaptive bounded 50–150 ms, extrapolation ≤100 ms then hold. Do not predict remote damage or AI decisions as authoritative.

Server sends snapshots at 20 Hz and reliable ordered critical match lifecycle/death/reload state. Transient cosmetic impacts can be unreliable with deduplication. State contains enough ammo/action information to recover lost events. Interest management uses spatial cells and mandatory nearby actors; global critical match events independent of visibility. Proposed bandwidth target ≤40 KiB/s downstream and ≤10 KiB/s upstream/client at four players + eight bots, including transport overhead. Measure server and client CPU separately; headless target simulation p95 ≤6 ms/tick for N3 on named test host, no dedicated-server claim from iGPU client timing alone.

## Lag compensation and fairness

Retain 200 ms authoritative gameplay hit-volume history at 60 Hz (13 samples minimum; allocate 16) plus entity existence/team state. Estimate shot time from synchronized command time within server-approved window [now−200 ms, now]; clamp/reject invalid or excessive delays. Query interpolated historical hit capsules against static cover without moving live Unity colliders. Dynamic doors/destruction absent in slice; if added, rewind their occlusion history or disable lag compensation across them explicitly. Confirm shooter alive/ammo/cadence at authoritative processing rules, then emit shot/event ID once. Test death/late-arriving shot ordering and document trade rules. Server owns collision/weapon content hash.

## Session robustness and security

Handshake compares protocol, build compatibility, recipe/generator/gameplay hashes and map seed before accepting inputs. Client does not supply code, file paths, meshes or damage. LAN/private test sessions use explicit peer allowlist/session token and no public matchmaking claim. Before internet exposure require authenticated session establishment, encryption/replay protection supported by selected transport/service, rate limiting, server secrets outside client build, connection limits and deployment review. This is an N4 gate, not a requirement to build a custom cryptographic protocol.

Late join receives bounded full snapshot plus world manifest, then applies ordered deltas after acknowledged baseline; input disabled until Ready ack. Lost baseline triggers rate-limited resync. Disconnect clears actor/reservations/leases; reconnect creates fresh session IDs and cannot replay old shot events. Cap packet lengths, queues, peers and per-peer memory; reject malformed packets without throwing out of receive loop. Server overload drops cosmetic updates before changing tick rules and disconnects abusive peers with reason.

## Acceptance

Test 0/50/100/150 ms RTT, 0/20/50 ms jitter, 0/1/3/5% packet loss, reorder/duplicate bursts and 2 s outage using seeded network simulator; 150 ms/3% is required N3 playable gate, 5% is resilience. At 100 ms/1%, p95 local prediction correction ≤0.1 m in static map movement, no double shots/ammo divergence. At 150 ms/3%, no persistent state divergence >1 s after recovery and interpolation stays bounded. Cover shots and 200 ms history boundaries validated; forged future/stale/fire-rate packets cannot cause extra damage. Four clients + eight bots complete 30-minute soak, late join and server restart with bounded memory/bandwidth. Eight players is a new workload/version with separate evidence.


## Complete replay and match semantics

[Simulation protocol](../simulation-protocol.md) defines owning-client reconciliation state, current/historical shooter origin and same-tick death trade rules. [Gameplay baseline](../gameplay-baseline.md) chooses private cooperative four-player mode, spectator late join after Playing and no respawn. N3 must implement these exact semantics; full-state join transport does not imply permission to spawn an extra player during a running match.

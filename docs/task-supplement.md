# Additional task cards and ownership

These extend [backlog](backlog.md) without renumbering T001–T025. Integrator assigns named workers in coordination.json. All shared contracts stay integrator-owned. No new artistic assets, physical destruction or public networking scope is introduced.

| Task | Owner / paths | Gate and dependencies | Deliverable / acceptance |
|---|---|---|---|
| T026 Contract details and fixtures | WP-00 Contracts, Tools/Contracts.Tests | M0 F02 before parallel merges | Implement explicit results/leases/enums and [prediction fields](simulation-protocol.md); pure C# tests consume independent fixtures; query overflow/lifetime tests pass |
| T027 Gameplay defaults/collision matrix | WP-05 Simulation; Settings edits integrator | M2 C01–C03 before world/nav finalization | Versioned [defaults](gameplay-baseline.md), layer audit and hit/vision masks; damage rounding, stance ceiling, same-tick trade and actor nonblocking tests |
| T028 Soldier baseline/pose comparison | WP-04 generator/rig; WP-05 gameplay evaluator | M1 P05 baseline then M2 C03 live integration | [Anatomy/IK baseline](soldier-baseline.md), 1000 pose sweep and overlay viewer; shared pose discrepancy thresholds tested on all LODs |
| T029 Objective/session rules | WP-05 Simulation/Match, integrator Bootstrap | Define now; M3 W01 consumes; implement M4 O01 | Compound Clear state machine, deterministic spawn/capture, loss/restart/co-op spectator rules; all boundary/reset tests pass |
| T030 Synthesized audio | WP-03 Presentation/Audio; compiler samples if cached | M4 O03 after M2 event fixtures | Analytic noise/oscillator shot/impact/footstep envelopes, peak limiter, pooled ≤24voices, distance attenuation; no recorded samples; accessibility volume controls; audio CPU≤0.5 ms/frame target inside main budget |
| T031 Generated lighting/probes | WP-03 Presentation/Rendering; compiler owner bake adapter | M1 basic sun; M4 polish | Low fixed sun+ambient, generated sky gradient; reflection probe bake once on load/compile if measured; no per-frame capture; version/hash validation and no stale world lighting |
| T032 Death collapse/ragdoll decision | WP-04 Presentation/Rig; WP-05 death policy | M2 deterministic collapse, M4 optional ragdoll spike | Baseline damped joint-limit-constrained collapse with root ground clamp, corpse nonblocking. Optional generated rigidbody ragdoll max 4 only if perf/collision tests pass; not required for M4. No imported death clips |
| T033 Exact benchmark fixture/harness | WP-09 Benchmarks and Tests/Performance | M0 B01-minimal, M1 materials, M3 B02/B03 freeze, M6 B04 | [Workloads/result schema](benchmark-fixtures.md), deterministic shot count, route/counter validity and nearest-rank analysis; normal AI soak separate |
| T034 Repository entry/runbook | WP-00 root AGENTS.md and Tools wrappers | M0 before worker assignments | Actual path-aware agent rules, locked-editor checks, client/server tests and build wrapper help; update docs from measured execution |

## Scope clarification

Cosmetic physics ragdolls are optional and bounded. Analytic collapse is required baseline; this supersedes the prior implication that full ragdoll construction was mandatory without a task. Synth audio and simple lighting are required but deliberately small implementations. No background cloud services or public auth is necessary for these cards.

## Integration order

T026/T034 first; T027 and T028 share pose schema and merge through integrator; T029 rules feed W01 before environment acceptance; T030/T031/T032 can work independently on event fixtures after C0, integrate only after real events/rig. T033 starts immediately and never waits until optimization to define workload. No benchmark owner changes gameplay defaults to improve timings; optional ragdoll failure keeps analytic collapse and records measured spike outcome.

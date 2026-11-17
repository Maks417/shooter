# Validation strategy and requirement traceability

## Current product regression status

Pure-lane results are recorded per delivery in the [M11 handoff](milestones/m11-playable-release/handoff.md); the latest delivery (0.3.17) recorded Game 99, Maps 72, Generation 508 and source provenance PASS, with the earlier 59316d3 failures repaired. Wrapper reports are the source of truth for counts; this page does not track them. Use [next playable build](next-playable-build.md) and [FIX01–FIX14](release-remediation-validation.md) with original milestone gates. No Unity/player/device/hardware acceptance is inferred from pure lanes; M8–M10 integration and M11 acceptance remain open.

Test through public ports with small deterministic fixtures; add integration/hardware checks where unit tests cannot establish correctness. Numerical tolerances live in fixture schemas. Avoid tests that merely duplicate implementation formulas. All tests report build, toolchain, seed, content hash and profile.

## Layers and scenarios

| Suite | Method | Pass condition / owner |
|---|---|---|
| V01 Contracts/schema | Compile pure assembly, roundtrip fixtures, invalid/overflow input | [Contracts](contract-details.md) invariants and codec tolerances; integrator |
| V02 Deterministic compiler | PCG known vectors, seed boundary corpus, parallel/cold/warm runs | Stable canonical hashes pinned toolchain, no same-key/different-bytes; content |
| V03 Geometry/material | Bounds/index/winding/normal tests, seams, tangent orientation, recipe extremes | No NaN/invalid topology; declared open surfaces; reviewed silhouettes; geometry/render |
| V04 Cache resilience | Truncate/corrupt manifest/payload, traversal paths, bomb lengths, cancel/concurrent build | Reject safely, atomic publish, no leaked locks/leases/files outside cache; content |
| V05 Rig/motion | 1000 seeded poses, stance/speed/terrain sweep, IK degeneracy | Weight/grip/slide/hit-volume tolerances in [soldier spec](features/soldiers-animation.md); character |
| V06 Motor/combat | Fixed command scripts at 30/60/144 render FPS, wall/corner/stair/ceiling cases | Consistent tick outcomes within 1 cm static-map position tolerance; exact ammo/event counts; simulation |
| V07 World/nav/stream | Four canonical seeds, border samples, reachability, 100 traversals/cancel/teleport | No missing collision, connectivity/capacity/clearance gates; world |
| V08 AI | Occlusion/hearing/reload/stuck/cover scenarios and bounded queues | No privileged unseen knowledge, per-tick query caps, 30-min soak; AI |
| V09 Cross-profile fairness | Same gameplay hashes/commands across all quality/rig update rates | Collision/nav/hit profile/weapon IDs equal; no cover/vision changes; integrator |
| V10 Runtime performance | B01/B02/B03, product TDM workloads and 30-min soaks (B04 parked) | [Exact performance protocol](performance.md); perf owner |
| V11 Networking (parked, ADR-076) | Seeded delay/loss/reorder/duplicate/outage, late join and malicious codec corpus | [N3 acceptance](features/networking.md); network |
| V12 Provenance/release | Source allowlist, manifest chain, clean regeneration and clean build | No prohibited assets, full cache trace, correct server stripping; integrator |
| V13 UI/visual | 720p max-scale settings, reduced motion, baseline image review | No clipping; readable/contrast; visual plausibility signed review; presentation |
| V14 Docs/contracts sync | Relative file+anchor checks, index coverage, contract/schema registry | Zero broken references; version changes documented; integrator |

## Negative and property tests

Generate valid/invalid recipe parameter ranges, max/min uint64 seeds, empty dependency graphs, cycle detection, duplicate IDs, zero-length bones, impossible dimensions and too-large layer counts. Compiler must fail boundedly with recipe/stage diagnostic. Fuzz disk and network decoders with bounded memory/time, checking no out-of-bounds reads, arbitrary deserialization or worker crashes. Run repeated cancellation/unload and snapshot pooling tests under leak instrumentation. Do not expose test-only debug commands in release server builds.

Gameplay random property tests preserve ammo ≥0, health monotonic under damage, one death event, unique IDs, no fire before cooldown, one processing per input sequence, no collision holes on quality changes. For non-bit-deterministic adapters compare semantic outcomes and documented tolerances, not fake cross-platform hash guarantees.

## Required evidence artifacts

`test-results.xml`, `generation-report.json`, `provenance-report.json`, `benchmark-results.json/csv`, frame-time plot, memory trend, visual captures, toolchain lock, match/build/content manifests and a gate-review Markdown record. Benchmark JSON includes machine/OS/driver/adapter/power conditions, duration/warmup, resolution/profile, workload counts, distributions, allocation and memory data; missing measurements are explicit nulls plus reason. Reviewer signs subjective geometry/motion realism separately from machine tests.

## Traceability

| Requirement | Primary suites | Gate |
|---|---|---|
| R01 engine/languages | V01,V12 | M0 and release |
| R02–R03 generated-only art | V02,V03,V05,V12 | M1 onward |
| R04 reference configuration | V10 machine report | M0/M4 |
| R05 native720p/30 and stretch | V10 | M4; stretch M7 |
| R06 early profiles/perf | V09,V10 | M0 |
| R07 gameplay fairness | V06,V09 | M2–M6 |
| R08 deterministic compiler/cache | V02,V04,V12 | M1 |
| R09 parallel boundaries | V01,V14 and PR ownership audit | Every merge |
| R10 full feature phases | V03,V05–V08,V11,V13 | M2–M6 |
| R11 offline team-match rules/capacity | TDM01–TDM08, V06,V08,V09 | M9 and REL02/REL04 |
| R12 two distinct packaged maps | MAP01–MAP03, V02,V04,V07,V12 | M10 and REL01 |
| R13 real shell/settings/results | UI01–UI02, SET01–SET02, V13 | M10 and REL02/REL03 |
| R14 independently playable build | REL01–REL06, V10,V12 | M11 |

Game-code test results live in wrapper reports and milestone handoffs, not on this page. Documentation delivery verification is separate: local file existence, relative links/anchors, index coverage and integrity manifest. This cannot substitute for future Unity/runtime testing.

That statement describes the original documentation creation. Historical M0–M7 handoffs now record actual pure and selected Unity foundation checks, with later player/hardware/visual gaps retained. New M8–M11 scenario IDs are defined in their [validation packs](milestones/index.md); they are **NotRun** until implemented/executed. Game-v1 fixture/schema validators must be added through T035, with independent C# reproduction and explicit test discovery counts. Refreshing documentation hashes does not generate any of that evidence.


## Specification fixture validation

Run [fixture verifier](fixtures/verify_fixtures.py) and standards-compliant schemas against catalog valid/invalid cases. C# tests independently reproduce PRNG/canonical hashes, malformed query/lease scenarios and full restore/replay fields. Verify objective120tick boundaries, shot/death trade, reload+switch precedence, actor nonblocking layers and deterministic GUID independent of payload. [Benchmark fixture](benchmark-fixtures.md) shot counts and route validity are preconditions to frame-time acceptance, not optional metrics.

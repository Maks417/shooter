# Validation strategy and traceability

Test through public ports with small deterministic fixtures. Add integration or hardware checks where unit tests cannot prove correctness. Numeric tolerances live in fixture schemas. Every run records build, toolchain, seed, content hash and profile.

Results are recorded in wrapper reports and milestone handoffs, never on this page. Scenario IDs and evidence rules are in [acceptance](acceptance.md).

## Suites

| Suite | Method | Pass condition |
|---|---|---|
| V01 Contracts/schema | Pure build, fixture roundtrip, invalid and overflow input | [Contract](contract-details.md) invariants and codec tolerances |
| V02 Deterministic compiler | PCG vectors, seed boundary corpus, parallel/cold/warm runs | Stable canonical hashes; never the same key with different bytes |
| V03 Geometry/material | Bounds, index, winding, normal, seam and tangent tests; recipe extremes | No NaN or invalid topology; open surfaces declared |
| V04 Cache resilience | Truncated/corrupt manifest or payload, traversal paths, oversized lengths, cancel/concurrent builds | Safe rejection, atomic publish, no leaked locks, leases or files |
| V05 Rig/motion | 1000 seeded poses, stance/speed/terrain sweeps, IK degeneracy | [Soldier](features/soldiers-animation.md) weight, grip, slide and hit-volume tolerances |
| V06 Motor/combat | Fixed command scripts at 30/60/144 render FPS; wall, corner, stair and ceiling cases | Same tick outcomes within 1 cm; exact ammo and event counts |
| V07 World/nav | Approved seeds, reachability, map rules, 100 traversal/cancel cycles | No missing collision; `ArenaRules`/`SiteRules` pass |
| V08 AI | Occlusion, hearing, reload, stuck and cover scenarios; bounded queues | No unseen knowledge; per-tick query caps; 30-minute soak |
| V09 Cross-profile fairness | Same commands across every quality tier | Collision, nav, hit profile and weapon IDs equal |
| V10 Runtime performance | Product TDM workloads and soaks | [Performance](performance.md) protocol |
| V12 Provenance/release | Source allowlist, manifest chain, clean regeneration and build | No prohibited assets; full cache trace |
| V13 UI/visual | 720p at maximum UI scale, reduced motion, image review | No clipping; readable contrast; signed visual review |
| V14 Docs/contracts sync | `docs/verify_docs.py`, lock verifiers | Zero broken links; versions documented |

Networking (V11) is parked under ADR-076.

## Negative and property tests

**Invalid input.** Generate invalid recipe ranges, min/max uint64 seeds, empty or cyclic dependency graphs, duplicate IDs, zero-length bones and oversized layer counts. The compiler must fail within bounds and report the stage.

**Robustness.** Fuzz the disk decoders with bounded memory and time. Run repeated cancel/unload and pooling tests under leak instrumentation.

**Gameplay properties:**
- ammo ≥0;
- health only decreases under damage;
- one death event;
- unique IDs;
- no fire before cooldown;
- each input sequence processed once;
- no collision holes after a quality change.

For adapters that are not bit-deterministic, compare semantic outcomes within the documented tolerances.

## Evidence

Required evidence, as applicable:
- test results (TRX/XML);
- generation, provenance and benchmark JSON;
- frame-time plots and memory trends;
- captures;
- toolchain and build/content manifests;
- a gate review record.

Missing measurements are `null` with a reason. A reviewer signs subjective realism separately from machine tests.

## Traceability

| Requirement ([vision](vision.md#requirements)) | Suites | Gate |
|---|---|---|
| R01 engine/languages | V01, V12 | Release |
| R02–R03 generated-only art | V02, V03, V05, V12 | Every content change |
| R04–R06 reference machine, Low30, profiles | V09, V10 | REL04 |
| R07 gameplay fairness | V06, V09 | TDM08, MAP03 |
| R08 deterministic compiler/cache | V02, V04, V12 | Every content change |
| R10 features | V03, V05–V08, V13 | Feature acceptance |
| R11 team-match rules and capacity | TDM01–TDM08, V06, V08, V09 | M9, REL02, REL04 |
| R12 distinct packaged maps | MAP01–MAP03, V02, V04, V07, V12 | M10, REL01 |
| R13 real shell and settings | UI01–UI02, SET01–SET02, V13 | M10, REL02–REL03 |
| R14 installable build | REL01–REL06, INS01–INS06, V10, V12 | M11 |
| R15 visual quality | VD0–VD9 | [Visual direction](visual-direction.md) |

Fixture checks: `python docs/fixtures/verify_fixtures.py`, the pinned schema validator, and independent C# reproduction of the PRNG and canonical hashes ([fixture catalog](fixtures/index.md)). Documentation hashes never count as game evidence.

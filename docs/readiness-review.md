# Implementation readiness review — closure record

Revision1.1, 2026-11-11. This historical record resolves the pre-implementation documentation review, which authorized M0 under one integrator. Current implementation/tests/builds and remaining acceptance conditions are in the [M0 handoff](milestones/m0-foundation/handoff.md); this review itself implies no Unity/gameplay/graphics/performance result.

| Finding | Resolution | Implementation evidence required at review time |
|---|---|---|
| Ambiguous contracts/leases/overflows | [Contract details](contract-details.md), corrected original port signatures, explicit enums/limits | F02 compiling C# and lease/adapter conformance tests |
| No repository entry rules | Root AGENTS.md supplied alongside docs | Named ownership and actual coordination ledger at M0 |
| M0 setup too broad | [Bootstrap runbook](bootstrap-runbook.md), wrapper/path/failure contracts | Inspect actual editor, pin versions, implement wrappers and builds |
| No executable examples/goldens | [Fixture catalog](fixtures/index.md), valid/invalid JSON, schemas, exact vectors and verifier | Independent C# reproduction and standards-compliant schema validation in CI |
| GUID/content and telemetry contradictions | [Exact identity/format](deterministic-format.md), stable LogicalAssetId separate from versioned payload, deterministic manifests | Compiler publication and same-key/different-bytes tests |
| Tick/replay ambiguity | [Simulation protocol](simulation-protocol.md), shared pose timing, same-tick trades, complete restore state | Restore/replay tests and impaired network tests later |
| Incomplete game/filter defaults | [Gameplay baseline](gameplay-baseline.md), default data, collision matrix | Motor/combat conformance and profile fairness tests |
| Objective/respawn undefined | Compound Clear with explicit capture/death/restart/co-op/spectator policy | M3 spawn/nav validation; M4 state machine tests |
| Workload not reproducible | [Benchmark fixtures](benchmark-fixtures.md), stage-specific IDs/routes/shot/counter rules and result schema | Freeze generated route/content hashes at M3; actual reference captures |
| Unassigned complex features | [T026–T034](task-supplement.md), audio/lighting/pose/collapse ownership | Owner-specific feature acceptance; optional ragdoll only after spike |
| Soldier baseline missing | [Anatomy/bind/gait recipe](soldier-baseline.md), numeric source fixture | M1 topology/IK/performance/visual review |

## Delivery checks versus future gates

Documentation verifier checks local links, reachable pages and SHA256 inventory including all fixtures/scripts. Fixture verifier checks published PCG outputs, byte encodings/hashes, schema keyword subset, valid/invalid examples, benchmark shot count and unmeasured result fixtures. These are specification checks. M0 subsequently installed the pinned full JSON Schema validator and ran real pure/Unity foundation tests and player builds; the handoff records their actual scope. Local execution does not prove hosted CI execution or full gameplay acceptance.

Do not delay M0 awaiting empirical answers only a build/spike can provide. Do not start parallel shared-contract edits before F02/F03 pass. Exact toolchain and real hardware timings remain explicit tasks, not documentation omissions or permission to invent values. [Start here](implementation-start.md).

## Playable release planning review — 2026-11-13

The historical review above is retained. [ADR-033](decisions.md#adr-033--accepted-next-offline-playable-product-scope-and-staged-delivery) and the [new release plan](playable-release-plan.md) address the owner's playable bot-game request. All original M0–M7 closures remain qualified by their handoffs; root summaries now agree on eight original closures. No new runtime, Unity/player, hardware or visual success is claimed by the planning review.

| Finding | Prepared resolution | Still required before/at implementation |
|---|---|---|
| Default runtime remains diagnostic | M8 real composition tasks and actor/map/view/audio seams | T035 compiling freeze; T036–T039 actual player |
| Current asset source audit fails | Record 51 missing metadata entries and one unapproved Assets Markdown file; T035 owns baseline repair | RUN00 source provenance/assembly/reference checks before dispatch |
| Old objective has no team score/respawn/deadline | New TDM specification with explicit tick, tie, credit, life and result rules | M9 fixtures, real 5v5/6v6 and full match evidence |
| Existing AI is eight-bot/single-target | Team-aware 9/11-bot task with shared budgets and authority-state/path/sequence tests | T042 full-roster runtime and fairness evidence |
| Only planned map/settings models | Two distinct map recipe/catalog spec and interactive shell/device/persistence flows | M10 native map and UI/device acceptance |
| No accepted complete product archive | M11 clean extraction, full match matrix, P05 remediation/review and exact-roster performance | Actual binary/content pair, named review and real reference captures |
| Future agents need bounded assignments | Four complete packs, T035–T051 dependency cards, path ownership and merge order | Explicit assignment, named owners/current main SHA/fixture hashes and worktree leases |

Planning readiness is distinct from parallel execution readiness: Game-v1 code/fixtures are still planned, so do not mark coordination.json ready or launch new agents from the existence of these docs. Actual remaining external evidence is editor/license/modules, native runtime behavior, human visual/playability review and reference hardware performance. No additional product decision is needed to start T035 with the documented defaults.

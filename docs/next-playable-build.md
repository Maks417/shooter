# Next playable build — remaining work after 59316d3

Reviewed 2026-11-14 at clean `main`, `59316d387e5345716392e332ca225a581a671c33`. This is the implementation queue after T052–T064, within existing **M8–M11**. No M12 is needed: the missing work completes the already agreed offline product. Original T035–T065 requirements, FIX01–FIX14 and RUN/TDM/MAP/UI/SET/REL gates remain mandatory. See the [current audit](product-state-audit.md), [original repair cards](release-remediation-plan.md) and [acceptance matrix](release-remediation-validation.md).

This assignment updates documentation only. The cards below are proposed bounded implementation assignments, with no active coding lease, agent dispatch or authorization for a fourth P05 clearance search. One integrator owns Bootstrap, Contracts, schemas/locks, project settings, workload definitions and coordination.json. Keep Unity 6000.3.28f1 / URP 17.3.0, C0-v1, Game-v1 with ADR-036 settings schema 2, ADR-035 packages and ADR-037 workloads; any required format/identity change needs its reviewed ADR/migration/fixtures first.

## What the owner will receive

The deliverable is an extractable Windows x64 archive with a local launcher, controls and known issues. It opens offline to Main menu, offers Training Compound / Depot Yard and 5v5 / 6v6 Team Deathmatch, and supports movement, aiming, rifle/pistol, reload, allied/enemy bots, death/respawn, pause, scoreboard, results and replay. Required final rules stay 150 kills / 15 active minutes. No Unity installation, repository, developer cache, internet or server process is needed.

Two checkpoints make delivery concrete without changing release acceptance:

| Checkpoint | Exit | What it means |
|---|---|---|
| Owner playtest candidate (T071) | T066–T069 integrated; clean Unity tests/builds; archive extracted and manually played on both maps/sizes with the checks below | A usable local preview, explicitly listing P05 FAIL and pending final visual/reference acceptance. No milestone closure or REL PASS is inferred. |
| Accepted offline release (T065) | T070, T062 visual scope/review and all original/FIX/REL exits on one final candidate | Complete matches, usability/device review, reference Low30 matrix, soaks, provenance, independent delivery and rollback accepted with actual evidence. |

The preview checkpoint is a handoff sequence, not a reduced game scope or an inherited hardware waiver. Unknown preview performance is labeled unmeasured. Any functional blocker prevents handing it over as playable. Source fixes after preview require a new build/package identity and affected acceptance reruns.

## Bounded task queue

| Task | Existing milestone / repair | Dependencies | Concrete output |
|---|---|---|---|
| T066 — Restore current integration baseline | M8/M11; FIX01, REL05 | 59316d3 review | Tracked metadata, resolved cancellation regression, trustworthy Unity compile/test and provenance baseline |
| T067 — Finish packaged content and native navigation | M8/M10; T055, FIX04/07 | T066 | Data-only release catalog; actual visual/weapon/rig closure; native bake/install/path evidence; local packaged discovery/recovery |
| T068 — Finish authority-driven presentation | M8; T056, FIX05 | T066; package integration T067 | Correct rifle/pistol and action views, packaged visuals, coherent life/pose/event binding |
| T069 — Finish controls and applied preferences | M10; T059–T061, FIX08–10 | T066; integrate T068 | Editable working bindings, complete applied settings, keyboard/pointer UI, device confirmation |
| T070 — Validate real measurement and multi-match soak | M9/M11; T063, FIX12 | T067–T069 | Product collector validated in player; honest counters; finish/replay-aware capture and two-map soaks |
| T071 — Build and hand over a playable candidate | M11; T064, FIX13, preliminary T065 | T066–T069 | Clean matching client/server builds, independent local archive, playtest evidence and owner launch instructions |
| T062 + T065 — Visual disposition and final acceptance | M8/M11; FIX11/14 | Stable T070/T071 candidate | Named visual/device reviews, complete match/performance/soak matrices, final archive/hash and rollback |

T070 can be developed before T071, but reference measurement uses the same final binary/content pair. Start with T066, then T067; do not repeat the already landed Contracts lock repair or contract freeze. Uncovered defects return to the owning card. No estimates or dates are promised before the first source-matched Unity compile/player run.

## T066 — Restore the integration baseline

Owner: integrator. Allowed paths: metadata for files introduced by 59316d3, `Tools/Generation.Tests/CompilerTests.cs`, compiler lock/cancel implementation under `Generation/Editor/`, and relevant integration tests; Bootstrap/asmdefs only for actual compile/isolation failures, with exact diff recorded. Do not regenerate settings, replace goldens or relax provenance rules to conceal failures.

Fresh evidence at 59316d3:

- `Test-SourceProvenance.ps1` fails on **19 missing metadata entries**, including ProductTdmBenchmark/Host, VerifiedMapPackageStore, map-package sources/recipes, SpawnSafetyPolicy, the Tests/Performance folder and ProductTdmWorkloadIdentityTests. The [audit](product-state-audit.md) records the complete failure evidence. Preserve existing GUIDs and add stable tracked metadata for the actual new sources.
- Generation executes 311 cases: 310 pass, `CompilerTests.BusyKeyLockCanTimeOutOrCancelWithoutPublication` fails at line 120 (expected Cancelled, got Busy); a second full run repeats the same failure. Its 25 ms cancellation / 50 ms timeout is timing-sensitive; cause is unproven. Distinguish compiler ordering from test scheduling, then fix meaningfully. A later green run does not erase these failures.
- Contracts 72/72, Simulation 101/101 and Game 38/38 pass, all zero skipped. Offline advisory lookup remains PendingNetwork. No current Unity compilation/player proof was produced by this review.

Exit: source provenance and locked suites pass with counts; cancellation/timeout outcomes and no partial publication have deterministic coverage; configure the pinned editor, then run EditMode/PlayMode and Client/Server builds. Record exact compile/test failures and assign repairs before claiming readiness. Keep source/build/fixture hashes and raw failing attempts. A pure .NET build does not compile all new Unity classes.

## T067 — Finish content delivery

Owner: content/world integrator. Allowed paths: `Content/Maps/`, map package compiler/codecs/publisher, `Generation/Editor/Compilation/`, `World/Foundation/CompoundMapRuntime.cs`, runtime loaders and associated tests; shared Bootstrap/build/package/index/schema changes through the integrator.

Confirmed source gaps:

1. `StaticMapCatalog.CreateReleaseSeed` calls `CompoundCatalogFactory.BuildGameplay` and `MapPackageBuilder.Build` eight times. The factory runs numeric planners. Loading verified collision later does not remove this release-startup compilation. Persist catalog/gameplay metadata at compile time and load verified bytes in the player; no planner/builder invocation or placeholder toolchain fallback on the shipping path.
2. `MapPackageBuilder.AddVisualClosure` supplies a combined box mesh, 1×1 material and **single-joint skeleton**, plus shader/font descriptor JSON. This is not the actual soldier/weapon dependency closure. Publish the real approved soldier rig/meshes, rifle and pistol, materials/LODs and explicit built-in shader/font provenance; validate transitive identities and sizes. Built-in exceptions must match assets actually retained by the player.
3. `ProductMapView` constructs a unit box and numeric materials from collision; `ProductActorPresentation.InitializeShared` invokes SoldierGeometry/WeaponGeometry at runtime. Wire both to verified visual payloads and retain only allowed upload/pose work at runtime. Removing a required visual dependency must fail recoverably, not succeed through generation.
4. `UnityNavMeshBakePublisher` exposes constants and a pending-reason string only. Map packages contain `BuildWalkableGrid` output; `CompoundMapRuntime` decodes it but routes queries through `CompoundConnectivity.TryFindPath`. Implement pinned native bake publication and the matching runtime navigation adapter, with real installed collision/clearance/reachability/corners on both maps. Do not label the software grid as a completed native bake.
5. Bootstrap defaults resolve two directories above `Application.dataPath` to repository paths. Package tooling emits `launch-offline.cmd` with relative explicit overrides. Make the intended launcher work from any current directory and test direct executable behavior against the promised delivery contract; prefer executable-relative packaged defaults. Missing/corrupt content before shell creation currently reaches Bootstrap failure; provide the required repair/reinstall screen and return path.

Exit: FIX04/FIX07 actual cold/warm/forced, cancel/corrupt and native-world cases; both maps/profiles loaded from packaged bytes; gameplay equivalent across quality; server gameplay-only closure. Empty compiler/developer caches and unavailable repository cannot change load success. Any necessary schema/generator/nav identity migration is reviewed before consumers.

## T068 — Finish presentation from real authority

Owner: presentation integrator. Allowed paths: `Presentation/Rendering/`, `Rig/`, `View/`, `Session/ProductSessionView.cs` and real integration tests; any Game-v1 projection change through the integrator with ADR/fixtures if required.

`ProductActorPresentation` always creates a held rifle and does not consume `ActorLifecycleState.ActiveWeaponSlot`. `PoseAvatar` starts a guessed reload phase when magazine decreases and uses a fixed 2.4-second duration. A shot can therefore trigger reload presentation, while pistol/action timing is not faithfully displayed. Consume the read-only selected weapon and actual action start/end state/events. Show the correct rifle/pistol, aim/fire/reload/death/respawn poses and local view feedback; never infer a reload from an ammo drop or alter authority to match visuals.

Use T067 packaged meshes/rig/materials. Preserve bounded corpse/life binding, snapshot coherence and quality-independent hit poses. Exit: player captures and meaningful adapter checks for both weapons, actions and repeated human/bot replacement, followed by FIX05 profile/lifecycle/load/traversal coverage. Existing binder tests remain useful but do not prove that the actual Unity renderer consumes the correct state. This task does not authorize P05 generator parameter changes.

## T069 — Finish preferences and usability

Owner: client integrator. Allowed paths: settings UI/editor/device application, `Presentation/Input/`, `Session/`, menus/scoreboard/audio and relevant tests. Preserve ADR-036 schema 2 unless a reviewed migration is necessary.

**Software landed (T069):** editable bindings with conflict swap/cancel; `LocalPlayerInput`/scoreboard read applied bindings; Escape reserved; `ApplyUserSettings` consumes bindings, ReducedFlash (cosmetic muzzle overlay), ReducedCameraMotion (ADS FOV zoom off) and Subtitles (shot/impact/hit/reload/UI captions); device resolution enumeration (baseline pair only when empty); pending display persisted only after Keep. ADR-036 schema 2 unchanged; no gameplay hash changes.

**Still required for FIX08–FIX10/SET01–02 exit:** named 720p 100%/150% UI keyboard/pointer review, 15s Keep/Revert on real display, audible output/gain/mute review, and 20 alternating load/replay cycles — record Pending until player/device evidence exists.

## T070 — Make measurements executable

Owner: measurement integrator. Allowed paths: ProductTdmHost/Benchmark, product telemetry, `Tools/Benchmark*`, analyzer/negatives and performance tests; frozen workload changes need ADR/fixtures first.

T063 adds real collector/tooling, so the old B01-only claim is obsolete. However, `ProductTdmHost` starts a match once and has no finish/replay loop. Soak workloads request 108000 active ticks (30 minutes), while a normal match finishes by 54000 ticks or 150 kills; Bootstrap stops advancing finished sessions. The current single-match host cannot satisfy the required two-match/replay soak. Implement explicit finish/results/replay transitions with coherent session epochs and cumulative capture time; never extend production match rules to force a soak.

Audit counters against actual adapters: current respawns uses net living-count growth and can miss a respawn accompanied by a same-tick death; querySamples sums AI ray/path counts rather than all world queries; contentBindings is assigned one. Also `WorkloadId` is set before BeginLoadAsync, which resets measurement. Establish and test the intended semantics, preserve identities across load/replay, and reject absent counters instead of stamping expected values. Verify map/roster/tick/build/profile identity and required sampling windows in a rendered diagnostic first. Hardware capture stays PendingHardware until actual reference runs.

Exit: FIX12 real collector plus negative cases, both-map score/timeout transitions, cumulative ≥30-minute normal-AI 6v6 two-match/replay soak per map, then twelve native720p Low captures (60s warmup / 180s measurement / three runs per map and size). Record real CPU/GPU/thermal/reference evidence; keep B01–B04 and 60 FPS stretch separate.

## T071 — Produce the owner playtest candidate

Owner: release integrator. Allowed scope: clean build/package execution, product failure fixes assigned back to T066–T069, controls/known-issues/evidence and M11 handoff. Use existing T064 safeguards; do not copy historical binaries or invent manifest identities.

1. Commit completed implementation to `main`; configure fresh checkout with the pinned editor. Require clean source, passing provenance/regressions, Unity EditMode/PlayMode and non-development Client build; build Server and prove presentation/graphics isolation.
2. Compile and package verified both-map content against matching source/toolchain/build identities. Use `Package-Release.ps1 -ClientBuildRunDirectory <actual-run>` and retain inventory/SHA256, licenses/font policy, controls, known issues and previous binary/content pair. Review the current script's software-only extraction/corruption/rollback checks; perform real player checks too.
3. Extract outside the repo with Unity/developer caches/network/server unavailable. Launch the documented local launcher from another working directory. Exercise both maps × both sizes with actual manual move/aim/fire/rifle-pistol switch/reload, allied/enemy combat, human/bot death/180-tick respawn, pause/focus, live scoreboard, settings and restart/leave. Play at least one complete normal match on each map, see immutable results and replay. Capture build/map/seed/profile/session identities and blocking errors.
4. Test missing/corrupt content in the actual executable, restore the original pair and repeat launch. Prove the previous binary/content rollback pair launches. Hand over the archive's real absolute path/hash, launcher and controls, plus every unresolved defect and unmeasured device/performance gate.

This is the earliest owner-playable handoff; a successful synthetic packaging suite or READY line cannot substitute. No working archive is claimed to exist at the reviewed source. P05 FAIL is disclosed and its evidence retained. The full eight-match score/timeout matrix, draw fixture, named visual acceptance and hardware runs still belong to T065 before accepted release.

## Final acceptance and maintenance

T062 remains ToolingReady; P05 skin/forearm/strap/eye-sight residuals remain FAIL under ADR-024. Capture the actual player first, inventory defects, and obtain the explicit owner scope decision before any further clearance/generator search. Any authorized fix uses reviewed identities/new caches and retains failed artifacts. A preview handoff does not waive REL03.

T065 executes [REL01–REL06](milestones/m11-playable-release/validation-acceptance.md) and [FIX01–FIX14](release-remediation-validation.md) on one final clean binary/content candidate after repairs. Record eight full matches (both maps × both sizes × score/timeout), draw fixture and named manual matches, visual/UI/audio/display sign-offs, twelve reference captures and two soaks. Missing reviewers/hardware/counters remain Pending, not PASS. Preserve exact limitations in M8–M11 handoffs and keep coordination history intact; no leases are created by this plan.

Update shared docs and affected handoffs with actual source, versions, commands/counts, artifacts and rollback. Commit all completed assigned work to `main` under the repository's standing rule, verify preservation, and remove temporary task worktrees/branches only after retaining required ignored evidence. No public upload or distribution is part of this queue.

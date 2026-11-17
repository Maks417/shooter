# Additional task cards and ownership

These extend [backlog](backlog.md) without renumbering T001–T025. Cards end at T090; T091–T094 and current dispositions are tracked as rows in [next playable build](next-playable-build.md#task-disposition), and visual roadmap work under ADR-084–095 in the [visual roadmap](visual-roadmap.md). Integrator assigns named workers in coordination.json. All shared contracts stay integrator-owned. No new artistic assets, physical destruction or public networking scope is introduced.

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

## Playable release task cards — T035–T051

Original T035–T051 requirements remain. Current disposition of every task (T066–T094) is in [next playable build](next-playable-build.md#task-disposition); read it with the [audit](product-state-audit.md), [playtest guide](playtest-guide.md) and [FIX matrix](release-remediation-validation.md). Game-v1 stays frozen with ADR-036/037 extensions and the ADR-038 catalog sidecar; T062/T065 acceptance remains open.

| Task / milestone | Dependency | Owner / output | Required acceptance |
|---|---|---|---|
| T035 / M8 | Historical locks and code reviewed | Integrator: repair baseline source-audit/metadata first; compiling separate Game-v1, executable fixture/schema registry, authority/map/menu/settings seams, TDM workload definitions | RUN00–RUN01; source audit and version/count/lease/life/clock/stale-session fixtures; freeze on main before feature dispatch |
| T036 / M8 | T035 | Integrator: reusable actor authority and real session Bootstrap, local input/tick/pause/cancel lifecycle | RUN03–RUN04; C0 harness regression unchanged |
| T037 / M8 | T035; integrate T036 | World/content: first full compound package, native collision/nav install, map compile/test wrappers | RUN02; manifests/cold-warm hashes, real path/corner and corrupt-load cases |
| T038 / M8 | T035; integrate T036–T037 | Client: real input/camera/weapon/rig/HUD/synth device adapters | RUN03–RUN04; actual player uses input, sees/receives combat and hears effects |
| T039 / M8 | T036–T038 | Integrator/visual owner: executable vertical review, bounded P05 defect triage and live fixes | RUN05, real builds/smoke; visual acceptance remains pending until T049 |
| T040 / M9 | M8 exit | Match: authoritative scoring/deadline/results reducer | TDM01–TDM02,TDM06; full-tick trades, draw, immutable results |
| T041 / M9 | T035 lifecycle; integrate T040 | Spawning: participant/life reset, safe team spawn and failure policy | TDM03,TDM06; 180/30/600-tick boundaries |
| T042 / M9 | M8 real adapters; integrate T040–T041 | AI: 9/11 bots, both teams, multi-enemy sensing, state/sequence/path lifecycle | TDM04–TDM05; global budget and fair opportunities |
| T043 / M9 | T040–T042 | Integrator: one-map complete 5v5/6v6 with minimal diagnostic score/results view | TDM01–TDM08; actual score and unshortened timer matches |
| T044 / M10 | T035,T037; acceptance after M9 | Catalog/map: approved map/mode/size metadata, compound team spawns, packaged closure | MAP01–MAP03 on compound |
| T045 / M10 | T035,T037; integrate T044 | Depot: distinct numeric grammar, native nav/spawn/route/visual package | MAP01–MAP03 on depot; distinct topology and identity |
| T046 / M10 | T035; integrate T043–T045,T047 | Shell: menus/load/pause, team HUD, live scoreboard/final results | UI01–UI02, both maps/sizes, keyboard/mouse/focus |
| T047 / M10 | T035; integrate real M8 services | Settings: validated store/migration and actual device application | SET01–SET02; relaunch, storage errors, display revert, audible mute/gains |
| T048 / M11 | M10 exit | Release integrator: cold regenerate/build/package/clean install | REL01,REL05; no dev cache/editor/network dependency |
| T049 / M11 | T039 defects, M10 live views | Character/presentation: procedural source fixes and named new visual review | REL03; original P05 FAIL retained, new review is separate |
| T050 / M11 | T048,T049 stable candidate | Validation/perf: complete match matrix, real hardware and soak | REL02,REL04–REL05; all four map/size workloads, normal-AI capacity |
| T051 / M11 | T048–T050 accepted | Integrator: playable local archive/hash, controls, evidence/rollback handoff and main cleanup | REL06; all required checks actual PASS or explicitly unresolved, no inherited waiver |

Future task dispatch uses current accepted main, actual owner/base SHA/branch/allowed files, fixture hashes and exact commands. Compile-GameContent, Test-Maps, Test-Game and Package-Release now exist but have the limited scopes recorded in the audit; their names do not prove content/player acceptance. T052–T065 retain the original repair cards; T066–T071 now separate landed software from remaining native-navigation and player/device/delivery validation. Workers return versioned evidence/rollback references; integrator updates shared docs and affected handoffs, commits completed work to main and verifies preservation before cleanup.

## T072 — Local sprint slide (owner assigned 2026-11-14)

M8/M10 local gameplay extension under ADR-039, explicitly authorized by Max for implementation now. Sole integrator owns motor, CombatHarnessSimulation, local session/envelope/sampler, input/settings binding normalization, associated pure/Unity tests, packaging controls and shared docs/locks/provenance refresh. No agents. Preserve C0/N1/content/toolchain formats, existing P05 failures and all open acceptance gates. Deliver Left Shift + F (applied Sprint + Slide), fixed-tick crouch slide with momentum decay/collision/cooldown/life reset, working rebind/migration, passing required checks and a new identifiable local package. Record actual build/test/player limits in M8/M11 handoff; do not advertise slide as present in the retained 7cd7e81 archive.

## T073 — Windows installer and in-place upgrade

Owner requirement recorded 2026-11-14; M11 delivery extension. Final distribution is a Windows installer that recognizes and updates an existing installation when a newer installer is run, preserving user settings/controls and one stable app identity/path/shortcuts. ZIPs remain preview/internal artifacts. See [installer contract and INS01–INS06](features/windows-installer.md). Release integrator owns future installer tooling around Tools/Package-Release and the validated client/content pair; shared build/settings/schema/identity changes need the normal reviewed ADR/fixtures/migration. First freeze installer technology, version/identity strategy, repair/uninstall/rollback behavior, then implement and exercise clean install, N→N+1 update, repair, interrupted update, uninstall and installed rollback. Status 2026-11-14: implemented under owner-authorized dispatch with Inno Setup ([ADR-041](decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073)); INS02/INS03/INS05/INS06 PASS in silent owner-PC exercises, INS04 Partial, INS01 Pending (clean machine). REL01/REL06 and FIX13/FIX14 remain open until evidence; no online service or server purchase is needed.

## T074 — Visual redesign, stage 1: shell and combat HUD

Follow-up planning request: [VD0–VD9](design-milestones.md) define the full Battlefield V / Bodycam / Warface quality goal and supersede the earlier informal five-stage order. This follow-up creates the roadmap only. T074's existing source remains a partial UI foundation, and future component assignments/acceptance decisions remain explicit; no soldier clearance search, runtime edit or agent dispatch follows from generating the plan.

Screenshot clarification, same day: replace the flat main menu with a cinematic procedural stage, compact left navigation and rendered map cards; use a smaller world-led HUD with compass, friendly minimap/squad and recent elimination feed. Extend the integrator's paths to `ProductMenuScene`, `IProductShellBackend`'s optional menu-byte reader, `ProductActorPresentation`'s cosmetic-only showcase/first cold weapon bind, the Bootstrap client adapter/runtime reader and bounded clock-transition repair, plus relevant tests. Do not alter soldier generator identities, anatomy/clearance parameters, authoritative visibility or combat, content formats, seeds or acceptance. Preserve the independently landed bot fix at `2ee1a3d`; its client measurement assembly dependency was repaired by the integrator. The scene and HUD remain read-only, with no gameplay fake adapters or imported screenshot assets.

Owner assigned 2026-11-14: implement the visual redesign in Unity in stages, starting with menus and HUD; references Battlefield, Call of Duty, Warface and Bodycam. Sole integrator owns `Presentation/Session/TacticalUi.cs`, `Presentation/Session/ProductShellHost.cs`, `ProductSessionView.cs`, associated validation, installer delivery and shared design/handoff documentation. Stage 1 covers Main/local setup/loading/pause/confirms/grouped settings/live and final score tables and the read-only combat HUD. Preserve settings schema 2, applied bindings, native map packages, all gameplay/contract identities, server isolation, installer identity and open acceptance. No agent dispatch, new imported assets, soldier-generator clearance search or waiver is created by this stage. [Brandbook](brandbook.md) records the direction and later environment/operator/weapon/motion stages. Validate locked Unity tests, required pure/provenance/docs checks, clean Client/Server builds and real player at 720p 100%/150%; record actual results and limitations in M10/M11.

## T075 — Procedural presentation implementation wave

Owner authorized continued Battlefield V / Warface / Bodycam design implementation and multiple agents on 2026-11-15. Base: `4e71bea`; integrator owns shared files, verification and delivery. This is software toward VD1/VD3/VD4/VD6/VD7, not approval of the comparison set or acceptance of any VD exit. P05 soldier-generator/clearance work is excluded.

Three isolated worktrees own disjoint paths: surfaces (`ProductMapView` and `ProductSurface*` helpers/tests), handling (`ProductActorPresentation`, `ProceduralViewModel`, `FirstPerson*` helpers/tests), menu (`ProductMenuScene`, `MenuStaging*` tests). `coordination.json` records leases. Root owns the reduced-motion call-site in `ProductSessionView`, integration tests/builds, and shared docs. Frozen C0/Game-v1/content encoding and existing seeds, authoritative collision/navigation/hit poses/weapon timing, settings schema 2 and installer identity stay unchanged.

Deliver spatial packaged PBR channel binding with correct URP conversion and resource teardown; analytic equipment-relative first-person handling with truthful ADS, life reset and reduced motion; scene-led menu using verified material families. No imported source art or runtime generation fallback. No per-frame material construction or extra reflection captures. Validation: frozen identity guards, Contracts/Generation/Game wrappers, relevant Unity EditMode/PlayMode, source provenance, Client/Server builds, specification fixtures/docs verification and source-matched installer. Player visual review, named reference comparisons and hardware performance remain separate Pending evidence. Rollback: revert this wave's implementation commits and rebuild matching binary/content; retain prior installer and all failed evidence. Integration results live in the M11 handoff.

T075 publication extension: [ADR-044](decisions.md#adr-044--accepted-versioned-procedural-map-surface-publication) versions both map generators to 1.0.1 for four actual MaterialBaker surface sets and required normal/mask companions. Integrator owns strict schemas/examples/registry/catalog labels, numeric variant material generation and native republishing. Decoded gameplay remains unchanged; package/envelope/catalog identities change and must be rebuilt. Prior binary/content pairs remain historical rollback artifacts.

T075 delivery (2026-11-15): bounded presentation implementation merged to main, clean source `7407bc4`, installer 0.3.4; [actual results and remaining gates](milestones/m11-playable-release/handoff-archive.md#t075--procedural-presentation-preview-034). Root also corrected the stale input/ceiling test fixtures without runtime input/motor changes, and moved real resource teardown/card-pixel coverage into PlayMode. Full VD/REL acceptance remains open.

## T076 — Compact TDM map layouts

Owner assigned 2026-11-15: focus on map design; maps should be small but sized for 5v5 and 6v6, planned from shooter level-design best practice, implemented by Sonnet/Haiku subagents. Integrator (Claude) owns the spec, shared docs, merge and verification; subagents owned the arena foundation and one planner each in isolated worktrees. Paths: `Generation/Core/Environment/Arena*`, `ProductMapPlanner`, `CompoundArenaPlanner`, `DepotArenaPlanner`, the arena-aware `CompoundPlan`/`MapSpawnLayout`/`MapTopologyValidation` branches, catalog dispatch in `MapPackageBuilder`/`ReleaseCatalogDocument`/`CompoundCatalogFactory`, and Generation/Game tests. No contract, lock, schema, seed, presentation (T075 leases) or soldier change. Decision: [ADR-045](decisions.md#adr-045--accepted-compact-tdm-arena-layouts-for-catalog-maps-t076).

## T077 — Presentation polish (merged)

Owner-authorized 2026-11-15 Battlefield V / Warface / Bodycam presentation polish. Three worktrees delivered HUD craft, atmosphere/sky-fog, and actors/FX; merged to main at `946a58d` and included in installer 0.3.6+. Software only; VD exits and P05 remain open. See [M11 handoff](milestones/m11-playable-release/handoff.md).

## T078 — Architectural / FP / material readability wave

Owner directed 2026-11-15: continue design until Battlefield V / Warface / Bodycam quality. Base `3c6f640`. Three disjoint worktrees merged to main at `ddce776` (env `d56c34f`, weapons `8e88a68`, materials `b5683e1`). Decision: [ADR-049](decisions.md#adr-049--accepted-non-gameplay-visual-architecture-boxes-for-map-solids-t078).

Delivered software: non-gameplay architectural solids + roof inclusion in `BuildSolidsMesh`; FP near-field/kick/magazine travel/denser FX; concrete/cover/ground/apron material response. Frozen C0/Game-v1, weapon 1.0.1, soldier/P05 (ADR-024), and gameplay collision/nav hashes unchanged. Packaged mesh/package identities change — native republish and installer still required before player review. Temporary `visual/t078-*` worktrees/branches removed after merge preservation. VD/REL acceptance remains Pending.

## T079 — Material-split maps, menu staging, combat presence

Owner-directed continuation 2026-11-15 after T078 merge (`eb81151`). Three worktrees merged to main at `73a18e4`: env `7bfa1ec` (concrete/metal/trim solids + props), menu `5f32a02` (BFV staging depth/materials/cards), combat `0936190` (bob, hit feedback, thinner HUD, denser FX). `TacticalUi` auto-merged. Native republish Verified; Generation 387 / Maps 53 / Simulation 211 / provenance PASS. No P05. Gameplay collision/nav and weapon 1.0.1 unchanged. Temporary `visual/t079-*` worktrees/branches removed after merge. Client/package/installer and VD review remain Pending until recorded in the M11 handoff.

## T080 — Visual review and material-scale correction (2026-11-15)

Owner requested review and implementation after the T079 / 0.3.8 preview. Sole integrator scope: `Presentation/Rendering/ProductSurfaceMaterial.cs`, `ProductMapView.cs`, `Presentation/Session/ProductMenuScene.cs`, `MenuStagingComposition.cs`, their existing Unity tests and affected shared design/delivery docs. No delegation, soldier-generator/clearance search, schema, contract, package or gameplay change. Base `7b05e9e`.

Review found fixed per-object texture repeats on nonuniformly scaled boxes, eightfold denser cover texture than neighbouring metric solids, old 256 m operation-card staging and loss of material submeshes in card renderers. Implement metric face UV adaptation of the verified unit box at upload, consistent two-metre repeats for all map/menu surface families, actual-mesh-bounds card framing, concrete/metal/plaster card roles and scoped card ambient light. Clones preserve source positions/indices/tangents and are owned/released alongside existing uploads. No new texture bytes or runtime procedural content fallback.

Validation: existing source/frozen guards, Contracts/Generation/Game, Unity EditMode/PlayMode including projected bounds/material roles/UV preservation/lifetime, Client/Server isolation, native-content package validation, source-matched upgrading installer, docs/fixtures. Evidence/actual limitations belong in the [M11 handoff](milestones/m11-playable-release/handoff-archive.md#t080--visual-review-and-material-scale-correction). Rollback the presentation commit and rebuild against retained native content; prior 0.3.8 remains a distinct rollback pair. P05 FAIL and all VD/REL acceptance remain open.

## T081 — Architectural surface and place identity

Owner-authorized continued visual review/implementation, 2026-11-15, base `615b6a5`. Sole integrator owns `Generation/Core/Environment/MapSolidsMesh.cs`, `Presentation/Rendering/ProductMapView.cs` / `ProductSurfaceMaterial.cs`, `Presentation/Session/ProductMenuScene.cs`, weapon coating in `ProductActorPresentation.cs`, associated Generation/Unity tests, native publication/build/installer wrappers and shared design/evidence docs. No shared contract/schema/toolchain or soldier-generator change.

Implement ADR-050 packaged wall articulation and recognisable cargo, six shared PBR roles, menu loading-bay facades and restrained weapon coating. Existing collision/nav/spawn identities remain identical; old paired installers remain rollback artifacts. Run Contracts/Generation/Game/Maps, Unity EditMode/PlayMode, provenance/frozen guards, native publication, clean Client/Server/package and upgrading installer build. Capture actual player views; missing full gameplay/reference/installed-upgrade evidence stays Pending. [Handoff](milestones/m11-playable-release/handoff.md) records results and limitations.

## T082 — Solid-FPS bot combat behaviour (2026-11-15)

Owner requested an investigation, then bot behaviour "like in solid FPS games". Base `ffd0754`, rebased onto `e140dd6`; worktree `.local/worktrees/t080-bot-tactics` on branch `t080-bot-tactics`. The branch name predates the discovery that T080 and then T081 (and ADR-050) were taken by concurrent visual work; this card is T082 with ADR-051. Scope:
- `Simulation/AI/**`: new `AiTuning`, `AiNoise`, `AimController`, `FireDiscipline` and `CoverFinder` files, each with a committed `.meta`.
- `Game/Session/OfflineGameSession.cs`: wiring only.
- `Tools/Simulation.Tests/AI/**` and `Tools/Game.Contracts.Tests/TeamBotPlannerTests.cs`.
- The AI feature doc, [ADR-051](decisions.md#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082) and the M11 handoff.

Frozen and unchanged: C0/Game-v1 contracts and fixtures, the 16-ray and 2-path budgets in `tdm-workloads.json`, lock files, map packages, generators and toolchain. No difficulty selector is added (excluded by the TDM spec).

Investigation found:
- **Instant aim.** Aim snapped perfectly in one tick, with no rate limit, error or recovery, contrary to `ai.md`.
- **Nonstop fire.** `FireHeld` was held continuously at every range.
- **No combat movement.** Bots stood still while a target was visible.
- **Naive targeting.** The target was simply the first visible candidate in rotation, and one-tick dropouts re-armed the reaction delay.
- **No hearing or damage reaction.** There was no hearing, and `NotifyDamage` was never called in the product.
- **Cover never used.** `SetCoverSites` was never called in the product, so `SeekCover` always fell through to Search.
- **Stale routes.** An active patrol route blocked every new goal until it finished.
- **Memory wipes on respawn.** `OfflineGameSession` rebuilt the whole planner on every respawn, wiping all bot memory.

Implementation is described in [AI combat behaviour](features/ai.md#combat-behaviour-t082-2026-11-15).

Validation:
- Pure lanes: Simulation, Game, Maps, Contracts, Generation and SourceProvenance.
- Docs, fixture and contract-lock checks.
- New `BotTacticsTests` (15 tests), plus updated AI and planner tests.
- Unity EditMode/PlayMode, client build and real-player feel are listed in the handoff.

Rollback: revert the T082 merge. AI state is not persisted and no content identity changes.

## T083 — First-person gloved hands and continuous reload reach

Owner-authorized visual continuation, 2026-11-15. Sole integrator, base merged T082 `e2c26e9` with docs-only `a9f228d` retained. Paths: new `Generation/Core/Environment/FirstPersonHandGeometry.cs`, `MapVisualClosure.cs` / `MapPackageBuilder.cs`, `Presentation/Rig/ProductActorPresentation.cs`, existing `View/FirstPersonHandling.cs` / `FirstPersonMagazineTravel.cs`, associated Generation/Simulation/Unity tests, content publication/build/installer evidence and shared docs. No soldier-generator/clearance search, C0/Game-v1/N1/schema/toolchain or gameplay change.

Deliver ADR-052 offline packaged rifle/pistol glove/sleeve pairs, material reuse, slot/life/session ownership, support-hand reach and continuous reload path. Preserve T081 architecture and landed T082 bot behaviour. Required checks: Contracts/Generation/Simulation/Game/Maps, Unity EditMode/PlayMode, frozen/provenance guards, docs/fixtures, clean native publication, Client/Server/package/installer and actual player review. Tests/pure evidence cannot close P05, visual comparison, full gameplay, installed-upgrade or reference performance. Rollback the paired closure/runtime changes together. [M11 handoff](milestones/m11-playable-release/handoff.md) records actual results.

## T084 — Near-field surfaces, sleeve shape and hip framing

Owner-authorized continuation, 2026-11-15, sole integrator at `12ff5ac`. Paths: new `Generation/Core/Environment/FirstPersonSurfaceBaker.cs`, existing `FirstPersonHandGeometry.cs`, map closure/builder, `Presentation/Rig/ProductActorPresentation.cs`, `View/FirstPersonHandling.cs`, associated Generation/Simulation/Unity tests and shared design/delivery docs. Preserve T082 bot baseline, all gameplay and frozen generators/locks/settings/toolchain. No delegation or third-person soldier/P05 iteration.

Deliver ADR-053 numeric PBR triplets and strict dependencies, mask-driven binding/resource ownership, shallow sleeve folds and ADS-safe hip composition. Run Contracts/Generation/Simulation/Game/Maps, Unity EditMode/PlayMode, provenance/frozen guards, docs/fixtures, native content publication, clean Client/Server and installer builder; record actual player/device limits in the [M11 handoff](milestones/m11-playable-release/handoff.md). Old paired installers remain rollback artifacts; no comparison/hardware/acceptance waiver.

## T085 — Bot rear awareness, team spread and readable time-to-kill (2026-11-15)

The owner playtested T082 and reported: bots "running after each other", they "don't see enemies behind their back", and time-to-kill "is not obvious and too short". Base `12ff5ac`; worktree `.local/worktrees/t084-bot-awareness`, branch `t084-bot-awareness`. Scope:
- `Simulation/AI/**` (existing files only).
- `Presentation/Hud/CombatHudVisual.cs` and `Presentation/Session/ProductSessionView.cs`, for the incoming-damage direction markers.
- `Tools/Simulation.Tests/AI/**` and `Tools/Simulation.Tests/Hud/CombatHudVisualTests.cs`.
- The AI feature doc, [ADR-054](decisions.md#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085) and the M11 handoff.

Frozen and unchanged: C0/Game-v1 contracts, fixtures, locks, the AI ray and path budgets, weapon damage and rate (`combat.md`), map packages and the toolchain. The concurrent T084 near-field surface work (`2b9c743`, which also took ADR-053) is not touched. This card was drafted as T084 and renumbered; the branch name `t084-bot-awareness` predates that.

Investigation found:
- **Conga line.** Patrol destinations are ordered route corners, and bots picked them by `(entity id + cursor) % n`. Teammates with consecutive ids therefore walked the same route one corner apart.
- **Allies as noise.** Teammates' gunfire was investigated at the teammate's position, and every calm ally in range answered every noise.
- **Blind behind.** There was no footstep or proximity hearing, and the vision cone was 90°. An engaged bot ignored a second enemy shooting it from behind, because damage from a non-target attacker never changed its aim while it had a visible target.
- **Front-loaded accuracy.** Accuracy recovery ran from sighting. The 250–500 ms reaction delay consumed most of the acquisition error, so the opening shots hit and still targets died within about 0.4 s of the first shot.
- **No direction cue.** The player had no direction cue for incoming hits, only a full-screen flash.

Implementation is described in [AI awareness, team spread and lethality](features/ai.md#awareness-team-spread-and-lethality-t085-2026-11-15).

Validation:
- New `BotAwarenessTests` (6 tests) and `BotLethalityTests` (TTK envelope at 8, 20 and 40 m on the real combat harness).
- HUD indicator math tests.
- All pure lanes, the docs, fixture and lock checks, and Unity EditMode/PlayMode after the merge.
- A build, an installer and real-player feel are listed as Pending in the handoff.

Rollback: revert the T085 merge. No persisted state or content identity changes.

## T086 — Sample-bank combat audio (BFV-style feel, ADR-055) (2026-11-15)

Owner authorized replacing the analytic sine+noise beeps with Battlefield V–class combat feel via generated (or provenance-recorded found) PCM banks for this non-commercial build. Sole integrator. Scope:
- `Presentation/Audio/**` (bank baker, bank container, sample mixer, router, settings binding; Unity device output stays callback-only).
- Pure `Tools/Simulation.Tests/Fx/**` audio coverage.
- Shared docs: [ADR-055](decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086), [features/audio.md](features/audio.md), vision/game-shell/combat, M11 handoff, index.

Frozen: C0/Game-v1 contracts and fixtures, weapon damage/rate, map packages, toolchain, ≤24-voice peak-limiter policy, master/effects/UI schema-2 gains. No music score, no commercial-game audio extraction, no middleware (FMOD/Wwise) requirement.

Deliver: deterministic layered cue bank (rifle/pistol shot, impact, damage-taken, footstep, reload start/complete, death, UI, ambient loop); sample mixer with distance atten, stereo pan, HF rolloff; router mapping for reload/death/damage and weapon id; pure tests; FIX10 named device hearing remains Pending.

Rollback: revert T086. Analytic-only mixer is the prior baseline.

## T087 — Knife (slot 3) and grenade (slot 4) (2026-11-15)

Owner-directed combat expansion so humans and bots can still fight when rifle/pistol ammo is empty. Sole integrator. Decision: [ADR-057](decisions.md#adr-057--accepted-knife-and-grenade-loadout-slots-t087).

Scope:
- C0 prediction schema 2 / `Require.Slot` 0..3 / `WeaponAction.Cooking`, fixtures + `contract-lock.json`.
- `Simulation/Combat` knife melee + cook/throw grenade projectile/blast; four-slot loadout in the combat harness.
- Game-v1 additive `Slot3`/`Slot4` bindings; local input / HUD / presentation; AI empty-ammo knife + single grenade.
- Procedural knife/grenade meshes packaged in map visuals (`LoadoutPropGeometry`); package identities change → native republish for player review.

Frozen otherwise: toolchain, Game-v1 settings schema 2 layout, rifle/pistol recipes (later T088 bumps to 1.0.2), friendly-damage policy, online wire migration (deferred).

Validation: Contracts/Simulation/Game/Generation/Maps pure suites, Unity EditMode where touched, provenance, docs/fixtures/manifest. Manual gameplay and native republish evidence remain Pending in the M11 handoff.

Rollback: restore prior contract-lock/fixture/prediction-schema-1 pair and two-slot combat kit together.

## T089 — Stereo combat WAV bank and footstep distance (ADR-062) (2026-11-15)

Owner playtest: weapon audio still felt synthetic and bot footsteps were audible across the map. Sole integrator. Decision: [ADR-062](decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089).

Scope:
- `Presentation/Audio/**` — stereo bank load (`WavPcmReader`, `CombatAudioManifest`, path resolve), mixer true L/R + mild azimuth, per-kind distance (`CombatAudioDistance`), router cull.
- `Content/Audio/combat/*.wav` + `manifest.json` (provenance + hashes); `Tools/generate_combat_audio_bank.py` authoring helper.
- Client/`Package-Release` copy bank beside player under `Content/Audio/combat` (not under `Assets`).
- Pure `Tools/Simulation.Tests/Fx/ProductAudioTests.cs`; docs/audio/ADR/handoff/index.

Frozen: C0/Game-v1, ≤24-voice peak limiter, schema-2 gains, no FMOD/Wwise, no commercial-game extraction.

Deliver: stereo product bank path; footsteps hard-culled beyond 18 m; gunfire retains longer range; FIX10 named hearing remains Pending.

Rollback: revert T089; prior T086 mono/soft-distance path.

T089 owner follow-up, 2026-11-15: implement the reviewed first audio improvement pass (camera-relative positioning, rate-correct playback, recorded-PCM gunfire body/reflections, priority/limiter and bounded map-collision acoustics). Scope additionally includes `Presentation/Session/ProductSessionView.cs` and its real Unity integration test in `Tests/EditMode/ProductViewBindingTests.cs`. Existing CC0 bank, settings/contract/content formats, toolchain and ≤24 voices stay fixed. See [audio refinement](features/audio.md#owner-requested-spatial-and-mix-refinement--2026-11-15) and [handoff](milestones/m11-playable-release/handoff.md#audio-spatial-and-mix-refinement--2026-11-15). Listening and reference acceptance remain Pending.

## T090 — Bot tactics: cover peek, wall hearing, ledge strafe, route spread (2026-11-15)

Owner approved the bot-AI improvement plan Phase 1. Sole integrator. Decision: [ADR-070](decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090).

Scope:
- `Simulation/AI/**` (`CoverFinder`, `CombatAiDirector`, `AiTuning`, `AiTypes`).
- Pure `Tools/Simulation.Tests/AI/**` (new `BotTacticsT090Tests` plus budget assertion update).
- Docs: [features/ai.md](features/ai.md), ADR-070, M11 handoff, this card.

Frozen: C0/Game-v1, ≤16 AI rays / ≤2 new paths per tick, weapon damage/rate, T085 lethality envelope, no difficulty selector, no map cover-proxy bake, toolchain.

Deliver: peek-preferring cover probes with blind fallback; wall-aware gunfire/footstep hearing; ledge-safe strafe ground probes; route-corridor spread penalties; counters; pure tests. Owner feel / installer / `SHOOTER_AI_SUMMARY` remain Pending.

Rollback: revert T090; restore T082/T085 AI behaviour.

## T088 — Full weapon visual overhaul (VD3 slice+) and fire FX

Owner-authorized 2026-11-15. Sole integrator. Decision: [ADR-061](decisions.md#adr-061--accepted-geometryweapon-102-mechanical-parts-and-denser-fire-fx-t088).

Scope:
- `geometry.weapon` **1.0.2**: richer rifle/pistol body LODs; separate packaged magazine + cycle (bolt/slide) meshes; sockets/collision mechanical dims unchanged; reject 1.0.0/1.0.1.
- Presentation: bind body/mag/cycle for FP + world; mag travel from reload phase; cycle impulse on `ShotAccepted`; reduced-motion scales.
- Analytic FX: denser muzzle, `ShellCasing`, `Tracer`; `ReducedFlash` suppresses muzzle+tracer.
- Paths: `Generation/Core/Weapons/`, `MapVisualClosure` / `MapPackageBuilder`, `Presentation/View` / `Rig` / `Fx` / `Session`, Generation/Simulation tests, feature lock/fixtures, native republish + installer evidence docs.

Frozen: C0/Game-v1 combat rules, ammo/timing, soldier/P05 (ADR-024), toolchain, settings schema 2. No articulated fingers, no imported art/clips.

Validation: Generation/Simulation/Maps/Contracts/Game as touched, provenance, docs/fixtures/manifest, native publication, Client/Server/package/installer. Named visual acceptance Pending.

Rollback: restore geometry.weapon 1.0.1 lock/fixture pair and prior presentation/FX together.

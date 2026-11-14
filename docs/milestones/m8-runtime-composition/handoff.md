# M8 — Runtime composition and Game-v1 foundation — handoff and gate record

T074 delivery adds read-only menu visual-byte access and cosmetic presentation, without changing C0/Game-v1/content schemas. Reproduced loading-to-play overload was repaired by rebasing the first active clock sample (`e3851f1`): foundation regression 73/73 preserves genuine active overload and fractional debt. The independent bot fix remains intact; its client measurement assembly dependency was repaired. [M11 visual-preview evidence](../m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview) identifies clean b531da9 builds, menu startup and the owner-stopped interactive review. RUN/player, reference and P05 acceptance are not closed.

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M8 integration validation remains open. T066 provenance/cancellation and T067 packaged catalog/visual loading are repaired; T068 authority-driven weapon/action views are landed in the retained `7cd7e81` candidate. Native-navigation publication/query semantics and actual RUN/player/repeated-life evidence remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.


## T072 — Final slide validation

Source 5eaced5 has clean Game 56/56, EditMode 26/26, PlayMode 13/13, Client/Server and package passes; the independent extracted executable reaches Main. The [M11 delivery record](../m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery) records actual paths/hash, prior failing/dirty attempts and cleanup. Final native tests cover real slide collision/low-ceiling recovery across quality tiers; named both-map gameplay/visual/device/performance reviews remain Pending. The current [guide](../../playtest-guide.md) points to the slide-enabled preview; final installer/upgrade delivery is planned T073.

## T072 — Owner-requested local sprint slide — implementation record

Owner Max requested **implementation now** of slide (подкат), default Left Shift + F, on 2026-11-14. ADR-039 and the T072 card bound this work to local motor/input/session/read-only views/settings/tests/build/docs; no agents, online rollout or waiver. The old 7cd7e81 ZIP is retained unchanged and has no slide.

Software: local command envelope carries a buffered SlidePressed edge with the C0 command; the authority accepts it only with a fresh command for the current human life. Motor owns crouch capsule/eye pose, 6.5 m/s entry, 4 m/s² decay, ≤45 ticks, 60-tick cooldown, jump/air/wall cancellation and safe stand recovery. Read-only movement view now drives camera/rig crouch; no animation-driven motion. Schema-2 Slide binding/defaults/rebinding/legacy-F preservation are implemented under the reviewed additive Game-v1 lock; C0/N1/content/toolchain formats stay unchanged.

Implementation-time evidence at `77bfa93` + working changes (sourceDirty=true): Contracts 72 PASS (`20261114T161803424Z-043e9d72`), Generation 319 PASS (`20261114T161814495Z-5eb83155`), Simulation 119 PASS (`20261114T161911919Z-5a6e4fce`), Game 55 PASS (`20261114T162353106Z-1d18c101`). Runs live under their usual Artifacts milestone/77bfa93de99ef98fc5232f73e9946b4628fad752 directories. Provenance passes, 662 entries / 331 GUIDs; specification fixtures pass 35 assertions. The initial nullable compile failure is retained (`20261114T161713482Z-46e6fbfe`), then repaired before reruns.

Unity EditMode initially failed **before project compilation**: editor Unity.dll could not load; dependency inspection found missing compress_bc7e.dll. Failure retained at `Artifacts/m0-foundation/77bfa93de99ef98fc5232f73e9946b4628fad752/tests-editmode-20261114T161720443Z-808d8f64/`. Exact 6000.3.28f1 installer redownload verified against original MD5 aa4e924970ec074fe7da2cb711b3fd49 and valid Unity Technologies signature; the missing factory runtime/SDK files were restored from the authenticated isolated installation (10,017 files / 2.15 GiB; existing files and PackageManager trees untouched), without upgrading editor/packages. Configure-Unity PASS at `configure-unity-20261114T165406204Z-09c7e415`; repaired-editor EditMode PASS at `tests-editmode-20261114T165547008Z-c207f4a6` (856d628 + bounded-binding migration fix, sourceDirty=true). Restore hashes/proof remain in `Artifacts/unity-setup/t072-editor-runtime-restoration.json`. Final Game migration rerun PASS 56/56 (`20261114T165540759Z-89d58830`), including full-table preservation and preallocation count bounds. Final source-matched checks/build/package are recorded in the M11 T072 delivery record when complete. Interactive both-map slide/UI/audio/performance review remains Pending; all existing release gates/P05 FAIL remain unchanged.


## Current source/evidence handoff

T066 baseline is repaired: fresh Contracts 72, Generation 319, Simulation 110 and Game 51 pass; provenance passes (660 entries / 330 GUIDs). Retained clean `7cd7e81` EditMode 24/24, PlayMode 12/12, Client/Server and ZIP reports were inspected, not rerun here. ADR-038 catalog, actual packaged visual payloads, executable-relative defaults/recovery and T068 slot/action views exist. T067 remains partial: normal publisher still produces software-grid NVB1; native bridge publication and partial/fallback semantics/load/clearance need completion. Native installer/open-quad tests do not establish real two-map routes or repeated-life player acceptance. See [audit C01–C05](../../product-state-audit.md); old failures below stay historical.

## Historical review — 59316d3 (superseded)

T052–T056/T061 software is committed in 59316d3. Fresh baseline: Contracts 72 PASS, Generation 310/311 FAIL (cancellation/lock), Simulation 101 PASS, Game 38 PASS; provenance FAIL on 19 missing metadata entries. No fresh Unity/player run. T066 repairs baseline; T067 finishes actual packaged visual/native nav delivery and T068 weapon/action views. Historical software runs below retain their original sourceDirty flags.

## T052 / FIX01 regression repair — 2026-11-14

T052 repaired the stale `Tools/Contracts.Tests/packages.lock.json` project graph using locked SDK 10.0.301 and the existing local package cache. The only dependency-graph change is the already-present `Shooter.Foundation` reference to `Shooter.Generation.Core` plus that project's transitive `Shooter.Contracts` edge; package names, resolved package versions, SDK, C0, Game-v1, fixtures and runtime semantics are unchanged. The original audit failure remains preserved at `Artifacts/m0-foundation/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T094640978Z-4e9545e2/`; the assignment-time reproduction is `Artifacts/m0-foundation/6329262446428f92617600d8526b13f9c90a358a/20261114T095616966Z-97bf26a5/` and fails before tests with NU1004.

Strict shared TRX validation now rejects missing or zero-test results, any unexecuted/skipped/aborted result, result-row/count disagreement and test failures. `Tools/test_test_results.ps1` passes its positive case and rejects zero, skipped, failed and missing-result cases. Required offline wrappers ran from source commit `6329262446428f92617600d8526b13f9c90a358a` with the T052 working tree dirty:

| Command | Result | Evidence |
|---|---|---|
| `Tools/Test-Contracts.ps1 -Offline` | PASS — 71 total/executed/passed, 0 failed, 0 skipped; no NU1004 | `Artifacts/m0-foundation/6329262446428f92617600d8526b13f9c90a358a/20261114T095801909Z-19547e7e/` |
| `Tools/Test-Generation.ps1 -Offline` | PASS — 298 total/executed/passed, 0 failed, 0 skipped | `Artifacts/m1-procedural-feasibility/6329262446428f92617600d8526b13f9c90a358a/20261114T095826167Z-1fc6d040/` |
| `Tools/Test-Simulation.ps1 -Offline` | PASS — 61 total/executed/passed, 0 failed, 0 skipped | `Artifacts/m2-combat-harness/6329262446428f92617600d8526b13f9c90a358a/20261114T095855553Z-a5ddc132/` |
| `Tools/Test-Game.ps1 -Offline` | PASS — 15 total/executed/passed, 0 failed, 0 skipped | `Artifacts/m8-runtime-composition/6329262446428f92617600d8526b13f9c90a358a/20261114T095913280Z-960cf9d0/` |
| `Tools/Test-Maps.ps1 -Offline` | PASS — 6 filtered tests total/executed/passed, 0 failed, 0 skipped | `Artifacts/m8-runtime-composition/6329262446428f92617600d8526b13f9c90a358a/test-maps-20261114T095933307Z-ed9bd714/` |

FIX01 is PASS for repaired pure regression execution. Offline advisory lookup is separately `PendingNetwork` by design and is not represented as a test PASS. This clears the FIX01 prerequisite for T053 and T054; it does not accept their lifecycle, pause or input behavior and does not change the remaining M8 disposition.

## T061 / FIX10 audible product audio — 2026-11-14

T061 binds ordered product combat/footstep/UI cues to a real Unity audio device path. `ProductAudioEventRouter` consumes combat `EventId`s once, emits stride footsteps, and plays UI beeps. `SynthAudioMixer` synthesizes device-rate PCM with ≤24 voices, peak limiter, distance attenuation and master/effects/UI gains applied at mix time (including existing voices). `UnitySynthAudioOutput` installs `AudioSource` + `OnAudioFilterRead` (no recorded samples). `OfflineGameSession.TryLeaseCombatEvents` and `ProductSessionView` pump events each frame; stop/clear detaches and silences stale sessions. Callback never mutates authority.

| Check | Result | Evidence / limitation |
|---|---|---|
| Pure Fx suite (`FullyQualifiedName~Fx`) | PASS — 9/9, zero skipped | `dotnet test Tools/Simulation.Tests/... -c Release --filter "FullyQualifiedName~Fx"` (voice cap/limiter/attenuation/gains/mute, event dedupe, footstep/UI, stale detach, device-rate Fill) |
| View+Fx regression | PASS — 16/16 | same project filter `View\|Fx` |
| Named physical device / reviewer audible sign-off | **Pending** | Waveforms alone do not claim FIX10 device PASS |
| T060 settings applier binding | **Bound (software)** | `UserSettingsAudioBinding` / `ProductClientSettingsApplier.Apply` → `SetChannelGains(Master, Effects, UI)`; SET02 named device review still Pending |

FIX10 software path is implemented. Full FIX10 remains **Pending** until named device review hears shot/impact/footstep/UI on a real output and T060 binds frozen preferences through the applier.

## T062 / FIX11 visual review tooling — 2026-11-14

T062 lands initial product capture/overlay tooling and an honest defect inventory on the existing T056 presentation path. Live pose metrics (arm/body clearance, eye/sight residual, grip/plant/stock) are retained by `ProductActorPresentation`; `ProductVisualReviewOverlay` draws eye→sight / clearance cues and can write JSON sample batches under Artifacts when enabled with `-shooterVisualReview 1` / `-shooterVisualReviewOutput`. Catalog/inventory builders and `Tools/Inventory-ProductVisualDefects.ps1` record named P05 and product readability defects without claiming PASS.

| Check | Result | Evidence / limitation |
|---|---|---|
| Defect catalog + inventory builder (pure View) | PASS — `ProductVisualDefectCatalogTests` | `dotnet test Tools/Simulation.Tests/... -c Release --filter "FullyQualifiedName~ProductVisualDefect"` |
| Python inventory package guard | PASS | `python Tools/test_product_visual_defect_inventory.py` |
| Inventory package writer | ToolingReady | `./Tools/Inventory-ProductVisualDefects.ps1` → `Artifacts/m8-runtime-composition/<commit>/visual-defect-inventory-<utc>/` |
| P05 visual acceptance | **FAIL retained** | ADR-024; no fourth clearance/parameter search; generator/recipe sources unchanged |
| Named product-player capture review / REL03 | **Pending / Open** | Overlays + inventory only; unresolved P05-D01/D02 keep FIX11/REL03 open |

Durable inventory pointer: [visual-defect-inventory.md](../../visual-defect-inventory.md). FIX11 software disposition is **ToolingReady**. Full FIX11 remains blocked on owner-authorized character work (not granted here) plus named captures showing defects resolved/unresolved. Historical failed P05 artifacts are immutable.

## T056 / FIX05 visible product presentation — 2026-11-14

T056 binds read-only product presentation to verified map-package solids and authority lifecycle snapshots. `ProductMapView` installs MeshRenderers for packaged collision/cover (no diagnostic gallery launch, no imported art). `ProductActorPresentation` reuses `SoldierGeometry`/`WeaponGeometry`/`SoldierMotion` with team-distinct materials, held rifles, FP local weapon spring, and cosmetic corpse caps. `ProductActorBinder` reconciles spawn/despawn/life-replacement handles. Bootstrap client composition reads `FoundationEntry.ProductMap` (T055 package-load seam) only; World collision install is unchanged. Presentation does not mutate authority.

| Check | Result | Evidence / limitation |
|---|---|---|
| Pure View suite (`FullyQualifiedName~View`) | PASS — 7/7, zero skipped | `dotnet test Tools/Simulation.Tests/... -c Release --filter "FullyQualifiedName~View"` (includes `ProductActorBinderTests` + prior `ViewModelTests`) |
| Unity EditMode product view binding | NotRun this handoff | `ProductViewBindingTests` added under `Tests/EditMode/`; requires locked editor run |
| Source-matched product-player captures (both maps) | **Pending** | FIX05 player visual review / captures not executed in this assignment |
| Stale-handle policy (20 loads / 100 cycles in player) | **Pending** | Pure binder + EditMode cover reconcile/clear; alternating product loads owned with T059 shell |

FIX05 software wiring is implemented. Full FIX05 remains **Pending** until source-matched product-player captures on each map show terrain/cover, team-readable actors, weapons and authority-bound motion, and until player stale-view cycles are evidenced. T059 owns functional shell/menu start-stop; T061 owns audible device path; T062 owns P05 visual defect acceptance.

## T068 / FIX05 authority-driven weapon and action views — 2026-11-14

T068 finishes presentation from real authority without Game-v1 contract changes. `ProductAuthorityActionView` consumes `ActorLifecycleState.ActiveWeaponSlot` plus combat `ReloadStarted`/`ReloadCompleted`/`ShotAccepted`/`Death` events (`ActionPayload` start/end ticks for slot-correct duration). `ProductActorPresentation` switches packaged rifle/pistol meshes (held + local FP), drives reload/fire from those events, and never infers reload from magazine decrease. `ProductSessionView` leases combat events once per frame for actors, audio and accessibility cues. SoldierMotion remains the Generation rifle-socket spike; pistol mesh is re-anchored at the right hand. P05 generator parameters are unchanged.

| Check | Result | Evidence / limitation |
|---|---|---|
| Pure View suite (`FullyQualifiedName~View`) | PASS — 18/18, zero skipped | Includes 8 `ProductAuthorityActionViewTests` (slot, shot≠reload, rifle/pistol durations, life replacement, FP filter) |
| Pure Fx regression | PASS — 10/10 | Unchanged audio path with shared event lease |
| Unity EditMode authority presentation | NotRun this handoff | `ProductViewBindingTests.ProductActorPresentation_ConsumesActiveWeaponSlotAndReloadEventsNotAmmoDrop` added; locked editor run Pending |
| Source-matched player captures / FIX05 profile lifecycle load/traversal | **Pending** | Honest Pending; no player captures claimed |

T068 software closes B06. Full FIX05 player evidence remains **Pending**.

## T054 / FIX03 pause and buffered-input repair — 2026-11-14

T054 makes the Bootstrap fixed-tick clock explicitly suspendable. Product pause, non-playing phases and client focus loss stop calls into `OfflineGameSession.Step`; suspended wall time is discarded while the fractional pre-pause debt and next tick identity are retained. Focus loss pauses only a running product session, focus return resumes only that focus-owned pause, and both transitions reset the wall-clock baseline. Diagnostic composition and its overload behavior remain separate and unchanged.

Local input now captures keyboard/mouse state once per render frame and consumes it at fixed ticks. Press edges survive zero-tick render frames and clear after one tick; held state can feed multiple ticks; mouse delta changes the absolute aim once; movement is normalized; sensitivity is degrees per mouse-delta unit; yaw/pitch stay inside the C0 receive interval; and selected weapon state persists in subsequent commands and across pause/focus. Pause, focus/menu ownership and replacement-life boundaries discard captured action edges. Focus/menu recapture suppresses held fire and the first look delta until release/new input, preventing UI clicks or cursor warp from becoming gameplay actions.

| Check | Result | Evidence / limitation |
|---|---|---|
| Focused `FoundationTests` | PASS — 9/9, zero skipped | `dotnet test Tools/Contracts.Tests/Contracts.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~FoundationTests"`; includes ten-second suspension/no-catch-up ordering |
| Unity EditMode | PASS — 18/18, zero skipped; five T054 input cases including 30/60/144 FPS | `Artifacts/m0-foundation/6329262446428f92617600d8526b13f9c90a358a/tests-editmode-20261114T100835735Z-5af4d31a/` |
| Pure Game-v1/session suite | PASS — 20/20, zero skipped | Direct locked `dotnet test ... -c Release --no-restore`; this includes concurrent T053-owned lifecycle work and is not claimed as FIX02 acceptance |
| Source provenance | PASS — 540 asset entries / 270 GUIDs | `Tools/Test-SourceProvenance.ps1`; source/provenance only |

The first Unity attempt failed compilation on an ambiguous `EntityId`; the next two ran the new tests and exposed boundary-value test failures. All failed evidence is retained beside the final run. `Tools/Test-Game.ps1 -Offline` could not access its sandbox-local NuGet cache and recorded zero tests at `Artifacts/m8-runtime-composition/6329262446428f92617600d8526b13f9c90a358a/20261114T100920509Z-0fd06237/`; the direct no-restore run above is reported separately rather than relabeling that wrapper failure.

FIX03 software behavior is implemented and the automated timing/input cases pass. Full FIX03 remains **Pending**: no source-matched product-player run held pause and OS focus loss for ten real seconds, and no physical keyboard/mouse UI-click review was performed. T053 owns authority retirement/respawn internals; T054 only prevents their advancement while Bootstrap is suspended and does not claim T053 lifecycle acceptance.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software)**, 2026-11-13. Product OfflineGameSession is the default Bootstrap composition. Diagnostic floor/box retained for `-shooterSmokeSeconds` / gallery / benchmark. Historical M0–M7 waivers and P05 FAIL (ADR-024) unchanged. No inherited hardware waiver; named visual/P05 acceptance remains open until T049.

| Field | Historical recorded disposition |
|---|---|
| Owner / reviewer / base / final main commit | Cursor M8–M11 integrator (bc-c6e6a4b4); base `5fb3e30`; see coordination `m8Close` |
| Tasks | T035–T039 |
| Game-v1 / C0 / content / toolchain / workload identities | **Game-v1 frozen** (ADR-034); C0-v1 unchanged; TDM workloads defined |
| Build / packaged content / executable paths and SHA256 | IL2CPP client + server Build/Smoke PASS; product composition READY evidenced |
| Pure / Unity / player / visual checks | RUN00–RUN01 PASS; RUN02 software PASS (CompoundMapRuntime); RUN03 product READY PASS / named human review Pending; RUN04 diagnostic Smoke PASS + product pause API; RUN05 captures Pending / P05 retained |
| Reference hardware / counters / thermal review | PendingHardware / NotRun |
| Scope limitations / rollback pair | P05 FAIL retained (ADR-024); named human vertical review not claimed PASS |
| Known entry blocker | Cleared |

## Deliver before downstream assignment

| Artifact | Status |
|---|---|
| Game-v1 source/fixture/lock | Done |
| Authority lifecycle adapter | Done |
| Team deathmatch pure reducer | Done |
| Packaged compound + real collision/nav | Done (`CompoundMapRuntime` + planner catalog spawns) |
| Client input/camera/HUD/audio product path | Done (`LocalPlayerInput`, `ProductSessionView`, synth mixer) |
| Vertical product player review | Product READY evidenced; named human review Pending |
| Frozen workloads | Done |

## Task and acceptance ledger

| Task | Status |
|---|---|
| T035 — Freeze product boundary first | **PASS** |
| T036 — Extract and compose real authority | **PASS** |
| T037 — Compile and install the compound | **PASS** (Compile-GameContent / Test-Maps wrappers + CompoundMapRuntime) |
| T038 — Connect the client path | **PASS** (software) |
| T039 — Run and review the vertical player | **PASS** software / **Pending** named review |

| ID | Result | Evidence |
|---|---|---|
| RUN00 | PASS | Provenance repair retained |
| RUN01 | PASS | `Tools/Test-Game.ps1 -Offline` → 15/15 |
| RUN02 | PASS | CompoundMapRuntime PhysX install + RevisionedPathAdapter corners; fake rejected |
| RUN03 | Partial | Product READY + composition log; named keyboard/mouse review Pending |
| RUN04 | PASS | Diagnostic client/server Smoke PASS; pause/resume API covered in pure tests |
| RUN05 | Pending | T062 overlay/inventory ToolingReady; P05 FAIL retained (ADR-024); named live triage / hardware counters Pending |

### Player evidence

- Configure-Unity PASS: `Artifacts/m0-foundation/03fa6dd.../configure-unity-20261113T225812175Z-205c4638`
- Build Client PASS: `.../build-client-20261113T225853642Z-b4041c8e`
- Build Server PASS: `.../build-server-20261113T230217807Z-6414f348`
- Smoke Client PASS: `.../smoke-client-20261113T230344429Z-fd01d64a`
- Smoke Server PASS: `.../smoke-server-20261113T230356298Z-a8fb5fb8`
- Product READY: `Artifacts/m8-runtime-composition/product-ready-final.log` (`SHOOTER_COMPOSITION mode=product`, `SHOOTER_READY_CLIENT`)

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

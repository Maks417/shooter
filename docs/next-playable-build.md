# Next playable build — finish playtest and native navigation

**Kill-streak / headshot score badges — 2026-11-15 (source):** ADR-073 extends ADR-059 local awards with life-scoped DOUBLE/TRIPLE/MULTI kill ribbon+feed labels and an extra HEADSHOT feed badge; Score math unchanged. Pure Hud/session award tests in this change set; named 720p review Pending. [Handoff](milestones/m11-playable-release/handoff.md#kill-streak-and-headshot-score-badges--2026-11-15).

**Kill-feed weapon/headshot icons — 2026-11-15 (source):** elimination rows replace the arrow with procedural Fill weapon silhouettes plus a headshot mark, correlated from same-tick `ShotAccepted`/`DamageApplied` (C0 DeathPayload unchanged). Pure Hud tests in this change set; named 720p review Pending. [Handoff](milestones/m11-playable-release/handoff.md#kill-feed-weapon-and-headshot-icons--2026-11-15).

**ADR-072 weapon profiles — 2026-11-15 (source):** DefinitionId-keyed combat catalog + visual profiles; `geometry.weapon` 1.0.5 silhouette (`standard`/`carbine`); proof primary `weapons/rifle-carbine` packaged beside classic rifle; default loadout still rifle. C0 PredictionSchema unchanged. Pure catalog/visual/generation tests in this change set; content republish / named playtest Pending. [Handoff](milestones/m11-playable-release/handoff.md#adr-072--weapon-profiles).

**Combat feel — 2026-11-15 (source):** auto-reload on empty mag + louder reload soft cues; refuse/leave empty grenade slot; M67-style procedural grenade; bot hard-stuck spawn relocation. Simulation 308 / Generation 430 PASS; software + native content republish PASS; installer / named playtest Pending. [Handoff](milestones/m11-playable-release/handoff.md#combat-feel--auto-reload-empty-grenade-m67-grenade-mesh-bot-stuck-rescue--2026-11-15).

**ADR-071 Oil Depot + elevated routes — 2026-11-15 (source):** third catalog map `oil-depot` (seed `460072032`) with Y-aware connectivity, walkable stairs/catwalk, shell/workloads unlock; packages become 12 (3×4). Pure ArenaRules/oil tests and catalog/workload assertions in this change set; software/native republish and named playtest Pending. [Handoff](milestones/m11-playable-release/handoff.md#adr-071--oil-depot-third-map--elevated-routes--2026-11-15).

**T090 bot tactics — 2026-11-15 (source):** cover peeks, wall-aware hearing, ledge-safe strafe and route-corridor spread inside unchanged 16-ray/2-path budgets ([ADR-070](decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090)). Pure AI suites 54/54 PASS; owner feel / installer Pending. [Handoff](milestones/m11-playable-release/handoff.md#t090--bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread--2026-11-15).

**Weapon visual preview — 2026-11-15:** installer **0.3.14** at `Artifacts/deliveries/Shooter-Setup-0.3.14.exe`, clean binary/content source `b8235a3`. Improves rifle, pistol, knife and grenade geometry and spatial PBR finishes; Contracts 76, Generation 402, Unity EditMode 57 / PlayMode 21, native publication, clean Client, smoke, package and installer builders PASS. Installation/upgrade, first-person/world visual review, reference performance and acceptance remain Pending. [Exact evidence and limitations](milestones/m11-playable-release/handoff.md#arsenal-delivery-0314).

**ADR-066 UI copy + Barlow Condensed fonts — 2026-11-15 (source):** OFL typeface bank in `Presentation/Fonts/Resources`, `UiFonts`/`TacticalUi` wiring, player-facing `ShellUiCopy`/`InsertionScreenCopy` rewrite, provenance `namedFontSources`, closure `ui-typeface-bank-v1`; pure tests Pending named 720p readability/installer. [Handoff](milestones/m11-playable-release/handoff.md#adr-066--ui-copy--barlow-condensed-typeface-bank--2026-11-15).

**ADR-067 soldier pose + motor launch — 2026-11-15 (source):** motion.v5 slide dive pose, gait hysteresis + auth speed readout, motor corner anti-launch; pure motor/motion tests PASS; player/bot pose review and installer Pending. [Handoff](milestones/m11-playable-release/handoff.md#adr-067--soldier-pose--motor-corner-launch-fixes--2026-11-15).

**T087 follow-up knife/grenade presentation — 2026-11-15 (source):** ADR-064 FP slash/stab + cook/throw + flying grenade; rising-edge cook after equip; knife no longer uses gun muzzle/audio; Game-v1 `WeaponAction` on lifecycle; pure tests Pending player/installer. [Handoff](milestones/m11-playable-release/handoff.md#t087--knife-slot-3-and-grenade-slot-4-adr-057).

**ADR-063 hit damage coefficients — 2026-11-15 (source):** head×4 / torso×1 / limbs×0.5 (rifle one-shot head, ~4 chest, ~7 limbs); C0/base weapon damage/T085 aim unchanged; pure combat tests + docs Pending player feel. [Handoff](milestones/m11-playable-release/handoff.md#adr-063--hit-region-damage-coefficients--2026-11-15).

**ADR-065 PiP rifle scope — 2026-11-15 (source):** rifle ADS keeps hip FOV with screen-space blurred periphery + digital lens zoom (no opaque full-screen tube; no RTT optics); pistol irons unchanged; pure optic tests in this change set; named optic review Pending. [Handoff](milestones/m11-playable-release/handoff.md#adr-065--pip-rifle-scope-blurred-periphery--2026-11-15).

**Visible insertion loading — 2026-11-15 (source):** match/boot insertion briefing paints before sync load/bind (`DrawInsertionScreen`, paint yields, `BootLoadingHost`); pure shell copy tests PASS; named player review Pending. [Handoff](milestones/m11-playable-release/handoff.md#visible-insertion-loading-screens--2026-11-15).

**Analytic death collapse — 2026-11-15 (source):** `soldier.motion.spike.v4` cosmetic DeathPhase collapse replaces crouch-idle corpses; pure motion tests PASS; player/installer Pending. [Handoff](milestones/m11-playable-release/handoff.md#analytic-death-collapse-soldiermotionspikev4).

**T088 follow-up rifle POV fire — 2026-11-15 (source):** crossed capsule muzzle flame (no sphere bubbles); product path spawns dual lobe + tracer + casing; ReducedFlash keeps casings; stronger hip kick/roll + longer bolt travel; soft local overlay; pure FX/view tests; named fire review Pending. [Handoff](milestones/m11-playable-release/handoff.md#t088-follow-up--rifle-pov-fire-presentation).

**T088 weapon visual overhaul — 2026-11-15 (source):** ADR-061 `geometry.weapon` 1.0.2 richer rifle/pistol LODs, detachable mag + bolt/slide, denser muzzle/casing/tracer FX; native republish and named visual review Pending. [Handoff](milestones/m11-playable-release/handoff.md#t088--weapon-visual-overhaul-adr-061).

**T089 stereo combat audio — 2026-11-15 (source):** ADR-062 stereo WAV bank under `Content/Audio/combat` + per-kind distance (footsteps 18 m); T086 mono/soft-atten path superseded for product; pure audio tests PASS; FIX10 named hearing and installer Pending. [Handoff](milestones/m11-playable-release/handoff.md#t089--stereo-combat-wav-bank-and-footstep-distance-adr-062).

**T086 sample-bank audio — 2026-11-15 (source, historical):** ADR-055 generated PCM combat bank replaced beep synth; superseded by T089 for product stereo/distance. [Handoff](milestones/m11-playable-release/handoff.md#t086--sample-bank-combat-audio-adr-055).

**Latest combined preview — 2026-11-15:** installer **0.3.12**, clean binary/content source `50e8939`. It combines the T084 near-field PBR finishes, hip framing and continuous sleeve loft with the T085 bot update: teammates spread out, bots hear footsteps and turn on unseen attackers, time-to-kill on a still target is about 1.6 s at 8 m, and HUD markers point at whoever hit the player. Unity EditMode 53 / PlayMode 21, native publication (one transient failed attempt, retry PASS), clean Client/Server, server smoke, package and installer builders PASS. Installation/upgrade, player review, bot feel and all P05/VD/REL acceptance remain Pending. [Exact evidence and installer](milestones/m11-playable-release/handoff.md#release-0312). Older entries below are historical.

**Latest T083 preview — 2026-11-15:** installer **0.3.11**, clean binary/content source `df64aa8`, adds packaged first-person gloved hands/sleeves for both weapons, analytic support-hand reach and a continuous reload remove/insert arc. Includes the merged T082 bot baseline. Unity EditMode 53 / PlayMode 21, native publication, clean Client/Server and package/installer builders PASS. Depot 6v6 rifle hands observed; automation stopped on live player movement and left the game open. Live pistol/reload/ADS, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact evidence and installer](milestones/m11-playable-release/handoff.md#t083-delivery-0311). Older entries below are historical.

**Latest T081 preview — 2026-11-15:** Windows installer **0.3.10**, clean binary/content source `e140dd6`, adds packaged panelled walls, bevels/footings/coping, painted cargo with rails/corrugation/locks, map/menu material separation and rougher coated weapons. Native publication, clean Client/Server, PlayMode and package/installer builders PASS. Both gameplay roots/catalog records match 0.3.9 exactly. Limited player review observed Main/cards and Compound 6v6; automation stopped on live user input, leaving the player open. Depot close-up, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact delivery evidence](milestones/m11-playable-release/handoff.md#t081-delivery-0310). Older entries below are historical.

**Latest T080 correction preview — 2026-11-15:** installer **0.3.9**, clean binary source `beac9ae`, fixes physical surface scale, overview framing and discarded menu submeshes. Client/Server and native package/installer builders PASS; limited automated player review loaded Depot 5v5 and Compound 6v6 from a short verified copy. This remains visibly below BFV quality; soldiers, hands/actions and block architecture need further work. Deep evidence-path content loading fails; installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain open. [Actual evidence and installer](milestones/m11-playable-release/handoff.md#t080-delivery-039). Older entry pointers below are historical.

**Current T075 design preview — 2026-11-15:** Windows installer **0.3.4**, clean binary source `7407bc4`, implements procedural PBR map surfaces, textured menu staging, working actual-map cards and analytic first-person handling with LOD0 equipment on Low. Unity **43 EditMode / 17 PlayMode**, clean Client/Server, eight native packages, provenance and package/installer builders PASS. Automated player review observed both map cards and Compound 5v5 / Depot 6v6 loads; this is not full gameplay or comparative visual acceptance. P05 FAIL, reference performance and M8–M11/VD exits remain open. [Identities, evidence and limitations](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

T074 update: the [0.3.1 visual preview](milestones/m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview) at clean b531da9 is built with native packages, a scene-led shell and compact tactical HUD. The final interactive review was owner-stopped, not accepted. Preserve the remaining gameplay/native-route/device/reference/P05 queue below; older candidate and native-publication descriptions are historical snapshots superseded by the later integration/T074 handoffs.

Reviewed 2026-11-14 from `84befb9`; `a54ed83` adds CLAUDE guidance only. The previous queue assumed the code was still at `59316d3`. Later commits repaired T066, added packaged catalog/visual loading and navigation adapters, completed T068/T069/T070 software, and built the **`7cd7e81` test candidate**. Its ZIP is present and verified. Do not restart these tasks from old findings. See [current audit](product-state-audit.md), [launch/playtest guide](playtest-guide.md) and [M11 handoff](milestones/m11-playable-release/handoff.md).

This is documentation/planning only: no coding lease, runtime edit, dispatch, P05 clearance search or waiver. Work stays in M8–M11 with original T035–T065 requirements and RUN/TDM/MAP/UI/SET/REL plus FIX01–FIX14 exits. One integrator owns shared Bootstrap, Contracts, schemas/locks, project settings, workload definitions and coordination.json. Keep Unity 6000.3.28f1 / URP 17.3.0, C0-v1, Game-v1 (ADR-036 settings schema 2), ADR-035/038 content and ADR-037 workloads. Boundary changes require reviewed ADR/migration/fixtures before consumers.

## Owner priority: local play, no hosted multiplayer

The first version is a single offline process with one human and local allied/enemy bots. No server rental, hosting/account setup, external server process, online/LAN multiplayer implementation or deployment is needed. Keep existing authority boundaries and build-isolation guards; do not expand the current queue into networking work. The owner's 2026-11-14 clarification is recorded in [vision](vision.md#first-version-scope-local-bots-only).

## Final installer/update delivery

The owner now requires the final game to use a Windows installer: a newer installer updates the existing installed game and preserves preferences. Current ZIPs remain previews. [T073](task-supplement.md#t073--windows-installer-and-in-place-upgrade) is a planned M11 implementation assignment with [INS01–INS06 acceptance](features/windows-installer.md); recording this requirement does not start installer coding. It supplements REL01/REL06 without waiving earlier exits.

## T072 delivered preview

Local sprint slide is implemented and built at 5eaced5; clean Game 56 / EditMode 26 / PlayMode 13, Client/Server, eight content packages and ZIP validation pass, with an independent menu startup. [Guide](playtest-guide.md) and [M11 evidence](milestones/m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery) identify the actual new archive/hash. Full player/device/native-nav/reference acceptance remains open. T073 installer/update implementation is planned separately.

## Current task disposition

| Task | Delivered | Remaining |
|---|---|---|
| T066 — baseline | Metadata/cancellation fixes; fresh Contracts 72/72, Generation 319/319, Game 51/51 and provenance PASS; retained Unity/client/server reports. | Maintain regression baseline; preserve old failed attempts. Not the next implementation assignment. |
| T067 — content/navigation | Persisted ADR-038 catalog, actual packaged visuals/rig/rifle/pistol, executable-relative defaults/recovery, native editor bridge and runtime installer. | **Partial:** normal wrapper still publishes software-grid NVB1; native publication integration, truthful path statuses/fallback policy, load cost and real two-map native route/spawn/player evidence. |
| T068 — authority presentation | Rifle/pistol selection and Reload/Shot action events drive actual views. | FIX05 player captures, live action/life/pose validation; no evidence of named visual acceptance. |
| T069 — preferences | Editable applied bindings/accessibility, staged display persistence and device-mode enumeration. | Named UI/control/display/audio review, real Keep/Revert, 20 load/replay cycles. |
| T070 — measurement | Finish/results/replay, cumulative active soak capture and honest counters, included in candidate. | Twelve reference captures + two map soaks PendingHardware; validate on final matching binary/content. |
| T071 — owner candidate | Clean `7cd7e81` Client/Server, archive/hash/inventory, recorded menu launch. | **Partial:** named manual play matrix, complete matches/results/replay, actual corruption/recovery and prior-pair rollback. |
| T062 / T065 — final acceptance | Visual inventory/tooling and operator checklist. | P05 FAIL retained; named review, full match/device/reference/soak/delivery exits. REL01–REL06 NotClaimed. |

## Immediate owner path: T071 playtest

The existing archive is the fastest way to expose real blockers. Use [the guide](playtest-guide.md); do not claim an owner-playable handoff merely because the launcher reached Main. The requirements are unchanged:

- Independently extract, run the local launcher from another working directory and reach Main without Unity/developer cache/network/server dependency.
- Exercise both maps × 5v5/6v6: movement/aim/fire/rifle-pistol/reload, allied/enemy bots, human/bot death and 180-tick respawn, camera rebind, pause/focus, scoreboard/settings and restart/leave.
- Complete at least one normal 150-kill/15-minute match per map, see immutable results and replay. Record which sizes/finish reasons were actually completed; do not mark the full T065 matrix passed.
- On a disposable extraction test startup and map-load missing/corrupt content, repair and retry. Rehearse a real compatible prior binary/content rollback. Packaging software-only tests and SkippedNoPrevious do not prove these.
- Retain exact source/build/content/toolchain/seed/profile/session identities, named reviewer, raw log/capture and every functional blocker/unknown. Store evidence under ignored Artifacts before removing temporary folders.

Owner: release integrator/operator. Scope: build/package execution, controls/known issues/evidence and M11 handoff. Route code defects to their bounded owning cards; do not use this docs task as a runtime assignment. Never copy historical binaries or stamp current HEAD onto the retained archive.

## T067 — remaining technical implementation

Owner: content/world integrator. Allowed paths remain `Content/Maps/`, compiler/codecs/publisher, `Generation/Editor/Compilation/`, `World/Foundation/CompoundMapRuntime.cs`, `UnityNavMeshBakeInstaller.cs`, associated tests; shared Bootstrap/editor/build/package/index/schema changes through the integrator. Retain existing visual/catalog work rather than recreate it.

### Native publication must run in the actual tool path

`Tools/Compile-GameContent.ps1` invokes a separate pure `dotnet run` MapContent process. `UnityNavMeshBakeEditorBridge` registers its native provider only inside Unity Editor. Current `7cd7e81` compile proofs explicitly say both map bakes are `software-walkable-grid;unity-native-bake-pending`. Source registration in another process cannot complete that boundary.

Connect a locked-editor native compile/publication entry, prove it uses installed authoritative collision and frozen settings/seed/toolchain, publish matching native map/metadata/catalog/package identities, and make verification distinguish software diagnostics from release-native content. No current command is documented as already completing this. Pure tests may retain their explicit software fallback, but cannot satisfy native release acceptance. ADR-035 and relevant format/source guards remain mandatory.

### Runtime paths and installation need meaningful coverage

`UnityNavMeshBakeInstaller.Install` constructs NavMeshData using `NavMeshBuilder.BuildNavMeshData` from packaged surfaces with a near-zero radius. Measure cold/warm cost and validate areas, agent clearance, disconnected regions, corners and spawn/route reachability for both maps. The existing PlayMode test installs a small open quad only. Document how this construction meets the intended loading/offline-generation boundary; any necessary format/behavior revision needs an ADR and fixtures first.

`TryCalculatePath` accepts PathPartial and falls back to a packaged graph on an invalid native query. The bool adapter cannot currently distinguish those outcomes. Preserve truthful Complete/Partial/Failed semantics and explicitly review whether fallback is permitted in shipping composition. Do not claim native validation based on a successful graph query. These source findings require targeted tests and real player reproduction; this review does not assert a reproduced bot failure.

Exit remains FIX04/FIX07: native publication/install/query evidence on both maps; cold/warm/forced and cancel/corrupt recovery; packaged bytes independent of repository/cache; cross-quality gameplay equivalence; gameplay-only server closure and actual spawn/routes. Preserve failures. Native content/source changes require a new clean Client/Server/package identity and affected T071/T070/T065 reruns.

## T068/T069 — player validation of landed software

T068 owner: presentation integrator (`Presentation/Rendering`, `Rig`, `View`, session views and real integration tests); shared projection/contract changes through integrator. Capture rifle and pistol, real shots/reload timing, aim/death/respawn and repeated human/bot replacement on both maps. Preserve corpse/life bounds, snapshot coherence and quality-independent hit poses. FIX05 and P05 remain separate.

T069 owner: client integrator (settings/editor/device application, input, session/menu/scoreboard/audio and relevant tests). Record named 720p 100%/150% keyboard/pointer readability, binding apply/persist/conflict/cancel, 15-second display Keep/Revert including focus loss, audible output/gain/mute and 20 alternating load/replay cycles. Preserve schema 2 and gameplay hashes. Test results alone do not prove device behavior.

## T070, T062 and T065 — final acceptance

T070 owner: measurement integrator. The software at `59bbcbb` is included in `7cd7e81`. Do not rebuild again merely to include T070. If T067/player fixes change source/content, measure the new matching pair. Required reference evidence remains twelve native720p Low captures (both maps × sizes × three, 60s warmup/180s measurement) and cumulative ≥30-minute normal-AI 6v6 soak/map spanning normal matches/replay. Missing CPU/GPU/thermal/reference proof stays PendingHardware; keep B01–B04 and 60 FPS stretch separate.

T062 remains ToolingReady: P05 skin/forearm/strap/eye-sight residuals remain FAIL under ADR-024. Capture actual player defects and obtain the explicit owner scope decision before another clearance/generator search. Retain failed artifacts and review new identities for any later authorized fix; no new waiver is implied.

T065 executes [REL01–REL06](milestones/m11-playable-release/validation-acceptance.md) and [FIX01–FIX14](release-remediation-validation.md) on one stable final pair: eight score/timeout full matches, draw fixture, named visual/UI/audio/display reviews, shell cycles, twelve reference captures, two soaks, provenance/regressions/server isolation and independent delivery/rollback. Use the [M11 runbook](milestones/m11-playable-release/handoff.md). Unknowns remain explicit; commit completed assigned work to main and preserve ignored evidence before cleanup.

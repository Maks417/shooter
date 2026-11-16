# Product state audit — 2026-11-14

**Latest installer — 2026-11-16:** **0.3.17**, clean binary/content source `1676ff8`, at `Artifacts/deliveries/Shooter-Setup-0.3.17.exe` (36,541,803 bytes, SHA256 `7701fdd8…7df166b`). Ships everything merged since 0.3.16 (ADR-075–086). Game 99 / Maps 72 / Generation 508 / provenance PASS; native 12 packages, clean Client, smoke, package and installer builders PASS. Installation/upgrade, EditMode/PlayMode, playtest, P05 and REL acceptance remain Pending. [Delivery evidence](milestones/m11-playable-release/handoff.md#delivery-0317).

**Audio refinement — 2026-11-15 (source):** owner-authorized T089 follow-up adds camera-relative positioning, rate-correct PCM, bounded wall muffling/early reflections, voice priorities and a stereo limiter while retaining the existing CC0 bank and gameplay/settings identities. See [audio behavior](features/audio.md#owner-requested-spatial-and-mix-refinement--2026-11-15) and [exact validation/build limits](milestones/m11-playable-release/handoff.md#audio-spatial-and-mix-refinement--2026-11-15). Named listening and reference performance remain Pending; no Battlefield V parity is implied.

**Arsenal visual source — 2026-11-15:** owner-assigned [geometry.weapon 1.0.3](features/geometry-weapons.md#arsenal-visual-profiles--geometryweapon-103) improves rifle, pistol, knife and grenade profiles and five spatial PBR finishes. Installer **0.3.14**, clean source `b8235a3`, is available at `Artifacts/deliveries/Shooter-Setup-0.3.14.exe`; EditMode 57 / PlayMode 21, clean Client, native package and installer builders PASS. Exact verification/build status is in the [M11 handoff](milestones/m11-playable-release/handoff.md#arsenal-visual-improvement--2026-11-15-adr-068). This is an incremental procedural improvement; Battlefield V equivalence, player visual review and acceptance remain Pending.

**T086 sample-bank audio — 2026-11-15 (source):** ADR-055 generated PCM combat bank replaces sine+noise beeps (rifle/pistol, impact, reload, death, footsteps, UI, ambient) with distance/pan/HF mixer. Pure audio tests PASS; named FIX10 device hearing and a new installer remain Pending. [Handoff](milestones/m11-playable-release/handoff.md#t086--sample-bank-combat-audio-adr-055).

**T090 bot tactics — 2026-11-15 (source only):** cover probes prefer a peekable side sample; gunfire/footsteps respect walls; strafe flips on missing footing; spread penalizes ally path corridors ([ADR-070](decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090)). Pure AI 54/54 PASS. Owner feel, map AI capture and installer remain Pending. [Handoff](milestones/m11-playable-release/handoff.md#t090--bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread--2026-11-15).

**Latest combined preview — 2026-11-15:** installer **0.3.12**, clean binary/content source `50e8939`. It combines the T084 near-field PBR finishes, hip framing and continuous sleeve loft with the T085 bot update: teammates spread out, bots hear footsteps and turn on unseen attackers, time-to-kill on a still target is about 1.6 s at 8 m, and HUD markers point at whoever hit the player. Unity EditMode 53 / PlayMode 21, native publication (one transient failed attempt, retry PASS), clean Client/Server, server smoke, package and installer builders PASS. Installation/upgrade, player review, bot feel and all P05/VD/REL acceptance remain Pending. [Exact evidence and installer](milestones/m11-playable-release/handoff.md#release-0312). Older entries below are historical.

**Latest T083 preview — 2026-11-15:** installer **0.3.11**, clean binary/content source `df64aa8`, adds packaged first-person gloved hands/sleeves for both weapons, analytic support-hand reach and a continuous reload remove/insert arc. Includes the merged T082 bot baseline. Unity EditMode 53 / PlayMode 21, native publication, clean Client/Server and package/installer builders PASS. Depot 6v6 rifle hands observed; automation stopped on live player movement and left the game open. Live pistol/reload/ADS, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact evidence and installer](milestones/m11-playable-release/handoff.md#t083-delivery-0311). Older entries below are historical.

**T085 bot awareness — 2026-11-15 (source only, no installer):** teammates no longer trail each other along one route. Bots hear footsteps from behind, turn on unseen attackers and see a 120° cone. Opening shots now miss, so time-to-kill on a still target is about 1.6 s at 8 m and 2.5 s at 20 m (was 1.0 s and 1.5 s). The HUD points at whoever hit the player ([ADR-054](decisions.md#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085)). All pure lanes and Unity EditMode/PlayMode (53/21) PASS. A build or installer, a real-map AI capture and a feel review are Pending. [Handoff](milestones/m11-playable-release/handoff.md#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill).

**T082 bot combat behaviour — 2026-11-15 (source only, no installer):** bots now have bounded-rate aim with seeded error and accuracy recovery, range-based bursts, strafing and sticky threat-aware targets. They react to being shot and to gunfire, call out contacts, spread across destinations, retreat at low health, reload tactically and use runtime cover probes ([ADR-051](decisions.md#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082)). A respawn no longer wipes every bot's memory. Budgets and contracts are unchanged. All pure lanes and Unity EditMode/PlayMode (53/20) PASS. A build or installer, a real-map AI capture and a player feel review are Pending. [Handoff](milestones/m11-playable-release/handoff.md#t082--solid-fps-bot-combat-behaviour).

**Latest T081 preview — 2026-11-15:** Windows installer **0.3.10**, clean binary/content source `e140dd6`, adds packaged panelled walls, bevels/footings/coping, painted cargo with rails/corrugation/locks, map/menu material separation and rougher coated weapons. Native publication, clean Client/Server, PlayMode and package/installer builders PASS. Both gameplay roots/catalog records match 0.3.9 exactly. Limited player review observed Main/cards and Compound 6v6; automation stopped on live user input, leaving the player open. Depot close-up, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact delivery evidence](milestones/m11-playable-release/handoff.md#t081-delivery-0310). Older entries below are historical.

**Latest T080 correction preview — 2026-11-15:** installer **0.3.9**, clean binary source `beac9ae`, fixes physical surface scale, overview framing and discarded menu submeshes. Client/Server and native package/installer builders PASS; limited automated player review loaded Depot 5v5 and Compound 6v6 from a short verified copy. This remains visibly below BFV quality; soldiers, hands/actions and block architecture need further work. Deep evidence-path content loading fails; installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain open. [Actual evidence and installer](milestones/m11-playable-release/handoff.md#t080-delivery-039). Older entry pointers below are historical.

**Pre-release fixes — 2026-11-15:** bot NavMesh installer winding and packaged path fallback (diagnostics and `SHOOTER_AI_SUMMARY`), relaxed spawn fallback with a recoverable `SpawnFailure` menu return ([ADR-047](decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure)), and the 5 s pre-match countdown ([ADR-048](decisions.md#adr-048--accepted-session-internal-pre-match-countdown)) are landed in software. Pure lanes pass as recorded in the [M11 handoff](milestones/m11-playable-release/handoff.md#bot-navigation-spawn-fallback-and-pre-match-countdown--2026-11-15). Unity EditMode/PlayMode, the client build and real-player bot movement, respawn and spawn behaviour remain **Pending**; none is claimed as PASS.

**Current T075 design preview — 2026-11-15:** Windows installer **0.3.4**, clean binary source `7407bc4`, implements procedural PBR map surfaces, textured menu staging, working actual-map cards and analytic first-person handling with LOD0 equipment on Low. Unity **43 EditMode / 17 PlayMode**, clean Client/Server, eight native packages, provenance and package/installer builders PASS. Automated player review observed both map cards and Compound 5v5 / Depot 6v6 loads; this is not full gameplay or comparative visual acceptance. P05 FAIL, reference performance and M8–M11/VD exits remain open. [Identities, evidence and limitations](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

**Prior T074 visual preview:** T074 installer **0.3.1**, clean binary source **b531da9**, supersedes the older candidate pointers below. Scene-led menu/map cards and compact tactical HUD are implemented; clean EditMode 29 / PlayMode 16, Client/Server, eight native packages and installer builders PASS. The player log confirms Main and three cosmetic menu actors/two map cards. The owner stopped the final Computer Use review with Escape, so final interactive visual/gameplay/device acceptance remains Pending. [Exact installer/hash, evidence, repaired failures and limits](milestones/m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview). P05 FAIL, reference proofs and M8–M11 gates remain open. Older delivery sections below are historical snapshots, not the current installer recommendation.

## Latest delivery: T072 slide preview / final installer planned

The owner-authorized slide is built into clean-source `5eaced5`: Client/Server, verified eight-package content and ZIP PASS; standalone extracted executable reaches Main. Final Game 56, EditMode 26 and PlayMode 13 pass, with actual flags/paths in [M11 delivery](milestones/m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery). [Playtest guide](playtest-guide.md) now points to that 40,668,944-byte ZIP (SHA256 8240e298…b34580f), including Sprint + F. Old 7cd7e81 has no slide and is retained as a prior pair. Manual gameplay/device/reference acceptance remains Pending, P05 FAIL remains and REL gates remain NotClaimed.

The owner additionally requires a final [Windows installer with in-place upgrades](features/windows-installer.md), preserving controls/settings and stable installation identity. T073 is planned documentation scope, not an implemented installer. ZIP previews do not close that final delivery gate. Requested cleanup removed 24.64 GiB of old generated debug backups; current-build debug/temporary repair files were also cleaned after proof was retained, preserving runtime archives and required evidence.

## T072 follow-up: local sprint slide

The owner subsequently requested and authorized implementation of Left Shift + F slide. ADR-039/T072 extend local authority/input/settings and copied camera/rig movement views; existing C0/N1/content formats remain intact. The original review below is historical to that feature addition: 7cd7e81 has no slide. Follow [M8/M11 handoff evidence](milestones/m11-playable-release/handoff.md#t072--owner-requested-local-sprint-slide--implementation-record) and [playtest guide](playtest-guide.md) for actual new-build availability, test results and retained limitations. No acceptance gates are closed by adding this feature.

## Latest review: built playtest candidate, gameplay acceptance pending

Review started at clean `main` `84befb9b48c98b71716d67a0a3e799b8b112c6df`. During review, `a54ed8302151a134e47cc1a421153edc8ef4e0c7` added `CLAUDE.md` only; it is preserved. The latest retained binary is **`7cd7e8191e118929af742bef382774edb22a57c2`**, not the review HEAD. Later changes before this review are documentation and deletion of unused `Assets/Resources.meta`; there is no newer runtime C# implementation. Do not relabel the existing binary as a build of a later commit.

**Assessment:** the project has progressed from incomplete integration to a packaged offline playtest candidate. Source implements the two-map 5v5/6v6 loop, weapons, bots, respawn, menus and applied settings. Client/server builds and Unity tests have retained passing reports. The archive exists and passes a fresh inventory/hash check. The handoff records reaching Main menu, but complete manual gameplay has not been demonstrated. A percentage-complete or calendar estimate would imply evidence we do not have.

Owner clarification during this review: the first version is **local bots only**, with no online multiplayer, separate server or hosting purchase. Existing server-compatible architecture/isolation evidence is retained without adding an online-delivery prerequisite. See [scope](vision.md#first-version-scope-local-bots-only).

The practical next step is to play the existing candidate and capture failures, while finishing the remaining native-navigation publication work. Use the new [launch and playtest guide](playtest-guide.md) and the revised [remaining-work queue](next-playable-build.md). This review changes documentation only; it creates no runtime assignment, coding lease, agent dispatch or acceptance waiver.

### What changed since 59316d3

| Area | Current source / retained evidence | Still open |
|---|---|---|
| T066 baseline | `02037ce` restores metadata; `edf6269` prefers cancellation over Busy; `84befb9` removes an orphan meta. Fresh provenance PASS: 660 entries / 330 GUIDs. Contracts 72, Generation 319, Simulation 110 and Game 51 pass in fresh wrappers. | Preserve earlier failures; baseline repair is no longer the next feature task. |
| T067 catalog/content | ADR-038 `catalog.json`, `StaticMapCatalog.TryLoadPublished`, executable-relative `PackagedContentLayout` and retry recovery exist. `MapVisualClosure`, `PackagedVisualPayloads`, `ProductMapView` and `Presentation/Rig/ProductActorPresentation.cs` consume packaged meshes/materials/rig/rifle/pistol. | Native publication gap below; actual two-map cold/warm/cancel/corrupt/route/spawn/player evidence. |
| T067 navigation | `Bootstrap/Editor/UnityNavMeshBakeEditorBridge.cs` and `World/Foundation/UnityNavMeshBakeInstaller.cs` exist. Runtime uses Unity NavMesh sampling/path queries. | The shipped packages still come from the pure software-grid publisher; native bridge publication is not integrated into the wrapper. Runtime installation rebuilds NavMeshData and permits graph fallback. |
| T068 presentation | `585dbcd`: selected rifle/pistol and reload/shot events drive action views; no ammo-drop reload inference. | Both-weapon/live-life/render captures, FIX05 and named visual review. P05 FAIL retained. |
| T069 preferences | `42390eb`: editable applied bindings, conflict handling, accessibility consumers and staged display persistence exist. | Named keyboard/pointer/720p scale/display/audio review and 20 alternating load/replay cycles. |
| T070 measurement | `59bbcbb`: finish/results/replay coordinator, cumulative active soak ticks and corrected counters are in the retained binary. | Twelve reference captures and two ≥30-minute normal-AI map soaks: PendingHardware. Collector source/tests do not prove full-roster performance. |
| T071 delivery | Clean non-development IL2CPP Client + isolated Server reports at `7cd7e81`; package/inventory/launcher/controls exist. Fresh archive SHA256 and all 245 inventoried file hashes match. | Manual four-configuration playtest, complete matches/results/replay, player corruption/recovery and prior-package rollback. T071 is partially delivered, not fully accepted. |

### Remaining technical findings

| ID / priority | Evidence | Required follow-up |
|---|---|---|
| C01 / high — native publication | `Tools/Compile-GameContent.ps1` launches `Tools/MapContent/Program.cs` through `dotnet run`. That process cannot run the Unity Editor's `[InitializeOnLoad]` registration. Retained `compile-proof.json` for both maps reports `navBakeProvenance=software-walkable-grid;unity-native-bake-pending`, `nativeUnityNavBake=Pending`. | T067: connect a pinned-Unity publication path, record native provenance against installed authoritative collision, publish both maps and rerun affected content/player checks. The presence of the bridge is not native package proof. |
| C02 / high — runtime nav construction | `UnityNavMeshBakeInstaller.Install` calls `NavMeshBuilder.BuildNavMeshData` on the packaged triangulation with radius 0.05 / voxel 0.02. This is runtime construction from packaged surfaces, not loading prebuilt engine NavMeshData and not rebaking authoritative collision. | T067: measure cold/warm load cost and validate clearance/areas/reachability against ADR-035 and architecture. Resolve any boundary/format change through ADR and fixtures; no implicit relaxation of offline-generation requirements. |
| C03 / high — path result semantics | `TryCalculatePath` accepts any status except PathInvalid, including PathPartial; on invalid engine paths it calls `UnityNavMeshBakeCodec.TryFindPath`. `CompoundMapRuntime` passes only a bool to `RevisionedPathAdapter`. The PlayMode test covers one open quad, not both obstructed maps or disconnected destinations. | T067: cover disconnected/partial/invalid native queries and ensure Complete/Partial/Failed stay truthful. Review fallback policy explicitly. This is source-based risk, not a reproduced bot failure. |
| C04 / delivery blocker — no proven gameplay handoff | M11 handoff says manual play matrix Pending. Package `validation.json` still says `playerLaunchEvidence=Pending`, `releaseAcceptance=NotClaimed`, `rollbackRehearsal=SkippedNoPrevious`. The earlier `C:/Temp/shooter-t071-playtest-59bbcbb` extraction no longer exists at review time; archive and packaged directory do. | T071: fresh independent extraction, named gameplay and recovery/rollback evidence; retain raw logs/captures durably under Artifacts. Menu-only recorded launch does not establish playability. |
| C05 / release blockers — visual/device/performance | P05 FAIL under ADR-024; named visual/UI/audio/display reviews, full eight-match matrix, reference captures and soaks remain missing. | T062/T065 and T070 hardware work, with actual evidence. No new clearance search or waiver is authorized here. |

### Fresh verification at review HEAD

Commands started before documentation edits. SDK remains 10.0.301; offline advisory lookup is separately PendingNetwork. Evidence paths below are repository-relative and ignored. Initial sandboxed wrappers failed at restore before tests; retained reruns used the access needed for the installed SDK/cache. A concurrent Simulation build collided on shared `Tools/Contracts/obj` (CS2012), so it was rerun serially; do not run these SDK wrappers concurrently in one checkout.

| Check | Result | Evidence |
|---|---|---|
| Source provenance | PASS, 660 entries / 330 GUIDs | Snapshot retained in `Artifacts/project-state-audit/84befb9b48c98b71716d67a0a3e799b8b112c6df/source-provenance.json` |
| Contracts | PASS 72/72, zero skipped, sourceDirty=false | `Artifacts/m0-foundation/84befb9b48c98b71716d67a0a3e799b8b112c6df/20261114T155616967Z-2555165c/` |
| Generation | PASS 319/319, zero skipped, sourceDirty=false | `Artifacts/m1-procedural-feasibility/84befb9b48c98b71716d67a0a3e799b8b112c6df/20261114T155617823Z-bc16ae48/` |
| Game | PASS 51/51, zero skipped, sourceDirty=false | `Artifacts/m8-runtime-composition/84befb9b48c98b71716d67a0a3e799b8b112c6df/20261114T155617907Z-e9c6230c/` |
| Simulation | PASS 110/110, zero skipped, sourceDirty=false at docs-only a54ed83 | `Artifacts/m2-combat-harness/a54ed8302151a134e47cc1a421153edc8ef4e0c7/20261114T155825407Z-ef36285c/` |
| Archive / inventory | PASS: 40,645,312 bytes; SHA256 `fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50`; 245 hashes match; ZIP CRC passes | `Artifacts/project-state-audit/84befb9b48c98b71716d67a0a3e799b8b112c6df/archive-review.json` |
| Fresh Unity / player / hardware | NotRun here | This is a source/evidence/docs review, not a new runtime acceptance session. |

Retained clean `7cd7e81` evidence was inspected separately: EditMode 24/24 at `tests-editmode-20261114T152432007Z-ea907ae6`, PlayMode 12/12 at `tests-playmode-20261114T152818932Z-b6f6af89`, and successful Client/Server manifests at the exact paths in the [M11 handoff](milestones/m11-playable-release/handoff.md). These are prior runs, not fresh tests of review HEAD. Failed/dirty attempts also remain retained. Server manifest has no graphics source assets; this is build isolation evidence, not a full server runtime acceptance test.

Specification fixtures passed 35 assertions / 17 JSON files. The intentional documentation-manifest refresh and normal verifier pass cover 123 Markdown pages, links/anchors/index/fences and hashes; `git diff --check` passes. Final link/inventory counts live in `verification-report.json`. These are documentation checks only. No generated payload, golden, lock/schema, package version, runtime source or coordination lease was changed; the only new shared page is `playtest-guide.md`. All completed review changes are committed to main under the standing repository rule.

### Documentation corrections and unchanged acceptance

Entry docs, architecture, map feature, task queue and M8–M11 packs incorrectly kept the 59316d3 findings as current: 19 missing metas, failing cancellation, runtime catalog/visual generation, absent native adapter and unimplemented settings/measurement. Those statements are superseded by this review. M11 also mixed a delivered archive/menu launch with older tables saying no clean candidate existed. Current dispositions now distinguish built software, packaged candidate, manual gameplay and final acceptance. The new guide supplies actual archive location, defaults and a record template.

M0–M7 remain historically owner-waived; M8–M10 integration validation and M11 acceptance remain open. REL01–REL06 are NotClaimed. Historical audits and failures below are retained as dated records and do not prescribe the next assignment. Documentation integrity checks are not game tests.

## Historical review at 59316d3 (superseded)

Reviewed 2026-11-14 at clean `main`, `59316d387e5345716392e332ca225a581a671c33` (184 files changed; T052–T064 remediation committed). The prior audit at f3d9954 is retained below as history. This review inspects current source and reruns pure checks; it performs no fresh Unity build, interactive player/device review or reference-hardware capture. The [next playable-build queue](next-playable-build.md) assigns the remaining work within M8–M11; [original repair cards](release-remediation-plan.md) and [FIX acceptance](release-remediation-validation.md) remain applicable.

## Historical disposition at 59316d3

M0–M7 retain historical closures/waivers; P05 FAIL under ADR-024 remains. M8–M10 have substantial committed remediation, with specific implementation gaps and player validation still open. M11 has hardened package/measurement tooling, but no accepted clean current-source playable archive. Historical coordination records are unchanged; they are not current technical acceptance. This documentation assignment changes no runtime, generated content, contracts, locks, workloads or coding leases.

| Area | Implemented in latest commit | Remaining |
|---|---|---|
| Regression | Contracts lock repair and strict TRX counts; Contracts 72/72, Simulation 101/101, Game 38/38 pass | Generation cancellation test fails in fresh full suite; provenance fails on 19 missing metadata entries; current Unity compile/tests/builds unverified (T066) |
| Lifecycle/input | Authority retirement, monotonic life IDs, human/bot rebinding, pause/focus clock and frame input buffering | Actual Bootstrap/player repeated-life and focus evidence; no reason to reassign T052–T054 from scratch |
| Content/maps | Verified package publisher/store, three approved numeric maps (ADR-071 oil-depot + elevation), topology/spawn safety | Software 12-package publish landed; native bake/install and real three-map route/spawn/player proof remain T067 |
| Views | Map renderers, packaged soldier/rifle/pistol, binder/corpse cap, authority slot/action view, visual inventory tooling | T068 software drives slot/events; FIX05 player captures Pending; P05 review open |
| AI/TDM | Enemy filtering, path-corner following, reload commands, 9/11-bot scheduling and spawn ranking/deadline | Native-world mixed-team combat, repeated respawn and full match matrix on both maps/sizes |
| Shell/settings/audio | Shell-owned Main→load→play→pause→results, score tables, JSON schema 2/store/editor, device applier, synthesized callback | Binding editor/input application absent; some accessibility preferences not consumed; named UI/display/audio reviews and shell cycles (T069) |
| Measurement/delivery | ADR-037 collector; T070 finish/results/replay host + honest counters (software); source-matched T071 rebuild at `7cd7e81` | Twelve captures + soaks **PendingHardware**; `114815c` archive retained historical/superseded; final T062/T065 acceptance |

REL01–REL06 remain Open / NotClaimed. A local owner playtest candidate and a fully accepted release are separate checkpoints, defined in the [next-build plan](next-playable-build.md); neither is claimed by this audit.

## Historical source findings at 59316d3

Paths start at `UnityProject/Assets/Shooter/` unless prefixed Tools. These are source observations; no interactive reproduction is claimed.

| ID / priority | Current evidence | Bounded follow-up |
|---|---|---|
| B01 / blocker | Source provenance reports 19 missing `.meta` entries introduced with new files/folder. See exact list below. | T066; RUN00/REL05 |
| B02 / blocker | Full Generation suite: 310 pass / 1 fail; `CompilerTests.BusyKeyLockCanTimeOutOrCancelWithoutPublication:120`, expected Cancelled, actual Busy. Cause unresolved; timing-sensitive 25/50 ms deadlines. | T066; preserve failure, resolve deterministically |
| B03 / high | `Content/Maps/StaticMapCatalog.CreateReleaseSeed` builds gameplay via planners and eight MapPackageBuilder.Build calls. `FoundationEntry.StartProduct` calls this in release. | T067: load persisted catalog/gameplay bytes, remove shipping generation |
| B04 / high | `MapPackageBuilder.AddVisualClosure` has a single-joint skeleton, 1×1 material and descriptors, without actual soldier/rifle/pistol closure. `ProductMapView` constructs box visuals; `ProductActorPresentation.InitializeShared` generates soldier/rifle geometry at runtime. | T067: publish/consume actual visual dependencies |
| B05 / high | **2026-11-15 software:** `UnityNavMeshBakeInstaller` winds triangles face-up through `UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices`, and the product enables packaged path fallback, so bots should no longer idle on an effectively empty NavMesh. This is a code-reading hypothesis; real-player bot movement is Pending. `Generation/Editor/Compilation/UnityNavMeshBakePublisher` is a constants/pending-string stub. Builder uses BuildWalkableGrid; CompoundMapRuntime verifies nav bytes but path queries use CompoundConnectivity. | T067: real native bake/install/path and spawn/route checks |
| B06 / high | ~~Product actor renderer always creates Held rifle, ignores ActiveWeaponSlot, and starts 2.4s reload from magazine decrease.~~ **T068 software:** `ProductAuthorityActionView` + presentation consume `ActiveWeaponSlot` and Reload/Shot events with ActionPayload timing; packaged rifle/pistol meshes. Player FIX05 captures remain Pending. | T068 player Pending |
| B07 / high | ~~Settings binding count / fixed keys / missing accessibility consumers.~~ **T069 software:** editable bindings with conflict swap/cancel, applied CaptureFrame/scoreboard, ReducedFlash/Subtitles/ReducedCameraMotion consumers, device resolution enumeration and Keep-before-persist display. Named FIX08–10/SET01–02 UI/device/20-cycle evidence remains Pending. | T069 device/UI Pending; T071 playtest |
| B08 / high | ~~ProductTdmHost single-match / broken counters.~~ **T070 software:** `ProductTdmCaptureCoordinator` finish→results→replay accumulates soak across normal 150-kill/54000-tick matches (rules unchanged); respawns=life replacement; querySamples=`CountingWorldQueries` adapter; contentBindings=verified roots; WorkloadId preserved across `ResetForNewMatch` and re-applied by host. Twelve captures + soaks remain **PendingHardware**. | T070 hardware matrix/soak; rebuild measurement candidate after source tip |
| B09 / high | Bootstrap's default content/lock paths are repo-relative; package launcher supplies relative overrides. Missing startup content reaches Bootstrap failure before shell. Package corruption/rollback rehearsal is software-only, not a player launch. | T067/T071: packaged discovery, recovery UI and real extract/launch/rollback |
| B10 / release blocker | P05 FAIL retained; no fresh clean player, full match matrix, named visual/UI/audio/display acceptance or reference captures/soaks from this review. | T062 scope/review, T065; no inherited waivers |

## Fresh verification at 59316d3

All four wrappers ran before documentation edits with `sourceDirty=false`, SDK 10.0.301 and offline local package restore. Initial sandboxed Contracts/Generation restores exited before tests without useful diagnostics; the reruns below used the access required for the installed SDK/cache. Those failures remain separately retained. Offline NuGet advisory lookup is PendingNetwork, independent of suite outcomes.

| Check | Actual result | Repository-relative evidence / scope |
|---|---|---|
| Contracts wrapper | PASS 72/72, zero skipped; NU1004 resolved | `Artifacts/m0-foundation/59316d387e5345716392e332ca225a581a671c33/20261114T132808830Z-c62ef1ee/` |
| Generation wrapper | FAIL: 310 passed, 1 failed, 311 executed, zero skipped | `Artifacts/m1-procedural-feasibility/59316d387e5345716392e332ca225a581a671c33/20261114T132824187Z-5bdd0878/`; cancellation/lock case above |
| Simulation wrapper | PASS 101/101, zero skipped | `Artifacts/m2-combat-harness/59316d387e5345716392e332ca225a581a671c33/20261114T132845567Z-cf66ab2c/` |
| Game wrapper | PASS 38/38, zero skipped | `Artifacts/m8-runtime-composition/59316d387e5345716392e332ca225a581a671c33/20261114T132856943Z-d08cfaa0/` |
| Source provenance | FAIL: 613 asset entries / 297 GUIDs, 19 missing metadata entries | `Artifacts/project-state-audit/59316d3/source-provenance.json`; current source failure, no forbidden-art finding inferred |
| Specification fixtures | PASS 35 assertions / 17 JSON files | Specification only |
| C0/content/feature/Game identity guards | PASS (24/40/41/12 source registry entries respectively) | Identity checks only; Game fixture count 5, C0 fixture count 18 |
| Documentation | See final `verification-report.json` | Links/inventory/hashes only |
| Current Unity/player/device/reference hardware | NotRun / Pending | No new compile/build/rendered-game or acceptance claim |

Missing metadata, paths relative to `UnityProject/Assets/Shooter/`: `Bootstrap/Client/ProductTdmBenchmark.cs`, `ProductTdmHost.cs`; `Content/Maps/VerifiedMapPackageStore.cs`; `Game/Session/ProductMeasurementTelemetry.cs`, `ProductTdmWorkloads.cs`; `Generation/Core/Environment/MapDocumentJson.cs`, `MapDocumentReader.cs`, `MapPackageBuilder.cs`, `MapPackagePublisher.cs`, `MapRecipe.cs`, `MapSpawnLayout.cs`, `MapTopologyValidation.cs`, `UnityNavMeshBakeCodec.cs`; `Generation/Editor/Compilation/UnityNavMeshBakePublisher.cs`; `Generation/Recipes/map-compound.json`, `map-depot.json`; `Simulation/TeamDeathmatch/SpawnSafetyPolicy.cs`; `Tests/Performance` folder; `Tests/EditMode/ProductTdmWorkloadIdentityTests.cs`.

One full Generation rerun after documentation edits (same runtime source, `sourceDirty=true`) again failed the same cancellation case: 310 passed / 1 failed / zero skipped, at `Artifacts/m1-procedural-feasibility/59316d387e5345716392e332ca225a581a671c33/20261114T133744184Z-576e769a/`. No code or test was changed to force a pass; T066 must resolve the observed failure. Final documentation/fixture/identity checks pass independently of this failure and the provenance failure. This assignment changes only docs/ and AGENTS.md, with completed changes committed to main under the standing repository rule; ignored evidence is retained locally.

## Historical baseline audit at f3d9954

The following findings/verification describe `f3d995497d88376d171c86b4088ae37fa8f289f0`, with some implementation-time annotations retained. They do not override the current findings above. In particular, NU1004, absent renderers/audio and fatal pause are historical defects with committed software fixes; source provenance has regressed since that baseline.

### Historical findings and source evidence

Paths below start at `UnityProject/Assets/Shooter/` unless prefixed `Tools/`. Line numbers describe the reviewed source, not a permanent API. Source conclusions are distinguished from measured tests; no defect below was interactively reproduced in a fresh Unity player during this audit.

| ID / priority | Observed source and consequence | Repair |
|---|---|---|
| A01 / critical | `World/Foundation/CompoundMapRuntime.cs:169` creates GameObjects, BoxColliders and StableSurface only. `Presentation/Session/ProductSessionView.cs:33` creates camera/light/text but no map, soldier or weapon renderers. Product composition bypasses the generated galleries. Collider installation and READY do not prove a visible game. | T055–T056 |
| A02 / critical | `Simulation/TeamDeathmatch/TeamDeathmatchMatch.cs:159` spawns a new life without retiring the old one. `Simulation/Session/HarnessActorLifecycle.cs:27` caps its retained states at 12; DespawnLife only removes its own dictionaries, not harness authority. Initial 6v6 therefore fills capacity before the first respawn. `Game/Session/OfflineGameSession.cs` retains the original humanEntity and bot registrations. | T053 |
| A03 / critical | Paused `OfflineGameSession.Step` returns Busy; `Bootstrap/Runtime/FoundationEntry.cs:165` throws for unsuccessful steps unless Finished, while Update keeps advancing its clock. Escape pause can therefore reach fatal session handling. | T054 |
| A04 / high | `Simulation/AI/TeamBotPlanner.cs:34` supplies all living participants as candidates; `CombatAiDirector.PlanEnemies` only excludes self. Allies can consume target/fire intent despite friendly damage rejection. `PollPaths` discards completed corners; BuildCommand moves toward PathGoal. Reload state changes before command construction also need authority-backed coverage. | T057 |
| A05 / high | `TeamDeathmatchMatch.TryPickSpawn` chooses the lowest-ID statically valid slot without actor occupancy/enemy-pressure ranking. `CompoundCatalogFactory` supplies six initial slots per team rather than the specified minimum twelve and can repeat fallback positions. Retry updates the due tick used for timeout; preserve an independent first-failure deadline. **2026-11-15 software:** occupancy and enemy-pressure ranking are in the strict policy, with a relaxed fallback after 120 ticks and a recoverable `SpawnFailure` menu return ([ADR-047](decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure)). Slot-count closure and real-player respawn/spawn review remain Pending. | T058 |
| A06 / high | ~~`FoundationEntry.StartProduct` loads/starts before the shell exists…~~ **T059 software (2026-11-14):** product launch prepares session/catalog only; `ProductShellHost` owns Main→Setup→Loading→Playing→Paused→Results, cancellable load, Tab scoreboard and retained results. Remaining: player 20-launch soak, 720p UI review, full-match acceptance after T057–T058. | T059 |
| A07 / high | ~~Historical schema-1 semicolon store / quality-only apply.~~ **T060+T059/T061 software (2026-11-14):** ADR-036 schema 2, `UserSettingsEditor`, `FileUserSettingsStore` JSON safety, `ProductClientSettingsApplier` audio/display/FOV/input bind. Remaining: SET02 named device/15s Keep/Revert on real display; full rebind UI polish. Input buffer repair is T054. | T054, T060, T059, T061 |
| A08 / high | Historical: `NotifyCombat` had no caller; mixer output discarded; no AudioSource path. **T061 software repair (2026-11-14):** ordered event lease → router → `UnitySynthAudioOutput`/`OnAudioFilterRead`; pure Fx PASS. Named device/reviewer audible acceptance and T060 settings binding remain open — waveforms alone are not FIX10 device PASS. | T061 |
| A09 / high | `Tools/Compile-GameContent.ps1` runs Compound-filtered tests and reports seeds; it does not compile/publish a dependency package and does not use Map to select actual outputs. Catalog visual hashes are hashes of labels. CompoundMapRuntime generates map plans in the release player instead of loading verified compiled content. | T055 |
| A10 / high | **T063 software 2026-11-14:** ADR-037 adds depot measurement IDs + product collector/analysis/wrappers; negatives cover wrong map/roster/profile/build. Twelve captures + soaks remain PendingHardware. Historical audit note: B01-only path and compound-only fixtures were the baseline gap. | T063 |
| A11 / high | ~~Copy/hash-only Package-Release that stamped HEAD and shipped backup/debug folders.~~ **T064/FIX13 software (2026-11-14):** `Tools/Package-Release.ps1` + `Tools/release-package-policy.json` reject dirty/mismatched/absent build identities (no HEAD stamp onto arbitrary binaries), require verified map-package closure, exclude backup/debug/pdb/user files, emit archive/SHA256/inventory/licenses/provenance/rollback pair, and rehearse clean extract + corrupt recovery + prior-pair rollback. Synthetic wrapper suite PASS (`Tools/test_package_release.ps1`, 7 cases); dirty `03fa6dd` client build FAIL as required. **Pending:** player launch / REL01–REL06 on one clean source-matched candidate (T065). | T064 |
| A12 / high | Fresh required `Tools/Test-Contracts.ps1 -Offline` fails restore with NU1004: Foundation now references Generation.Core but `Tools/Contracts.Tests/packages.lock.json` has an older project graph. No Contracts tests execute in that attempt. | T052 |
| A13 / release blocker | P05 remains FAIL under ADR-024. **T062 tooling 2026-11-14:** capture/overlay helpers + defect inventory (ToolingReady); no fourth clearance search; FAIL not relabeled. Named player/UI/soldier review, full matches and reference performance/soak are still missing. Historical waivers do not accept new gates. | T062, T065 |

### Historical verification at f3d9954

Evidence paths are repository-relative and ignored; preserve them before any workspace removal. Initial sandboxed dotnet attempts exited before tests with incomplete diagnostics. The successful/failing runs below were rerun with the process access needed by the existing wrappers. Failed attempts remain separate; no result was changed into PASS.

| Check | Actual outcome | Evidence / limitation |
|---|---|---|
| Specification fixtures | PASS: 35 assertions / 17 JSON files | `python docs/fixtures/verify_fixtures.py`; specification only |
| Documentation integrity | PASS: 117 Markdown pages, 867 links, 152 integrity files | `python docs/verify_docs.py`; audit-time counts, not runtime evidence |
| Source provenance | PASS: 538 asset entries / 269 GUIDs | `Tools/Test-SourceProvenance.ps1`; old 51-metadata/one-Markdown planning failure is no longer current |
| Generation wrapper | PASS: 298 passed / zero skipped | `Artifacts/m1-procedural-feasibility/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093052026Z-82d371e9/` |
| Simulation wrapper | PASS: 61 passed / zero skipped | `Artifacts/m2-combat-harness/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093122259Z-bf34510e/` |
| Game-v1 suite | PASS: 15 passed / zero skipped | Direct `dotnet test ... -c Release --no-restore`; `Artifacts/project-state-audit/f3d9954/audit-game.trx`; not a successful Test-Game wrapper run |
| Contracts wrapper | FAIL before tests: NU1004 | `Artifacts/m0-foundation/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093045793Z-da623fd8/restore.log` |
| C0/content/feature/Game-v1 identity guards | PASS in audit commands/wrappers | Frozen identity only; not runtime conformance |
| Fresh Unity/player/device/hardware checks | NotRun / PendingHardware | Historical build/smoke logs exist, but no new acceptance from this audit |

The Game-v1 session integration test advances only 30 ticks with FakeMapRuntime. Reducer tests use a recording lifecycle. These tests do not exercise the first real respawn, Bootstrap pause, rendered content, audio devices or menu-driven replay; their green results do not contradict the findings.

### Historical interpretation and maintenance

The [2026-11-13 planning validation](planning-validation.md) remains historical evidence. Current next work is [T052–T065](release-remediation-plan.md), replacing instructions to restart T035 or merely collect missing reviews. Preserve T035–T051 requirement IDs and RUN/TDM/MAP/UI/SET/REL acceptance IDs. Future implementation updates this audit's dispositions with source-matched evidence rather than deleting failures. The docs-only follow-up changes no contracts, package versions, generated content, goldens, benchmark fixtures or coordination leases.

### Historical documentation follow-up verification — 2026-11-14

The owner subsequently requested corrected repository docs and implementation/fix plans. This follow-up changes docs/ and AGENTS.md only; runtime source, generated content, frozen fixtures/locks and coordination.json remain unchanged. The index, milestone packs and entry instructions now distinguish current incomplete acceptance from retained historical software claims. New T052–T065/FIX01–FIX14 are planned assignments, not executed repairs.

Fixture verification (35 assertions), documentation links/inventory/hash checks, source provenance (538 entries/269 GUIDs), C0/content/feature/Game-v1 identity guards and whitespace checks pass. Final documentation counts are recorded in verification-report.json; documentation hashes remain delivery integrity only. The first verifier attempt found an unreachable historical view note after navigation edits; its index link was restored before final verification.

The required wrappers were rerun with docs dirty on the same f3d9954 runtime baseline: Generation PASS, 298/298, zero skipped, at `Artifacts/m1-procedural-feasibility/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T094648602Z-05769344/`; Contracts FAIL before tests with unchanged NU1004 at `Artifacts/m0-foundation/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T094640978Z-4e9545e2/`. T052 owns the implementation fix; no lock was relaxed here. No new Unity build/player/visual/audio/hardware acceptance was attempted or claimed by this documentation follow-up.

## Remediation landed in 59316d3 — 2026-11-14

After docs-audit commit `6329262446428f92617600d8526b13f9c90a358a`, T052–T064 remediation was committed to clean `main` as `59316d3`. The implementation-time software summaries below are retained; historical B01–B10 above qualify them. Committed source is not a built/accepted player candidate.

| Task | Software | Remaining |
|---|---|---|
| T052 FIX01 | Lock repair landed | T066 Generation/provenance failures; offline advisory PendingNetwork |
| T053 FIX02 | Software met | Actual-player lifecycle Pending |
| T054 FIX03 | Software met | Player pause/focus device Pending |
| T055 FIX04 | Partial packages/load | T067 catalog/actual visual closure/native nav implementation + release-player proof |
| T056 FIX05 | Software wired | T067 packaged visuals, T068 slot/action views; captures both maps Pending |
| T057 FIX06 | Pure AI met | Mixed-team real-map player Pending |
| T058 FIX07 | Software spawn safety | Native/player review Pending |
| T059 FIX08 | Shell software | 20-launch soak + 720p UI review Pending |
| T060 FIX09 | ADR-036 + UI/audio bind | T069 binding/accessibility consumers; SET02 real display Pending |
| T061 FIX10 | Device callback software | Named audible review Pending |
| T062 FIX11 | ToolingReady inventory | P05 FAIL retained; REL03 open |
| T063 FIX12 | ADR-037 tooling | T070 software multi-match/counters landed; 12 captures + soaks PendingHardware |
| T064 FIX13 | Package-Release software | Clean source-matched client + player launch Pending |
| T065 FIX14 | Checklist + operator runbook | REL01–REL06 NotClaimed until measured |

T065 acceptance steps and commands live in [M11 handoff](milestones/m11-playable-release/handoff.md). No REL gate is PASS; failed P05 artifacts remain; packages/editor were not upgraded by the T065 docs handoff. The implementation is committed; no current release-player acceptance is inferred.

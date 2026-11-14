# Product state audit — 2026-11-14

Reviewed 2026-11-14 at clean `main`, `59316d387e5345716392e332ca225a581a671c33` (184 files changed; T052–T064 remediation committed). The prior audit at f3d9954 is retained below as history. This review inspects current source and reruns pure checks; it performs no fresh Unity build, interactive player/device review or reference-hardware capture. The [next playable-build queue](next-playable-build.md) assigns the remaining work within M8–M11; [original repair cards](release-remediation-plan.md) and [FIX acceptance](release-remediation-validation.md) remain applicable.

## Current disposition at 59316d3

M0–M7 retain historical closures/waivers; P05 FAIL under ADR-024 remains. M8–M10 have substantial committed remediation, with specific implementation gaps and player validation still open. M11 has hardened package/measurement tooling, but no accepted clean current-source playable archive. Historical coordination records are unchanged; they are not current technical acceptance. This documentation assignment changes no runtime, generated content, contracts, locks, workloads or coding leases.

| Area | Implemented in latest commit | Remaining |
|---|---|---|
| Regression | Contracts lock repair and strict TRX counts; Contracts 72/72, Simulation 101/101, Game 38/38 pass | Generation cancellation test fails in fresh full suite; provenance fails on 19 missing metadata entries; current Unity compile/tests/builds unverified (T066) |
| Lifecycle/input | Authority retirement, monotonic life IDs, human/bot rebinding, pause/focus clock and frame input buffering | Actual Bootstrap/player repeated-life and focus evidence; no reason to reassign T052–T054 from scratch |
| Content/maps | Verified package publisher/store, two approved numeric maps, topology/spawn safety | Catalog still runs planners/builders; visual closure contains placeholder skeleton; native bake/install absent; package discovery/recovery needs real player proof (T067) |
| Views | Map renderers, packaged soldier/rifle/pistol, binder/corpse cap, authority slot/action view, visual inventory tooling | T068 software drives slot/events; FIX05 player captures Pending; P05 review open |
| AI/TDM | Enemy filtering, path-corner following, reload commands, 9/11-bot scheduling and spawn ranking/deadline | Native-world mixed-team combat, repeated respawn and full match matrix on both maps/sizes |
| Shell/settings/audio | Shell-owned Main→load→play→pause→results, score tables, JSON schema 2/store/editor, device applier, synthesized callback | Binding editor/input application absent; some accessibility preferences not consumed; named UI/display/audio reviews and shell cycles (T069) |
| Measurement/delivery | ADR-037 product collector/wrappers/analysis; clean-build package guards/archive/inventory/launcher | Multi-match soak host missing; counter semantics need repair (T070); clean independent player/rollback handoff (T071); final T062/T065 acceptance |

REL01–REL06 remain Open / NotClaimed. A local owner playtest candidate and a fully accepted release are separate checkpoints, defined in the [next-build plan](next-playable-build.md); neither is claimed by this audit.

## Current source findings

Paths start at `UnityProject/Assets/Shooter/` unless prefixed Tools. These are source observations; no interactive reproduction is claimed.

| ID / priority | Current evidence | Bounded follow-up |
|---|---|---|
| B01 / blocker | Source provenance reports 19 missing `.meta` entries introduced with new files/folder. See exact list below. | T066; RUN00/REL05 |
| B02 / blocker | Full Generation suite: 310 pass / 1 fail; `CompilerTests.BusyKeyLockCanTimeOutOrCancelWithoutPublication:120`, expected Cancelled, actual Busy. Cause unresolved; timing-sensitive 25/50 ms deadlines. | T066; preserve failure, resolve deterministically |
| B03 / high | `Content/Maps/StaticMapCatalog.CreateReleaseSeed` builds gameplay via planners and eight MapPackageBuilder.Build calls. `FoundationEntry.StartProduct` calls this in release. | T067: load persisted catalog/gameplay bytes, remove shipping generation |
| B04 / high | `MapPackageBuilder.AddVisualClosure` has a single-joint skeleton, 1×1 material and descriptors, without actual soldier/rifle/pistol closure. `ProductMapView` constructs box visuals; `ProductActorPresentation.InitializeShared` generates soldier/rifle geometry at runtime. | T067: publish/consume actual visual dependencies |
| B05 / high | `Generation/Editor/Compilation/UnityNavMeshBakePublisher` is a constants/pending-string stub. Builder uses BuildWalkableGrid; CompoundMapRuntime verifies nav bytes but path queries use CompoundConnectivity. | T067: real native bake/install/path and spawn/route checks |
| B06 / high | ~~Product actor renderer always creates Held rifle, ignores ActiveWeaponSlot, and starts 2.4s reload from magazine decrease.~~ **T068 software:** `ProductAuthorityActionView` + presentation consume `ActiveWeaponSlot` and Reload/Shot events with ActionPayload timing; packaged rifle/pistol meshes. Player FIX05 captures remain Pending. | T068 player Pending |
| B07 / high | ~~Settings binding count / fixed keys / missing accessibility consumers.~~ **T069 software:** editable bindings with conflict swap/cancel, applied CaptureFrame/scoreboard, ReducedFlash/Subtitles/ReducedCameraMotion consumers, device resolution enumeration and Keep-before-persist display. Named FIX08–10/SET01–02 UI/device/20-cycle evidence remains Pending. | T069 device/UI Pending; T071 playtest |
| B08 / high | ProductTdmHost starts once with no replay; 30-minute/108000-tick soak exceeds 15-minute/54000-tick match, after which Bootstrap stops ticks. Respawns counts net alive growth; querySamples is AI ray/path sum; contentBindings assigned one; BeginLoad resets WorkloadId set by host. | T070: multi-match capture, defined real counters/identities; do not extend match rules |
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
| A05 / high | `TeamDeathmatchMatch.TryPickSpawn` chooses the lowest-ID statically valid slot without actor occupancy/enemy-pressure ranking. `CompoundCatalogFactory` supplies six initial slots per team rather than the specified minimum twelve and can repeat fallback positions. Retry updates the due tick used for timeout; preserve an independent first-failure deadline. | T058 |
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

After docs-audit commit `6329262446428f92617600d8526b13f9c90a358a`, T052–T064 remediation was committed to clean `main` as `59316d3`. The implementation-time software summaries below are retained; current B01–B10 above qualify them. Committed source is not a built/accepted player candidate.

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
| T063 FIX12 | ADR-037 tooling | T070 multi-match/counter repairs; 12 captures + soaks PendingHardware |
| T064 FIX13 | Package-Release software | Clean source-matched client + player launch Pending |
| T065 FIX14 | Checklist + operator runbook | REL01–REL06 NotClaimed until measured |

T065 acceptance steps and commands live in [M11 handoff](milestones/m11-playable-release/handoff.md). No REL gate is PASS; failed P05 artifacts remain; packages/editor were not upgraded by the T065 docs handoff. The implementation is committed; no current release-player acceptance is inferred.

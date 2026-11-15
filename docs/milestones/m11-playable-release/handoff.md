# M11 — Playable build and acceptance — handoff and gate record

## T081 — Architectural surface and place identity

2026-11-15 owner-authorized continuation from `615b6a5`, sole integrator. [Task scope](../../task-supplement.md#t081--architectural-surface-and-place-identity), [ADR-050](../../decisions.md#adr-050--accepted-packaged-architectural-surface-construction-t081). Map mesh generation now adds small physical bevels, footing/coping/end bands and panel joints to long walls, with cargo corrugation/rails/door hardware and blue/oxide painted steel. Map/menu share six material roles; variants share texture planes. Menu skyline receives loading-bay facades; weapons receive dark rougher gunmetal/matte-polymer coating. Gameplay collision/nav/spawn/hit/weapon rules and soldier/P05 sources remain unchanged. No imported/AI art, authored animation or runtime fallback.

Initial working-source checks at base `615b6a5`: Generation 391, Contracts 76, Game 68, Maps 55 and provenance 736 entries / 368 GUIDs PASS. Unity initial 52 EditMode PASS before the final facade/coating/two-map material checks; final working-source EditMode **53/53 PASS** at `tests-editmode-20261115T134227748Z-a52a3d27` under `Artifacts/m0-foundation/615b6a54b0237e8bb962dfa537683a5df33fd168/`. Earlier 52/53 run `tests-editmode-20261115T134056782Z-044e30cf` failed its old polished-metal smoothness assertion (>0.5 versus the intentional new coating 0.46); the test now specifies the coated finish and dark tint. Failure retained. Later publication/build/installer/player evidence follows below. Old 0.3.9 remains the exact prior binary/content pair, not a build of this source.

Scope limitations: first-person hands, anatomy/uniform construction and full weapon/reload continuity remain unresolved. P05 FAIL / VD / REL / full gameplay / installed-upgrade / GPU/thermal/reference acceptance remain open. More detailed geometry is not reference-machine performance evidence. Revert the implementation and restore an exact retained binary/content pair for rollback.

## Archived T079 alternate branch integration — 2026-11-15

Owner explicitly requested merging the highlighted `archive/t079-env-alt-2415153` ref. It is an archive tag at `241515387f2ed1780e05c154b1b879080dbcd69a`, not a live branch, and was not an ancestor of `main` (`ffd0754`). Merge resolution integrates its distinct plaza/yard lamp posts, outer fence rows, full-length axis-aware ridge caps and package/prop tests. Existing main gate lamps, inner fence stubs, container corners, finer concrete/metal/trim classification and all T080 metric UV/menu-card fixes are retained; alternative material assignment/tint implementations are superseded by those newer consumers. Short roof ridges are replaced, avoiding duplicate ridge geometry.

Validation of the resolved working tree (sourceDirty=true at base `ffd0754`): Generation **391**, Contracts **76**, Maps **55**, Unity EditMode **52** and source provenance **736 entries / 368 GUIDs** PASS. Generation includes both maps' unchanged gameplay box/count/collision fingerprints and visual symmetry/budget checks. Evidence: `Artifacts/m1-procedural-feasibility/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/20261115T133038119Z-0249d5bb/`; Contracts `Artifacts/m0-foundation/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/20261115T133206617Z-8e9f6658/`; EditMode `tests-editmode-20261115T133057239Z-c7cf4d57/` under that same source root. Map wrapper results follow its retained report `Artifacts/m8-runtime-composition/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/test-maps-20261115T133215973Z-e4a5d92d/`.

This is source integration only. Visual mesh/package identities change; native content publication, new Client/Server builds, PlayMode/player review and a new installer are **NotRun** for this merge. The 0.3.9 installer below remains the prior `beac9ae` binary/content pair and does not contain these newly merged props. P05 and VD/REL acceptance remain unchanged. The archive tag is retained; no temporary branch/worktree was created, and the unrelated active `t080-bot-tactics` worktree is preserved.

## T080 — Visual review and material-scale correction

2026-11-15 owner-authorized visual review and implementation, base `7b05e9e`; sole integrator, no temporary branches/worktrees. Scope and rollback: [task card](../../task-supplement.md#t080--visual-review-and-material-scale-correction-2026-11-15).

Implemented: metric face UVs for scaled verified boxes; shared two-metre texture scale across map and menu; actual-bounds operation-card framing with preserved concrete/metal/plaster roles; scoped ambient card lighting. The menu uploader previously flattened all submeshes; corrected to preserve the packaged ranges. Small unit-box CPU buffers stay readable for loading-time UV clones; large map meshes still release CPU buffers. Clones follow existing resource ownership. Cached content, gameplay/nav/hit volumes, weapon/soldier/P05 identities, locks/settings/toolchain and installer identity remain unchanged.

Review: current T079 source plus retained T075 player/staging images. Computer Use observed the already-installed Shooter menu; its binary identity was not established, so this is not claimed as a 0.3.8 capture. It shows prominent mannequin soldiers, repetitive box structures and tiny pale map previews. The subsequent source-matched player review is recorded in the delivery section below.

Failures retained under `Artifacts/m0-foundation/7b05e9e9f36863414171a06316cae5e4288deb78/`: sandbox Unity `tests-editmode-20261115T131127202Z-13e333a0` exited before tests with licensing/WMI access failure; sandbox Contracts restore failed before tests. Permitted EditMode rerun `tests-editmode-20261115T131251011Z-3281048f` found the discarded-submesh defect (three materials on one submesh); source upload was fixed, not the assertion weakened.

Pre-build checks: Contracts 76, Generation 387, Game 68, Unity EditMode 52 and PlayMode 20 PASS on the dirty T080 working source (reports remain under base `7b05e9e`; not relabelled clean-source runs). Source provenance 736 entries / 368 GUIDs, fixture 35 assertions / 17 JSON files and docs checks PASS. These test runs began before the implementation commit; their base source/dirty flags are retained. Final clean-source build/package/installer verification follows below. P05 FAIL, named comparison, full gameplay, installed upgrade, GPU/thermal/reference proofs and VD/REL acceptance remain open. This is a bounded material/composition correction, not AAA visual acceptance.

### T080 delivery 0.3.9

Binary source **`beac9aec1e1af0548c4b396f380f3ef010f067cd`**, source hash `309bbcfb86e30ae06a470b8ca648f79204caecb7edd392865ac336184fd34763`. Later documentation commits do not relabel the binary. Locked Unity 6000.3.28f1 / URP 17.3.0; native content closure `a4b2827a9cdd453db131539db49081ab7873f9ee46fd744a7f6dd238f2250794` is reused from T079, with no generator/content edits.

| Check | Actual evidence |
|---|---|
| Client | Clean IL2CPP PASS, `Artifacts/m0-foundation/beac9aec1e1af0548c4b396f380f3ef010f067cd/build-client-20261115T131800764Z-fb383fbf/` |
| Server / smoke | Clean build PASS with zero graphics source assets, `build-server-20261115T131952744Z-f971cb30/`; smoke PASS `smoke-server-20261115T132350139Z-7b8eb3bd/` under the same source root |
| Native package | PASS, 294 files, native requirement, extract/corrupt/rollback software rehearsals; `Artifacts/m11-playable-release/beac9aec1e1af0548c4b396f380f3ef010f067cd/package-20261115T132000149Z-5695e5dd/` |
| Installer | PASS **0.3.9**, 26,446,324 bytes; SHA256 `fa0c684d3613312befdfc8046ed97443948f69f02e9cc6337cd35119b703a521`; stable AppId / preference policy, predecessor 0.3.8 |
| Player review | Inventory-verified short copy `Artifacts/T080Preview` (294/294 hashes); observed Main with enlarged distinct cards, Depot 5v5 load, pause/leave return, Compound 6v6 load. Logs + inventory review retained in `Artifacts/visual-design/t080/`. Automated limited review, not named visual acceptance or a full match |

Installer: `C:/Job/shooter/Artifacts/m11-playable-release/beac9aec1e1af0548c4b396f380f3ef010f067cd/installer-20261115T132256394Z-24c64b00/installer/Shooter-Setup-0.3.9.exe`. Actual installation/upgrade was **NotRun**. The wrapper initially failed on sandbox-redirected ISCC discovery and then temporary-junction access; permitted rerun used the exact pinned compiler and passed. Failed reports are retained.

Direct player launch inside the deep evidence package path reproduced the known Windows content-path failure; the short inventory-matched copy loaded both maps. This limitation remains, with `deep-path-failure.log`; do not recommend direct launches from deeply nested evidence paths. No long-path runtime repair is claimed.

Review conclusion: map cards and material consistency improved, but current menu soldiers remain visibly mannequin-like; live spawn views retain long unbroken box walls, basic equipment and no convincing first-person hands. The requested BFV quality goal is **not achieved**. Further architecture/character/hand/action work, original P05 disposition, named comparisons, full matches, installed upgrade and reference-machine gates remain open. All VD/REL acceptance remains NotClaimed.

**Prior preview — 2026-11-15:** T079 installer **0.3.8**, binary source `07c5db7` (env/menu/combat design wave). [Delivery evidence](#t079--design-wave-installer-038). Prior installer **0.3.7** at `e251702` remains a valid rollback pair. VD/REL acceptance remains open and P05 FAIL is unchanged.

## T079 — Material-split maps, menu staging, combat presence

Owner-directed continuation after T078. Three worktrees merged to main: env `7bfa1ec` → `7c5a4cd`, menu `5f32a02` → `ea40c07`, combat `0936190` → `73a18e4`. `TacticalUi` auto-merged (menu MapCard + combat HUD helpers). Temporary `visual/t079-*` worktrees/branches removed after preservation.

| Slice | Software | Evidence |
|---|---|---|
| Env | Concrete/metal/trim solids submeshes + visual prop enrichment; gameplay collision unchanged | Generation 387 / Maps 53 PASS at `73a18e4` |
| Menu | BFV-like staging depth bands, packaged materials, stronger operation cards | EditMode assertions extended; Unity EditMode run Pending (editor contention) |
| Combat | Camera bob, hit marker/damage flash, thinner HUD, denser FX | Simulation 211 PASS |

Native `Compile-GameContent -Native -Force` Verified (`allNative=true`) for merge source `73a18e4` (docs tip `07c5db7`). Provenance 736/368 PASS. Player comparative review and VD exits remain Pending. P05 unauthorized.

### T079 — Design wave installer 0.3.8

Offline-only delivery from clean `07c5db7` (includes T079 merges + docs). Locked Unity 6000.3.28f1 / 8f082d2fe476.

| Gate | Result | Evidence |
|---|---|---|
| Pure lanes | Generation 387, Maps 53, Simulation 211, provenance 736/368 PASS at merge | `Artifacts/*/73a18e42…/` |
| Content | `Compile-GameContent.ps1 -Native -Force` PASS, unity-native-navmesh | `Artifacts/map-packages-native` + compile-gamecontent under `73a18e42…` |
| Client / smoke | IL2CPP client PASS; client smoke PASS | `Artifacts/m0-foundation/07c5db7f…/build-client-20261115T125203351Z-59cca004/`, `smoke-client-20261115T125453238Z-41220655/` |
| Package | `Package-Release.ps1 -RequireNativeNav` PASS, 294 files, content closure `a4b2827a…` | `…/package-20261115T125501399Z-75276c03/` |
| Installer | `Shooter-Setup-0.3.8.exe`, 26,433,023 bytes, SHA256 `13954c29686ddd42e7d0c3df01b9414d11aa8b0eb90c78d44ee89482dad66a56`, predecessor 0.3.7, AppId unchanged | `…/installer-20261115T125512181Z-c97a7959/installer/` |

Pending: actual install/upgrade and named visual/gameplay review of T078/T079 presentation. `releaseAcceptance` NotClaimed.

## T078 — Architectural / FP / material readability (source merge)

Owner-directed continuation toward Battlefield V / Warface / Bodycam design. Three disjoint worktrees merged to main: env `d56c34f` → `25ae4e5`, weapons `8e88a68` → `836b173`, materials `b5683e1` → `ddce776`. Temporary `visual/t078-*` worktrees/branches removed after preservation on main.

| Slice | Software delivered | Limitation |
|---|---|---|
| Env | `ArenaVisualDetail` non-gameplay eaves/frames/windows/trim/curbs; `BuildSolidsMesh` includes roofs/visual boxes; gameplay collision unchanged | Packaged mesh identities stale until `Compile-GameContent -Native`; VD5 Pending |
| Weapons/FP | Near-field scale, ADS-safe kick, magazine travel arcs, denser pooled FX; weapon recipe 1.0.1 untouched | Player handling review Pending |
| Materials | Concrete/cover/soil/asphalt/apron response profiles; cover inset; sky/probe tune | Player material review Pending |

Pending: Generation/Maps/Simulation/Unity verification of merged source, native republish, Client/package/installer, named visual review. P05 unauthorized.

## Bot navigation, spawn fallback and pre-match countdown — 2026-11-15

Three behaviour changes merged into `main` on 2026-11-15. Commits: bot NavMesh `70538c4` and `bd5ffb3`; relaxed spawn `8c7df18`; countdown `cd52534` and merge `3785db4`.

| Fix | Behaviour | Decision / docs |
|---|---|---|
| Bot NavMesh winding | `UnityNavMeshBakeInstaller` installs triangles wound face-up via `UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices`. The native clockwise bake had been installed face-down, giving an effectively empty NavMesh, so bots failed every path and idled at spawn. Code-reading hypothesis, not yet confirmed on a real player. Product composition enables `CompoundMapRuntime.AllowPackagedPathFallback` (sample miss or native failure falls back to the packaged nav graph; fallback corners capped at 120). New counters: `NavStartSampleMisses`, `NavGoalSampleMisses`, `NavCalculatePathFailures`, `NavPartialPaths`, `NavFallbackPathsUsed`, `NavFallbackPathsFailed`. `SHOOTER_AI_SUMMARY` is written to Player.log every 300 ticks. | [Bot navigation](../../features/team-deathmatch.md#team-bot-behavior-and-capacity), [AI note](../../features/ai.md) |
| Relaxed spawn fallback | `SpawnSafetyPolicy.SpawnRelaxAfterTicks` = 120. After 120 failed active ticks the 40 m enemy line-of-fire rejection is dropped; slots are ranked by farthest nearest enemy, least-recently-used, then lowest slot ID. Retry cadence (30 ticks) and failure deadline (600) are unchanged. | [ADR-047](../../decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure) |
| Recoverable spawn failure | On abort, `OfflineGameSession` records `GameErrorCode.SpawnFailure` in a non-frozen `TerminalFault`, moves to Stopped, and `Step` returns Ok as a no-op. `ProductShellHost` returns to the main menu with "No safe spawn available. Returning to the menu." Previously the match stopped silently while still Playing, soft-locking the player on "Awaiting a safe spawn". IGameSession and Game-v1 are unchanged. | [ADR-047](../../decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure) |
| Pre-match countdown | `SessionDefaults.PreMatchCountdownTicks` = 300, read through `OfflineGameSession.PreMatchTicksRemaining`. Phase stays Playing; no `GameMatchPhase` or Game-v1 change. Human input, bot planning, respawns and end-of-tick evaluation are held; the clock and timeout stay at 0. HUD shows "MATCH STARTS IN N" then "FIGHT!". Only `ProductShellHost` sets 300. | [ADR-048](../../decisions.md#adr-048--accepted-session-internal-pre-match-countdown), [Team Deathmatch](../../features/team-deathmatch.md#pre-match-countdown) |

Tests added: `ProductBotNavigationTests` (PlayMode, not yet run in Unity), `NavBakeInstallWindingTests` (xUnit), `SpawnRelaxedFallbackTests` (3), `PreMatchCountdownTests` (4).

### Pure-lane results (recorded from the merged `main`, 2026-11-15)

| Lane | Result |
|---|---|
| `Tools/Test-Generation.ps1` | PASS, 376 |
| `Tools/Test-Simulation.ps1` | PASS, 204 |
| `Tools/Test-Game.ps1` | PASS, 68 |
| `Tools/Test-Maps.ps1` | PASS, 51 |
| `Tools/Test-SourceProvenance.ps1` | PASS |
| `Tools/verify_contract_lock.py` | PASS |

### Pending (not run; never PASS without evidence)

- Unity EditMode and PlayMode, including `ProductBotNavigationTests`, the countdown HUD and the relaxed-spawn path in the product shell.
- Client build (`Tools/Build.ps1 -Target Client`) and a fresh Client/Server identity for this change set.
- Real-player playtest: bots moving on both maps, `SHOOTER_AI_SUMMARY` values, respawn after the relaxed fallback, the SpawnFailure menu return and the countdown feel.
- Confirmation that the empty-NavMesh cause was the face-down winding; until the player log shows fewer path failures, this stays a hypothesis.

## Physics gap fixes — motor overlap recovery, stand-up sweep, muzzle cover, stuck-actor rescue

Owner request 2026-11-15: audit the physics path against [gameplay baseline](../../gameplay-baseline.md#motor-camera-and-weapons) and fill missing pieces with agents. Merged to `main` at `956a27d` and `e6312bd`. Worker worktrees and branches were removed after the merges.

| Gap found | Fix | Paths |
|---|---|---|
| Spec "overlap recovery max 4; unresolved overlap stops and reports invalid spawn" was unimplemented: `MaxOverlapRecovery` was unused and the motor never called `IWorldQueries.Overlap`. | At step start, up to 4 passes push the feet out along the deepest hit (depth > 1e-4; ties go to the lowest stable id) by depth + skin, and remove velocity into the normal. Measured penetration left after the last pass, or `BufferOverflow`, keeps the previous feet with zero velocity and sets the new optional `MotorBodyState.OverlapUnresolved`. `InvalidInput` (near contact without penetration) skips recovery so valid actors are not frozen. | `Simulation/Motor/KinematicMotor.cs` |
| The stand-up ceiling check was a single ray from the head-sphere centre. A 0.32 m capsule could stand into an off-centre ledge. | Upward `SweepCapsule` of the current capsule by the growth delta. | `KinematicMotor.cs` |
| The muzzle-inside-cover probe could never fire with Unity, because a ray starting inside a collider does not report it. The muzzle sits 0.45 m ahead of the eye but the body radius is 0.32 m, so shots against a wall passed through it. | Eye→muzzle segment against `WorldSolid` blocks the shot (`BlockedAtMuzzle`). The old probe is kept. | `Simulation/Combat/HitscanResolver.cs` |
| Nothing consumed an unresolved overlap, so an embedded actor stayed frozen forever. | After the harness step and lifecycle sync, `OfflineGameSession` relocates each living actor with `OverlapUnresolved` to a `SpawnSafetyPolicy`-picked team slot through the new `CombatHarnessSimulation.RelocateActor` / `HarnessActorLifecycle.RelocateLife`. This is a position rescue only: no death, score, LifeId/EntityId, health or ammo change. Bots reset their command epoch and path state (`TryReplaceLife`). With no safe slot the actor stays put and is retried next tick. | `Game/Session/OfflineGameSession.cs`, `Simulation/Foundation/CombatHarnessSimulation.cs`, `Simulation/Session/HarnessActorLifecycle.cs` |

**Out-of-world kill volume (owner request 2026-11-15, merged at `1cd6280`).** `Simulation/Foundation/WorldKillVolume.cs`: a living actor dies when feet.Y < `Bounds.Min.Y` − 10 m, when X/Z leave the authored map `Bounds` by more than 10 m (strict comparisons), or when any coordinate is NaN. `CombatHarnessSimulation` runs the check once per tick after movement and bullet resolution, so a bullet kill on the same tick wins. It is enabled only through `SetWorldKillBounds`, which `OfflineGameSession` calls at map load. Harness-only tests and B01 are therefore unchanged. The death is an ordinary `CombatEventKind.Death` with an invalid instigator, so it maps to `DeathResolution.KillerEntityId = null`: an environmental death per [TDM kill credit](../../features/team-deathmatch.md) (victim deaths +1, no team point, normal 180-tick respawn). It runs before the stuck-actor rescue, so fallen actors are never rescued. Killfeed (merged at `231abaf`): killer-less deaths read "Victim  >  ENVIRONMENT" through the pure `Presentation/Hud/KillFeedText.cs`. The label is cause-neutral because the Death event carries no cause. `Test-Simulation.ps1` **190/190 PASS** and provenance **PASS** (684 entries / 342 GUIDs) at `231abaf`. The `ProductSessionView` call site has not been compiled in Unity. Tests: `Harness/WorldKillVolumeTests.cs` (thresholds, fall, unset bounds, side exit, single death) and `WorldKillVolumeSessionTests.cs` (deaths +1, no kills, respawn scheduled). Evidence at `1cd6280`: `Test-Simulation.ps1` **187/187 PASS**, `Test-Game.ps1` **61/61 PASS**, `Test-SourceProvenance.ps1` **PASS** (682 entries / 341 GUIDs).

No Contracts, Game-v1, lock, schema, fixture or telemetry field changed. Tests added: motor recovery/unresolved/resting/off-centre ledge (`Tools/Simulation.Tests/Motor`, real AABB penetration in `StaticPlaneWorld`); Unity-like eye/muzzle wall (`Combat/CoverHitscanTests.cs`); `RelocateActor` (`Harness/CombatHarnessTests.cs`); human/bot rescue and no-flag no-move (`Tools/Game.Contracts.Tests/EmbeddedActorRescueTests.cs`). `CrouchCeilingFailureKeepsStandingHeight` now starts crouched because its old spawn was inside the ceiling, which is now an invalid spawn.

**Evidence at `e6312bd` (clean tree, offline restore):** `Test-Simulation.ps1` **176/176 PASS**, `Test-Game.ps1` **60/60 PASS**.

**Pending / not claimed:** no Unity EditMode/PlayMode run and no player build. Real `UnityWorldQueries.Overlap`/`ComputePenetration` behaviour, rescue in a live match and per-tick query cost (one extra `Overlap` per living actor) are unverified. Initial-overlap `CapsuleCastNonAlloc` hits (distance 0, normal −direction) are still passed through unchanged by the adapter. Fall damage, ragdoll, projectiles, penetration, grenades and actor–actor blocking remain deferred by the baseline, not gaps. No existing candidate archive contains these changes.

## VD presentation polish — owner-authorized parallel slices

Owner request 2026-11-14: implement what is possible toward Battlefield V / Bodycam / Warface quality using parallel agents. Draft [VD0 art brief](../../visual-art-brief.md) and expanded [defect inventory](../../visual-defect-inventory.md) land with three presentation slices merged into `main`; temporary `.local/worktrees/vd-*` trees and `agent/vd-*` branches were removed after content preservation on main.

| Slice | Paths | Worker evidence |
|---|---|---|
| Map surfaces / atmosphere | `ProductMapView`, `GeneratedSky`, `GeneratedReflectionProbe`, `GeneratedSkyTests` | Generation **330 PASS** in `vd-world-surfaces` |
| Combat FX | `AnalyticFxPool`, `AnalyticFxPresenter`, minimal `ProductSessionView` hooks, `FeedbackTests` | Simulation **131/131 PASS** in `vd-combat-fx` |
| Menu / HUD craft | `ProductMenuScene`, `TacticalUi`, `ProductShellHost` | Shell/Hud filter **14/14 PASS** in `vd-menu-craft` |

**Out of scope / still Pending:** P05 soldier clearance, contract/content-lock changes, VD0 owner frame approval, VD1–VD9 acceptance, Unity EditMode/player visual review, reference-hardware PASS. Packaged map materials remain largely 1×1 albedo-only until Generation packaging expands normals/masks. All VD exits remain Pending; this merge is software polish only.

## T074 — scene-led menu and tactical HUD preview

Subsequent owner planning request establishes the [VD0–VD9 quality track](../../design-milestones.md), targeting Battlefield V / Bodycam / Warface-level finished design. The plan adds concrete visual exits inside M8–M11 and preserves this source/evidence record. No runtime or candidate change is made by the planning assignment; all new VD acceptance is Pending. VD9 requires comparative owner sign-off together with the existing REL/installer/reference gates.

Current visual preview: **Windows installer 0.3.1**, binary source **`b531da9cceb0607b9c5046bb1700077fd9cb8d4a`**. The owner's six Battlefield screenshots superseded the contour-only Main: the new menu has a live procedural staging scene, compact left navigation and rendered cards for both maps. The compact HUD adds compass/score, a friendly-only minimap, squad, ammo/reload and four recent elimination rows. [Brandbook](../../brandbook.md) and [scope](../../task-supplement.md#t074--visual-redesign-stage-1-shell-and-combat-hud) define the code and later visual stages. All completed source is committed to main; no task worktrees were created.

```text
Artifacts/m11-playable-release/b531da9cceb0607b9c5046bb1700077fd9cb8d4a/installer-20261114T223635947Z-fc640123/installer/Shooter-Setup-0.3.1.exe
Bytes: 26114012
SHA256: 05cf16e263d2bbef8032e41dff5945994395bda53c22c94e8a918f42cb8b133c
```

AppId `B01E0F69-2123-4D91-AB59-88864A944E39`, the per-user upgrade policy and schema-2 preferences are preserved. This task built the installer but did not execute its install/update flow. INS01–INS06 retain their existing individual dispositions. `53cb9ea` updates packaging text only; later documentation does not relabel the binary. ZIP remains an internal preview.

### Source-matched evidence

The following Unity directories share prefix `Artifacts/m0-foundation/b531da9cceb0607b9c5046bb1700077fd9cb8d4a/`.

| Check | Actual result / retained evidence |
|---|---|
| Client | PASS, IL2CPP, non-development, sourceDirty=false; `build-client-20261114T223131165Z-cb3c8dbe/` |
| Server | PASS, sourceDirty=false, no graphicsSourceAssets; `build-server-20261114T223407718Z-c276b57e/` |
| EditMode / PlayMode | **29/29** / **16/16**, zero skipped; `tests-editmode-20261114T222917776Z-5c34addc/`, `tests-playmode-20261114T223103219Z-a68277c3/` |
| Visual regressions | Cold initial FP bind assigns the rifle before slot switching; menu renders three packaged soldiers and both map cards with zero colliders/rigidbodies. Editor capture: `Artifacts/t074-visual-review/editor-20261114T223031255Z/staging-area.png` |
| Native package | PASS, eight native packages / 246 inventoried files; `Artifacts/m11-playable-release/b531da9cceb0607b9c5046bb1700077fd9cb8d4a/package-20261114T223625514Z-04d80c8c/` |
| Package / installer regressions | **7/7** (`Artifacts/tooling-tests/package-release/20261114T223612066Z-8390febd/`); **11/11** (`Artifacts/tooling-tests/build-installer/20261114T212025155Z-63da5cc5/`) |
| Source / content | Source hash `92a9a34a0b59844b7b46902a066d1b205cc7d2c276b1a178bf13c5fab96061d5`; unchanged native closure `a494f206aaeb6838e4121fb837324e8266635a8bc2236722520eddf44bd4c0a4`; seeds 460072030 / 460072031; locked toolchain unchanged |
| Archive | SHA256 `6b61451e2df1561b17cf08353e7017c3230e117b2b4d4dcb4034376837a99ce4`; software extract/corrupt/rollback rehearsals PASS, no player acceptance implied |
| Contrast math | Palette / worst-white-world backing calculations PASS: `Artifacts/t074-visual-review/b531da9cceb0607b9c5046bb1700077fd9cb8d4a/palette-contrast.json` |
| Player startup | Short extraction `Artifacts/t074-scene-preview-031/`; raw `Artifacts/t074-visual-review/b531da9cceb0607b9c5046bb1700077fd9cb8d4a/player.log` records `SHOOTER_MENU_SCENE_READY actors=3 cards=2 colliders=0 authority=0` and `SHOOTER_READY_CLIENT` |
| Final interactive review | **Pending — owner stopped Computer Use with physical Escape**. Window left available. No final-source match/HUD/scoreboard/results, keyboard/device or 100%/150% visual acceptance is claimed |

Earlier pure runs in this task, not fresh b531 all-suite results: Contracts/Foundation **73/73** at e3851f1 (`20261114T215050895Z-959e58c0`); Generation **329/329** at e462821 (`20261114T210952690Z-68fb71d0`); Game **57/57** at 0aa57aa (`20261114T214331162Z-e45eac24`); Simulation **128/128** at 0aa57aa (`20261114T214514945Z-06cff397`). Reports remain in their normal milestone artifact roots. Final Unity/build evidence above is source-matched.

### Repairs and open checks

- Preserved independent bot fix `2ee1a3d`; repaired its direct client `Shooter.Simulation` assembly reference after real CS0012 failure. The visual task changes no bot rules.
- Earlier b3f54ce play counted a 3.5278115-second visual bind as combat time and exited on overload. `e3851f1` rebases suspended-to-active time; its regression preserves real active overload/fractional debt. A prior e385 6v6 player reached a live 150%-scale HUD beyond that failure; this is not final b531 gameplay acceptance.
- Deep internal extraction failed visual-file loading; a short extraction resolved it. Long-path robustness stays open. Use the normal installer path or a short preview path. Earlier raw logs/captures remain under `Artifacts/t074-visual-review/b3f54cefd358016b8dee71336e0429eb93addd5b/` and `e3851f1ff37cfbc390df72eb222f665b486a634c/`.
- Corrected 150% contour transforms, Linear-color IMGUI font tints, backed HUD hints and once-per-frame Escape recovery. The scene-led Main supersedes the contour-only version. Final device review was interrupted.
- Retained failures: initial sandboxed Unity socket crash; Generation zero-allocation assertion (328/329) and loose audio host-time assertion (127/128), each followed by a passing serial full-suite rerun. No generator/audio/assertion relaxation. Initial scene compile missed a MeshData namespace, then passed after correction. Evidence remains under 9f2219f / 0aa57aa / 3d67279 artifact roots.
- Existing soldier generator/rig remains non-accepted. Menu composition/palette is cosmetic; no soldier clearance search or P05 waiver. Environment, soldier, hands, magazine/optics and animation detail remain staged work.
- Full normal matches/results/replay, two-map routes/spawns, player recovery/installed rollback, named UI/device/audio review, reference captures/soaks and REL01–REL06 remain Pending/NotClaimed. **P05 FAIL remains.** Hashes, documentation and diagnostic scene renders are not those gates.

**Current disposition — 2026-11-14 T072:** Slide-enabled 5eaced5 preview is built/packaged and independently reaches Main; installer/upgrades T073 remain planned. Earlier review from 84befb9 / a54ed83 follows: M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. The `114815c` archive remains historical/superseded for measurement. Use the [playtest guide](../../playtest-guide.md), [audit](../../product-state-audit.md), [remaining tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). No online multiplayer or hosted server is required for the first local-bot version.



## T072 — Slide-enabled playtest delivery

**Delivered preview source:** `5eaced5fe526ec9dbb8e5c18f1defd9235036808` (clean non-development Client request); later documentation commits do not relabel this binary. Local default: sprint forward with W + Left Shift, press F; Space cancels. No online game/server is required. This is a ZIP playtest candidate; final installer/update delivery is planned T073, not implemented.

| Check | Actual result / repository-relative evidence |
|---|---|
| Contracts / Generation / Simulation | Clean `856d628` passes 72 / 319 / 119, zero skipped; code/ports unchanged by the later bounded settings fix. `Artifacts/m0-foundation/856d6285f832724a3e77d16a7f9621d73e4837d9/20261114T163202629Z-8530085b/`, Generation `…/20261114T163216892Z-807aa5cc/`, Simulation `…/20261114T163340705Z-ce95c7eb/`. |
| Final Game suite | Clean `5eaced5fe526ec9dbb8e5c18f1defd9235036808` PASS 56/56, zero skipped, `Artifacts/m8-runtime-composition/5eaced5fe526ec9dbb8e5c18f1defd9235036808/20261114T172026341Z-d762b678/`. |
| Final Unity EditMode | Clean `5eaced5fe526ec9dbb8e5c18f1defd9235036808` PASS 26/26, `Artifacts/m0-foundation/5eaced5fe526ec9dbb8e5c18f1defd9235036808/tests-editmode-20261114T172050192Z-c40e58ef/`. |
| Final Unity PlayMode | Clean `5eaced5fe526ec9dbb8e5c18f1defd9235036808` PASS 13/13, `…/tests-playmode-20261114T172149395Z-c7d66562/`; real slide sweeps, ceiling recovery and all quality tiers. |
| Source provenance / specifications | PASS 662 entries / 331 GUIDs; C0/Game/content guards, 35 specification assertions, legacy/new settings JSON schemas and doc/link/hash checks pass. |
| Content | Clean Compile-GameContent Force PASS, 8 packages, `Artifacts/m8-runtime-composition/5eaced5fe526ec9dbb8e5c18f1defd9235036808/compile-gamecontent-20261114T170256678Z-16c13e1b/`; contentClosureHash `715e762129848f4942f908e896ead3f2fa42a8d2b19cb2ab5612339647e41f07`. Native bake/player route review still Pending. |
| Client | PASS `Artifacts/m0-foundation/5eaced5fe526ec9dbb8e5c18f1defd9235036808/build-client-20261114T171221410Z-229cc4a0/`, IL2CPP, non-development, sourceDirty=false. |
| Server isolation regression | PASS `…/build-server-20261114T171748901Z-016d4ae2/`, sourceDirty=false; graphicsSourceAssets empty. This is no online-hosting requirement. |
| Package | PASS `Artifacts/m11-playable-release/5eaced5fe526ec9dbb8e5c18f1defd9235036808/package-20261114T172353598Z-47719aa8/`; clean matching identities, extraction/corruption checks, distinct prior 7cd7e81 pair presence/identity rehearsal PASS. These are software rehearsals, not actual player rollback/repair. |
| ZIP | `Artifacts/m11-playable-release/5eaced5fe526ec9dbb8e5c18f1defd9235036808/package-20261114T172353598Z-47719aa8/archive/Shooter-Offline.zip`, 40,668,944 bytes, SHA256 `8240e2983cc74a4e5ef665258ab12efd72fe4d05137b90c9b24fa5026b34580f`. All 245 inventoried hashes and ZIP CRC pass (`archive-review.json`). |
| Independent executable startup | PASS Main, `matchActive=0`, `Artifacts/m11-playable-release/5eaced5fe526ec9dbb8e5c18f1defd9235036808/package-20261114T172353598Z-47719aa8/launch-probe/player.log` and `result.json`. Extracted to fresh OS temp, cwd outside repo, executable-relative defaults, batch mode; no server/editor process used. Temporary extraction removed after retaining logs. |
| Manual gameplay / installer / REL | Pending / NotRun / NotClaimed. No named complete matches, interactive two-map slide/device/visual review or reference matrix/soaks were performed here. P05 FAIL unchanged. |

Both Unity test modes create unrelated persistent New Agent settings through existing nav code; the final clean runs retain `navsettings-test-side-effect.patch` and restore only that tracked project-setting side effect after exit. No navigation setting change is included in the slide source. Earlier dirty passing runs and native startup failures remain separate, with their true flags/results.

The first Client attempt `build-client-20261114T170523318Z-73e41930` failed because pinned Windows IL2CPP support was missing. Official exact-version Windows IL2CPP/server modules were reinstalled (no upgrade); proof/log is `Artifacts/unity-setup/t072-windows-module-repair.log`. The failed build and generated performance-test Resources were retained in its evidence folder; those untracked build-hook leftovers were then removed. Successful retry source stayed clean. The previously damaged engine/Mono/SDK repair and hashes are retained in the implementation record below.

Owner-requested cleanup removed 24.64 GiB historical nonshipping debug backups, then 1.07 GiB new nonshipping debug output after archive verification; runtime files/archives/reports/P05 evidence stay. Temporary authenticated repair installation/installer (11.73 GiB) and startup extraction were removed; the working locked editor remains intact. Cleanup proof: `Artifacts/workspace-cleanup/t072-generated-debug-cleanup.json`, `t072-current-build-debug-cleanup.json`, `t072-temporary-repair-cleanup.json`.

Use the [updated playtest guide](../../playtest-guide.md). The retained 7cd7e81/114815c records below are historical; neither archive gains slide through these documentation changes. Final [Windows installer/update](../../features/windows-installer.md) acceptance remains planned.


## T072 — Owner-requested local sprint slide — implementation record

Owner Max requested **implementation now** of slide (подкат), default Left Shift + F, on 2026-11-14. ADR-039 and the T072 card bound this work to local motor/input/session/read-only views/settings/tests/build/docs; no agents, online rollout or waiver. The old 7cd7e81 ZIP is retained unchanged and has no slide.

Software: local command envelope carries a buffered SlidePressed edge with the C0 command; the authority accepts it only with a fresh command for the current human life. Motor owns crouch capsule/eye pose, 6.5 m/s entry, 4 m/s² decay, ≤45 ticks, 60-tick cooldown, jump/air/wall cancellation and safe stand recovery. Read-only movement view now drives camera/rig crouch; no animation-driven motion. Schema-2 Slide binding/defaults/rebinding/legacy-F preservation are implemented under the reviewed additive Game-v1 lock; C0/N1/content/toolchain formats stay unchanged.

Implementation-time evidence at `77bfa93` + working changes (sourceDirty=true): Contracts 72 PASS (`20261114T161803424Z-043e9d72`), Generation 319 PASS (`20261114T161814495Z-5eb83155`), Simulation 119 PASS (`20261114T161911919Z-5a6e4fce`), Game 55 PASS (`20261114T162353106Z-1d18c101`). Runs live under their usual Artifacts milestone/77bfa93de99ef98fc5232f73e9946b4628fad752 directories. Provenance passes, 662 entries / 331 GUIDs; specification fixtures pass 35 assertions. The initial nullable compile failure is retained (`20261114T161713482Z-46e6fbfe`), then repaired before reruns.

Unity EditMode initially failed **before project compilation**: editor Unity.dll could not load; dependency inspection found missing compress_bc7e.dll. Failure retained at `Artifacts/m0-foundation/77bfa93de99ef98fc5232f73e9946b4628fad752/tests-editmode-20261114T161720443Z-808d8f64/`. Exact 6000.3.28f1 installer redownload verified against original MD5 aa4e924970ec074fe7da2cb711b3fd49 and valid Unity Technologies signature; the missing factory runtime/SDK files were restored from the authenticated isolated installation (10,017 files / 2.15 GiB; existing files and PackageManager trees untouched), without upgrading editor/packages. Configure-Unity PASS at `configure-unity-20261114T165406204Z-09c7e415`; repaired-editor EditMode PASS at `tests-editmode-20261114T165547008Z-c207f4a6` (856d628 + bounded-binding migration fix, sourceDirty=true). Restore hashes/proof remain in `Artifacts/unity-setup/t072-editor-runtime-restoration.json`. Final Game migration rerun PASS 56/56 (`20261114T165540759Z-89d58830`), including full-table preservation and preallocation count bounds. Final source-matched checks/build/package are recorded in the M11 T072 delivery record when complete. Interactive both-map slide/UI/audio/performance review remains Pending; all existing release gates/P05 FAIL remain unchanged.


## Documentation review / candidate recheck — 2026-11-14

The archive below still exists: 40,645,312 bytes, matching SHA256 `fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50`. All 245 inventory hashes and ZIP CRC pass in a fresh read-only check; source remains `7cd7e81`, not later review HEAD. Retained clean EditMode 24/24, PlayMode 12/12 and Client/Server manifests were inspected. Fresh pure/provenance results and attempts are in the [audit](../../product-state-audit.md). This review performed no new Unity build or player run.

T067 is still technical work, not just missing sign-off: the normal pure MapContent wrapper publishes software-grid NVB1 (`nativeUnityNavBake=Pending`); native editor registration exists in another process. Runtime installer reconstructs NavMeshData and permits partial/fallback paths; actual native two-map publication/query/spawn/load evidence is outstanding. T068/T069/T070 software is already built. T071 has delivered the ZIP/menu-launch record, but manual gameplay and player corruption/recovery/rollback remain Pending. Prior tables below are implementation-time history where explicitly labeled.

The old `C:/Temp/shooter-t071-playtest-59bbcbb` extraction is absent at review time. Preserve the archive; use a fresh independent extraction and retain new raw player logs under Artifacts. Recorded menu launch in this handoff was not independently repeated. Package `playerLaunchEvidence=Pending` remains unchanged; no REL stamp is inferred.

## Historical T071 owner candidate — 7cd7e81 before T072 — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T071 (owner playtest candidate; preliminary T065 / FIX13 player path; source-matched after T070) |
| Source | `7cd7e8191e118929af742bef382774edb22a57c2` (`sourceDirty=false` at Client request); includes T070 tip `59bbcbb5629571be4a2f9ffbd56f200d6db3f6a4` plus committed settings provenance refresh for `ShooterBuild.cs` |
| Toolchain | Unity `6000.3.28f1` / `8f082d2fe476`; toolchainHash `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Content | `Compile-GameContent -Force` PASS — 8 packages (both maps × gameplay/low/balanced/high); contentClosureHash `715e762129848f4942f908e896ead3f2fa42a8d2b19cb2ab5612339647e41f07` |
| Client | `Artifacts/m0-foundation/7cd7e8191e118929af742bef382774edb22a57c2/build-client-20261114T153050748Z-b2a23740` — Succeeded, non-development, `sourceDirty=false` |
| Server | `Artifacts/m0-foundation/7cd7e8191e118929af742bef382774edb22a57c2/build-server-20261114T153304840Z-bd3cbeda` — Succeeded, `sourceDirty=false`, `graphicsSourceAssets` empty |
| Package | `Artifacts/m11-playable-release/7cd7e8191e118929af742bef382774edb22a57c2/package-20261114T153524429Z-8edfd0c3/` |
| Archive | `…/archive/Shooter-Offline.zip` SHA256 `fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50` |
| Extract probe | `C:\Temp\shooter-t071-playtest-59bbcbb` via archive; launcher from cwd `C:\Temp` |
| Automated launch | PASS to Main menu — Player.log `SHOOTER_SHELL screen=Main matchActive=0` (IL2CPP 6000.3.28f1, D3D11 AMD Radeon Graphics) |
| Manual playtest | **Pending** — both maps × both sizes, combat/UI/settings/complete matches/results/replay |
| REL01–REL06 | **NotClaimed** |
| P05 | **FAIL** retained |

### Commands run (T071 post-T070 rebuild)

```powershell
./Tools/Configure-Unity.ps1 -EditorPath 'C:\Job\shooter\.local\Unity\6000.3.28f1\6000.3.28f1\Editor\Unity.exe'
# WriteSettingsProvenance refresh committed as 7cd7e81 (ShooterBuild source hash only)
./Tools/Test-SourceProvenance.ps1          # PASS (662 entries)
./Tools/Test-Contracts.ps1 -Offline        # 72/72
./Tools/Test-Generation.ps1 -Offline       # 319/319
./Tools/Test-Simulation.ps1 -Offline       # 110/110
./Tools/Test-Game.ps1 -Offline             # 51/51
./Tools/Test-Unity.ps1 -Mode EditMode      # PASS at 7cd7e81
./Tools/Test-Unity.ps1 -Mode PlayMode      # PASS at 7cd7e81
./Tools/Compile-GameContent.ps1 -Force     # PASS, 8 packages, sourceDirty=false
./Tools/Build.ps1 -Target Client           # PASS, sourceDirty=false at 7cd7e81
./Tools/Build.ps1 -Target Server           # PASS, graphics isolated
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client-build-run>
```

### Exact Pending list (do not upgrade to PASS without evidence)

- Manual owner playtest matrix (both maps × 5v5/6v6; move/aim/fire/rifle-pistol/reload; bots; death/180-tick respawn; pause/focus; scoreboard; settings; complete match; results/replay)
- Player corrupt-content recovery + prior-package rollback rehearsal (tooling extract/corrupt PASS; player confirmation Pending; rollback SkippedNoPrevious)
- FIX02–FIX10 / SET01–02 named player/device/UI/audio reviews
- FIX05 visibility captures; FIX11/REL03 visual sign-off (P05 FAIL immutable)
- REL04 / FIX12 / T070: twelve native720p Low captures + ≥30 min soak/map — **PendingHardware**
- Offline NuGet advisory — **PendingNetwork**
- REL01–REL06 acceptance stamps — **NotClaimed**

### Product fixes landed under T071 to unblock the candidate

- Unity compile: Settings→Contracts asmdef, Bootstrap→Generation.Core, PlayMode→Generation.Core, `com.unity.modules.audio`, InputBinding/EntityId aliases, nullable `PublishedMapPackage.MapId`, lifecycle definite assignment
- EditMode: DestroyImmediate outside play mode for GeneratedReflectionProbe/GeneratedSky
- NVB1 install: voxelSize below agentRadius; +Y triangle winding so SamplePosition works
- `ShooterBuild` records `sourceDirty`/`sourceCommit` at request time (before BuildPlayer side effects)

## T071 — Historical superseded candidate at 114815c — 2026-11-14

Superseded for source-matched measurement after T070 (`59bbcbb`). **Do not retcon** these package/build hashes; use only as historical record.

| Field | Disposition |
|---|---|
| Task | T071 (pre-T070 owner playtest archive) |
| Source | `114815c66dd5b951f1c1124e228f9a8acb2472a1` (`sourceDirty=false` at Client request) |
| Toolchain | Unity `6000.3.28f1` / `8f082d2fe476`; toolchainHash `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Content | contentClosureHash `715e762129848f4942f908e896ead3f2fa42a8d2b19cb2ab5612339647e41f07` |
| Client | `Artifacts/m0-foundation/114815c…/build-client-20261114T145025293Z-1d4df0da` |
| Server | `Artifacts/m0-foundation/114815c…/build-server-20261114T145215699Z-4f4663eb` |
| Package | `Artifacts/m11-playable-release/114815c66dd5b951f1c1124e228f9a8acb2472a1/package-20261114T145536425Z-de3b650c/` |
| Archive | `…/archive/Shooter-Offline.zip` SHA256 `ccadafb06ae0a4541fac974ff459d4936814c57e8d48bb4362836a66c1e9afa4` |
| Status | **Superseded** — missing T070 finish/results/replay measurement software |

## Latest-commit review — 59316d3 (historical)

59316d3 contains committed T052–T064 remediation. The [T066–T071 queue](../../next-playable-build.md) defined the path to the first owner-playable archive; the post-T070 T071 rebuild at `7cd7e81` is the current source-matched candidate. FIX12 hardware remains **PendingHardware**; T062 visual disposition and T065 full acceptance remain open; REL01–REL06 remain NotClaimed.

## Historical T065 / FIX14 readiness at 59316d3 — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T065 (original T049–T051 / FIX14) |
| Software prerequisites T052–T064 | Committed in 59316d3; current gaps T066–T071 remain before a clean built candidate |
| REL01–REL06 | **Open / NotClaimed** — require one clean source-matched client + Package-Release archive + operator evidence |
| P05 | **FAIL** retained (ADR-024); failed artifacts must not be deleted or relabeled |
| Operator runbook | Below — Compile-GameContent → clean Client build → Package-Release → extract/launch → named reviews → matrix/soaks |
| This document | Acceptance checklist + runbook only; does not mark gates PASS |

### Historical software vs remaining at 59316d3 (T052–T065)

Implementation-time summary at 59316d3, retained as history. The current review and T071 record above supersede its baseline/content/build availability claims; current audit C01–C05 and remaining queue govern next work. “Software” means code/tooling/tests landed; it is **not** REL/player/hardware PASS.

| Task | Gate | Software disposition | Remaining for true playable release |
|---|---|---|---|
| T052 | FIX01 | Contracts lock graph repaired; 72 tests PASS at 59316d3 | T066 Generation/provenance failures; offline advisory **PendingNetwork** |
| T053 | FIX02 | **Software met** — harness lifecycle/respawn | Actual-player lifecycle evidence **Pending** |
| T054 | FIX03 | **Software met** — pause/focus/input buffer | Player pause/focus device review **Pending** |
| T055 | FIX04 | **Partial** — packages/load (ADR-035) | T067 data-only catalog, actual visual closure and native bake/install; player proof **Pending** |
| T056 | FIX05 | **Software wired** — map/actor/rifle+pistol/action views (T068) | Player captures / FIX05 full coverage **Pending** |
| T057 | FIX06 | **Pure AI met** — enemy-only sensing/corners | Mixed-team real-map player **Pending** |
| T058 | FIX07 | **Software spawn safety** — rank/defer/deadline | Native/player route+spawn review **Pending** |
| T059 | FIX08 | **Shell software** — menu/load/pause/results | 20-launch soak + 720p UI review **Pending** |
| T060 | FIX09 | **ADR-036 + T069 software** — bindings/accessibility applied | SET02 named Keep/Revert/device **Pending** |
| T061 | FIX10 | **Device callback software** | Named audible review **Pending** |
| T062 | FIX11 | **ToolingReady** inventory/overlay | P05 **FAIL** retained; REL03 **Open** |
| T063 | FIX12 | **ADR-037 tooling** + T070 finish/replay/counters (software) | Twelve captures + soaks **PendingHardware**; rebuild after T070 tip |
| T064 | FIX13 | **Package-Release software** rejects dirty/mismatched | Clean source-matched client + player launch **Pending** |
| T065 | FIX14 / REL01–REL06 | **Checklist + runbook ready** | Execute operator path; do not claim PASS until measured |

### T065 acceptance checklist (all Open until measured)

Record each item with source commit, `sourceDirty=false` build identity, content/toolchain hashes, artifact paths, named reviewer where required. Use Pending / PendingHardware / FAIL / NotRun — never invent PASS.

| ID | Check | Status now | Evidence required |
|---|---|---|---|
| REL01 | Clean independent artifact | Open | Clean Client build (`sourceDirty=false`) + `Compile-GameContent` closure + `Package-Release` archive; extract/launch without Unity/dev cache/network/server; corrupt content recovers |
| REL02 | Full play matrix | Open | Both maps × both sizes × score + unshortened 15-min timeout; draw fixture; ≥1 named manual match/map; replay/menu/settings |
| REL03 | Visual/usability sign-off | Open | Named review + captures; P05 FAIL immutable; live soldiers/UI at 720p 100%/150% |
| REL04 | Reference performance | Open / PendingHardware | Twelve TDM captures (both maps × both sizes × 3) + ≥30 min soak/map on reference machine |
| REL05 | Regression and provenance | Partial software | Locked offline wrappers + provenance; advisory **PendingNetwork**; Unity client/server isolation on candidate |
| REL06 | Delivery and rollback | Open | Archive path/hash, CONTROLS/KNOWN_ISSUES, rollback pair rehearsal; completed assigned work committed on main under the standing repository rule |
| FIX02 player | Lifecycle in player | Pending | First/repeated 6v6 lives in packaged player |
| FIX03 player | Pause/focus device | Pending | 10s pause + OS focus loss in player |
| FIX04 native/player | Nav bake + packaged load | Pending | Native Unity nav bake + release-player load both maps |
| FIX05 captures | Visibility both maps | Pending | Source-matched player captures |
| FIX06 player | Mixed-team AI | Pending | Real-map mixed-team combat |
| FIX07 player | Spawn/routes | Pending | Native/player review both layouts |
| FIX08 soak/UI | Shell durability | Pending | 20-launch soak + 720p UI |
| FIX09 SET02 | Display apply | Pending | Real display 15s Keep/Revert |
| FIX10 audible | Device review | Pending | Named shot/impact/footstep/UI hearing |
| FIX11/P05 | Visual defects | FAIL retained | No fourth clearance search unless owner authorizes |
| FIX12 matrix | Perf/soak | PendingHardware | Reference twelve-run + soaks |
| FIX13 candidate | Package from clean build | Pending | Reject dirty builds; one matching candidate for all REL gates |

## T065 operator runbook (do not stamp PASS)

Execute from repository root on the **intended acceptance candidate** only. Require a clean committed tree (`sourceDirty=false`). The retained 7cd7e81 candidate has build/package reports; use it for immediate T071 playtest. Remaining T067/player fixes require a new matching candidate and affected acceptance reruns. This is the final acceptance runbook, not an instruction to rebuild for documentation changes. Keep Smoke diagnostic separate from product launch. Failed P05 artifacts stay immutable.

### 1. Content closure

```powershell
./Tools/Compile-GameContent.ps1
```

Confirm both Training Compound and Depot Yard packages publish under the verified map-package cache with matching toolchain hash. Incomplete/unknown Map selection must fail.

### 2. Clean Client (and Server isolation) build

```powershell
./Tools/Configure-Unity.ps1   # locked editor only; fresh checkouts
./Tools/Build.ps1 -Target Client
./Tools/Build.ps1 -Target Server
```

Require `build-manifest.json` with `result=Succeeded`, `target=Client`, `development=false`, **`sourceDirty=false`**, and matching `sourceCommit` / `sourceHash` / `toolchainHash` / `contentManifestHash`. Dirty or HEAD-stamped directories are rejected by T064.

### 3. Package-Release

```powershell
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <clean-client-build-run> [-PreviousPackageDirectory <prior-package>]
```

Expect archive under `Artifacts/m11-playable-release/<build-sourceCommit>/package-*/`, SHA256 inventory, CONTROLS, LICENSES, KNOWN_ISSUES, `validation.json` with `releaseAcceptance=NotClaimed` and `playerLaunchEvidence=Pending` until operator launch is recorded. Tooling PASS ≠ REL01/REL06.

### 4. Extract and launch (REL01 rehearsal)

Extract to a clean directory (no Unity Library, no absolute repo Artifacts path, no network, no server process). Launch the packaged `launch-offline.cmd` (supplies content/lock overrides; executable-relative defaults are implemented, with independent direct-exe confirmation still Pending); reach main menu with no active match. Confirm both maps load from packaged content. Corrupt one content file and confirm recoverable failure. Record paths/hashes — still **NotClaimed** until full REL checklist completes.

### 5. Named product reviews (Pending until done)

On the same packaged player:

- Pause 10s + OS focus loss/return (FIX03); no fatal Busy; no duplicate input.
- Lifecycle deaths/respawns including human camera rebind (FIX02).
- Visibility captures both maps (FIX05); spawn/route review (FIX07); mixed-team AI (FIX06).
- Shell: start/cancel load/pause/restart/leave/replay; 20 alternating launches (FIX08).
- Settings SET02 Keep/Revert on real display (FIX09); audible shot/impact/footstep/UI (FIX10).
- Visual/UI at native720p 100% and 150%; attach captures; keep P05 FAIL (FIX11/REL03).

### 6. Full match matrix (REL02)

Both maps × 5v5 and 6v6: complete 150-kill finish and unshortened 15-active-minute timeout; equal-score draw via boundary fixture; ≥1 named manual complete match per map; results immutable; replay/menu/settings after stop.

### 7. Reference matrix and soaks (REL04 / FIX12) — PendingHardware

After the same candidate content/build:

```powershell
./Tools/Compile-GameContent.ps1
./Tools/Benchmark-Product-Matrix.ps1 -BuildRunDirectory <same-clean-client-build-run> -SensorLog <live-sensor-csv> -HardwareEvidence <reference-proof-json>   # twelve captures: both maps × both sizes × three runs, 60s+180s, native1280×720 Low
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <same-clean-client-build-run> -Workload TDM-soak-6v6-compound-v1 -HardwareEvidence <reference-proof-json>
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <same-clean-client-build-run> -Workload TDM-soak-6v6-depot-v1 -HardwareEvidence <reference-proof-json>
```

Or equivalent `Benchmark.ps1` / `Benchmark-Series.ps1` with `TDM-5v5-v1`, `TDM-6v6-v1`, `TDM-5v5-depot-v1`, `TDM-6v6-depot-v1`. Missing reference machine/counters → **PendingHardware**, never PASS.

### 8. Regression / provenance (REL05)

```powershell
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
./Tools/Test-SourceProvenance.ps1
./Tools/Test-Contracts.ps1 -Offline
./Tools/Test-Generation.ps1 -Offline
./Tools/Test-Simulation.ps1 -Offline
./Tools/Test-Game.ps1 -Offline
```

Report offline advisory lookup as **PendingNetwork** separately. Confirm server build excludes client presentation/content.

### 9. Handoff closeout (REL06) — after measured evidence only

Update this file with archive path/hash, evidence inventory, every FAIL/Pending/PendingHardware, rollback pair, and named reviewers. Commit completed assigned acceptance work to `main` under the standing repository rule; preserve ignored Artifacts. Do not upgrade packages/editor; do not delete failed P05 artifacts; do not edit gameplay solely to force greens.

## T062 / FIX11 visual inventory tooling — 2026-11-14

Initial product visual defect inventory and capture/overlay helpers are recorded under [visual-defect-inventory.md](../../visual-defect-inventory.md) and the M8 handoff T062 section. **P05 remains FAIL** under ADR-024. REL03 stays Open: tooling/inventory alone is not named visual sign-off, and no fourth clearance search was authorized or performed. Generator/recipe sources and failed historical artifacts are unchanged. This task did not edit Benchmark* or Package-Release* ownership.

| Gate | Status |
|---|---|
| FIX11 tooling/inventory | ToolingReady |
| REL03 named visual/usability sign-off | Open |
| P05 | FAIL (immutable) |

## T063 / FIX12 software — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T063 (original T043/T050 measurement) |
| FIX12 software | **PASS** tooling/collector/negatives — ADR-037 depot workloads; product TDM collector v4; `analyze_tdm_benchmark.py` rejects wrong map/roster/profile/build/counters/windows |
| Twelve captures / soaks | **PendingHardware** — plan-only matrix/soak recorded; reference-machine runs not executed |
| Commands | `Compile-GameContent.ps1`; `Benchmark.ps1 -Workload TDM-…`; `Benchmark-Series.ps1`; `Benchmark-Product-Matrix.ps1`; `Benchmark-Soak.ps1` |
| Evidence | `python Tools/test_tdm_benchmark_analysis.py` (10 PASS); Game-v1 ProductTdmWorkload tests (6 PASS); Compile-GameContent PASS under `Artifacts/m8-runtime-composition/<commit>/compile-gamecontent-*`; plan PendingHardware under `Artifacts/m0-foundation/<commit>/benchmark-product-matrix-*` and `benchmark-soak-*` |
| Handoff to T064 | Same final candidate build/content for packaging; do not stamp HEAD onto arbitrary binaries; require matching source/content/toolchain identities |

## T064 / FIX13 software — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T064 (original T048/T051 packaging requirements) |
| FIX13 software | **PASS** tooling — dirty/mismatched builds rejected; content closure required; exclude policy; archive/inventory/licenses/provenance/rollback |
| Player launch / REL01 | **Pending** — needs clean matching client build + operator extract launch |
| REL06 acceptance | **NotClaimed** by tooling alone (`releaseAcceptance=NotClaimed`) |
| Commands | `Tools/Package-Release.ps1 -ClientBuildRunDirectory <build-run> [-PreviousPackageDirectory <prior-package>]`; negatives: `Tools/test_package_release.ps1` |
| Policy | `Tools/release-package-policy.json`; Bootstrap `ReleasePackagePolicy` / `ReleaseRollbackLedger` |
| Evidence | `Artifacts/tooling-tests/package-release/` (7 wrapper cases); `Artifacts/tooling-tests/t064-release-dotnet/`; dirty `03fa6dd` reject under `Artifacts/m11-playable-release/03fa6dd.../package-*/validation.json` |

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software package tooling)**, 2026-11-13. That PASS stamped a dirty `03fa6dd` build and copied Unity backup/debug folders; T064 now rejects that candidate. Named visual review (REL03/T049), native-720p matrix (REL04/T050), and soak remain **PendingHardware / Pending named review**. P05 historical FAIL retained (ADR-024). No invented goldens; no fake hardware PASS.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T048–T051 |
| T048 Clean package | Historical **PASS** tooling — superseded by T064 reject of dirty/debug-shipping package |
| T049 Visual review | Pending (P05 FAIL retained) |
| T050 Perf/soak matrix | PendingHardware |
| T051 Handoff | This record + package inventory; rollback = prior diagnostic build pair |

### Package evidence

- Historical (dirty, superseded): `Artifacts/m11-playable-release/<commit>/package-20261113T230405706Z-7636baef/`
- T064 software: `Tools/test_package_release.ps1` results under `Artifacts/tooling-tests/package-release/`

[Release roadmap](../../playable-release-plan.md) · [Previous](../m10-maps-shell/index.md) · [Shared docs](../../index.md).

## Owner-requested disk cleanup — 2026-11-14

Follow-up cleanup, 2026-11-15, inspected source `df4a6df`: removed **4,695,436,906 bytes (4.37 GiB)** of rebuildable Unity build/shader caches, .NET bin/obj and Python caches, plus disposable playtest files whose bytes matched retained release ZIP entries. Unique extraction files/logs/captures, source, settings, the pinned editor, offline PackageCache/curated package, runtime builds, historical failures and rollback pairs were preserved. All **29** retained release ZIPs/installers were SHA256 checked before/after with zero mismatches. Exact inventory, per-path results and archive hashes are in ignored `Artifacts/workspace-cleanup/cleanup-20261115-plan.json` and `cleanup-20261115-result.json`. Automatic approval review initially rejected the historical debug-backup deletion. The owner then explicitly approved it: all **49** planned nonshipping debug-backup directories were removed (**19,666,579,365 bytes / 18.32 GiB**), bringing this follow-up cleanup to **24,362,016,271 bytes / 22.69 GiB** total. All 29 release archive/installer hashes were rechecked unchanged and source provenance passed (714 entries / 357 GUIDs). Per-path results and repeat archive verification are retained in `Artifacts/workspace-cleanup/cleanup-20261115-approved-debug-result.json`. This cleanup is not a build/gameplay/acceptance result; the next Unity/.NET build regenerates the removed caches.

Removed 65 generated IL2CPP/debug backup/PDB entries directly under historical `Artifacts/m0-foundation/*/build-*/player/` folders: **26,453,819,207 bytes (24.64 GiB)**. Exact checked paths/bytes are retained in `Artifacts/workspace-cleanup/t072-generated-debug-cleanup.json`. These are nonshipping debug products excluded by the release policy. Runtime executables/data, archives/rollback pairs, manifests/reports/logs, P05 failed artifacts, source and the locked working editor were preserved. The 7cd7e81 archive SHA256 was rechecked unchanged after cleanup. Temporary authenticated repair installation/download cleanup follows successful test/build execution; no active dependencies are removed.

## Planned final Windows installer (T073)

Owner requirement, 2026-11-14: the final game is delivered by a Windows installer. Running a newer installer must update the existing installed game, preserve settings/controls and stable app identity/path/shortcuts, and maintain a matching executable/content pair. ZIPs remain preview/internal artifacts. [Installer contract and INS01–INS06](../../features/windows-installer.md) extend REL01/REL06 and FIX13/FIX14. T073 is planned documentation scope only here; a future bounded release-integrator assignment implements installer/version/upgrade/repair/uninstall/rollback tooling. No installer, online service or acceptance waiver is delivered by this update.

## Integration session 2026-11-14

Owner-authorized orchestration: an integrator (Opus) dispatched bounded Sonnet workers in git worktrees, reviewed and merged each branch into `main`, then removed every worktree and branch; worker evidence is preserved under ignored `Artifacts/worker-evidence/`. No acceptance gate is waived or claimed by this session.

### Landed changes

| Change | Where | Evidence / limits |
|---|---|---|
| T067 C03 truthful paths | `World/Navigation/RevisionedPathAdapter.cs` (`NavPathOutcome`), `World/Foundation/UnityNavMeshBakeInstaller.cs`, `CompoundMapRuntime.AllowPackagedPathFallback` (off in product) | PathPartial → `PathStatus.Partial`; packaged-graph fallback is diagnostics-only and counted. PlayMode covers disconnected/obstructed quads; two-map route/spawn player evidence Pending. |
| T067 C01 native publication | `Bootstrap/Editor/ShooterNativeContentBuild.cs`, `nav-provenance.json` sidecar, `Compile-GameContent.ps1 -Native`, `Package-Release.ps1 -RequireNativeNav` | Both maps bake natively and deterministically (compound 4,710 / depot 3,386 polygons). The bake no longer dirties `ProjectSettings`. |
| T073 installer | ADR-041, `Tools/Build-Installer.ps1`, `Tools/installer/` | See [installer status](../../features/windows-installer.md#implementation-status). |
| Lock identities | ADR-040 | 13 CRLF-derived hashes refrozen to LF bytes with owner approval; fresh clones/CI now pass. |
| Main-menu crash | ADR-042, `FoundationEntry.cs`, `Assets/Plugins/x86_64/ShooterAllocationMeter.cpp` | Root cause from crash dumps: managed IL2CPP allocation hook recursing on thread attach. Normal play installs no hook; benchmarks use a native counter. |
| Baseline repairs | `Tests/Performance` orphan metadata removed; `test_package_release.ps1` catalog fixture; LF output in `docs/verify_docs.py` | Package-release regression 7/7 again. |

### Defects found by verification

1. **Crash ~4 s after Main** in every build since synthesized audio (including the 5eaced5 playtest ZIP): fixed, see ADR-042. Earlier "reaches Main" evidence stopped looking too early; release checks now keep the windowed player alive for 60 s.
2. **Native packages could not start a match**: editor-published (Mono) content differed from .NET/IL2CPP in the lowest float bits of `skeleton-soldier.bin`, so the player's skeleton identity check rejected every native package (all 12 reference captures and both soaks aborted at match start on `075c548`). The `075c548` package/installer are therefore not playable deliverables. Fixed by [ADR-043](../../decisions.md#adr-043--accepted-two-stage-native-navigation-publication): the editor bakes only navigation from .NET-published collision and a byte-identity guard protects all other payloads; release checks now start a real match on both maps. The defective cache is retained as ignored `Artifacts/map-packages-native-mono-defect-075c548`.
3. **Bots never fought** (found by the reference captures below): the product session never passed the packaged patrol/combat destinations to the bot planner, so every bot orbited 4 m around its spawn with its spawn yaw, never met the enemy team and retried a failing path every tick. Fixed in `2ee1a3d`: map destinations are applied, bots face their route, rotate destinations, back off 30 ticks after a failed path and leave SeekCover when no cover site is free; due-south yaw is clamped into the C0 interval. Pure tests PASS (Simulation 128, Game 57, five new regressions). **Real-player confirmation is Pending**: the verify run was stopped by owner decision before any build; bots moving, shooting and scoring in a player has not been observed.

### Reference captures on 9f2219f — reference laptop, AC power

Twelve product captures (4 workloads × 3, Low 1280×720, native content) and two soaks ran on 2026-11-14. None is reference evidence:

| Result | Detail |
|---|---|
| 9 captures ComputedFail | No crash. Frame p50 ≈ 1.34–1.48 ms, p99 ≈ 1.95–2.14 ms, RSS ≈ 240–273 MB, but **0 shots, kills and vision rays** (defect 3), so the frames contain no combat. Each also fails the zero-allocation tick gate: 19,082 B/tick (5v5) and 21,626 B/tick (6v6) of managed allocation; not fixed. |
| 3 captures FAIL (330 s timeout) | Player stalled after validation. A concurrent session launched builds and game windows during the run; focus loss pauses the product match, the likely cause (not proven). Captures now abort with an explicit focus-loss reason (`2ee1a3d`). |
| 2 soaks FAIL | `Product TDM frame capacity exceeded` after ~6 minutes: a fixed 262,144-frame buffer at ~700 fps uncapped. Capacity now scales with the capture window (`2ee1a3d`). |

Not measured: GPU timing (PresentMon produced a CSV during the soak, but it was not analyzed), thermal stabilization (no sensor log). The 12-capture/2-soak reference set must be rerun on an idle machine after the bot fix and the per-tick allocation work; the owner deferred that long run on 2026-11-14. Evidence: ignored `Artifacts/m0-foundation/9f2219f9d67a91c5103decdf5ea6bd0958eb0573/benchmark-*`.

## T076 — Compact TDM map layouts (2026-11-15)

Owner assignment: map design for 5v5/6v6. Spec [compact TDM map layouts](../../features/tdm-map-layouts.md), decision [ADR-045](../../decisions.md#adr-045--accepted-compact-tdm-arena-layouts-for-catalog-maps-t076). Arena foundation and the two planners were implemented by Sonnet subagents in isolated worktrees (`maps/tdm-compact`, `maps/compound-arena`, `maps/depot-arena`) and integrated by Claude.

| Map | Playable | Anchor→centre / →enemy path | Longest sightline | Worst cover gap | Boxes / cover proxies | Spawn visibility worst case |
|---|---|---|---|---|---|---|
| training-compound | 88 × 104 m | 54 / 108 m | 58 m | 6.27 m | 130 / 42 | 4 of 12 slots |
| depot-yard | 80 × 112 m | 54 / 106 m | 49.75 m | 6.75 m | 116 / 44 | 4 of 12 slots |

Both pass all 13 `ArenaRules` rows and `MapTopologyValidation` (3 staging exits per team, 3 distinct routes with one covered, no spawn-to-spawn or centre-to-spawn eye line, mirror symmetry).

Evidence at final merged source `736548e` (includes T075 ADR-044 surface publication from main): Contracts 73, Generation 371, Maps 51, Game 61, Simulation 194, source provenance (714 entries / 357 GUIDs), docs, fixtures and contract lock PASS. Unity EditMode 43/43 and PlayMode 17/17 PASS. `Compile-GameContent.ps1 -Native` PASS with native nav, reproduction and spawn→objective paths for both maps. New gameplay identities (prefix): compound package root `e7cc94c0`, collision `a5ad5876`, navigation `c2fc4ed0`, metadata `cb2a7f13`; depot root `3f7679e4`, collision `fa38e9f0`, navigation `33a97d23`, metadata `604af33b`. These supersede the ADR-043/ADR-044 map identities. An intermediate run at `5d50b20` showed 4 EditMode failures (`InputBufferTests` ×3, `MenuStagingSurfaceTests`) that reproduced at unmodified base `cbd359b` and were fixed by T075 on main before this merge.

Limitations and pending items:
- Delivery 0.3.5 from clean source `ad6bdea` (locked Unity 6000.3.28f1 / 8f082d2fe476, Client IL2CPP / Server Mono): Client and Server builds PASS, Client and Server smoke PASS (`Artifacts/m0-foundation/ad6bdea6c74a8627f7afadd9ae4aace31de169bb/`). `Package-Release.ps1 -RequireNativeNav -MapCacheRoot Artifacts/map-packages-native` PASS with `navigationSource` unity-native-navmesh, content closure `c8ca52fb6d57582669d2dc9a372cad6fe9dced068a67b9498f57873b2f040bb3`, index `3842cccae20c4b1ab830b73c339b809332a867534cee9fa60994eda152c514f7`, archive `cdcfc45dca8818deef6a447db01e57cd7bca0d9fbfbdbc7d110f434dd50e83fb`, plus extract, corrupt and rollback rehearsals PASS. Note that the wrapper's default cache `Artifacts/map-packages` is the software-nav cache, so native packaging needs `-MapCacheRoot`.
- Installer: `Artifacts/m11-playable-release/ad6bdea6c74a8627f7afadd9ae4aace31de169bb/installer-20261115T093200473Z-a2bdafc9/installer/Shooter-Setup-0.3.5.exe`, 26,346,316 bytes, SHA256 `b6e4f18455d8c577d3c854a031b578541f7d0ec80ddbf4b7a0942aa10f92a68c`, predecessor 0.3.4. AppId and settings policy are unchanged. Actual install/upgrade and player match start on the new layouts were NotRun (`playerLaunchEvidence` Pending).
- Path anchor→centre is 54 m against the 55 m limit on both maps. The compound sightline is 58 m against 60. The depot container-lane sightline reaches 29.25 m, above the spec's ≤20 m goal; its layout test allows 30 m.
- Pending manual playtest at 5v5 and 6v6 on both maps: route feel, spawn pressure under `SpawnSafetyPolicy` (40 m exposure refusal), and bot cover use.

## T075 — Procedural presentation preview 0.3.4

Owner authorized design implementation and multiple agents on 2026-11-15. Three isolated worktrees delivered surfaces (a9ea3e6 / 5c739a1), handling (b7c3105) and menu (eca8a84); all are merged to main and their temporary worktrees/branches removed. The unrelated pre-existing maps/tdm-compact worktree was preserved. This is software toward VD1/VD3/VD4/VD6/VD7, not VD or release acceptance.

Implemented: four actual 128px / two-metre MaterialBaker triplets (concrete, metal cover, soil, asphalt); strict companion extraction; spatial URP metallic/occlusion/inverted-roughness binding and correct opaque-alpha XYZ normal upload; metric map UV scale; explicit resource release on map replacement. Menu variants share planes, cargo doors have numeric detail, card grounds match the packaged families, and shader descriptors fail closed. Three generated numeric materials retain the runtime Lit keyword combinations in Client only. Actor/weapon/soldier generators, gameplay rules, sockets/hit poses, C0/Game-v1/SGAF encoding, seeds and settings/installer identity stay unchanged.

First-person presentation uses stable analytic recoil, distance-driven gait, equip/reload offsets, reduced-motion suppression and per-life reset. Full ADS places both verified sight lines on the camera ray. The local near-field rifle/pistol use already-packaged LOD0 independently of distant world LOD, and camera-local hip placement keeps the stock out of the near plane. Menu showcase actors now face the viewing area. Hands, moving magazine/mechanical geometry and new soldier anatomy are not implemented by this wave.

ADR-044 versions map generators to `1.0.1` and migrates strict schema/example/registry/catalog labels; old `1.0.0` inputs reject. Current package/catalog/envelope identities therefore change. Material generator `1.0.1` and SGAF/package/catalog format v1 remain. Native content was published at `88f6b22`; later changes are presentation/tests only, so final packaging verifies and reuses those exact bytes without relabeling that publication. `Artifacts/visual-design/t075-migration/body-identity.json` proves collision and native navigation bodies byte-identical against all eight retained old package pairs. No generated payload/golden was edited by hand.

Verification: Contracts 73 at `4e71bea` (successful rerun after sandbox restore failure); Generation 337 / Game 61 / Maps 37 at clean `a190fc5`; Simulation 194 at `cbd359b`; final Unity/build/delivery identities below. Pure tests from earlier commits are identified as such, not claimed as fresh final-source runs. SDK wrapper tests used `-Offline`; advisory lookup remains PendingNetwork. Six pinned jsonschema 4.26.0 migration/example cases PASS in `Artifacts/visual-design/t075-migration/schema-validation.json`. C0/Game-v1/content/feature identity guards and source provenance are checked independently. Specification/doc checks do not count as gameplay.

Failures retained: sandboxed SDK restores failed before tests; Unity configure crashed inside sandbox Mono socket setup, then passed outside. Initial Unity EditMode 36/40 found three stale pistol assertions (existing eight-tick buffer) and an invalid synchronous EditMode destruction assumption. Input tests now distinguish single rifle press vs eight-tick pistol buffer at 30/60/144 Hz; no input runtime changed. The second EditMode 42/43 identified every retained resource by name; normal automatic teardown moved to an activated/suspended PlayMode lifecycle test and passes. An older ceiling fixture started an invalid standing penetration; it now starts a valid crouch and tests blocked expansion under the existing overlap-recovery policy. Runtime motor/input code did not change.

Player review retained two intermediate installers, 0.3.2 source `88f6b22` and 0.3.3 source `7d0f9e6`. Main and real 6v6 loads were observed (compound on88f6b22, depot on7d0f9e6), with pause/leave and a corrected, visible first-person rifle. A momentary automated Slot2 key did not establish a visible switch; do not claim manual weapon/input acceptance. Old screenshots exposed empty card images and coarse Low FP equipment. The first layer hypothesis was insufficient (the layer already existed); final source captures static cards after URP startup and adds GPU pixel-content checks for both, plus actual resource destruction checks. Raw logs/images remain under `Artifacts/visual-design/88f6b22/player-review/` and `Artifacts/visual-design/7d0f9e6/player-review/`.

Remaining: environments/soldiers still read as blockout; no Battlefield V/Bodycam/Warface parity claim. P05 FAIL / ADR-024 scope stop is unchanged. Full hands/magazine/action continuity, map architecture/detail, named comparisons and 720p 100%/150% UI/device reviews remain Pending. No full normal match/replay, four-configuration gameplay, manual recovery/installed rollback or new installer-upgrade execution is proved here. INS01/INS04, thermal/GPU/reference captures and zero-allocation/full-roster gates remain open. REL01–REL06 NotClaimed. No acceptance waiver or long reference run was authorized/performed. Revert the whole source/schema/registry migration together and restore a retained matching prior binary/content pair for rollback.

### Final delivery identities

- Binary source: `7407bc4a830e6c88510ca56057b0967291f95f33`; source hash `d218cef9cc22f39b0c8470e35ecec94dea005a21656ef013465b13993bd96401`. Final documentation commits do not relabel these binaries.
- Installer: `C:\Job\shooter\Artifacts\m11-playable-release\7407bc4a830e6c88510ca56057b0967291f95f33\installer-20261115T085925627Z-3971f717\installer\Shooter-Setup-0.3.4.exe`; **26,299,389 bytes**; SHA256 `22fd07838cbe9b37476ad3748fd7ad9861a39706ce0558521d5210e363d46c7b`. Stable AppId `B01E0F69-2123-4D91-AB59-88864A944E39` and schema-2 preference policy are unchanged. Builder predecessor is 0.3.3; actual 0.3.4 install/upgrade was **NotRun**.
- Internal ZIP: `C:\Job\shooter\Artifacts\m11-playable-release\7407bc4a830e6c88510ca56057b0967291f95f33\package-20261115T085913396Z-cdae6ee7\archive\Shooter-Offline.zip`; SHA256 `ac809fdc0a116fe5f5b7e7c4f27de78ea1aa399679f42a68e971f0e3ac4f1539`; inventory **294 files**. Preview/rollback packages remain internal artifacts.
- Content closure `51fa375f91e75c73f2f1f60c052989070cb5476ceba257ea5fab10e407cfbd78`; index `da2d3a79eaabc9d265ff7d1dbf066bd988290fa6facceffad6c2be1add8dccea`; locked Unity 6000.3.28f1 / 8f082d2fe476, URP 17.3.0, D3D11, Client IL2CPP / Server Mono.

| Check | Actual result / source | Retained evidence |
|---|---|---|
| Final EditMode | PASS 43/43 at 7407bc4 | `Artifacts/m0-foundation/7407bc4a830e6c88510ca56057b0967291f95f33/tests-editmode-20261115T085321525Z-6b06c211/` |
| Final PlayMode | PASS 17/17, including both GPU card-content checks and suspended resource teardown | `Artifacts/m0-foundation/7407bc4a830e6c88510ca56057b0967291f95f33/tests-playmode-20261115T085203953Z-51409cb0/` |
| Client | PASS, non-development IL2CPP, sourceDirty=false | `Artifacts/m0-foundation/7407bc4a830e6c88510ca56057b0967291f95f33/build-client-20261115T085500928Z-e6db2f5f/` |
| Server / smoke | PASS, sourceDirty=false, zero graphics source assets; real startup/ticks | `Artifacts/m0-foundation/7407bc4a830e6c88510ca56057b0967291f95f33/build-server-20261115T085707101Z-a367346b/`; `smoke-server-20261115T085916762Z-141eebf6/` under the same source root |
| Native publication | PASS 8 packages, 20 selected publication tests; non-nav byte guard PASS, source 88f6b22 | `Artifacts/m8-runtime-composition/88f6b228e3a7e27fd67ff214c28a5f1a231e930c/compile-gamecontent-20261115T082846907Z-51785344/` |
| Migration / provenance | PASS body identity for 8 prior/new pairs; six schema cases; provenance 702 entries / 351 GUIDs | `Artifacts/visual-design/t075-migration/`; final provenance snapshot under `Artifacts/visual-design/7407bc4/` |
| Package / installer | PASS with native requirement, clean workspace and builder source; software extract/corrupt/rollback rehearsals only | `C:/Job/shooter/Artifacts/m11-playable-release/7407bc4a830e6c88510ca56057b0967291f95f33/package-20261115T085913396Z-cdae6ee7`; `Artifacts/m11-playable-release/7407bc4a830e6c88510ca56057b0967291f95f33/installer-20261115T085925627Z-3971f717` |
| Independent player | 294/294 hashes match; alive **112 seconds**; both map cards render, Compound 5v5 and Depot 6v6 reach Playing; pause/leave/return observed | `Artifacts/visual-design/7407bc4/player-review/`: observations, extraction review, Player.log and Main/two map PNGs |

The final UI review is **Codex automated inspection**, not named owner/human acceptance. It confirms visible surfaces, first-person rifle and card rendering at native 720p Low, not manual aim/fire/reload/respawn, bot combat, complete matches, audio, 150% UI, performance or installer acceptance. The remaining 20-cycle actor/resource retention audit also remains Pending; actor mesh/material uploads live until component destruction and repeated rebind retention needs separate review. No additional runtime changes were made to close that gate.

## 0.3.5 playtest fixes (2026-11-15)

Owner report after installing 0.3.5: nobody (player or bots) could sprint, jump or slide; after Pause → Resume the game closed instead of continuing/returning to the menu; two rifles rendered in first person. Investigated and fixed by Sonnet subagents on branch `fix/0.3.5-playtest`, reviewed and verified by Claude.

| Defect | Root cause | Fix |
|---|---|---|
| No sprint/jump/slide | Product spawns put feet exactly on the floor. PhysX `CapsuleCast` reports an already-touching collider as distance 0 with normal = −direction for every sweep, so `KinematicMotor` projected all horizontal, jump and step motion to zero. Fake test worlds never modelled this. | `ProbeGround` lifts a body in initial contact (≤1 mm) to rest `Skin` above the surface. Pure regression `InitialContactWorldTests` (PhysX-style fake) and PlayMode `ProductLocomotionPhysXTests` (motor on a product-style floor and the real depot-yard `OfflineGameSession` human), both failing before the fix. Exact-Y-at-contact expectations in three pure tests now allow `[ground, ground+Skin]`. |
| Game closes after Resume | `Player-prev.log`: `SHOOTER_FAILURE: Offline tick overload … elapsed=0.1026` — any ≥~83 ms frame latched `Overloaded`, `FoundationEntry.Fail` called `Application.Quit(1)`; Resume had no rebase frame. | [ADR-046](../../decisions.md): product client drops hitch debt and logs `SHOOTER_HITCH`; diagnostic/benchmark/server stay fatal; first sample after resume is zero. New `FoundationTests`. |
| Two rifles | Local avatar's third-person "Held" weapon renderer stayed visible beside the FP viewmodel; only the body was shadow-only, and only at creation. | `ProductActorPresentation.PoseAvatar` sets body and held weapon `ShadowsOnly` for the living local human every pose (`On` otherwise). EditMode regression in `ProductViewBindingTests`. |

Evidence at base `9940fb1` with the working-tree fix: Contracts 76, Simulation 196, Game 61, Maps 37, source provenance, docs, fixtures and contract lock PASS; Unity EditMode 44/44 (`Artifacts/m0-foundation/9940fb1600e9f14e495715045beb2104cac349df/tests-editmode-20261115T101232639Z-1cd38f4a/`) and PlayMode 19/19 (`…/tests-playmode-20261115T101327899Z-7cf9f574/`) PASS.

Pending: no new client build or installer; `Tools/Test-FailureShutdown.ps1` NotRun (needs a matching release build); manual playtest of sprint/jump/slide, bot movement, Pause → Resume → Main Menu and first-person weapon view in an installed build. The cause of the ≥83 ms hitch frames is unexplained. The stray merge-conflict markers left in this file by the T075/T076 merge were removed, keeping both sections.

### Delivery 0.3.6 (2026-11-15)

Offline-only delivery (owner: server build not required while online play is postponed) from clean `946a58d`, which contains the 0.3.5 playtest fixes (`2a43277`) and the T077 presentation merge. Locked Unity 6000.3.28f1 / 8f082d2fe476.

| Gate | Result | Evidence |
|---|---|---|
| Pure lanes | Contracts 76, Simulation 204, Game 61, Maps 37, Generation 371, source provenance (718 entries / 359 GUIDs), docs, contract lock PASS. The first Generation run failed `SoldierMotionTests.ReusedPoseEvaluationDoesNotAllocateAfterWarmup` (7,056 B) and passed on an unchanged rerun, so that allocation test is flaky. | `Artifacts/*/946a58da684099abc1e6fd220ac49e97dde7891c/` |
| Unity | EditMode and PlayMode PASS | `Artifacts/m0-foundation/946a58da684099abc1e6fd220ac49e97dde7891c/tests-editmode-20261115T102813708Z-58eefdca/`, `tests-playmode-20261115T102902043Z-8311cd86/` |
| Content | `Compile-GameContent.ps1 -Native -Force` PASS; content closure `c8ca52fb…` unchanged from 0.3.5 | `Artifacts/map-packages-native` |
| Client / smoke | IL2CPP client PASS, client smoke PASS; server build and smoke not run (offline-only) | `…/build-client-20261115T103130236Z-bffc9b91/`, `…/smoke-client-20261115T103646563Z-336f4ac6/` |
| Package | `Package-Release.ps1 -RequireNativeNav -MapCacheRoot Artifacts/map-packages-native` PASS, unity-native-navmesh, 294 files | `Artifacts/m11-playable-release/946a58da684099abc1e6fd220ac49e97dde7891c/package-20261115T103704961Z-379f2cb3/` |
| Installer | `Shooter-Setup-0.3.6.exe`, 26,355,085 bytes, SHA256 `57659b9d8be7b687cdcf18f44e07959a0a0a3f4fc3728eaa1f0299dad11bc690`, predecessor 0.3.5, AppId unchanged, workspace clean | `Artifacts/m11-playable-release/946a58da684099abc1e6fd220ac49e97dde7891c/installer-20261115T103721220Z-d25742a5/installer/` |

Pending: actual install/upgrade and manual playtest of sprint/jump/slide, bot movement, Pause → Resume → Main Menu, first-person weapon view and the T077 visuals. `releaseAcceptance` NotClaimed.

### Delivery 0.3.7 (2026-11-15)

Offline-only delivery from clean `e251702`. It contains the bot NavMesh winding fix with packaged-path fallback (`70538c4`), the relaxed spawn fallback with recoverable SpawnFailure (`a095cf7`/`db524b1`, ADR-047), the 5 s pre-match countdown (`cd52534`, ADR-048), and their docs (`6fb7d34`). Locked Unity 6000.3.28f1 / 8f082d2fe476.

| Gate | Result | Evidence |
|---|---|---|
| Pure lanes | Generation 376, Simulation 204, Game 68, Maps 37, source provenance, contract lock PASS at `3785db4`; docs and fixtures PASS at `e251702` | `Artifacts/*/3785db495954b68a8e285e1b77db397ab21add82/` |
| Unity | EditMode and PlayMode **NotRun** for this delivery, so the new `ProductBotNavigationTests` has never run | — |
| Content | Reused `Artifacts/map-packages-native`, not recompiled; content closure `c8ca52fb…` unchanged from 0.3.6 | `Artifacts/map-packages-native` |
| Client / smoke | IL2CPP client build PASS; client smoke **NotRun**; server build and smoke not run (offline-only) | `Artifacts/m0-foundation/e2517024dfa59ee5c74372f042efbff0d25c9d8b/build-client-20261115T111151155Z-191f256d/` |
| Package | `Package-Release.ps1 -RequireNativeNav -MapCacheRoot Artifacts/map-packages-native` PASS, unity-native-navmesh, 294 files | `Artifacts/m11-playable-release/e2517024dfa59ee5c74372f042efbff0d25c9d8b/package-20261115T111645759Z-c45285ea/` |
| Installer | `Shooter-Setup-0.3.7.exe`, 26,362,673 bytes, SHA256 `24a3c3db1ebfe266980b2a2c12c1955210d41dcdc1d07a3065fd7bde80416bdd`, predecessor 0.3.6, AppId unchanged, workspace clean | `Artifacts/m11-playable-release/e2517024dfa59ee5c74372f042efbff0d25c9d8b/installer-20261115T112034943Z-919f4b3a/installer/` |

Pending (all manual):
- Install or upgrade over 0.3.6, and run a playtest in the installed build. The owner could not test in this session.
- Bots must leave spawn. Check that `SHOOTER_AI_SUMMARY` in `Player.log` shows `PathsCompleted` rising.
- Respawn should take about 3–5 s, with no "Awaiting a safe spawn" stall.
- The 5 s countdown overlay and freeze, then "FIGHT!".
- Unity EditMode/PlayMode and client smoke for this commit.

`releaseAcceptance` NotClaimed.

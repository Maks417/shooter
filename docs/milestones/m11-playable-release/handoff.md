# M11 — Playable build and acceptance — handoff and gate record

Records from 2026-11-15 onward. Older entries — T062–T085, deliveries up to 0.3.12, historical candidates and the T065 operator runbook — are in the [handoff archive](handoff-archive.md).

## ADR-074 — Compact 64 × 80 m maps with distinct layouts — 2026-11-15

Owner: the maps were too big (all fights happened in the middle) and looked alike (a box building in the middle surrounded by odd free-standing walls). Shrink all three maps, keep Oil Depot's best parts, and give Compound and Depot Yard new layouts. [ADR-074](../../decisions.md#adr-074--accepted-compact-64--80-m-arenas-with-distinct-per-map-layouts); layouts in [tdm-map-layouts.md](../../features/tdm-map-layouts.md).

**Implemented (software):**
- `ArenaRules` compact thresholds: 56–72 × 72–88 m, anchors 60–76 m, spawn→centre 26–44 m, spawn→enemy 60–100 m, sightline ≤ 48 m.
- `ArenaSymmetry` (`MirrorZ` / `Rotate180`) in `ArenaLayout`, `ArenaBoxBuilder` and the mirror rule.
- `ArenaFeature` / `CompoundPlan.Features` plus the mesh-only `ArenaFeatureDressing` (trucks, tankers, trailers, boxcars, flatcar, tarped load, sandbags, booths, flag, pipe racks).
- Planners: `environment.compound.arena` 3.0.0 (garrison square, `Rotate180`), `environment.depot.arena` 3.0.0 (rail crossing), `environment.oil.arena` 2.0.0 (compacted).
- `ArenaVisualDetail` and `OilDepotDressing` re-placed for the new layouts. The ADR-071 loading gantry was removed with its containers.
- Map ids, seeds, recipes, package/metadata schemas, nav settings and the 256 m frame are unchanged. The legacy W01 compound (B02/B03 freeze) is untouched.

**Measured (pure `ArenaRules`):**

| Map | Path to centre | Path to enemy | Sightline |
|---|---|---|---|
| Compound | 40 m | 84 m | 46.5 m |
| Depot | 42 m | 80 m | 47.25 m |
| Oil | 40 m | 88 m | 46 m |

- All three maps: 64 × 80 m, zero spawn LOS, worst cover gap ≤ 5.7 m.
- Spawn visibility from outside staging is ≤ 4 own slots on Compound and Depot (gate chutes and offset staging openings).
- ArenaVisualDetail gameplay fingerprints refrozen for the new planners: compound 113 boxes `3157c05b…`, depot 81 boxes `83b8251d…`.

**Checks (clean source `d7adc84`):**
- **Wrapper lanes:** `Test-Generation` 437 PASS, `Test-Maps` 15+53 PASS, `Test-Simulation` 321 PASS, `Test-Contracts` 76 PASS, `Test-SourceProvenance` PASS.
- **Game-v1 tests:** 88/88 PASS via `dotnet test`.
- **`Test-Game.ps1` wrapper:** FAILs its Game-v1 lock pre-check on any clean LF checkout. This is pre-existing at `334812b`: the lock hash for `docs/game-fixtures/user-settings-last-weapon.valid.json` was frozen from a CRLF working copy. It is not caused by ADR-074.
- **Content:** software `Compile-GameContent.ps1 -Force` PASS, 12 packages; gameplay package roots compound `0653de52…`, depot `5da3d4af…`, oil `d5f1e3e4…` (software-walkable-grid NVB1). Evidence: `Artifacts/m8-runtime-composition/d7adc84…/compile-gamecontent-20261115T220041193Z-bda40418/`.
- **Native content (merged `48373d1`, locked editor):** `Compile-GameContent.ps1 -Native -Force` PASS. All 12 packages are native (`unity-ai-navigation-2.0.0;pinned-physx-collision-bake`) and the non-navigation byte-identity guard PASSes. Gameplay package roots: compound `d15c01d7…`, depot `50229906…`, oil `5a77763e…`. Evidence: `Artifacts/m8-runtime-composition/48373d1…/compile-gamecontent-20261115T221531760Z-36b2c426/`.

**Pending:**
- Unity EditMode/PlayMode and Server build.
- The named 5v5/6v6 playtest of all three maps: time to first contact, spawn pressure, bot routing on the new topologies.

Client/native/package/installer for the compact maps shipped as [Delivery 0.3.16](#delivery-0316). This does not close FIX/SET/REL/P05/VD exits.

## Kill-streak and headshot score badges — 2026-11-15

Owner: different score badges for 2 / 3 / 4+ kills in a row until local death, plus an extra headshot badge.

**Implemented (software):**
- [ADR-073](../../decisions.md#adr-073--accepted-life-scoped-multi-kill-and-headshot-hud-badges): life-scoped local-human streak on `OfflineGameSession`; `LocalScoreAward` carries `KillStreak` + optional `Headshot` kind (0 points); Score still kill=100 / assist=HP.
- `ScoreBadgeVisual` labels: `ENEMY KILLED` / `DOUBLE KILL` / `TRIPLE KILL` / `MULTI KILL` / `HEADSHOT` / `ASSIST`; ribbon uses streak kill label; headshot is feed-only.
- Fatal head correlated from same-batch `DamageApplied.RegionId` (no C0 DeathPayload change).
- Pure tests: `ScoreBadgeVisualTests`, `LocalKillBadgeLogicTests`.
- Checks (software): Simulation **322/322** PASS (`Artifacts/m2-combat-harness/129a0ad8…/20261115T215354942Z-a74cf281/`); Game **96/96** PASS (`Artifacts/m8-runtime-composition/129a0ad8…/20261115T215253560Z-6af3d5d0/`); provenance PASS (826 entries / 413 GUIDs); docs/fixtures PASS after manifest refresh.

**Pending:** named 720p player/installer badge readability. No Game-v1 / contract-lock change.

## Kill-feed weapon and headshot icons — 2026-11-15

Owner: replace the elimination-row arrow with the kill weapon icon and a headshot mark (AAA FPS layout).

**Implemented (software):**
- `KillFeedEntry` / `KillFeedWeapon` plus `KillFeedCause.Resolve` correlate same-batch `ShotAccepted.WeaponId` and fatal `DamageApplied.RegionId` (head = 1); grenade kills use Damage-without-Shot; environment deaths stay icon-less.
- `ProductSessionView.DrawKillFeed` draws procedural Fill silhouettes (`TacticalUi.KillFeedWeaponIcon` / `KillFeedHeadshotIcon`) between names; no sprites; C0 `DeathPayload` unchanged.
- Pure tests: `KillFeedCauseTests`, extended `KillFeedTextTests`.

**Pending:** named 720p player/installer readability review. No contract-lock change.

## Bot short first-name display names — 2026-11-15

Owner: replace synthetic `BotN` roster labels with readable short first names.

**Implemented (software):**
- `BotDisplayNames` fixed ASCII pool (≥24) with seed-deterministic unique assignment per match (`Resolve(botIndex, botCount, seed)`).
- `OfflineGameSession.BuildRoster` keeps human as `Player` and assigns bot `ParticipantState.DisplayName` from the helper (scoreboard / kill feed / shell already consume DisplayName).
- Pure coverage in `BotDisplayNamesTests` (determinism, uniqueness, offline 5v5 roster smoke).

**Checks (software):** Game **88/88** PASS (`Artifacts/m8-runtime-composition/8f70a4af…/20261115T211434277Z-db90755b/`). Docs/fixtures PASS after manifest refresh.

**Pending:** named player/installer review of scoreboard and kill-feed readability. No Game-v1 / contract-lock change.

## ADR-072 — weapon profiles

2026-11-15. Owner: make adding a new rifle a catalog/recipe row instead of slot-0 forks. [ADR-072](../../decisions.md#adr-072--accepted-definitionid-weapon-profiles-catalog--visual-bind).

**Implemented (software):**
- Simulation `WeaponCatalog` + `WeaponLoadoutPolicy` (default primary still `weapons/rifle`; proof primary `weapons/rifle-carbine`).
- Presentation `WeaponVisualProfiles` drive optic/handling/audio/mesh keys by DefinitionId; `ActorLifecycleState.ActiveWeaponDefinition` mirrors authority.
- `geometry.weapon` **1.0.5** required `silhouette`; map packages publish carbine LOD/mag/cycle paths; presentation binds by profile asset key.
- AI fire interval / magazine thresholds read equipped catalog definitions.

**Checks (software):** Simulation **314/314** PASS; Generation **431/431** PASS; Game **88/88** PASS; feature-content-lock / Game-v1 lock / docs verify PASS.

**Pending:** content republish with 1.0.5 packages (carbine meshes in map visual closure), named player/installer review. C0 PredictionSchema unchanged.

## Combat feel — auto-reload, empty grenade, M67 grenade mesh, bot stuck rescue — 2026-11-15

Owner playtest fixes (no C0 / Game-v1 wire freeze).

**Implemented (software):**
- `WeaponSimulator` auto-starts reload when magazine is empty and reserve remains (manual `R` still works for partial mags); reload cues use existing `ReloadStarted`/`ReloadCompleted` WAV bank with slightly hotter soft-cue attenuation.
- Empty grenade slot cannot be selected; after the one-per-life throw/cook boom the authority equips rifle so hands are not empty. `LocalPlayerInput` blocks Digit4 for the rest of the life after a local spend.
- `LoadoutPropGeometry.BuildGrenade` rebuilt as an M67-style frag (shorter egg, tall fuse well, thicker spoon, pin/ring, seam rings). `feature-content-lock.json` hash refreshed.
- AI `NeedsRelocation` after repeated stuck failures (and stuck accumulation with move intent even when path tickets are gone); `OfflineGameSession.RescueEmbeddedActors` relocates those bots via the same spawn-safety path as overlap rescue (no death/score/life change).

**Checks (software):** Simulation **308/308** PASS (`Artifacts/m2-combat-harness/e89eb9ee…/20261115T204558703Z-8ecfef13/`); Generation **430/430** PASS (`Artifacts/m1-procedural-feasibility/e89eb9ee…/20261115T204626247Z-ef9d9f24/`). Software `Compile-GameContent.ps1 -Force` **PASS** 12 packages (`…/compile-gamecontent-20261115T204831933Z-bfe73c5a/`); native `-Force -Native` **PASS** (`…/compile-gamecontent-20261115T204955153Z-919b1ab1/`, `nativeUnityNavBake=PASS`, non-nav byte-identity guard PASS). Client/installer and named player review remain Pending.

## ADR-071 — Oil Depot third map + elevated routes — 2026-11-15

Owner: Warface Oil Depot–inspired third catalog map with walkable stairs/catwalk. [ADR-071](../../decisions.md#adr-071--accepted-oil-depot-third-map-and-limited-elevated-routes); layouts in [tdm-map-layouts.md](../../features/tdm-map-layouts.md); catalog in [playable-maps.md](../../features/playable-maps.md).

**Implemented (software):**
- Y-aware `CompoundConnectivity` (pass-under thin decks; layered climb ≤0.25 m steps); `ArenaGeometry` stair/ramp/catwalk helpers; software/native nav bake walkable slabs.
- `OilArenaPlanner` / `map.oil` / seed `460072032` / gameplay ids `30000+`; ArenaRules + elevated unit tests.
- Release catalog/UI/shell/workloads unlock three MapIds (`training-compound`, `depot-yard`, `oil-depot`); packages scale to 12 (3×4); content-format + game-contract locks refreshed.

**Checks (software):** ArenaRulesTests **27/27** PASS (incl. oil + elevated climb); MapPackageTests+NavBakeStage **16/16** PASS; Game catalog/workload filter **10/10** PASS; docs/fixtures/manifest PASS; content-format + game-contract locks PASS. Software `Compile-GameContent.ps1 -Force` **PASS** — **12 packages** (3 maps × 4 profiles), oil gameplay root `8e36c567…`, software-grid NVB1, `sourceDirty=true` working tree, evidence `Artifacts/m8-runtime-composition/b7cc5aba…/compile-gamecontent-20261115T194305487Z-1a59462b/`. Full wrapper lanes (`Test-Generation`/`Test-Maps`/`Test-Game`), Unity EditMode/PlayMode, Client/Server, native `-Native` publication and named Oil Depot playtest remain Pending. Does not close FIX/SET/REL/P05/VD exits. Compound and Depot Yard gameplay layouts are unchanged.

## Audio spatial and mix refinement — 2026-11-15

Owner assigned the reviewed first audio improvement pass as a T089 / ADR-062 follow-up. Base `077b77f`; sole audio integrator worked in an isolated checkout after unrelated active geometry work was observed in the shared checkout. Scope: `Presentation/Audio/Runtime`, the audio pump/acoustic probes in `ProductSessionView`, pure audio tests, a real-collision Unity integration test and affected docs. Existing CC0 bank and all authority/contracts/settings/content identities are preserved.

Implemented camera-relative equal-power panning with live direction updates, local cue centering, rate-correct interpolated PCM, distance width/body and boundary fade, bounded map-only occlusion/enclosure probes, quiet recorded-PCM early reflections, priority-based replacement within 24 voices and a linked stereo limiter. Unity audio configuration is cached outside the callback. Horizontal footsteps reject teleport bursts. [Detailed behavior and limitations](../../features/audio.md#owner-requested-spatial-and-mix-refinement--2026-11-15).

Final audio runtime source **`e7798c5251915e8deecc773087668c4a1348f3a3`** (initial pass `e8ace07`, then ambience recovery after voice saturation). Locked Unity **6000.3.28f1 / 8f082d2fe476**, unchanged toolchain hash `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8`. Durable evidence is relocated to **`Artifacts/audio-refinement/isolated-evidence/`**; raw reports keep original absolute worktree paths, with a relocation/inventory proof beside them. The 12-second matched before/after PCM examples and provenance are in **`Artifacts/audio-refinement/audition/`**: local rifle, enclosure, distant rifle, right-side rifle, wall-muffled rifle and pistol. These explicit acoustic parameters are an offline illustration, not a captured device/player test.

| Check | Actual result / run relative to durable evidence root |
|---|---|
| Contracts / Generation | **76 / 405 PASS**, dirty implementation runs on base `077b77f`: `m0-foundation/077b77f…/20261115T190003492Z-cc57bacd/`, `m1-procedural-feasibility/077b77f…/20261115T190101169Z-4d17e0a3/`. No contract/generation source changes in this task. |
| Final Simulation | **297/297 PASS**, including **30 audio cases**, clean source `e7798c5`: `m2-combat-harness/e7798c5…/20261115T191756925Z-9a888d49/`. Prior 292/294/296 runs retained. |
| Final Unity EditMode | **58/58 PASS**, including actual map-only occlusion/query-budget integration: `m0-foundation/e7798c5…/tests-editmode-20261115T191823674Z-fb000b6b/`. |
| Final Unity PlayMode | **21/21 PASS**, zero skipped/inconclusive: `m0-foundation/e7798c5…/tests-playmode-20261115T192039305Z-37610895/`. Initial run had two Inconclusive cases because this fresh checkout lacked map fixtures; no passing status was inferred. Matching pure diagnostic map fixtures were then generated (eight packages / 39 publication regressions PASS at `m8-runtime-composition/e8ace07…/compile-gamecontent-20261115T190717947Z-c46cf47a/`) before the successful rerun. This is not new native publication or navigation acceptance. |
| Clean IL2CPP Client | **PASS**, `sourceDirty=false`, non-development source `e7798c5`: `m0-foundation/e7798c5…/build-client-20261115T192259171Z-07ec32a2/`. Initial `e8ace07` clean build retained separately. This is an internal compile artifact, not a new installed game or final candidate. |
| Client smoke | **PASS**, `m0-foundation/e7798c5…/smoke-client-20261115T192519824Z-0284e624/`; startup/shutdown only, not a listening or manual gameplay review. |
| Provenance / fixtures / docs | PASS: **802 entries / 401 GUIDs**, **35 specification assertions / 17 JSON**, docs links/hash inventory and whitespace. Documentation verification is not gameplay evidence. |

Owner explicitly requested **no installer now**, because other agents are working. No package/installer/native republish was produced; no fresh Server build was needed for this presentation-only task. Other agents' committed changes are preserved on integration and their uncommitted work is excluded from this audio build identity. Combined final-player/release validation belongs to the later integrator. Named headset/speaker listening, output-device-change proof, 6v6 reference CPU/GPU/thermal measurements and Battlefield V equivalence remain Pending. FIX10/SET02, P05 FAIL and other open release/hardware gates remain unchanged.

Rollback: revert the bounded T089 follow-up source/docs together; the bank/manifest and settings need no migration. Preserve test/build evidence outside the temporary checkout before removing it.

## T090 — Bot tactics (cover peek, wall hearing, ledge strafe, route spread) — 2026-11-15

Owner: approved Phase 1 bot-AI improvement plan. [ADR-070](../../decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090); [task](../../task-supplement.md#t090--bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread-2026-11-15); [ai.md](../../features/ai.md#bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread-t090-2026-11-15).

**Implemented (software):**
- `CoverFinder`: prefer peekable cover (side peek ray) over first-hidden; blind fallback when candidates exhaust.
- `CombatAiDirector`: gunfire/footstep wall occlusion; strafe footing probes; route-corridor spread penalties; `AiRaysUsedThisTick`.
- Tuning/counters in `AiTuning` / `AiCounters`; pure `BotTacticsT090Tests` (5).

**Checks:** `Test-Simulation.ps1` **290/290** PASS (includes new `BotTacticsT090Tests`); docs/fixtures/manifest PASS. Owner feel review, `SHOOTER_AI_SUMMARY` on product maps, AI CPU 1.5 ms, Unity EditMode/PlayMode, Client/Server/package/installer remain Pending. Does not close FIX/SET/REL/P05/VD exits. T085 lethality envelope unchanged.

## ADR-067 — Soldier pose + motor corner launch fixes — 2026-11-15

Owner: player/bot models flicky; sprint feet poor; slide animation missing; sprint near wall/floor seam launches into the sky. [ADR-067](../../decisions.md#adr-067--accepted-soldier-motionv5-slide-pose-gait-stability-and-motor-corner-anti-launch).

**Implemented (software):**
- `KinematicMotor`: airborne/non-jump upward velocity clamped after contact projection; non-walkable projection cannot raise `velocity.Y`; `TryStep` requires real rise.
- `soldier.motion.spike.v5`: `SoldierMotionInput.Slide` dive pose; walk/run hysteresis; clearer run duty/clearance.
- `LocalMovementView` + `ProductActorPresentation`: authoritative speed/distance for gait; slide wired; cosmetic root blend.

**Checks:** Simulation **285/285** PASS; Generation **401/401** PASS (feature-content-lock `motionVersion` v5); Game **79/79** PASS; docs/fixtures/manifest PASS. Incidental: `content-format-lock.json` hash for already-committed `release-catalog.valid.json` synced so P02 verify matches HEAD (no schema/format change). Named player/bot pose review, Unity EditMode/PlayMode, Client/Server/package/installer remain Pending. Does not close FIX/SET/REL/P05/VD exits.

## ADR-066 — UI copy + Barlow Condensed typeface bank — 2026-11-15

Owner: shell/HUD text felt technical; want BFV-like condensed fonts (non-commercial). [ADR-066](../../decisions.md#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed).

**Implemented (software):**
- OFL Barlow Condensed Regular/SemiBold/Bold under `Presentation/Fonts/Resources`; `UiFonts` + `TacticalUi` assign display/body/button faces; engine IMGUI fallback retained.
- Provenance `namedFontSources` + `Test-SourceProvenance.ps1`; map closure token `ui-typeface-bank-v1`; Package-Release ships `Content/Fonts/OFL-BarlowCondensed.txt` and license prose.
- Player-facing rewrite via `ShellUiCopy` / `InsertionScreenCopy` (e.g. Standing by, Linking the battlefield…, LOCAL MATCH, ON HOLD, Finding a safe drop…); catalog map flavor lines updated.

**Checks:** provenance + Simulation shell/HUD copy tests + Generation font-closure assertion + docs/manifest in this change set. Named 720p readability, Unity EditMode/PlayMode, Client/Server/package/installer remain Pending. Does not close FIX08/SET/REL/P05/VD exits.

## ADR-065 — PiP rifle scope (blurred periphery) — 2026-11-15

Owner: rifle ADS covered the whole screen; want sharp zoomed circle with live blurred world outside (reference PiP look). [ADR-065](../../decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery).

**Implemented (software):**
- AdsOpticVisual PiP helpers (PipStrength, LensUvZoom, KeepsHipCameraFov); rifle keeps hip camera FOV.
- AdsScopePipPresenter + Shooter/AdsScopePip shader: separable blur + circular composite (sharp zoomed lens, blurred periphery, soft rim). Preloaded in Client build beside ProceduralSurface.
- TacticalUi.AdsScope drops opaque corner fills; housing ring + mil-dot only.
- Pistol irons / vignette / camera FOV blend unchanged. ReducedCameraMotion skips blur/digital zoom.

**Checks:** Simulation pure optic tests + docs/manifest in this change set. Named player optic review, Unity EditMode/PlayMode, Client/Server/package/installer remain Pending. Does not close FIX/SET/REL/P05/VD exits.

## ADR-063 — Hit-region damage coefficients — 2026-11-15

Owner: headshot should kill; chest 3–4 rifle shots; limbs (arms/legs; no separate hand/foot volumes) 6–7. [ADR-063](../../decisions.md#adr-063--accepted-hit-region-damage-coefficients-one-shot-head).

**Implemented (software):** `DamageMath.RegionMultiplier` → head×4 / torso×1 / limbs×0.5. Rifle: 120 / 30 / 15. C0 region IDs, base weapon damage, fire rate and T085 bot-aim envelope unchanged; effective player TTK by hit region changes.

**Checks:** pure combat rounding tests + docs/manifest in this change set. Named player feel and installer identity remain Pending. Does not close FIX/SET/REL/P05/VD exits.

## Visible insertion loading screens — 2026-11-15

Owner feedback: match/boot felt like a blank hitch with no loading UI. Software already had `DrawLoading`, but synchronous package/nav/visual bind finished (or froze) before IMGUI could paint.

**Implemented (software):**
- Shared `TacticalUi.DrawInsertionScreen` briefing layout (contour backdrop, accent-rail plate, map name, rules, phase strip, optional packaged map-card media, bracketed indeterminate activity bar; reduced-motion freezes the segment).
- `ProductShellHost.StartSelectedMatch` awaits paint frames before `BeginLoadAsync` and before `BindVerifiedMap`; Cancel remains usable between phases.
- `BootLoadingHost` paints the same insertion screen for at least two frames before Main; recovery still replaces it immediately when content is missing/corrupt.
- Pure `InsertionScreenCopy` helpers + Simulation shell tests.

**Checks:** Simulation shell tests and docs/fixtures refresh in this change set. Named player visual review of boot/match insertion, Unity EditMode/PlayMode, Client/Server/package/installer remain Pending. Does not close FIX08/SET/REL/P05/VD exits.

## T088 — Weapon visual overhaul (ADR-061)

2026-11-15. Owner: improve weapon models, animations and fire feedback (full VD3 slice). [ADR-061](../../decisions.md#adr-061--accepted-geometryweapon-102-mechanical-parts-and-denser-fire-fx-t088).

**Implemented (software):**
- `geometry.weapon` **1.0.2**: richer body LODs; detachable magazine + cycle (bolt/slide) meshes packaged beside LOD meshes; sockets/collision mechanical dims preserved; 1.0.0/1.0.1 reject.
- FP/world bind of body/mag/cycle; mag remove/seat from reload phase; cycle open on `ShotAccepted` (~90 ms close); reduced-motion scales.
- Analytic FX: denser muzzle (dual lobe), `ShellCasing`, `Tracer`; `ReducedFlash` hides muzzle+tracer.

**Checks (software):** Generation 398/398, Simulation 263/263, Maps 56/56, feature-content-lock / docs / fixtures PASS on the working tree. Incidental `SoldierMotion` death-collapse local `right` shadowing rename (compile fix) included so Generation rebuilds cleanly.

**Native content:** `Compile-GameContent.ps1 -Native` PASS — eight packages; training-compound / depot-yard roots updated with magazine/cycle meshes; nativeUnityNavBake PASS. Evidence under `Artifacts/m8-runtime-composition/.../compile-gamecontent-...`.

**Builds:** Client PASS `Artifacts/m0-foundation/be6293b5c82041bdd397b6533c28af7b04a02493/build-client-20261115T162134243Z-c53f2c95`; Server PASS `.../build-server-20261115T162539462Z-5db920b3`. Package-Release with `-RequireNativeNav` **rejected dirty workspace** (`sourceDirty=true`) — needs a clean committed HEAD before ZIP/installer. Unity EditMode/PlayMode and named player visual review remain Pending. Does not close FIX/SET/REL/P05/VD exits.

### T088 follow-up — rifle POV fire presentation

2026-11-15. Owner playtest: gunfire read as spherical “bubbles” instead of rifle POV muzzle flame / recoil / bolt cycle. Completes ADR-061 product-path wiring (presentation only; no C0/Game-v1/`geometry.weapon` bump).

**Implemented (software):**
- `AnalyticFxPresenter` muzzle uses thin emissive capsules (crossed dual lobes), not spheres; hotter white/orange emission.
- `AnalyticFxPool.SpawnGunshotFeedback` spawns primary + cross-lobe muzzle, tracer, and ejected casing; `ReducedFlash` suppresses muzzle+tracer only (casings remain). Product `ObserveCombatFx` calls this helper (fixes prior single-sphere spawn and early `reducedFlash` return that dropped casings).
- Stronger rifle shot spring (~4.0 pitch), ~1.4× hip equipment kick, mechanical roll/pitch oscillation from recoil energy; rifle bolt travel 0.070 m; soft lower-center local overlay (not full-screen wash). Knife/grenade paths unchanged. ADS-safe kick rules preserved.

**Checks (software):** `Test-Simulation.ps1 -Offline` **283/283 PASS** (`Artifacts/m2-combat-harness/.../20261115T174854597Z-689efe26`). Docs/fixtures integrity refreshed. Named player fire review and installer remain Pending. Does not close FIX/SET/REL/P05/VD exits.

### T088 follow-up — weapon switch holster/draw

Cosmetic FP switch is two-phase: holster the visible mesh (~0.14 s), swap at the tuck, draw the incoming weapon (~0.20 s). Mid-switch keeps the outgoing gun on screen with an outward hip arc (position + pitch/yaw/roll); `AdvanceEquip` consumes leftover dt across holster→draw in one Step so hitch/test frames finish ready. ADS presentation blend gates with equip so the tucked gun cannot snap onto the sight ray. Reduced-motion shortens both phases and scales the arc. Pure `FirstPersonHandling` tests cover holster→draw, mid-draw re-switch, and ADS gating.

### T088 follow-up — ADS optic presentation

Rifle ADS now raises a procedural circular scope mask (soft housing + mil-dot reticle) with tighter blended FOV; the near-field rifle mesh hides at full optic so the lens stays clean. Pistol ADS keeps iron sights with a soft edge vignette and milder zoom. Hip reticle yields once optic strength passes the hide threshold. `AdsOpticVisual` pure tests cover mask/FOV/slot gating. Sight-socket alignment and combat aim rules unchanged. Named player optic review remains Pending.

## ADR-060 — Local tightness (stamina)

2026-11-15. Owner: held Shift sprinted forever; need tightness gating run/slide/jump with 5 s walk restore. [ADR-060](../../decisions.md#adr-060--accepted-local-authoritative-tightness-stamina).

**Implemented (software):**
- `MotorBodyState.Tightness` + `MotorConfig` recipe (8 s sprint drain, 5 s walk regen, jump 0.25, slide 0.40) gated in `KinematicMotor.Step`.
- `LocalMovementView.Tightness01` → compact STA bar above HP in combat HUD; C0/Game-v1 unchanged.
- Pure motor drain/regen/cost/exhaust tests.

**Pending:** interactive feel on a matching player/installer identity. Does not close FIX/SET/REL/P05.

## T087 — Knife (slot 3) and grenade (slot 4) (ADR-057)

2026-11-15. Owner asked for knife on Digit3 and grenade on Digit4 so players (and bots) can fight when guns are dry; one grenade per life until respawn; full cook/throw arc.

**Implemented (software):**
- [ADR-057](../../decisions.md#adr-057--accepted-knife-and-grenade-loadout-slots-t087): C0 `PredictionSchema` 2, `Require.Slot` 0..3, `WeaponAction.Cooking`, four `WeaponState` records; fixtures + `contract-lock.json` refreshed.
- Knife melee (1.8 m / 55 / interval 24 / sprint fire) and cook/throw grenade (fuse 120, blast 5 m, self-damage yes, friendly 0) in `WeaponSimulator` / `GrenadeSimulator` / combat harness.
- Game-v1 additive `Slot3`/`Slot4`; local input + HUD; AI empty-ammo knife / single grenade; procedural `LoadoutPropGeometry` packaged in map visuals.

**T087 follow-up (ADR-064, 2026-11-15):** owner playtest — knife/grenade reused gun animations and throws had no visible projectile.
- Rising-edge cook/melee after equip; `ActorLifecycleState.WeaponAction` + Game-v1 lock refresh.
- FP slash vs close-range stab; grenade cook/throw poses; hide held grenade when empty/thrown; `CosmeticGrenadeFlight` + presenter.
- Knife skips muzzle/overlay/rifle audio/gunfire reveal.

**Checks (follow-up software):** Simulation 277/277, Game 79/79, docs/fixtures + Game-v1 lock PASS on the working tree.

**Pending:** clean Client/Server/installer identity, Unity EditMode/PlayMode on the locked editor, interactive two-map playtest of knife stab-in-range and grenade throw/blast. Does not close FIX/SET/REL/P05.

## ADR-058 — Minimap self marker and temporary gunfire reveal

2026-11-15. Owner playtest: minimap did not show the local player; enemies who are shooting should appear briefly. [ADR-058](../../decisions.md#adr-058--accepted-minimap-self-marker-and-temporary-gunfire-reveal).

**Implemented (software):**
- Fill-based self pip + Fill facing tick at map center (replaces Line chevron clipped inside `GUI.BeginGroup`).
- Enemy-team `ShotAccepted` → 8-slot presentation ring (2.5 s / 0.5 s fade) plotted as coral plus markers from shot origin XZ; allies/local ignored; silent enemies never plotted.
- Pure `CombatHudVisual` filter/TTL/ring tests; brandbook / visual brief / VD7 wording updated.

**Pending:** interactive player confirmation on both maps; package/installer identity for a build that includes this change. Does not close FIX/SET/REL/P05.

## Analytic death collapse (soldier.motion.spike.v4)

2026-11-15. Owner playtest: dead bots held a confusing crouch-idle frame until respawn. Software path:

- `SoldierMotionInput.DeathPhase` (`-1` living, `0..1` collapse) and `EvaluateDeathCollapse` in `Generation/Core/SoldierMotion/SoldierMotion.cs` (version `soldier.motion.spike.v4`).
- `ProductActorPresentation` advances phase over 0.75 s on corpse / `!Alive`, holds a side-lying pose, hides weapons; removes the prior crouch=1 + always-reset corpse hack.
- Feature registry `motionVersion` + SoldierMotion/TwoBoneIk source hashes refreshed; visual-defect motion identity updated.
- Pure `SoldierMotionTests` cover collapse lowering, phase=1 determinism, living `-1` baseline and rejected phases.

**Pending:** interactive player confirmation; package/installer identity for a build that includes this change. Physics ragdoll remains optional/unimplemented. Does not close FIX/SET/REL/P05.

## T086 — Sample-bank combat audio (ADR-055)

2026-11-15. Owner asked for Battlefield V–class combat sound on the non-commercial build and authorized generated/found PCM (not commercial-game extraction). Software path:

- [ADR-055](../../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086) and [features/audio.md](../../features/audio.md).
- `CombatAudioBankBaker` / `CombatAudioBank`: deterministic layered mono PCM (rifle/pistol shot, impact, damage-taken, footstep, reload start/complete, death, UI, ambient loop) with content hash.
- `SynthAudioMixer` plays bank samples with distance atten, stereo pan, HF rolloff, ≤24 voices, peak limiter, schema-2 gains.
- `ProductAudioEventRouter` maps reload/death/damage and rifle vs pistol; starts ambient on session attach.
- Pure `ProductAudioTests` extended (bank stability, reload/death/pistol mapping, stereo pan).

**Pending (not PASS):** named FIX10 / SET02 device hearing of distinct cues and mute/gains; installer/player package identity for this change. Analytic beep path is superseded; rollback is revert T086. **Superseded for product path by T089** (stereo WAV bank + per-kind distance); T086 remains historical.

## T089 — Stereo combat WAV bank and footstep distance (ADR-062)

2026-11-15. Owner: weapon sound still bad; bot footsteps audible across the map. Software path:

- [ADR-062](../../decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089), [features/audio.md](../../features/audio.md), [task card](../../task-supplement.md#t089--stereo-combat-wav-bank-and-footstep-distance-adr-062-2026-11-15).
- `Content/Audio/combat/` stereo 48 kHz WAVs + `manifest.json` (sha256/source/license); bank id `combat-audio-bank-v3-stereo`.
- Mixer plays interleaved L/R with mild azimuth balance; per-kind distance (footsteps 18 m steep, soft cues 35 m, gunfire 80 m).
- Client build and `Package-Release` copy the bank beside the player; generated stereo Foley is fallback only.
- Pure Simulation audio tests PASS (cull, found-bank load, hash-mismatch fallback).

**Pending (not PASS):** FIX10 / SET02 named device hearing; package/installer identity that includes this bank. Does not close REL/P05.

**Shot sample refresh (same task):** rifle/pistol cues replaced with OpenGameArt CC0 real target-shooting recordings (Tabasco SKS/Mosin + CZ-52; kurt 22 Pistol/Magnum), sliced to 48 kHz stereo one-shots via `Tools/import_real_gunshots.py`. Manifest source notes updated. Named hearing still Pending.

**Knife/grenade cues (bank v4):** KnifeSwing on knife ShotAccepted; GrenadeThrow on ProjectileSpawned; GrenadeExplode on blast Impact heuristic. Samples via Tools/import_knife_grenade_audio.py (OGA swing + explosion/shotgun layers). Pure ProductAudio mapping tests PASS.

## ADR-056 — Last weapon (Q) and local-only muzzle overlay

2026-11-15. Owner playtest: screen flicks while bots fire nearby, and request for Q last-weapon quick switch. [ADR-056](../../decisions.md#adr-056--accepted-last-weapon-quick-switch-q-and-local-only-muzzle-overlay).

**Implemented (software):**
- Full-screen muzzle overlay / “Shot” subtitle gated to local human `ShotAccepted` via `FirstPersonHitFeedback.IsLocalShot`; world muzzle proxies and shot audio unchanged for bots.
- Additive schema-2 `LastWeapon` binding (default Key.Q), client previous-slot toggle in `LocalPlayerInput`, settings label “Last weapon”, legacy normalize (Q free → Key.Q, else Unbound; multiple Unbound allowed).
- Fixtures/tests: `user-settings-last-weapon.valid.json`, Game.Contracts UserSettings migration/conflict cases, EditMode last-weapon edge/life/device and local-shot unit tests. Game-v1 lock refrozen for Contracts sources + game-fixtures.

**Checks:** Game.Contracts 74/74 PASS; Unity EditMode PASS (`Artifacts/m0-foundation/d6ac76c9cfafe2ef3b775c7c2bf6d859df1be475/tests-editmode-20261115T152955036Z-b87e6536/`); docs + Game-v1 lock PASS on the working tree. Interactive two-map confirmation, package/installer identity for a build that includes this change, and FIX/SET/REL/P05 remain Pending.

## Arsenal visual improvement — 2026-11-15 (ADR-068)

Owner requested improvement of all weapon visuals using Battlefield V as the quality reference. Bounded scope covers rifle/pistol geometry and moving meshes, knife/grenade props, five spatial PBR finishes, near-field/world slot mapping, diagnostic consumers/capture tooling, migration/schema/registry and tests. See [feature details](../../features/geometry-weapons.md#arsenal-visual-profiles--geometryweapon-103). No imported assets, gameplay tuning, soldier clearance search or acceptance waiver. Source comparison and numeric tests preserve sockets/collision dimensions.

Work is isolated from concurrent uncommitted movement/soldier changes in `Artifacts/worktrees/weapon-visuals`; this candidate does not claim to contain those changes. Baseline `724d722` contained a stale hash for `docs/content-schemas/release-catalog.valid.json`; the existing committed LF bytes hash to `7abce498…255d74`. Only its source-lock identity was repaired, without editing fixture bytes or format. Failed sandbox attempts (SDK-cache restore and Unity local-socket initialization) and early generator/test failures are retained. Minimum slot thickness, all special-slot null moving meshes, diagnostic rifle version and spatial-mask rendering were corrected before final verification. The old holster assertion exposed a reduced-motion tuck of 96.25 mm; increasing the cosmetic tuck by 10 mm preserves the existing >100 mm test rather than weakening it.

Final verification/build identities and durable evidence relocation are recorded below when available. Battlefield V equivalence, named first-person/world review, manual gameplay and reference performance remain Pending. P05 FAIL / M8–M11 acceptance are unchanged.

Pre-build source verification (isolated worktree, dirty implementation over `724d722`): Contracts 76/76 PASS (`20261115T182522202Z-8496f66d`); Generation 402/402 PASS before the final recess albedo encoding adjustment (`20261115T182401605Z-0cc6a0d9`, final rerun recorded below); Unity EditMode 57/57 PASS (`tests-editmode-20261115T182801521Z-45542c75`); provenance PASS 802 entries / 401 GUIDs; feature schema/migration 19 cases PASS; specification 35 assertions / 17 JSON PASS; docs verifier and diff whitespace PASS. These checks do not establish player visual acceptance. The initial eight source renders at `capture-arsenal-20261115T182213686Z-f461385e` are superseded by the final smoothed-profile/PBR render set below.

Final pure source suite: **Generation 402/402 PASS** at `20261115T182938006Z-dea6b604`. Assembled LOD0 rifle **13,196 triangles**, pistol **6,452**, including detachable parts (ceilings 18,000 / 8,000). EditMode 57/57 as above. First PlayMode attempt: 18 pass, 1 fail (stale pre-detachable-part lifetime inventory expected 3 meshes, actual correct inventory 5), 2 inconclusive because the isolated checkout had no native map cache. The inventory check now explicitly includes body, magazine, cycle and both hands; native publication and the final rerun follow below. No inconclusive attempt is counted as PASS.

### Arsenal delivery 0.3.14

Clean binary/content source **`b8235a38f157108af9af7a90a6144961fab69be6`**. Windows installer **0.3.14**, **33,600,396 bytes**, SHA256 **`39eb7ad9781842cc376a6e8d5aa97eda33b0a9bcfeb156124417c44b005bd680`**. Convenient delivery: `Artifacts/deliveries/Shooter-Setup-0.3.14.exe`. Stable per-user AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; version checked against retained 0.3.13. Actual install/upgrade/settings preservation remains Pending.

Durable evidence root: **`Artifacts/weapon-visuals-evidence/b8235a3/`**, containing the isolated checkout's original Artifacts subtree. Raw reports retain original absolute worktree paths; `relocation.json` records the move. `previews/` contains final side/three-quarter source renders for all four weapons. The temporary worktree and task branch are removed after verification; concurrent movement changes on main are preserved, with backup/preservation proof at `Artifacts/weapon-visual-integration/`.

| Check | Actual result / retained run relative to durable evidence root |
|---|---|
| Contracts / Generation | 76/76 / 402/402 PASS; baseline `724d722` dirty implementation runs listed above. Final generation `20261115T182938006Z-dea6b604`. |
| Unity EditMode | 57/57 PASS, zero skipped; `m0-foundation/724d722…/tests-editmode-20261115T182801521Z-45542c75/`. |
| Unity PlayMode | **21/21 PASS**, zero skipped/inconclusive; `m0-foundation/b8235a3…/tests-playmode-20261115T183845486Z-2e00d975/`. |
| Native packages | **8 PASS**, 39 filtered publication regressions PASS; `m8-runtime-composition/b8235a3…/compile-gamecontent-20261115T183138397Z-c56a04bd/`. All non-navigation payloads equal the pure publisher; gameplay roots are unchanged by these visual changes. |
| Clean IL2CPP Client | PASS, `sourceDirty=false`; `m0-foundation/b8235a3…/build-client-20261115T184138827Z-1d7c16eb/`. |
| Release smoke | PASS; `m0-foundation/b8235a3…/smoke-client-20261115T184348602Z-cae902f1/`. Boot/shutdown only, not manual gameplay. |
| Package / installer | PASS, native navigation required; `m11-playable-release/b8235a3…/package-20261115T184240651Z-ec158f97/` and `installer-20261115T184423619Z-a8b753df/`. Extraction and corrupt-content rehearsals PASS; rollback `SkippedNoPrevious`. |
| Final source renders | Eight PNGs, reviewed for gross geometry/material defects; `m0-foundation/b8235a3…/capture-arsenal-20261115T184052474Z-832f46f4/`. Dirty flag is the test-created empty Resources metadata, no runtime code change; this is source diagnostic lighting, not a gameplay capture or named VD review. |
| Provenance / schema / docs | PASS: 802 entries / 401 GUIDs; 19 migration/schema cases; 35 specification assertions / 17 JSON; docs links/hash inventory and diff whitespace. |
| Server / hardware / manual | Fresh Server build not run for this offline presentation change. Named four-slot first-person/world gameplay, installer upgrade, reference captures, P05 and acceptance remain Pending / NotClaimed. |

Failure history retained: first native build exhausted disk while copying `GameAssembly.pdb`; incremental retry compiled successfully but had the test-created empty Resources metadata (`sourceDirty=true`). Removing only this task's failed player output and empty test folder allowed the final clean build above. The task's generated Unity Library cache was removed after builds/captures to recover disk space; no installed package, curated source or generated payload was edited to bypass provenance. All failure reports remain. The preview contains the weapon assignment; concurrent uncommitted soldier/motor work is not relabelled as part of this binary.

Package ZIP SHA256 `923d2614f6820e42fa85eea4130ac27f638f64846b3602a4ae10a7cc740cd0b5`; source hash `0de06fdd6f6a802aed8a12a28169aaa59bc49595c859b8497109a5db50456893`; content closure `6e24f0c55221a1ece738544777bf57cf5b275ef50ed876bd223c66e96dcb774d`; index `b92af46fa08e0b6767765f774b9c54e0269e615ef410ddac0d400c3fd07929ba`. No Battlefield V parity or release/visual/performance acceptance is claimed.

### Delivery 0.3.15 (2026-11-15)

Clean binary/content source **`d51e377d4d68c2dde305d6c7935f3c99c788316f`**. It adds everything merged since 0.3.14: articulated first-person hands and procedural reflections, spatial combat audio, T090 bot tactics, motor corner-launch fix and slide pose, rifle shoulder framing, and the Oil Depot third map (ADR-071). It also includes the owner-approved B02 `routeHash` refreeze (ADR-071 addendum, `ea09345`) and a bounded retry for atomic cache-file replacement (`d51e377`). Windows installer **0.3.15**, **36,927,184 bytes**, SHA256 **`3ed1e4476cd13a05829cafbcbd2dcfe09111d7a868ee10e3920ae2c3c3834a6f`**. Convenient delivery: `Artifacts/deliveries/Shooter-Setup-0.3.15.exe`. Stable per-user AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; predecessor is 0.3.14. Locked Unity 6000.3.28f1 / 8f082d2fe476; Inno Setup 6.7.3.

Before the build, the repository was consolidated: the merged `codex/audio-pass` branch and its worktree, two superseded stashes and two empty worktree folders were removed; the worktree's ignored evidence was copied without overwrite into the main `Artifacts/`.

| Check | Actual result / retained run |
|---|---|
| Pure lanes | Contracts, Game, Maps, Simulation, SourceProvenance, docs, fixtures and contract lock PASS at `e2b8b69`. Generation 424/425 there (B02 route freeze); after the refreeze 425/425 PASS at `ea09345` and `d51e377`; Maps and provenance re-run PASS at `d51e377`. |
| Native packages | **12 PASS** (3 maps × 4 profiles), `nativeUnityNavBake=PASS`, `sourceDirty=false`; `m8-runtime-composition/d51e377…/compile-gamecontent-20261115T200602339Z-e1e693e4/`. |
| Clean IL2CPP Client | PASS, `sourceDirty=false`; `m0-foundation/d51e377…/build-client-20261115T200801637Z-cd6db336/`. |
| Release smoke | PASS; `m0-foundation/d51e377…/smoke-client-20261115T201353735Z-2a3da4f6/`. Boot/shutdown only, not manual gameplay. |
| Package / installer | PASS, native navigation required, 902 inventory files; `m11-playable-release/d51e377…/package-20261115T201219186Z-23ff7a9b/` and `installer-20261115T201246187Z-744c9199/`. Extraction and corrupt-content rehearsals PASS; rollback `SkippedNoPrevious`. |
| Not run | Unity EditMode/PlayMode, Server build, benchmarks/reference captures, actual install/upgrade/settings preservation, named Oil Depot playtest. All remain Pending / NotClaimed. |

Failure history retained: three native compiles at `ea09345` failed in the final .NET republish with `Unable to remove the file to be replaced` (`nav-provenance.json` twice, `catalog.json` once), each leaving an orphan stage `.tmp`. The cause is an external transient sharing lock; it was not reproduced outside the publisher and no lingering editor/dotnet process held the files. `d51e377` routes the three staged writes through `MapPackagePublisher.CommitStagedFile` (bounded IOException retry, stage cleanup). The failed runs remain under `m8-runtime-composition/ea09345…/`.

Package ZIP SHA256 `815977a8b8f094925949cb9008864f2e503674896adc957cfd577b47b1551f24`; source hash `6fbddf5f2cf6f90a62f4c530e207c944e96365e519bf5c038035fff1f74485a8`; content closure `5c32c37f8287d2b6deb9b3e2fd4cd7c41a82c71ffba09c05158ba90926a7396c`; index `aba6dbbc5ee4ce9dc20b814884548ceb5b194169ae092dc5c3ef997f5ca9c9f0`. No release, visual or performance acceptance is claimed.

### Delivery 0.3.16

Clean binary/content source **`7e54be9ae8ff720baccf5aab221bf5cabfb6bc9b`**. It adds everything merged since 0.3.15: Oil Depot petroleum dressing, combat-feel auto-reload/empty-grenade/bot stuck rescue, unique bot first names, ADR-072 DefinitionId weapon profiles, kill-feed weapon/headshot icons, life-scoped multi-kill/headshot HUD badges, ADR-074 compact 64 × 80 m arenas with distinct layouts, and the ADR-040 last-weapon fixture LF lock refreeze. Windows installer **0.3.16**, **37,380,820 bytes**, SHA256 **`03e50de130978f98e7fb524e583e3e73e7cb1b03c24ed4c7b6518bf1ae5841cb`**. Convenient delivery: `Artifacts/deliveries/Shooter-Setup-0.3.16.exe`. Stable per-user AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; predecessor is 0.3.15. Locked Unity 6000.3.28f1 / 8f082d2fe476; Inno Setup 6.7.3.

| Check | Actual result / retained run |
|---|---|
| Pure lanes | Game **96/96**, Maps **68**, Generation **437**, SourceProvenance **828/414** PASS at clean `7e54be9` (`m8-runtime-composition/7e54be9…/20261115T222351628Z-3d80aa99`, `test-maps-20261115T222429628Z-8abef4d6`; `m1-procedural-feasibility/7e54be9…/20261115T222544155Z-b028c807`). |
| Native packages | **12 PASS** (3 maps × 4 profiles), `nativeUnityNavBake=PASS`, `sourceDirty=false`; `m8-runtime-composition/7e54be9…/compile-gamecontent-20261115T222717623Z-4c1f5810/`. |
| Clean IL2CPP Client | PASS, `sourceDirty=false`; `m0-foundation/7e54be9…/build-client-20261115T222905078Z-266f7d6b/`. |
| Release smoke | PASS; `m0-foundation/7e54be9…/smoke-client-20261115T223246495Z-a1412ca6/`. Boot/shutdown only, not manual gameplay. |
| Package / installer | PASS, native navigation required, 956 inventory files; `m11-playable-release/7e54be9…/package-20261115T223254515Z-edab47ab/` and `installer-20261115T223324333Z-7960e785/`. Extraction and corrupt-content rehearsals PASS; rollback `SkippedNoPrevious`. |
| Not run | Unity EditMode/PlayMode, Server build, benchmarks/reference captures, actual install/upgrade/settings preservation, named compact-map playtest. All remain Pending / NotClaimed. |

Package ZIP SHA256 `4d9326ae4e43ab9e348c567ecffca59dc245c5ec1826101395fdddba686f2ec1`; source hash `e27dcaf30098f88b2c3a9ec7154a687db12e10e971e56b7d32a85eabd1b49032`; content closure `10162c185d48bbd1491df5c5f9370e323b2d21dbf5057bd1282a5cdac1a4e6f4`; index `89fc582497d8d2c70b0f2db09d3aa6d823cc06a23c0589b6dfbfc0b220980e8b`. No release, visual or performance acceptance is claimed.

### Delivery 0.3.17

Clean binary/content source **`1676ff88a0a0dffeabbb7859d441f5fd66293751`**. It adds everything merged since 0.3.16: ADR-075 third-party provenance and FastNoiseLite surfaces, ADR-076 offline-only scope, code-built InputActionMap, Shooter.VectorMath, ADR-077/078 weapon definition files and the shared weapon package, ADR-079 engine toolchain lock, ADR-080 code-built post-processing/HDR/SSAO tiers, ADR-081 baked AO volumes, ADR-082 parametric rifle parts, ADR-083 map registry and `map.arena` layouts, ADR-084 SDF UI primitives and HUD fixes, rifle hip framing, ADR-085 SDF icon atlas and ADR-086 animated debrief/front-end motion. Windows installer **0.3.17**, **36,541,803 bytes**, SHA256 **`7701fdd83cb387ec0a8cbb8f4c28108afb224c081980284fb2d06363e7df166b`**. Convenient delivery: `Artifacts/deliveries/Shooter-Setup-0.3.17.exe`. Stable per-user AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; predecessor is 0.3.16 (monotonic check against its installer report PASS). Locked Unity 6000.3.28f1 / 8f082d2fe476; Inno Setup 6.7.3.

| Check | Actual result / retained run |
|---|---|
| Pure lanes | Game **99/99**, Maps **72**, Generation **508**, SourceProvenance **932/466** PASS at clean `91dfaef` (`m8-runtime-composition/91dfaef…/20261116T202847191Z-2f697849`, `test-maps-20261116T202926393Z-dcbf318d`; `m1-procedural-feasibility/91dfaef…/20261116T203043273Z-942d3993`). `1676ff8` only adds docs and `Tools/Capture-UiReview.ps1`; no linked source changed. |
| Native packages | **12 PASS** (3 maps × 4 profiles), `nativeUnityNavBake=PASS`, `sourceDirty=false`; `m8-runtime-composition/1676ff8…/compile-gamecontent-20261116T203423312Z-180f9090/`. An earlier identical compile at `91dfaef` recorded `sourceDirty=true` because commit `1676ff8` was being made in the checkout when it started; it is not used. |
| Clean IL2CPP Client | PASS, `sourceDirty=false`; `m0-foundation/1676ff8…/build-client-20261116T203628981Z-02287b71/`. |
| Release smoke | PASS; `m0-foundation/1676ff8…/smoke-client-20261116T203705716Z-640b7b3d/`. Boot/shutdown only, not manual gameplay. |
| Package / installer | PASS, native navigation required, 760 inventory files; `m11-playable-release/1676ff8…/package-20261116T203714090Z-20f733d9/` and `installer-20261116T203731483Z-0cee3ad7/`. Extraction and corrupt-content rehearsals PASS; rollback `SkippedNoPrevious`. |
| Not run | Unity EditMode/PlayMode, Server build (postponed, ADR-076), benchmarks/reference captures, actual install/upgrade/settings preservation, playtest of this build. All remain Pending / NotClaimed. |

Package ZIP SHA256 `8988cc140bccc374f53d64c80a86d6051fb2570cffd7774893837b58a3e51090`; source hash `aaa2fdc7e370c5906a9dd156b701e86a0b6c80c40046cb16c20d1a41bf56bc41`; content closure `222d46cdd928e086c5b41e082a88ed2bd9194840a0975aae6d133068f18c4cab`; index `d84e129d6ef00bc4511338dba084070bef745e6518b1f60b51db556452e63483`. No release, visual or performance acceptance is claimed.

## Workspace cleanup evidence loss — 2026-11-15

A manual disk cleanup deleted 40 ignored per-commit run folders under `Artifacts/m0-foundation/` and `Artifacts/m11-playable-release/` (≈5.5 GB). Its reference check matched only full 40-character hashes, so runs cited in docs by 7-character short hash were deleted too. The deletion bypassed the Recycle Bin; the raw files are **unrecoverable** and can only be rebuilt from their source commits. The recorded results, hashes and run IDs in the handoffs stand as written, but their raw files no longer exist:

| Short hash | Deleted roots | Cited for |
|---|---|---|
| `7d0f9e6` | m0-foundation, m11-playable-release | Retained intermediate 0.3.3 installer/package and its build runs (this handoff). `Artifacts/visual-design/7d0f9e6/player-review/` is intact. |
| `075c548` | m0-foundation, m11-playable-release | Package/installer whose native identity check aborted all captures (ADR-043 context). `Artifacts/map-packages-native-mono-defect-075c548/` is intact. |
| `0aa57aa`, `3d67279` | m0-foundation | Game/Simulation pure runs and scene compile evidence "under 9f2219f / 0aa57aa / 3d67279 artifact roots"; the `9f2219f` root is intact. |
| `a190fc5`, `cbd359b`, `4e71bea` | m0-foundation | Generation/Game/Maps, Simulation and base-commit pure-test reports. |
| `585dbcd` | m0-foundation | T068 presentation test runs (product-state audit). |
| `eb81151` | m0-foundation | Test/build runs at the T078 merge commit. |
| `28df022`, `9f73624` | m0-foundation | Rejected prescribed attempt and failed sandbox restore (see M0 handoff). |
| `2cbed1a` | m0-foundation | Runs at the B01-materials collection commit (see M1 handoff); the summary in `Artifacts/m1-continuation/` is intact. |

No PASS/FAIL/Pending status changes because of this loss; any claim that depended on re-inspecting these raw files must be re-produced, not assumed. Future cleanup must use `Tools/Clean-Artifacts.ps1` ([runbook](../../bootstrap-runbook.md#reclaiming-disk-after-an-installermilestone-build)), which keeps runs cited by full or short hash. Its first dry run after the loss reported only empty leftover folders and kept 24 GB of cited/recent evidence.

## Oil Depot visual identity pass — 2026-11-15

Owner report: Oil Depot looked the same as the existing maps. Cause: it reused depot-yard materials, lamps, fences and curbs, and its "octagon" tanks were square boxes. This pass is visual only. `OilDepotDressing.cs` is appended by `MapSolidsMesh.Build(plan, mapId)` (`MapPackageBuilder` now passes `mapId`). It adds round 32-sided tank shells (the square drum boxes are no longer drawn; their collision still applies), wall pipe racks, overhead pipe bridges, pump-house roof plant, a loading gantry and an out-of-bounds tank skyline with a flare stack. Two new accent roles, `TankWhite`/`SafetyYellow`, are added to `MapSolidsMaterialRole` and `ProductSurfaceMaterial.ArchitecturalAccents`. The design is original and does not reproduce any commercial map. Gameplay collision boxes, collision/nav hashes, routes, spawns, seeds and the Compound/Depot Yard meshes are unchanged.

**Checks (software, dirty tree on e89eb9e):** `Test-Generation.ps1` **430/430** PASS (`Artifacts/m1-procedural-feasibility/e89eb9ee…/20261115T204214795Z-36c68289`), `Test-Maps.ps1` **51/51** PASS, `Test-SourceProvenance.ps1` PASS. New `OilDepotDressingTests` cover identical collision/nav, round shells enclosing the drum collision, determinism and the vertex budget. `Compile-GameContent.ps1 -Map oil -Force` **PASS**: 4 packages, software-grid NVB1 (`Artifacts/m8-runtime-composition/e89eb9ee…/compile-gamecontent-20261115T204511761Z-57628ef9`). Offline three.js preview renders of the packaged mesh were reviewed. **Pending:** in-engine look under URP lighting, Unity EditMode/PlayMode, Client build/installer, native nav publication and the named Oil Depot playtest. Known limitation: at the tank cardinal faces the round shell sits up to ≈0.85 m outside the box collision, so a player hugging the tank can clip slightly into the visual shell.

## Game-v1 lock LF refreeze — 2026-11-15 (ADR-040 addendum)

`Tools/Test-Game.ps1` failed its identity pre-check on every clean LF checkout (CI, worktrees) with "Game-v1 fixtures differ from frozen baseline"; the Game tests themselves passed. Cause: `42ae931` froze `docs/game-fixtures/user-settings-last-weapon.valid.json` in `game-contract-lock.json` from CRLF bytes (`b8976f3f…`); the committed LF blob hashes to `966b8455…`. All 19 lock entries were compared with their committed LF blobs and only this one differed. It was refrozen with the ADR-040 guard (replace only when the old hash equals the LF→CRLF hash of the committed blob). No fixture, source or contract version changed.

**Checks (clean worktree):** `verify_game_contract_lock.py` PASS (12 sources / 7 fixtures); `Tools/Test-Game.ps1` **88/88** PASS including the lock pre-check (`Artifacts/m8-runtime-composition/129a0ad8…/20261115T220654143Z-f1a8472f`, dirty tree on `129a0ad` with only this change); `docs/fixtures/verify_fixtures.py` and `docs/verify_docs.py` PASS after the manifest refresh. Retained binaries and evidence keep their identities.

## ADR-075 third-party libraries: T091 allowlist and T092 noise — 2026-11-16

- **T091.** The allowlist now has categories for vendored third-party code and Win64 native binaries. `Tools/test_source_provenance.ps1` passed 36/36 (8 new cases), and the real `Test-SourceProvenance.ps1` passed.
- **T092.** FastNoiseLite v1.1.1 is vendored as a scripted double-precision port (`Tools/port_fastnoiselite_double.py`), behind `Generation/Core/Noise/NoiseField.cs`.
  - The first EditMode run caught float drift between Mono and .NET on three bases (`Artifacts/m0-foundation/6d2e5728…/tests-editmode-20261116T082234001Z-77cf10c9`). After the double port, all goldens matched in both lanes.
  - `Test-Generation` passed 458/458 (`Artifacts/m1-procedural-feasibility/6d2e5728…/20261116T083250771Z-02afa35a`). EditMode noise passed 4/4 (`…/tests-editmode-20261116T083249872Z-16442189`). Feature registry and C0/format locks pass, and no content identity changed.
- **5 EditMode `NullReferenceException` failures fixed (root cause: ADR-072).** `334812b` replaced the slot-based weapon object names (`Local FP rifle` / `pistol` / `knife` / `grenade`, `Held …`) with `"Local FP " + DisplayLabel` (`Rifle`, `Sidearm`, …). `FirstPersonHandlingTests` (3) and `ProductViewBindingTests` (2) look weapons up with `Transform.Find` by the old names, got null and threw. No production code looks these names up. Fix: `ProductActorPresentation.WeaponDisplayName` now uses the stable `WeaponVisualProfile.MeshAssetKey` instead of the `DisplayLabel` UI text. This restores the exact pre-ADR-072 names for the four default weapons and gives the carbine its own `rifle-carbine`. Test assertions are unchanged. Evidence: EditMode **64/64** (`Artifacts/m0-foundation/6d2e5728…/tests-editmode-20261116T083637089Z-939f2475`), `Test-Simulation` 322/322.
- **Pending.** The proof material generator, T093, T094 and every runtime, build and hardware gate.

## T093 Animation Rigging spike — 2026-11-16 (branch `spike/t093-animation-rigging`)

- **Scope.** The owner made the game local-only for now; multiplayer and dedicated-server work is postponed, so server gates are deferred.
- **Spike.** Animation Rigging 1.4.1 was resolved by the locked editor. A code-built support-hand TwoBoneIK on remote soldiers solves from the live `SoldierMotion` pose: hand error 0.00000 m, 0 m drift on all other bones across a 1 m move and turn. `RigTransform` markers break it (0.716 m), and the tests guard against them.
- **Evidence.** EditMode 67/67 (`Artifacts/m0-foundation/d8cbeb43…/tests-editmode-20261116T085736972Z-1f3b41ad`). IL2CPP Client build PASS (`…/build-client-20261116T085921756Z-fbcd6c2e`) with the package's DocCodeExamples kept out of the player by `ShooterBuildAssemblyFilter`. Editor cost for 10 actors is about 0.28 ms/frame (diagnostic only).
- **Decision (2026-11-16).** The owner declined the toolchain-lock refreeze, so Animation Rigging is Rejected (ADR-075 addendum). The package, the spike assembly, its tests and `ShooterBuildAssemblyFilter` were removed unmerged. Packages and toolchain lock are back to `d8cbeb4`.
- **Pending.** Production correction through pure `SoldierMotion`/`TwoBoneIk`: collision-backed cosmetic ground for remote soldiers, per-profile support grip and head look-at. Then EditMode, the Client build, reference-laptop CPU and a named visual review.


## ADR-087 — Combat HUD craft, first tranche — 2026-11-16

Owner requested continuation from the visual-roadmap commit `1676ff8`; work continues over the owner's focus and delivery commits `edbefa7` / `f3a6946`. Sole integrator changed only the HUD model, ProductSessionView, TacticalUi, relevant pure tests and shared docs. Damage arcs, vignette pulse, ammo/reload indicators, shaped minimap markers, team-plated kill-feed motion and gold headshot pop are implemented with existing numeric recipes and authority observations. No gameplay, sensing, content, settings or contract identities change. See [ADR-087](../../decisions.md#adr-087--accepted-tactical-hud-craft-first-tranche) and the [remaining roadmap](../../visual-roadmap.md).

Simulation 356 / Generation 508 / Game 99 / Contracts 79 / EditMode 82, source provenance (932 entries / 466 GUIDs), fixtures/docs, IL2CPP Client build and smoke PASS. Short Low TDM diagnostic: p95 5.1296 ms, 1,532 frames / 300 ticks; PendingHardware remains. Exact paths, source hash, retained initial restore/editor/build failures and dirty-source limitations are recorded once in [ADR-087](../../decisions.md#adr-087--accepted-tactical-hud-craft-first-tranche). The player reached readiness, but computer-use approval timed out before screenshot inspection; no appearance review is claimed.

This is a partial Wave 1b source delivery, not a clean installer candidate. Compass objective bearing, loading/insertion, menu/keycaps and Wave 1 evidence debt remain. Live event/reduced-motion/flash/720p 150%/named review, reference/P05/VD/REL acceptance and a future clean installer stay Pending. Installer 0.3.17 remains the current shipped preview.


## Full visual roadmap integration — ADR-087 through ADR-094 — 2026-11-17

Owner assigned all remaining work in [visual-roadmap](../../visual-roadmap.md). Sole integrator extended the HUD/insertion/keycaps, added `material.noise` 1.0.0, architectural weathering, visual-only layout decals, numeric map sky/LUTs, actor AO/contact shadows, `fx.flipbook` 1.0.0 and bounded particle combat presentation, plus `soldier.loft` 1.0.3 kit/camo/insignia and collision-backed cosmetic ground / weapon grip inputs. See the roadmap implementation ledger and ADR-088–094. Verified visual binding now yields between individual upload phases with cancellation/epoch checks; individual >33 ms uploads are diagnosed. Gameplay collision, navigation, spawns, weapon mechanics and C0/Game-v1 identities are preserved. Built-in ParticleSystem 1.0.0 is enabled at the existing pinned editor; no external art or new package/toolchain version.

P05 is **FAIL**. ADR-094 bounded the reopened search to three candidates / 90 minutes. Baseline clearance -0.001944 m; candidates 1/2/3 -0.055602 / -0.057712 / -0.011462 m. Eye residual falls below 0.000001 m, but D01/D03 penetration remains. The product retains the baseline pose solver; failed correction is opt-in diagnostics only with `-shooterP05Candidate`. All candidate source snapshots/residuals remain in `Artifacts/visual-design/adr-094/`; no fourth correction search or waiver is authorized.

Verification before clean source commit (parent `08eb4de`, `sourceDirty=true`): Generation **516/516** (`Artifacts/m1-procedural-feasibility/08eb4de61077097017e6968a1ff45d7777d4b8f4/20261116T230501006Z-9f42cf19`); Simulation **358/358** (`Artifacts/m2-combat-harness/08eb4de61077097017e6968a1ff45d7777d4b8f4/20261116T230741792Z-f897148c`); Game **99/99** (`Artifacts/m8-runtime-composition/08eb4de61077097017e6968a1ff45d7777d4b8f4/20261116T230803320Z-477c82b2`); EditMode **87/87** (`Artifacts/m0-foundation/08eb4de61077097017e6968a1ff45d7777d4b8f4/tests-editmode-20261116T230522038Z-32241b6f`); PlayMode **21/21** (`tests-playmode-20261116T230816587Z-ce6eebd3`, same root). Source provenance **962 entries / 481 GUIDs**, asmdef graph, frozen locks, source map generator **7**, weapon generator **22**, fixtures **35 assertions**, feature schemas **26 cases** and content schemas **9 cases** PASS. Offline SDK restore disables network advisory lookup (PendingNetwork).

Cross-runtime byte goldens cover all eight noise roles at both sizes, decal atlas, all registered sky/grade presets, all five FX at both sizes and the new soldier/insignia bundle. Soldier 1.0.3 initially exposed float differences in normals/UV/tangents; explicit radius/output canonicalisation and double frame arithmetic resolve them. Final paired aggregate SHA256 `a87c5bfba331b2a0213fbe820b4787457561f9700772a8cb468d4079694cb7a8`; raw focused experiments and matching payloads retained in `Artifacts/visual-roadmap-work/`. Legacy 1.0.2 path/goldens remain intact. Initial generation failures (incomplete mips / High resolution validation) are retained there; earlier Unity failure summaries are in `Artifacts/reports/tests-editmode/` after the existing ADR-076 wrapper retention policy pruned superseded run payloads. No raw screenshot evidence is claimed from those runs.

Clean Client/native-content/package/installer identities and release-player diagnostics are recorded in the following delivery entry when available. Installed upgrade, 100%/150% UI and reduced-motion/flash review, named visual review, reference 720p/thermal/GPU/eight-soldier costs, manual gameplay and REL/VD acceptance remain Pending. Missing evidence is never PASS.


Final pre-build wrappers: Maps **72/72** (`Artifacts/m8-runtime-composition/08eb4de61077097017e6968a1ff45d7777d4b8f4/test-maps-20261116T230853024Z-3fffac84`, 15 Game + 57 generation cases); Contracts **79/79** (`Artifacts/m0-foundation/08eb4de61077097017e6968a1ff45d7777d4b8f4/20261116T231056623Z-e12750ea`). Documentation verification PASS: 129 Markdown pages / 1,413 local links / 192 integrity files. These are dirty-parent integration results; the following clean build is identified separately.

## ADR-093 addendum — surface impacts — 2026-11-17

Bullet impacts now distinguish concrete (grey dust and chips), metal (sparks) and dirt (brown kick-up and clods) through pure `ImpactSurfaceFx`, resolved read-only from the verified packaged collision boxes and the map's registry ground surface. Simulation **364/364** (`Artifacts/m2-combat-harness/ff105841f92da3e717339607cbca30509a6e43ab/20261117T071440472Z-257ce52b`), EditMode **87/87** (`Artifacts/m0-foundation/ff105841f92da3e717339607cbca30509a6e43ab/tests-editmode-20261117T071546961Z-1bcef835`), source provenance PASS. No Client build, player capture or owner review was run for this change; those remain Pending.


## ADR-095 structural shooting posture — 2026-11-17

The owner reopened P05 for one structural revision after reviewing the ADR-094 root cause (head-only correction toward a chest-anchored weapon). Sole integrator changed `SoldierMotion` (opt-in `ShootingPosture`, now the product default), `ProductActorPresentation` (retired `-shooterP05Candidate`), the product defect catalog/inventory identities, `SoldierClearanceTests` and the feature-content lock.

| Gate (unchanged envelope and proxy, rifle) | v5 baseline | v6 shooting posture |
|---|---|---|
| Minimum arm/body clearance | -1.944 mm | **+3.000 mm** |
| Max eye/sight-line residual | 127.0 mm | **0.000 mm** |
| Max grip error | 7.5e-8 m | 6.8e-8 m |
| Max stock error | 1.2e-7 m | 1.2e-7 m |

Search: first full-envelope measurement 07:28:42Z, stopped at 07:47Z on gate pass (bound 180 minutes). All 4,190 measured parameter sets, the harness, a source snapshot and the disposition are under `Artifacts/visual-design/adr-095/`. A dense diagnostic (33,330 poses: crouch 0/0.5/1, yaw step 22.5 degrees, pitch step 5 degrees, phase step 0.005) found 0 negative samples. During the search a harness mode briefly parsed only 7 of 11 parameters; those runs are logged with their 7-parameter strings and the final values were re-measured with the fixed harness and the pure test.

Verification (parent `c87a20f`, sourceDirty): Generation 517/517 (`Artifacts/m1-procedural-feasibility/c87a20f19d1548fd0b4550338806243712961ba9/20261117T074703654Z-62d1ec75`, sourceDirty), Simulation 364/364 (`Artifacts/m2-combat-harness/c87a20f19d1548fd0b4550338806243712961ba9/20261117T074907727Z-80572b62`), source provenance PASS (964 entries / 482 GUIDs), feature registry PASS, Unity EditMode **87/87** with the locked editor (`Artifacts/m0-foundation/c87a20f19d1548fd0b4550338806243712961ba9/tests-editmode-20261117T074936291Z-5cb53d7b`), fixtures/docs/contract lock PASS. The second Generation run on the final lock is `20261117T075059331Z-40b6a935` (517/517).

Remaining: P05 visual acceptance stays **FAIL** pending a named visual review (front/side/back/three-quarter, live near/far, both teams); the historical FAIL record is retained. Pistol sockets leave a -4.3 mm left-arm residual at reload phase 0.91 (baseline v5 with pistol sockets: -60.3 mm). Slide and death poses are outside the gate. Eight-soldier CPU on the reference laptop, client build and installer are not rerun for this change.

## ADR-096 audio overhaul (bank v5, music, UI sound) — 2026-11-17

Code landed. Presentation only, plus an additive `musicVolume` settings key:
- bank v5 cue slots and routing: weapon-family reload fitted to the reload window, knife hits, grenade pin/bounce/far blast, flybys, hit/kill confirm, dry fire, weapon switch, distant-battle bed
- `MusicDirector` + `UnityMusicOutput`: menu theme, calm/tension bed, outcome stinger, results loop, ducking
- menu hover/click/back sounds on a session-independent mixer
- Music slider

Engine modules `unitywebrequest`/`unitywebrequestaudio` were added (lock-exempt). Game-v1 lock refrozen for `GameData.cs` and the settings schema fixture.

Evidence:
- `Test-Game` 100/100 PASS
- `Test-Simulation` 389/389 PASS (15 new ADR-096 cases)
- `Test-SourceProvenance` PASS
- Unity EditMode PASS
- docs/fixtures PASS

Recordings and music landed (same day). `python Tools/build_audio_bank.py build` rendered:
- `combat-audio-bank-v5-stereo`: 116 clips. Kept from v4: rifle/pistol shots, near grenade blasts, ambience and two knife swings. Everything else is new: OpenGameArt CC0 reload recordings, Free Firearm Sound Library mid-distance bursts (distant battle, far blast layer, flyby transients) and Kenney Impact/Interface/RPG Audio (CC0).
- `music-bank-v1`: 7 tracks.
  - menu: War Theme ver 2
  - calm bed: Tensions
  - tension bed: peastman Battle, looping 4.6–88.45 s
  - victory: Tread of War ending
  - defeat: War Theme ending, low-passed
  - draw: Battle final chord
  - results: The Precipice of Victory

`build_audio_bank.py check` passes, and `Shipped_repo_banks_load_every_cue_from_recordings_and_all_music_tracks` proves the C# loaders accept both banks. The audition page is `Artifacts/audio-review/index.html`.

Not done / Pending:
- Picks were made from waveform analysis without listening, so the owner listening review decides the takes.
- Provisional: flyby (built from gunshot transients plus a whoosh, no real whiz recording), defeat stinger (no dedicated somber track yet) and ambience (still the v4 render; no outdoor recording downloaded).
- Client build, package and named listening review (FIX10/SET02) are Pending.

## Delivery 0.3.18

Owner requested a new version/installer containing the committed changes, followed by project disk cleanup (2026-11-17). Clean binary/content source **`cbf12e5f8cdc3263c0d38ddc595c9c7d0fc7136b`** includes ADR-087–095 visual integration and ADR-096 audio/music/UI sound. Windows installer **0.3.18**, **62,149,268 bytes**, SHA256 **`941670d575ae0aa7178fd0825fc7db24e0b3705a0ca10cd59d971f866c42489f`**. Delivery copy: `Artifacts/deliveries/Shooter-Setup-0.3.18.exe`, with hash sidecar and `installer-report-0.3.18.json`. The delivery copy was independently rehashed before and after cleanup. Stable AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; monotonic check against the preserved 0.3.17 report PASS. Unity 6000.3.28f1 / 8f082d2fe476, IL2CPP, Inno Setup 6.7.3; C0-v1 and Game-v1 unchanged.

All paths below are beneath `Artifacts/`; commit roots use the full source hash above.

| Check | Actual result / retained run |
|---|---|
| Contracts | **79/79 PASS**, `m0-foundation/cbf12e5…/20261117T093944374Z-73e49754/`. |
| Generation | **517/517 PASS**, `m1-procedural-feasibility/cbf12e5…/20261117T094009415Z-7e5ca203/`. |
| Game / Simulation | **100/100** and **390/390 PASS**, `m8-runtime-composition/cbf12e5…/20261117T094235264Z-df9af860/` and `m2-combat-harness/cbf12e5…/20261117T094330925Z-630f7e48/`. Offline advisory lookup remains PendingNetwork. |
| Unity | EditMode **87/87**, `m0-foundation/cbf12e5…/tests-editmode-20261117T094010157Z-9523bd33/`; PlayMode **21/21**, `tests-playmode-20261117T094257166Z-8ab38a37/` in the same root. |
| Clean Client | PASS, `sourceDirty=false`, `m0-foundation/cbf12e5…/build-client-20261117T094423319Z-aa2d49a6/`. |
| Native packages | **12 PASS**, `sourceDirty=false`, `nativeUnityNavBake=PASS`; non-navigation byte-identity guard PASS; focused package tests **54/54**. `m8-runtime-composition/cbf12e5…/compile-gamecontent-20261117T094529495Z-a36a864a/`. |
| Release smoke | PASS, `m0-foundation/cbf12e5…/smoke-client-20261117T094804779Z-696c87db/`; boot/shutdown only. |
| Package | PASS, native navigation required, **966 inventory files**, extraction/corrupt-content/previous-pair rehearsal PASS; `m11-playable-release/cbf12e5…/package-20261117T094837573Z-090ef37b/`. Previous-pair identity/artifact rehearsal is not an installed rollback test. |
| Installer | PASS, `workspaceDirty=false`; `m11-playable-release/cbf12e5…/installer-20261117T094908942Z-c244dc72/`. |
| Provenance / specification | Source provenance **972 entries / 486 GUIDs**, fixture **35 assertions**, documentation verification PASS. These are separate from game acceptance. |

Source hash `a6be203ef00fe1351458850be7e82d74a7eb37a7d00c790b67e7b393b5ddb556`; toolchain hash `a8331fe2bc5b4293773762d829dc293c8aa5185024bd1a771543ac4eda840500`; build-content manifest `563d0cdae816c9d9a21cabd464d0f373031c6e3fcc1120b96fb88b5a9e620933`; native content closure `d296ce45194461345719de76055b09c5c9de065fca58c54ca9a17428cc40d4b6`; index `ce7105ca6a61a7a135e0fbebf5c2452a64e7a0154fe0e906d61d358bf669bb1c`; internal ZIP `f79d33e659e68bb606494218ca7bd2ee8c31b3a5b494f35dad17624c75db1c2e`.

Initial sandbox attempts failed: Contracts restore exited 1 without a diagnostic error; Unity exited -536870911. The single-node restore diagnostic succeeded; the standard wrappers then passed outside the sandbox. Initial Contracts evidence remains at `m0-foundation/cbf12e5…/20261117T093810346Z-c06e401a/`; failed Unity summary is retained by the existing ADR-076 policy under `reports/tests-editmode/cbf12e5…/tests-editmode-20261117T093831441Z-cfaf97ad/`. `restore-diagnostic.log` retains the single-node experiment. No test assertion or generated golden was changed.

**Post-installer cleanup:** dry run reviewed, then `Tools/Clean-Artifacts.ps1 -Apply -IncludeBuildCaches` freed **106,807,395 bytes**, zero failures, preserving cited/recent evidence; report `workspace-cleanup/20261117T095112342Z/cleanup-report.json`. After confirming no Unity editor was running and verifying the exact absolute target was ignored, untracked and within this repository, removed regenerable `UnityProject/Library` (**5,493,198,243 bytes**). Report `deliveries/cleanup-0.3.18-library.json`. Total explicit cleanup **5,600,005,638 bytes (5.22 GiB)**, additional to automatic wrapper retention. Source, `.local` pinned tools, content caches, current evidence and delivery installers are preserved. Next Unity launch will regenerate Library; no post-cleanup Unity import is claimed.

**Pending / NotClaimed:** actual 0.3.18 installation/upgrade/settings preservation, formal T071 gameplay, named audio/visual/device reviews, provisional ADR-096 cue replacements, reference benchmarks/soaks and REL/VD acceptance. P05 named visual acceptance remains FAIL pending review. Networking and Dedicated Server build are Parked (offline scope). Building the installer does not close these gates.

## Playtest fixes after 0.3.18 — 2026-11-17

Owner playtest of 0.3.18 reported six problems. Root causes and fixes (uncommitted working tree on `2686510`):

| Report | Root cause | Fix |
|---|---|---|
| Reload animation sometimes missing | A frame runs up to 4 simulation ticks, but `OfflineGameSession.TryLeaseCombatEvents` returned only the last tick's events. `ReloadStarted` (and any other event, including reload Foley and kill feed) from earlier ticks never reached presentation. Also: a weapon-switch cancel emits no `ReloadCompleted`, and scoped ADS hides the rifle mesh during a reload. | The session accumulates every tick's events and drains them once per lease. `ProductAuthorityActionView` clears a reload on a slot change and, after the reload window, when authority is no longer Reloading. The local viewmodel lowers ADS while reloading (presentation only). |
| Dark rectangle while scoped | The `TacticalUi.AdsScope` mask was a square, sized from the screen height and opaque out to its edges, so on 16:9 it dimmed x≈12–88 % with vertical seams. | The mask is now a housing ring that fades to transparent before the texture edge. |
| Headshot should be worth extra | ADR-059/073 fixed kill Score at 100 and gave the headshot badge 0 points. | [ADR-097](../../decisions.md#adr-097--accepted-headshot-kill-score-bonus): +20 personal Score for an enemy kill with a fatal head hit (bots too). The feed shows `+20 HEADSHOT` and the ribbon/ammo popup show 120. Team kills are unchanged. |
| Settings shows last scoreboard after a match | `Stop()` keeps phase Finished, and `ShellNavigation.SyncFromAuthority` forced any Settings screen to Results every frame. | Only in-match Settings (opened from Pause) moves to Results. |
| Default Unity icon | No player icon was ever set. | Analytic `Generation/Icons/AppIcon` reticle, baked by the new `ShooterBuild.ConfigureAppIcon` (also called from `InitializeFoundation`) into `Assets/Shooter/Settings/AppIcon.asset` and set as the default and Standalone icon. |
| No music | Every track load logged "Audio compression is not valid as streamAudio is set to true", because `compressed` was set before streaming was turned off. The load request was then disposed right after `GetContent`, and the code-built scene has no `AudioListener`. | Streaming is turned off first. The request lives as long as its clip. A cue starts only when its audio data is loaded, and each start logs `SHOOTER_MUSIC_PLAY`. An `AudioListener` is added when none exists. |

Verification (all on dirty tree `2686510`; evidence under `Artifacts/`):

- Game **102/102**, Simulation **395/395** and Generation **520/520** PASS (offline advisory PendingNetwork).
- Unity EditMode **87/87** PASS.
- Source provenance **976 entries / 488 GUIDs** PASS.
- asmdef graph PASS.

- Client build PASS (dirty, `build-client-20261117T121740830Z-5ed2a0ee`). The built `Shooter.exe` carries the reticle icon, checked by extracting it.
- The new player was run for 30 s over a scratch copy of the installed 0.3.18 content. The log shows `SHOOTER_AUDIO_LISTENER host=Music output`, which confirms the game had no listener before. It also shows `SHOOTER_MUSIC_PLAY cue=MenuTheme loadType=DecompressOnLoad length=283.8 state=Loaded playing=1 gain=0.85`, with no compression error.
- Package-Release correctly rejects the dirty candidate, so it is not run until commit.

New tests cover: multi-tick event lease, switch-cancel/stale reload, finished-match menu Settings, headshot 120 and AppIcon determinism.

Pending / NotClaimed: owner in-game confirmation of all six fixes (music audible on menu/match/results, reload visible in every case, scope look, icon on the installed exe/shortcuts) and a new installer.

## ADR-100 bot strafe, jump and slide motion — 2026-11-17

Owner playtest: strafing bots' legs floated instead of stepping, jumps had no pose, and bot jumps and slides were rarely or never seen. [ADR-100](../../decisions.md#adr-100--accepted-third-person-directional-gait-airborne-pose-and-bot-hopsslides) adds `soldier.motion.v7.locomotion` (travel-heading strides, reversal phase mirroring, airborne tuck, landing dip, travel-aligned slide), the presentation filter `ThirdPersonLocomotionBlend`, `LocalMovementView` velocity, and bot traversal hops, combat hops and more frequent slides. The owner approved the `feature-content-lock.json` refreeze.

Verification (dirty tree on `31ba0c7`; evidence under `Artifacts/`):

- Generation **526/526** (`m1-procedural-feasibility/.../20261117T134608427Z-45763d51`), Simulation **443/443** (`m2-combat-harness/.../20261117T134801508Z-d3ec939d`) and Game **103/103** (`m8-runtime-composition/.../20261117T134819514Z-eee649e4`) PASS (offline; advisory PendingNetwork).
- Unity EditMode **88/88** PASS (`m0-foundation/.../tests-editmode-20261117T134909020Z-2d073277`).
- Source provenance (988 entries / 494 GUIDs), feature registry, docs, fixtures, contract lock and asmdef graph PASS.

Pending / NotClaimed: client build and the owner's in-game look at bot strafes, hops and slides; P05 visual acceptance stays FAIL.

## Delivery 0.3.19

Owner requested a new version/installer containing the changes committed since 0.3.18 (2026-11-17). Clean binary/content source **`3c29b39d0b4a068064a4f44a4441bf3dc513fea1`** includes the six 0.3.18 playtest fixes with ADR-097, ADR-098 recoil presentation, ADR-099 hand/weapon choreography and ADR-100 bot motion. Windows installer **0.3.19**, **62,128,142 bytes**, SHA256 **`f2d6f272718297df3afc5e3d9ed9f9b021fe6bc2121e414ce10e640e668e83cb`**. Delivery copy: `Artifacts/deliveries/Shooter-Setup-0.3.19.exe`, with hash sidecar and `installer-report-0.3.19.json`. Stable AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; monotonic check against the 0.3.18 report PASS. Unity 6000.3.28f1 / 8f082d2fe476, IL2CPP, Inno Setup 6.7.3; C0-v1 and Game-v1 unchanged.

**Test fix before delivery:** the first PlayMode run on `21bee40` failed 20/21. `MenuStagingLifetimeTests.DestroyingPresentationReleasesCameraOwnedHandsAndWeaponRoot` expected 5 first-person meshes but found 6. ADR-099 intentionally adds a hidden "FP fresh magazine" child that reuses the packaged magazine mesh, and ADR-099 was committed with EditMode only. Commit `3c29b39` updates the expectation to 6 with an ADR-099 comment; no product code changed.

All paths below are beneath `Artifacts/`; commit roots use the full source hash above.

| Check | Actual result / retained run |
|---|---|
| Contracts | **79/79 PASS**, `m0-foundation/3c29b39…/20261117T140656966Z-d4e8bb1c/`. |
| Generation | **526/526 PASS**, `m1-procedural-feasibility/3c29b39…/20261117T140710008Z-b4359f2b/`. |
| Game / Simulation | **103/103** and **443/443 PASS**, `m8-runtime-composition/3c29b39…/20261117T140857569Z-1c74a6ab/` and `m2-combat-harness/3c29b39…/20261117T140935212Z-03720df0/`. |
| Unity | EditMode **88/88**, `m0-foundation/3c29b39…/tests-editmode-20261117T140955133Z-91082daa/`; PlayMode **21/21**, `tests-playmode-20261117T141152111Z-24dbb2d1/` in the same root. |
| Clean Client | PASS, `sourceDirty=false`, `m0-foundation/3c29b39…/build-client-20261117T141250765Z-9f80229e/`. |
| Native packages | **12 PASS**, `nativeUnityNavBake=PASS`; focused package tests **54/54**. `m8-runtime-composition/3c29b39…/compile-gamecontent-20261117T141503862Z-7011e2f1/`. |
| Release smoke | PASS, `m0-foundation/3c29b39…/smoke-client-20261117T141722691Z-46113000/`; boot/shutdown only. |
| Package | PASS, native navigation required, **966 inventory files**, extraction and corrupt-content rehearsal PASS; previous-pair rehearsal SkippedNoPrevious (no previous package directory supplied). `m11-playable-release/3c29b39…/package-20261117T141749852Z-eac99c43/`. |
| Installer | PASS, `workspaceDirty=false`; `m11-playable-release/3c29b39…/installer-20261117T141814501Z-20ff3215/`. |

Source hash `54ae9c6dc905751bbff547d9201f9a1c7292fb9b5c00e31897660ef1536ca9a4`; toolchain hash `a8331fe2bc5b4293773762d829dc293c8aa5185024bd1a771543ac4eda840500`; build-content manifest `c97c470544a923aed96f84e30c2a475d63765808c237fd84cd7c8cf498961247`; native content closure `d296ce45194461345719de76055b09c5c9de065fca58c54ca9a17428cc40d4b6`; index `ce7105ca6a61a7a135e0fbebf5c2452a64e7a0154fe0e906d61d358bf669bb1c`; internal ZIP `8cfc69fd5cf05d8df17c8f137dbf14be914a5419267334edf6510699e7c76696`.

The first packaging attempt failed `RequireNativeNav` because it read the default pure cache; rerun with `-MapCacheRoot Artifacts/map-packages-native` PASS. Wrapper retention pruned the 0.3.18 `cbf12e5` PlayMode, package and installer run folders; their JSON reports remain under `Artifacts/reports/`, and the 0.3.18 delivery copy and report in `Artifacts/deliveries/` are preserved.

**Pending / NotClaimed:** actual 0.3.19 installation/upgrade/settings preservation, in-game feel check of ADR-098–100, formal T071 gameplay, named audio/visual/device reviews, provisional ADR-096 cue replacements, reference benchmarks/soaks and REL/VD acceptance. P05 named visual acceptance remains FAIL pending review. Networking and Dedicated Server build are Parked (offline scope). Building the installer does not close these gates.

## Delivery 0.3.20

Owner requested a new release/installer (2026-11-17). Clean binary/content source **`ab83755347325057ba804b7021d739832a7a1e54`** adds to 0.3.19 the ADR-102 named arsenal (seven named guns, `geometry.weapon` 1.2.0, burst fire, range falloff, weapon move speed, random spawn loadouts, per-gun handling/flash/report, combat bank v6), the muzzle-flash flipbook/anchor fix and ADR-101 5/8/10/15 team sizes. Windows installer **0.3.20**, **66,395,468 bytes**, SHA256 **`53177431f5dae92cabbdb20507ad56cfba7b0ea0c06775332125ab4e12b9270f`**. Delivery copy: `Artifacts/deliveries/Shooter-Setup-0.3.20.exe`, with hash sidecar and `installer-report-0.3.20.json`. Stable AppId `B01E0F69-2123-4D91-AB59-88864A944E39` is unchanged; monotonic check against the 0.3.19 report PASS. Unity 6000.3.28f1 / 8f082d2fe476, IL2CPP, Inno Setup 6.7.3; C0-v1 and Game-v1 unchanged.

All paths below are beneath `Artifacts/`; commit roots use the full source hash above.

| Check | Actual result / retained run |
|---|---|
| Contracts | **79/79 PASS**, `m0-foundation/ab83755…/20261117T182857084Z-f00f67de/`. |
| Generation | **544/544 PASS**, `m1-procedural-feasibility/ab83755…/20261117T182913051Z-c92efdcd/`. |
| Game / Simulation | **106/106** and **468/468 PASS**, `m8-runtime-composition/ab83755…/20261117T183114199Z-47c91eeb/` and `m2-combat-harness/ab83755…/20261117T183155363Z-c867a5fc/`. |
| Unity | EditMode **88/88**, `m0-foundation/ab83755…/tests-editmode-20261117T183234392Z-84afc340/`; PlayMode **21/21**, `tests-playmode-20261117T183455108Z-513fb680/` in the same root. |
| Clean Client | PASS, `sourceDirty=false`, `m0-foundation/ab83755…/build-client-20261117T183554090Z-9f3302e8/`. |
| Native packages | **12 PASS**, `nativeUnityNavBake=PASS`; focused package tests **54/54**. `m8-runtime-composition/ab83755…/compile-gamecontent-20261117T183620382Z-82d92091/`. |
| Release smoke | PASS, `m0-foundation/ab83755…/smoke-client-20261117T183858328Z-85317d81/`; boot/shutdown only. |
| Package | PASS, native navigation required, **1042 inventory files**, extraction and corrupt-content rehearsal PASS; previous-pair rehearsal SkippedNoPrevious (no previous package directory supplied). `m11-playable-release/ab83755…/package-20261117T183910934Z-50ffd632/`. |
| Installer | PASS, `workspaceDirty=false`; `m11-playable-release/ab83755…/installer-20261117T183941428Z-298c39fd/`. |

Source hash `4bdfec2c74503559a8cef3b40330ad07d915028e4aba18ba339ea1f7cde9d33a`; toolchain hash `a8331fe2bc5b4293773762d829dc293c8aa5185024bd1a771543ac4eda840500`; build-content manifest `7862f5efb3bd7b5836c48b5cf1efce223fa6ba774b94683280b94197f4abd3c6`; native content closure `a82fc0b1533e1c9823e7e5011ef5978c9be1e81bfd4ba3ca2840d3a41f7e240f`; index `ee3c0b0af644b8d953e5abd9c3b7b27d6a4bee6f812548c6bf1729e9be3c154e`; internal ZIP `a2ba7f069c1ba40a71915e358e34a153413a839206a618a743b57730f5cf9b99`.

Wrapper retention pruned the 0.3.19 `3c29b39` package and installer run folders; their JSON reports remain under `Artifacts/reports/`, and the 0.3.19 delivery copy and report in `Artifacts/deliveries/` are preserved.

**Post-installer cleanup:** dry run reviewed, then `Tools/Clean-Artifacts.ps1 -Apply -IncludeBuildCaches` freed about 0.07 GB, zero failures, preserving cited/recent evidence; report `workspace-cleanup/20261117T184202476Z/cleanup-report.json`. After confirming no Unity editor was running and that the exact target was ignored, untracked and within this repository, removed regenerable `UnityProject/Library` (**5,034,982,144 bytes**); report `deliveries/cleanup-0.3.20-library.json`. Source, `.local` pinned tools, content caches, current evidence and delivery installers are preserved. Next Unity launch will regenerate Library; no post-cleanup Unity import is claimed.

**Pending / NotClaimed:** actual 0.3.20 installation/upgrade/settings preservation, in-game check of the ADR-102 named guns and ADR-098–101, formal T071 gameplay, named audio/visual/device reviews, provisional ADR-096 cue replacements, reference benchmarks/soaks and REL/VD acceptance. P05 named visual acceptance remains FAIL pending review. Networking and Dedicated Server build are Parked (offline scope). Building the installer does not close these gates.

## ADR-103 graphics uplift and damage feedback — 2026-11-17

Owner report: map flicker, poor distant/scoped image at 1080p High, weak damage feedback. Implemented on branch `graphics-quality-uplift` (see ADR-103): optical scope lens camera, MSAA fix and tier AA, Balanced/High shadow/aniso/fog/decal uplift, coplanar wall-trim/cover/apron fixes, damage flinch, damage-scaled flash, URP low-health grade and synthesised heartbeat/thud/muffle. Settings regenerated by `ShooterBuild.InitializeFoundation`; map packages recompiled with `Compile-GameContent.ps1 -Native` (PASS). Visual acceptance in a packaged build and a High benchmark are Pending owner review; Low budget expected unchanged except FXAA, not yet re-benchmarked.

## ADR-104 circular minimap and ring compass — 2026-11-17

Owner report: minimap missed the player and teammates and looked weak; wanted a round, view-rotating minimap with the compass around it. Root cause: rotated map glyphs inside `GUI.BeginGroup` (group offset applied before `GUI.matrix`). Replaced with a group-free heading-up disc masked by a new UiSdf circle clip and a rotating compass ring (see ADR-104). Lanes on the working tree (base fa0b34d): `Test-Simulation.ps1` PASS 500/500 (incl. 5 new minimap/compass tests), `Test-SourceProvenance.ps1` PASS, `verify_docs.py` PASS, `Test-Unity.ps1 -Mode EditMode` PASS 91/91 (incl. the new circle-clip shader test). Client build and interactive review (rotation, markers, clipping, 150% UI scale) Pending owner playtest.

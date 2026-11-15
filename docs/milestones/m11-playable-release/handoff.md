# M11 — Playable build and acceptance — handoff and gate record

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

## Release 0.3.12

2026-11-15. The owner asked to commit the pending main-checkout work and create a new release. The pending files were the T084 continuous sleeve loft and its welded-topology test, plus T084 doc wording. Generation 394 and docs verification PASS on the working tree (`Artifacts/m1-procedural-feasibility/7cbabd38bdd25c6e572c14f85d761da48448b1bd/20261115T145451354Z-dfce7da1/`); committed as `50e8939`.

Clean binary/content source **`50e893976efa51d57ecf965e34f61150dbe22c9a`**, source hash `220d70c263b4e28059435b7043e9b9554b6cebfd70f50b6aba54818052e9d208`. It contains [T084](#t084--near-field-finish-and-hip-composition) and [T085](#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill). Locked Unity 6000.3.28f1 / URP 17.3.0 / D3D11, Client IL2CPP / Server Mono.

| Check | Actual result / retained evidence |
|---|---|
| Unity | EditMode 53/53 and PlayMode 21/21 PASS, clean: `Artifacts/m0-foundation/50e893976efa51d57ecf965e34f61150dbe22c9a/tests-editmode-20261115T145603562Z-62734d45/`, `tests-playmode-20261115T145705401Z-500642bd/` |
| Native content | The first attempt `Artifacts/m8-runtime-composition/50e893976efa51d57ecf965e34f61150dbe22c9a/compile-gamecontent-20261115T145745436Z-42e7ae29/` FAILED in the MapContent publish tool: `InternalError: Unable to remove the file to be replaced` (an atomic `File.Replace`). No game or editor process was running afterwards; the cause is unconfirmed. This is the same symptom class as the transient T084 native failure. The immediate retry `compile-gamecontent-20261115T145844309Z-f6c0b77b/` PASSED: 8 native packages, `nativeUnityNavBake` PASS, and the non-navigation byte-identity guard PASS. Both runs are retained |
| Client / Server | Clean PASS: `Artifacts/m0-foundation/50e893976efa51d57ecf965e34f61150dbe22c9a/build-client-20261115T145959017Z-a52eab72/`, `build-server-20261115T150133132Z-8565b063/`; headless server smoke PASS `smoke-server-20261115T150253504Z-487e795b/` |
| Native package | PASS, 378 inventoried files, extract and corrupt-content rehearsals PASS (rollback rehearsal SkippedNoPrevious); `Artifacts/m11-playable-release/50e893976efa51d57ecf965e34f61150dbe22c9a/package-20261115T150302379Z-ca4f9856/`. Content closure `9c9c16dd6393b262d6d88ce7db8b33780dd25cadd350d399acdedc521e22ea50`, index `d883f2500b0cf4eb6f9967ac3d307229e1c26066c13fa878d834bdfa833a0cd8`, ZIP SHA256 `7e57c727eddf2ce12aba25cdb1d0a8ccdf17e4de9c9a0b972f7c23457a542066` |
| Installer | PASS **0.3.12**, 27,071,621 bytes, SHA256 `a8839dec54c90cc816c0b88dd5c8f63021ad4a7fc7c4067606c0e5b3baf32855`; stable AppId and per-user scope, predecessor report 0.3.11. Actual installation/upgrade **NotRun** |

Installer: `C:/Job/shooter/Artifacts/m11-playable-release/50e893976efa51d57ecf965e34f61150dbe22c9a/installer-20261115T150318690Z-28a5defc/installer/Shooter-Setup-0.3.12.exe`. The prior 0.3.11 binary/content pair remains the rollback.

**Pending** (never PASS without evidence): installing over 0.3.11, launch and player review of the final sleeve and finishes, the owner's bot feel review (spread, rear awareness, time-to-kill, hit markers), a `SHOOTER_AI_SUMMARY` capture, and reference GPU/thermal/performance. P05 FAIL and all VD/REL exits remain open.


## T085 — Bot rear awareness, team spread and readable time-to-kill

2026-11-15. The owner played the T082 bots and reported that they run after each other, do not see enemies behind their backs, and kill too fast to read. Base `12ff5ac`; code commit `ef96e45` (lanes ran here), then a comment-only renumbering commit after concurrent work took T084 and ADR-053 (`2b9c743`). Worktree branch `t084-bot-awareness`; the name predates the renumbering. Scope and rollback: [task card](../../task-supplement.md#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill-2026-11-15). Design: [AI awareness, team spread and lethality](../../features/ai.md#awareness-team-spread-and-lethality-t085-2026-11-15) and [ADR-054](../../decisions.md#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085).

**Implemented:**
- Spread-scored patrol and combat destinations, replacing the id-ordered route walk that made teammates trail each other.
- Ally gunfire points at the ally's enemy or is ignored, and at most 2 allies investigate one noise.
- Footstep hearing from any direction (24/13/5 m by speed, 2.5 m always) and a 120° vision cone.
- Turning on an unseen attacker after 2 hits (or 1 below 50 health).
- Accuracy recovery starts at the first possible shot, with a wider settled error.
- HUD chevrons pointing at whoever hit the player.

**Unchanged:** C0/Game-v1, fixtures, locks, AI ray and path budgets, weapon damage and rate, content and toolchain.

**Time-to-kill** (`BotLethalityTests`, real combat harness, still standing target, median of 9 bots, from sighting; this is a regression envelope, not player acceptance):

| Distance | T082 | T085 | Fastest after first shot, T082 → T085 |
|---|---|---|---|
| 8 m | 61 ticks (1.0 s) | 98 ticks (1.6 s) | 24 → 48 ticks |
| 20 m | 90 ticks (1.5 s) | 151 ticks (2.5 s) | 30 → 65 ticks |
| 40 m | 143 ticks (2.4 s) | 233 ticks (3.9 s) | 29 → 96 ticks |

Previous constants, for rollback by tuning only: reaction 15 + 0..15 ticks; acquisition miss 1.1 m; settled miss 0.12 m + 0.9 cm/m; recovery 40 ticks from sighting; moving penalty 0.25 m; vision cone 90°.

| Lane, source `ef96e45` (clean) | Result |
|---|---|
| Test-Simulation | PASS 239 (6 new `BotAwarenessTests`, 3 `BotLethalityTests`, 2 HUD indicator tests; one stance-aim assertion moved from an exact pitch to the settled-error bound; the M3 11-bot budget and soak are unchanged) |
| Test-Game | PASS 70 |
| Test-Maps | PASS 14 + 41 |
| Test-Contracts / Test-Generation | PASS 76 / 392 |
| Test-SourceProvenance | PASS (no new assets) |
| Fixtures, C0 lock, Game-v1 lock | PASS (35 fixture assertions) |
| Unity EditMode / PlayMode on merged `main` `80ee5a4` (clean, includes concurrent T084) | PASS 53 / 21; this compiles the Unity-only `ProductSessionView` marker drawing, and PlayMode `ProductBotNavigationTests` on the real product map Passed. Reports: `Artifacts/m0-foundation/80ee5a4f7548a7756fa5a87f894da7b867f40ebc/tests-editmode-20261115T144218037Z-52a3e3b8/` and `tests-playmode-20261115T144333177Z-c6200d2a/` |

Reports were copied to `Artifacts/m0-foundation/`, `m1-procedural-feasibility/`, `m2-combat-harness/` and `m8-runtime-composition/` under `ef96e4593494e538c7c071727656eac03aa5c033/`. The merged tree also passes Simulation 240 (pure, iteration run without a report).

**Pending** (not run; never PASS without evidence):
- Client and server builds, a smoke test and an installer.
- A `SHOOTER_AI_SUMMARY` capture on real maps, including the new counters.
- The owner's feel review: spread, rear awareness, time-to-kill, and whether the hit markers read well.

Known limits: hearing ignores walls; slow or crouched movement behind a bot beyond 5 m stays unheard; spread scoring knows allies' goals but not their routes.

## T084 — Near-field finish and hip composition

Owner requested continued visual implementation after 0.3.11, base `12ff5ac`; sole integrator. [Task](../../task-supplement.md#t084--near-field-surfaces-sleeve-shape-and-hip-framing), [ADR-053](../../decisions.md#adr-053--accepted-procedural-near-field-finish-triplets-and-hip-composition-t084).

Adds offline 128px PBR triplets for coated weapon metal, stippled polymer, cloth weave and glove grain at a 100 mm metric tile; all albedo/normal/mask dependencies are required, dimension/channel/mip validated and uploaded with shared metallic/occlusion textures. Sleeve-only geometry gains shallow folds and bend while glove/finger/grip coordinates remain unchanged. Static hip yaw/roll fades to identity at full ADS; aim/ballistics, weapon meshes/sockets, soldier/P05, physics/nav/spawn data and frozen formats/generators remain unchanged. The separately merged T085 bot update is preserved in the final combined candidate. T084 itself changes no authoritative combat rules.

Initial working-source EditMode PASS 53 under `Artifacts/m0-foundation/12ff5acefaa45637a184512d024a6e9de4fefcbf/tests-editmode-20261115T142519437Z-4fb52083/`. Initial Generation 392/393 failed a new test demanding >8 distinct encoded normal values even for shallow coated-metal relief. The test now requires spatial variation independently in both tangent axes, rather than forcing exaggerated bump amplitude. No generated payload/golden changed to pass it; raw failure retained in `Artifacts/m1-procedural-feasibility/12ff5acefaa45637a184512d024a6e9de4fefcbf/20261115T142458573Z-064b9be7/`. Final working-source pure lanes PASS: Contracts 76, Generation 393, Simulation 229, Game 70, Maps 56. Final EditMode 53/53 PASS at `Artifacts/m0-foundation/12ff5acefaa45637a184512d024a6e9de4fefcbf/tests-editmode-20261115T142920806Z-8cf1e2cc/`; these runs retain sourceDirty=true at base 12ff5ac. Later clean-source publication/build/review evidence follows below.

Intermediate clean source `2b9c7433ff241c5cb570c0413eaa54e206519299` passed PlayMode 21, native publication (8 packages / 39 selected tests), Client build and internal package verification. First native attempt `compile-gamecontent-20261115T143316674Z-bfd37acf` failed during editor startup/shutdown with thread assertions (exit -1073741510); cause unconfirmed. Retry `compile-gamecontent-20261115T143437239Z-0317e443` passed. Retain both under its M8 artifact root. Intermediate package `package-20261115T143912213Z-3ea35c9e` is review evidence, not the final installer source.

Actual intermediate player review showed the new surface response and hip framing, but exposed dark bands between stacked sleeve sections; screenshot `Artifacts/visual-design/t084/intermediate-rifle.jpg`. Replaced those sections with one continuous closed loft, smooth analytic tangent frames and caps only at the ends. A topology test welds coincident positions and requires every edge to have exactly two adjacent triangles at two sleeve lengths. Generation 394 PASS on the working source at `80ee5a4` (`20261115T144336928Z-ce56cfce`). During player review live user input was detected; automated controls stopped and that intermediate player was left open. Final-source player review remains separate from this observation.

This does not implement articulated fingers/full magazine exchange or repair third-person soldier anatomy. P05 FAIL and all comparison/VD/REL/full gameplay/installed-upgrade/reference GPU/thermal/performance gates remain open. Rollback the new material closure/consumers and sleeve changes together and restore a matching retained 0.3.11 pair.

## T083 — First-person glove and sleeve presentation

Owner requested continued visual implementation, 2026-11-15. T082 main/runtime merges are preserved; docs-only `a9f228d` landed while this working-source pass was underway. No agents or new worktrees. [Task](../../task-supplement.md#t083--first-person-gloved-hands-and-continuous-reload-reach), [ADR-052](../../decisions.md#adr-052--accepted-packaged-first-person-glove-and-sleeve-props-t083).

Four offline, required mesh dependencies add gloved trigger/support hands and sleeves for rifle/pistol, using existing uniform/polymer/gear materials and weapon grip coordinates. Camera-only props swap with slots, hide with their weapon root on death/clear, do not cast shadows and release their camera-owned root on owner destruction. Support-hand reach and equipment motion use reload phase, with a corrected continuous remove/insert boundary and reduced-motion scaling. No soldier/P05, hit pose, weapon socket, authoritative rule or contract change. Art remains numerical code; shipping only uploads verified cached geometry.

Initial pure working-source checks at `a9f228d`: Generation **392** and Simulation **228** PASS, including corruption rejection and reload continuity. Initial Unity EditMode failed its new camera-root destruction assertion because EditMode did not exercise the MonoBehaviour destruction lifecycle. The check now lives in PlayMode after a yielded activation; no runtime fallback or relaxed teardown requirement. Raw failure: `Artifacts/m0-foundation/a9f228d6aaed66dae062131de7fb43b08505b81b/tests-editmode-20261115T140323256Z-03c2b887/`. Final checks and delivery follow below.

Limits: rigid camera-prop hands, not articulated fingers or a complete magazine replacement; third-person mannequin anatomy and historical P05 FAIL remain. Visual comparative acceptance, installed upgrade, full gameplay and reference GPU/thermal/performance proofs remain Pending. Revert the whole hand closure/presentation change and restore a paired prior installer for rollback.

### T083 delivery 0.3.11

Clean binary/content source **`df64aa8aab3fc465b3d07f49f3642ca57121e14d`**, source hash `ee1e86eb30578dbd33d1c0350c09a50d54f091c8121f58f37ae0e5b0b71bda85`. Includes the previously merged T082 bot baseline. Later documentation does not relabel these binaries. Locked Unity 6000.3.28f1 / URP 17.3.0 / D3D11, Client IL2CPP / Server Mono.

| Check | Actual result / retained evidence |
|---|---|
| Pure lanes | Working source at `a9f228d`, sourceDirty=true: Contracts 76, Generation 392, Simulation 228, Game **70**, Maps 55 PASS. Evidence under matching `Artifacts/m0-foundation`, `m1-procedural-feasibility`, `m2-combat-harness`, `m8-runtime-composition` roots |
| Unity | Final EditMode 53 PASS at `a9f228d6aaed66dae062131de7fb43b08505b81b/tests-editmode-20261115T140518442Z-64d9f976/`; clean PlayMode **21** PASS at `df64aa8aab3fc465b3d07f49f3642ca57121e14d/tests-playmode-20261115T140725670Z-7af00c5a/`, both under `Artifacts/m0-foundation/` |
| Native content | PASS eight packages / 38 selected publication tests and non-nav guard, clean source; `Artifacts/m8-runtime-composition/df64aa8aab3fc465b3d07f49f3642ca57121e14d/compile-gamecontent-20261115T140839211Z-4aae7f0c/` |
| Hand / gameplay identities | Four meshes each 2,016 vertices / 1,224 triangles / three material roles, unskinned; both gameplay roots and full gameplay catalog identical to 0.3.10. `Artifacts/visual-design/t083/content-comparison.json` |
| Client | Clean PASS, sourceDirty=false; `Artifacts/m0-foundation/df64aa8aab3fc465b3d07f49f3642ca57121e14d/build-client-20261115T141004068Z-10576a08/` |
| Server | Clean PASS, sourceDirty=false, zero graphics source assets; `build-server-20261115T141509540Z-57665c1e/`; headless smoke PASS `smoke-server-20261115T141650906Z-d972351e/` under the same source root |
| Native package | PASS, 318 inventoried files, extract/corrupt/rollback software rehearsals; `Artifacts/m11-playable-release/df64aa8aab3fc465b3d07f49f3642ca57121e14d/package-20261115T141213128Z-8742220a/` |
| Installer | PASS **0.3.11**, 26,986,939 bytes, SHA256 `4d9bbe6c74df16a8aa3e5f58d78e759d52d26995ebad22b31bf8299aca343df8`; stable AppId/settings policy, predecessor 0.3.10. Actual installation/upgrade **NotRun** |
| Player | 318/318 file hashes match short review copy `Artifacts/T083Preview`. Main/cards, Depot 6v6 load, rifle support glove/cuffs/sleeves observed in a 1920×1080 window. One momentary Slot2 key did not establish a visible pistol switch. Subsequent view showed live player movement, so automation stopped and the game was left open. Live pistol/reload/ADS and full match review remain **Pending** |

Installer: `C:/Job/shooter/Artifacts/m11-playable-release/df64aa8aab3fc465b3d07f49f3642ca57121e14d/installer-20261115T141302718Z-dcdce976/installer/Shooter-Setup-0.3.11.exe`. Content closure `ec953f317c968069c1cb4fb818baa7832dbdfe9fe9ad9a48ea6efe75a7c79e29`; index `d7d1baeff8b4e37f36137e302d6bbc6b7feb5d9ebbd80652408e46096aa299a3`; internal ZIP SHA256 `a27a70a8d0e520c3281747589504b33b86b42a7e14cb4ae52b4ff31d596696b2`. Prior 0.3.10 remains a matching rollback pair.

Provenance PASS 748 entries / 374 GUIDs; C0/Game-v1/content/weapon/soldier freezes pass. Fixture/docs checks cover specifications only. The independent comparison, inventory review, raw player log and unmodified `rifle-player.jpg` are retained under `Artifacts/visual-design/t083/`. Rifle presence is visibly improved, but fingers remain rigid and sleeve silhouettes/simple surface finish still need refinement. No BFV parity, P05, VD or REL acceptance; installed upgrade, full gameplay, known deep evidence-path limitation and reference GPU/thermal/performance proofs remain open. New binary requires the new hand closure; do not combine it with old visual packages.

## T081 — Architectural surface and place identity

2026-11-15 owner-authorized continuation from `615b6a5`, sole integrator. [Task scope](../../task-supplement.md#t081--architectural-surface-and-place-identity), [ADR-050](../../decisions.md#adr-050--accepted-packaged-architectural-surface-construction-t081). Map mesh generation now adds small physical bevels, footing/coping/end bands and panel joints to long walls, with cargo corrugation/rails/door hardware and blue/oxide painted steel. Map/menu share six material roles; variants share texture planes. Menu skyline receives loading-bay facades; weapons receive dark rougher gunmetal/matte-polymer coating. Gameplay collision/nav/spawn/hit/weapon rules and soldier/P05 sources remain unchanged. No imported/AI art, authored animation or runtime fallback.

Initial working-source checks at base `615b6a5`: Generation 391, Contracts 76, Game 68, Maps 55 and provenance 736 entries / 368 GUIDs PASS. Unity initial 52 EditMode PASS before the final facade/coating/two-map material checks; final working-source EditMode **53/53 PASS** at `tests-editmode-20261115T134227748Z-a52a3d27` under `Artifacts/m0-foundation/615b6a54b0237e8bb962dfa537683a5df33fd168/`. Earlier 52/53 run `tests-editmode-20261115T134056782Z-044e30cf` failed its old polished-metal smoothness assertion (>0.5 versus the intentional new coating 0.46); the test now specifies the coated finish and dark tint. Failure retained. Later publication/build/installer/player evidence follows below. Old 0.3.9 remains the exact prior binary/content pair, not a build of this source.

Scope limitations: first-person hands, anatomy/uniform construction and full weapon/reload continuity remain unresolved. P05 FAIL / VD / REL / full gameplay / installed-upgrade / GPU/thermal/reference acceptance remain open. More detailed geometry is not reference-machine performance evidence. Revert the implementation and restore an exact retained binary/content pair for rollback.

### T081 delivery 0.3.10

Clean binary/content publication source **`e140dd6f5a8fb2677ffd71634c211a21a27fa113`**, source hash `dd24d29b7af25f761177cfcb23bd4c55e83c0bd967749e47188e6195c60c5062`. Later evidence/docs commits do not relabel the binary. Locked Unity 6000.3.28f1 / URP 17.3.0 / D3D11, Client IL2CPP / Server Mono.

| Check | Actual result / evidence |
|---|---|
| Native content | PASS all eight packages, 38 selected publication tests, non-nav identity guard; `Artifacts/m8-runtime-composition/e140dd6f5a8fb2677ffd71634c211a21a27fa113/compile-gamecontent-20261115T134402390Z-96001e54/` |
| Gameplay preservation | Both gameplay roots and complete gameplay catalog identical to 0.3.9; visual roots differ. Independently verified mesh body hashes: Compound 33,696 vertices / 24,648 triangles / four submeshes; Depot 60,720 / 37,320 / six. `Artifacts/visual-design/t081/content-comparison.json` |
| Clean PlayMode | PASS 20/20 at e140dd6; `Artifacts/m0-foundation/e140dd6f5a8fb2677ffd71634c211a21a27fa113/tests-playmode-20261115T134507784Z-7d3a796d/` |
| Clean Client | PASS, sourceDirty=false; `build-client-20261115T134616184Z-9216f25e/` under the same source root |
| Clean Server | PASS, sourceDirty=false, zero graphics source assets; `build-server-20261115T135014031Z-c0c5b674/`; headless smoke PASS `smoke-server-20261115T135210326Z-225e90b6/` under the same source root |
| Native package | PASS, 294 files; extract/corrupt/rollback software rehearsals; `Artifacts/m11-playable-release/e140dd6f5a8fb2677ffd71634c211a21a27fa113/package-20261115T134832418Z-decf629e/` |
| Installer | PASS 0.3.10, 26,581,066 bytes, SHA256 `0c64ce3c54c658922b759787a97356003c9baec027a8e2a6c5762eda606be6f1`; stable AppId and preference policy, predecessor 0.3.9. Actual install/upgrade **NotRun** |
| Player review | 294/294 inventory hashes match short copy `Artifacts/T081Preview`; Main, both distinct map cards and Compound 6v6 gameplay observed. Screenshot shows wall panel joints, footing/coping and dark coated weapon. Automation detected live user input; refreshed state showed continued player movement, so further controls stopped and the player was left open. Close-up Depot review **Pending**; no full match or comparative acceptance |

Installer: `C:/Job/shooter/Artifacts/m11-playable-release/e140dd6f5a8fb2677ffd71634c211a21a27fa113/installer-20261115T134907370Z-b586bcf1/installer/Shooter-Setup-0.3.10.exe`. Content closure `f19659447773a84131408a5933f89aaf7167a2cf06a2f60d5ece81f161a8d815`; content index `c4e05e422318c1d24139fc82bbb8ac68e09344c05323e544a70119d3163c78c4`; ZIP SHA256 `20d75c68817912c8194d650a0912af29d8956587e5413c93e24f2260dd24b56f`. Prior 0.3.9 binary/content is retained as rollback.

Raw review log, content comparison, verified-copy report and unmodified player screenshot (`compound-player.jpg`) are retained under `Artifacts/visual-design/t081/`. Capture was a 1920×1080 window; this is not native720p reference performance proof. The new wall construction and cargo palette improve place readability but the observed world remains sparse; near-field hands, believable soldiers, action continuity and richer authored-by-code place composition remain substantial deficits. No BFV parity / P05 / VD / REL acceptance is claimed. Full gameplay, installed upgrade, GPU/thermal/reference capture and known deep evidence-path loading limitation remain open.

## Archived T079 alternate branch integration — 2026-11-15

Owner explicitly requested merging the highlighted `archive/t079-env-alt-2415153` ref. It is an archive tag at `241515387f2ed1780e05c154b1b879080dbcd69a`, not a live branch, and was not an ancestor of `main` (`ffd0754`). Merge resolution integrates its distinct plaza/yard lamp posts, outer fence rows, full-length axis-aware ridge caps and package/prop tests. Existing main gate lamps, inner fence stubs, container corners, finer concrete/metal/trim classification and all T080 metric UV/menu-card fixes are retained; alternative material assignment/tint implementations are superseded by those newer consumers. Short roof ridges are replaced, avoiding duplicate ridge geometry.

Validation of the resolved working tree (sourceDirty=true at base `ffd0754`): Generation **391**, Contracts **76**, Maps **55**, Unity EditMode **52** and source provenance **736 entries / 368 GUIDs** PASS. Generation includes both maps' unchanged gameplay box/count/collision fingerprints and visual symmetry/budget checks. Evidence: `Artifacts/m1-procedural-feasibility/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/20261115T133038119Z-0249d5bb/`; Contracts `Artifacts/m0-foundation/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/20261115T133206617Z-8e9f6658/`; EditMode `tests-editmode-20261115T133057239Z-c7cf4d57/` under that same source root. Map wrapper results follow its retained report `Artifacts/m8-runtime-composition/ffd0754a2181112c163a8d18c20f0b9c6fd69e2e/test-maps-20261115T133215973Z-e4a5d92d/`.

This is source integration only. Visual mesh/package identities change; native content publication, new Client/Server builds, PlayMode/player review and a new installer are **NotRun** for this merge. The 0.3.9 installer below remains the prior `beac9ae` binary/content pair and does not contain these newly merged props. P05 and VD/REL acceptance remain unchanged. The archive tag is retained; no temporary branch/worktree was created, and the unrelated active `t080-bot-tactics` worktree is preserved.

## T082 — Solid-FPS bot combat behaviour

2026-11-15. The owner asked for an investigation of bot behaviour and then an upgrade. The work started from `ffd0754` and was rebased onto `e140dd6` after concurrent visual work took T080, T081 and ADR-050. Lanes ran on code commit `c5a16a4`. That commit was then rebased over the docs-only `d119045` to become `8e22f18`, with an identical non-docs tree. Worktree branch `t080-bot-tactics`; the branch name predates the renumbering. Scope and rollback: [task card](../../task-supplement.md#t082--solid-fps-bot-combat-behaviour-2026-11-15). Design: [AI combat behaviour](../../features/ai.md#combat-behaviour-t082-2026-11-15) and [ADR-051](../../decisions.md#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082).

**Implemented:**
- Bounded-rate seeded aim with accuracy recovery.
- Range-based burst discipline.
- Strafing.
- Sticky, threat-aware targeting.
- Hearing and damage stimuli, forwarded from session events.
- Callouts and destination spreading.
- Low-health retreat and tactical reload.
- Runtime cover probes inside the 16-ray budget.
- Urgent goals replace stale routes.
- Respawns rebind one planner slot instead of rebuilding every bot's memory.

**Unchanged:** C0/Game-v1, fixtures, locks, budgets, content and toolchain.

**Synthetic tuning check** (not evidence). A throwaway duel probe on `CombatHarnessSimulation` with an open plane and two stationary-spawned bots, 20 offsets per distance. Hit rate and time-to-kill after the first shot:

| Distance | Hit rate | Time-to-kill |
|---|---|---|
| 8 m | about 57% | about 0.5 s |
| 20 m, both strafing | about 34% | about 1.0 s |
| 40 m, crouched with ADS | about 60% | about 0.5 s |

The probe was deleted after tuning. It is a tuning aid, not acceptance.

| Lane, source `c5a16a4` (clean) | Result |
|---|---|
| Test-Simulation | PASS 227 (includes 15 new `BotTacticsTests` and the updated AI tests; the M3 compound 11-bot budget and 108k-tick soak are unchanged) |
| Test-Game | PASS 70 (respawn keeps the planner instance; damage stimulus uses the attacker's authoritative position) |
| Test-Maps | PASS 14 + 41 |
| Test-Contracts / Test-Generation | PASS 76 / 391 |
| Test-SourceProvenance | PASS, 746 entries / 373 GUIDs (5 new `.meta`) |
| Fixtures, C0 lock, Game-v1 lock, docs | PASS (35 fixture assertions) |
| Unity EditMode / PlayMode on merged `main` `e2c26e9` (clean) | PASS 53 / 20; PlayMode includes `ProductBotNavigationTests` on the real DepotYard package (Passed, not Inconclusive). Reports: `Artifacts/m0-foundation/e2c26e9629cdf694836f4cad2b336362c4d3a312/tests-editmode-20261115T135637676Z-ebb80009/` and `tests-playmode-20261115T135804010Z-dade6d9a/` |

Reports were copied from the worktree to `Artifacts/m2-combat-harness/`, `m8-runtime-composition/`, `m0-foundation/` and `m1-procedural-feasibility/` under `c5a16a4fb6102c7882bf8858fe728aed7b0ff074/`. A first run on the pre-rebase commit `d5e8db0` also passed in every lane, and its reports are kept alongside.

**Pending** (not run; never PASS without evidence):
- Client and server builds and smoke tests.
- A `SHOOTER_AI_SUMMARY` capture on real maps.
- A benchmark of AI CPU against the 1.5 ms mean budget.
- A real-player feel review: aim fairness, strafing near ledges, cover choices, and whether bots are too lethal or too passive.

Known limits:
- Strafing has no ledge query; the risk is limited only by the ±2 m leash around a navmesh point.
- Cover is judged by a single occlusion ray, with no exposure scoring.
- In the fake-map session test there was no combat (zero rays and shots), so stimulus forwarding is proven at the planner level plus in the code review of `OfflineGameSession`, not in a live session.

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

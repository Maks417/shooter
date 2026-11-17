# Next playable build — current state and remaining work

This page is the single source of truth for the current build, the product definition, task disposition and the order of remaining work. Other pages link here instead of repeating release banners. Exact identities, hashes and evidence live in the [M11 handoff](milestones/m11-playable-release/handoff.md); older deliveries (up to 0.3.12) are in the [M11 handoff archive](milestones/m11-playable-release/handoff-archive.md).

## Current build

**Latest installer — 0.3.19 (2026-11-17):** `Artifacts/deliveries/Shooter-Setup-0.3.19.exe`, clean binary/content source `3c29b39`. Adds to 0.3.18 the six 0.3.18 playtest fixes with ADR-097 headshot bonus, ADR-098 per-weapon recoil presentation, ADR-099 per-weapon hand/weapon choreography and ADR-100 bot strafe/jump/slide motion. Three maps × two roster sizes, native navigation in all 12 packages, rifle/pistol/knife/grenade. Pure lanes, Unity EditMode/PlayMode, native packages, clean IL2CPP Client, smoke, package and installer builders PASS. Actual install/upgrade, a playtest of this build, named listening/visual/feel review and all acceptance remain Pending. [Delivery evidence](milestones/m11-playable-release/handoff.md#delivery-0319).

The [visual roadmap](visual-roadmap.md) tranche (ADR-087–095) has been in installers since 0.3.18. ADR-096 retains its three provisional audio cues pending the assigned follow-up and owner listening review.

[Launch and playtest guide](playtest-guide.md) · [product state audit](product-state-audit.md) · [visual roadmap](visual-roadmap.md).

## Scope: local play with bots

The first version is a single offline process with one human and local allied/enemy bots ([vision](vision.md#first-version-scope-local-bots-only)). Under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence) (2026-11-16) networking is **parked**: `Networking` stays in git with no new code, the Dedicated Server build is not run by CI or the release flow, and server gates read as *Parked (offline scope)*, not Pending. Simulation stays the single authority and Presentation stays read-only.

## Required playable product

The deliverable is a source-matched Windows x64 installer with [in-place upgrades](features/windows-installer.md); the installed game launches offline into the Main menu. ZIPs are internal artifacts. The player selects Training Compound, Depot Yard or Oil Depot, Team Deathmatch, and 5v5 / 8v8 / 10v10 / 15v15 (ADR-101). One human joins Team 1 with nine to twenty-nine total bots. Teams race to 150 enemy kills or 15 active minutes; equal scores draw. Movement, sprint/slide, rifle/pistol/knife/grenade, aim/fire/reload, death/respawn, pause, live/final tables, replay and return to menu must work. Audio/video/control/accessibility settings persist and affect actual devices.

[Team rules](features/team-deathmatch.md), [maps](features/playable-maps.md), [map layouts](features/tdm-map-layouts.md) and [shell](features/game-shell.md) retain exact requirements. Native 1280×720 Low 30 FPS on the reference machine remains an acceptance gate; 60 FPS is a separate stretch. No external art or online scope is added.

## Task disposition

| Task | Delivered | Remaining |
|---|---|---|
| T066 — baseline | Metadata/cancellation repairs; pure lanes and provenance PASS on every delivery since. | Maintain the regression baseline. |
| T067 — content/navigation | Persisted catalog, packaged visuals, native NavMesh publication (`Compile-GameContent.ps1 -Native`, `Package-Release.ps1 -RequireNativeNav`) and truthful Complete/Partial/Failed path outcomes with diagnostics-only fallback. `nativeUnityNavBake=PASS` on all 12 packages since 0.3.16. | Named route/spawn evidence on all three maps, collected with the T071 playtest. |
| T068 — authority presentation | Slot selection and action events drive packaged views for all four weapons. | FIX05 player captures and named visual review. |
| T069 — preferences | Editable bindings/accessibility, staged display persistence, device modes. | Named UI/control/display/audio review, real Keep/Revert, 20 load/replay cycles. |
| T070 — measurement | Finish/results/replay, soak capture, honest counters; capture/soak defects found on 9f2219f fixed. | Twelve reference captures + two soaks on the final build (PendingHardware); per-tick managed allocation still fails the zero-allocation gate. |
| T071 — owner playtest | Owner playtest sessions on 0.3.x builds drove many fixes (see M11 handoff). | Formal matrix on the newest installer: 3 maps × 2 sizes, complete matches/results/replay, recovery and prior-pair rollback. |
| T073 — installer | Inno Setup installer with stable AppId and monotonic version check (ADR-041), shipped 0.3.1–0.3.19. INS02 upgrade, INS03 repair, INS05 uninstall and INS06 rollback PASS as earlier silent software exercises on the owner PC. | Actual 0.3.19 installed upgrade, INS01 on a clean machine, INS04 mid-copy interruption, interactive wizard; [status](features/windows-installer.md#implementation-status). |
| T062 — P05 soldier | ADR-095 structural shooting posture resolves the numeric P05 gate (D01–D03). | Named visual review; P05 visual acceptance stays FAIL until then. See [defect inventory](visual-defect-inventory.md). |
| T065 — final acceptance | Operator checklist. | REL01–REL06 and FIX01–FIX14 on one stable final pair. NotClaimed. |
| T091 — third-party provenance | ADR-075 allowlist categories enforced by `Test-SourceProvenance.ps1`. | Done (2026-11-16). |
| T092 — noise primitive | FastNoiseLite port, `NoiseField`, cross-lane goldens; `material.noise` generator landed under ADR-088. | Done. |
| T093 — runtime rig correction | Animation Rigging rejected (ADR-075 addendum); corrections stay in pure `SoldierMotion`/`TwoBoneIk`. | Collision-backed `ISoldierGround` for remote soldiers, per-profile support grip, bounded head look-at; EditMode, Client build, CPU and named review. |
| T094 — spatial audio | Not started. | Steam Audio spike behind the stereo mixer, native-binary allowlist, Client build, CPU and named headphone review. |
| T096 — named arsenal | [ADR-102](decisions.md#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix): AK-47, AK-102, M16A4 (burst), Mk 14 EBR, M-TAC 338 (bolt sniper), G17 and Golden Eagle .50 with geometry.weapon 1.2.0 meshes, per-gun stats/falloff, AK and bolt-action choreography, scope zoom, bank v6 reports, random spawn loadouts; muzzle flash playback and placement fixed. Branch `weapons-roster`. | Republish content, Unity EditMode/PlayMode, client build and installer; owner look/sound/feel and balance review in a packaged build. |
| T095 — audio overhaul | ADR-096 bank v5 (real reload/knife/grenade Foley, flybys, hit/kill confirm, menu UI sound) and music (menu, calm/tension bed, stingers, results) with a Music slider; commit `43adaca`. | Replace three placeholders after one more small download batch (owner approves the files): bullet flyby, defeat stinger, outdoor ambience. Then owner listening review of all takes. Steps: [audio follow-up](features/audio.md#follow-up-placeholders). |

## Order of remaining work

1. **Install 0.3.19.** The source-matched installer and locked-editor EditMode/PlayMode checks are delivered ([delivery](milestones/m11-playable-release/handoff.md#delivery-0319)); actual installed upgrade and the in-game feel check of ADR-098–100 remain Pending.
2. **T071 formal playtest on that installer.** Follow the [guide](playtest-guide.md): all three maps × 5v5/8v8/10v10/15v15, at least one complete normal match per map, results and replay, missing/corrupt content recovery and a real prior-version upgrade. Record named reviewer, identities, logs and every blocker; route defects to their owning cards.
3. **T096 named arsenal (ADR-102):** merge, republish content, build the next installer and review every gun in play.
4. **T093, T094 and the T095 audio placeholders** when the owner assigns them.
5. **T070 + T062 + T065 on the final stable pair.** Reference captures and soaks on the idle reference laptop, named visual/UI/audio/display reviews, eight full matches, draw fixture, provenance/regressions and the remaining installer INS checks. Missing reviewers/hardware/counters stay Pending/PendingHardware.

One integrator owns Bootstrap, Contracts, settings, locks, workloads and `coordination.json`. Keep Unity 6000.3.28f1 / URP 17.3.0, C0-v1 and Game-v1; boundary changes need an ADR plus migration/fixtures before consumers.

## Playtest versus acceptance

A delivered installer is a **test candidate**, not proof that T071 passed. A usable owner handoff needs all required interactions exercised and at least one complete normal match per map. Disclose P05 status and unmeasured reference performance. Any observed functional blocker prevents calling a build proven playable.

T065 executes [REL01–REL06](milestones/m11-playable-release/validation-acceptance.md) and [FIX01–FIX14](release-remediation-validation.md): eight score/timeout full matches (both sizes per map), draw fixture, named reviews, 20 shell cycles, real display/audio/control behaviour, twelve reference Low captures and two ≥30-minute normal-AI soaks, provenance/regressions and independent installer install/update/repair/uninstall/rollback. Use the [M11 operator runbook](milestones/m11-playable-release/handoff-archive.md#t065-operator-runbook-do-not-stamp-pass); its server-isolation step is Parked under ADR-076. Documentation hashes and Smoke/READY do not prove gameplay.

## Source changes delivered since 0.3.12

Compact index of handoff entries. All are included in the current installer; the exact first installer for each is in its record.

| Change | Record |
|---|---|
| ADR-068 arsenal visual profiles | [handoff](milestones/m11-playable-release/handoff.md#arsenal-delivery-0314) |
| T086/T089 combat audio bank, ADR-055/062 | [T089](milestones/m11-playable-release/handoff.md#t089--stereo-combat-wav-bank-and-footstep-distance-adr-062) |
| T087 knife/grenade, ADR-057/064 | [handoff](milestones/m11-playable-release/handoff.md#t087--knife-slot-3-and-grenade-slot-4-adr-057) |
| T088 weapon visuals and POV fire, ADR-061 | [handoff](milestones/m11-playable-release/handoff.md#t088--weapon-visual-overhaul-adr-061) |
| ADR-063 hit-region damage, ADR-065 PiP scope | [damage](milestones/m11-playable-release/handoff.md#adr-063--hit-region-damage-coefficients--2026-11-15), [scope](milestones/m11-playable-release/handoff.md#adr-065--pip-rifle-scope-blurred-periphery--2026-11-15) |
| ADR-066 UI copy and typeface, ADR-067 pose/motor fixes | [UI](milestones/m11-playable-release/handoff.md#adr-066--ui-copy--barlow-condensed-typeface-bank--2026-11-15), [pose](milestones/m11-playable-release/handoff.md#adr-067--soldier-pose--motor-corner-launch-fixes--2026-11-15) |
| Death collapse, insertion loading, T090 bot tactics | [T090](milestones/m11-playable-release/handoff.md#t090--bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread--2026-11-15) |
| ADR-071 Oil Depot, combat feel, ADR-072 weapon profiles | [Oil Depot](milestones/m11-playable-release/handoff.md#adr-071--oil-depot-third-map--elevated-routes--2026-11-15), [profiles](milestones/m11-playable-release/handoff.md#adr-072--weapon-profiles) |
| Kill-feed icons, ADR-073 badges, ADR-074 compact maps | [ADR-074](milestones/m11-playable-release/handoff.md#adr-074--compact-64--80-m-maps-with-distinct-layouts--2026-11-15) |
| ADR-075–086 content pipeline, post-processing, SDF UI | [delivery](milestones/m11-playable-release/handoff.md#delivery-0317) |
| ADR-096 audio overhaul: bank v5, music, menu UI sound (T095) | [handoff](milestones/m11-playable-release/handoff.md#adr-096-audio-overhaul-bank-v5-music-ui-sound--2026-11-17) |
| 0.3.18 playtest fixes, ADR-097 headshot bonus, ADR-098 recoil, ADR-099 hand choreography, ADR-100 bot motion | [fixes](milestones/m11-playable-release/handoff.md#playtest-fixes-after-0318--2026-11-17), [ADR-098](decisions.md#adr-098--accepted-per-weapon-first-person-recoil-presentation-camera-climb--per-shot-kick), [ADR-099](decisions.md#adr-099--accepted-per-weapon-first-person-hand-and-weapon-choreography), [ADR-100](milestones/m11-playable-release/handoff.md#adr-100-bot-strafe-jump-and-slide-motion--2026-11-17), [delivery](milestones/m11-playable-release/handoff.md#delivery-0319) |

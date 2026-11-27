# Architecture decision records

**Statuses:**
- **Accepted:** an architectural requirement.
- **Provisional:** build as specified until its evidence gate resolves.
- **Parked:** kept in git without new work.

Changes append a dated superseding record, and old rationale stays in its record. Records are kept short: Context, Decision, Consequences. Implementation evidence lives in the milestone handoffs, and the full original text of every record is in git history.

Accepting a decision never certifies a runtime or hardware gate. The headings below are stable link anchors, because schemas, locks and generator output cite some of them, so never rename them.

## Index

| ADR | Status | Title | Superseded or amended |
|---|---|---|---|
| [ADR-001](#adr-001--accepted-unity-6-urp-rasterization) | Accepted | Unity 6 URP rasterization |  |
| [ADR-002](#adr-002--accepted-zero-authored-visual-source-assets) | Accepted | zero authored visual source assets | licensed authored art track opened by ADR-124 |
| [ADR-003](#adr-003--accepted-compilerruntime-separation) | Accepted | compiler/runtime separation |  |
| [ADR-004](#adr-004--accepted-gameplay-independent-of-visual-quality) | Accepted | gameplay independent of visual quality |  |
| [ADR-005](#adr-005--accepted-60-hz-authoritative-simulation) | Accepted | 60 Hz authoritative simulation |  |
| [ADR-006](#adr-006--provisional-gameobjects-and-kinematic-motor) | Provisional | GameObjects and kinematic motor |  |
| [ADR-007](#adr-007--provisional-generated-humanoid-plus-analytical-ik) | Provisional | generated humanoid plus analytical IK | product body amended by ADR-124 |
| [ADR-008](#adr-008--provisional-transport-behind-pure-codec-port) | Provisional | transport behind pure codec port | parked by ADR-076 |
| [ADR-009](#adr-009--accepted-reproducible-source-not-universal-deterministic-physics) | Accepted | reproducible source, not universal deterministic physics |  |
| [ADR-010](#adr-010--provisional-bounded-compound-and-offline-first-scope) | Provisional | bounded compound and offline-first scope | scope replaced by ADR-033, ADR-076 |
| [ADR-011](#adr-011--provisional-exact-toolchain-resolved-at-m0) | Provisional | exact toolchain resolved at M0 | resolved by ADR-013 |
| [ADR-012](#adr-012--accepted-review-closure-defaults-revision-11) | Accepted | review closure defaults (revision 1.1) |  |
| [ADR-013](#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) | Accepted | initial M0 toolchain and explicit engine resource curation |  |
| [ADR-014](#adr-014--accepted-initial-c0-source-freeze-and-failure-capture-lifecycle) | Accepted | initial C0 source freeze and failure capture lifecycle |  |
| [ADR-015](#adr-015--accepted-calibrated-release-measurement-and-immutable-evidence-review) | Accepted | calibrated release measurement and immutable evidence review | allocation hook replaced by ADR-042 |
| [ADR-016](#adr-016--accepted-conservative-render-budget-proof-and-continuous-thermal-conditioning) | Accepted | conservative render-budget proof and continuous thermal conditioning |  |
| [ADR-017](#adr-017--accepted-m0-owner-waiver-of-remaining-hardware-validation) | Accepted | M0 owner waiver of remaining hardware validation |  |
| [ADR-018](#adr-018--accepted-initial-p02-compiler-format-and-byte-cache-boundary) | Accepted | initial P02 compiler format and byte-cache boundary |  |
| [ADR-019](#adr-019--accepted-additive-m1-feature-recipes-and-reproducible-source-byte-lock) | Accepted | additive M1 feature recipes and reproducible source-byte lock |  |
| [ADR-020](#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators) | Accepted | versioned first visual corrections for weapon and metal generators |  |
| [ADR-021](#adr-021--accepted-initial-material-benchmark-camera-and-central-cube-clarification) | Accepted | initial material benchmark camera and central-cube clarification |  |
| [ADR-022](#adr-022--accepted-versioned-soldier-spike-and-second-numeric-motion-baseline) | Accepted | versioned soldier spike and second numeric motion baseline |  |
| [ADR-023](#adr-023--accepted-owner-authorized-third-bounded-body-clearance-iteration) | Accepted | owner-authorized third bounded body-clearance iteration |  |
| [ADR-024](#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations) | Accepted | P05 soldier visual scope stop after exhausted clearance iterations | search reopened by ADR-094/095 |
| [ADR-025](#adr-025--accepted-m1-owner-waiver-of-remaining-hardwareresource-validation-and-milestone-close) | Accepted | M1 owner waiver of remaining hardware/resource validation and milestone close |  |
| [ADR-026](#adr-026--accepted-m2-owner-waiver-of-remaining-hardwarereference-validation-and-milestone-close) | Accepted | M2 owner waiver of remaining hardware/reference validation and milestone close |  |
| [ADR-027](#adr-027--accepted-m3-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close) | Accepted | M3 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close |  |
| [ADR-028](#adr-028--accepted-m4-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close) | Accepted | M4 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close |  |
| [ADR-029](#adr-029--accepted-m5-unity-transport-behind-inetworkchannel-custom-prediction-retained) | Accepted | M5 Unity Transport behind INetworkChannel; custom prediction retained | parked by ADR-076 |
| [ADR-030](#adr-030--accepted-m5-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close) | Accepted | M5 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close |  |
| [ADR-031](#adr-031--accepted-m6-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close) | Accepted | M6 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close | parked by ADR-076 |
| [ADR-032](#adr-032--accepted-m7-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close) | Accepted | M7 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close |  |
| [ADR-033](#adr-033--accepted-next-offline-playable-product-scope-and-staged-delivery) | Accepted | next offline playable product scope and staged delivery |  |
| [ADR-034](#adr-034--accepted-initial-game-v1-freeze-t035) | Accepted | initial Game-v1 freeze (T035) |  |
| [ADR-035](#adr-035--accepted-verified-release-map-packages-t055-boundary) | Accepted | verified release map packages (T055 boundary) |  |
| [ADR-036](#adr-036--accepted-usersettings-schema-2-shell-coverage-t060--fix09) | Accepted | UserSettings schema 2 shell coverage (T060 / FIX09) |  |
| [ADR-037](#adr-037--accepted-product-tdm-measurement-workloads-t063--fix12) | Accepted | product TDM measurement workloads (T063 / FIX12) |  |
| [ADR-038](#adr-038--accepted-persisted-release-catalog-t067--b03) | Accepted | persisted release catalog (T067 / B03) |  |
| [ADR-039](#adr-039--accepted-local-authoritative-sprint-slide-t072) | Accepted | local authoritative sprint slide (T072) |  |
| [ADR-040](#adr-040--accepted-lf-line-ending-identity-for-frozen-locks) | Accepted | LF line-ending identity for frozen locks |  |
| [ADR-041](#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073) | Accepted | Inno Setup per-user installer and in-place upgrade (T073) |  |
| [ADR-042](#adr-042--accepted-native-c-allocation-counter-for-benchmark-runs) | Accepted | native C++ allocation counter for benchmark runs |  |
| [ADR-043](#adr-043--accepted-two-stage-native-navigation-publication) | Accepted | two-stage native navigation publication |  |
| [ADR-044](#adr-044--accepted-versioned-procedural-map-surface-publication) | Accepted | versioned procedural map surface publication | surfaces replaced by ADR-088/106/116 |
| [ADR-045](#adr-045--accepted-compact-tdm-arena-layouts-for-catalog-maps-t076) | Accepted | compact TDM arena layouts for catalog maps (T076) |  |
| [ADR-046](#adr-046--accepted-offline-product-client-recovers-from-frame-hitches) | Accepted | offline product client recovers from frame hitches |  |
| [ADR-047](#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure) | Accepted | relaxed spawn fallback and recoverable spawn failure |  |
| [ADR-048](#adr-048--accepted-session-internal-pre-match-countdown) | Accepted | session-internal pre-match countdown |  |
| [ADR-049](#adr-049--accepted-non-gameplay-visual-architecture-boxes-for-map-solids-t078) | Accepted | non-gameplay visual architecture boxes for map solids (T078) |  |
| [ADR-050](#adr-050--accepted-packaged-architectural-surface-construction-t081) | Accepted | packaged architectural surface construction (T081) |  |
| [ADR-051](#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082) | Accepted | runtime cover probes share the AI ray budget (T082) |  |
| [ADR-052](#adr-052--accepted-packaged-first-person-glove-and-sleeve-props-t083) | Accepted | packaged first-person glove and sleeve props (T083) | geometry replaced by ADR-069/106 |
| [ADR-053](#adr-053--accepted-procedural-near-field-finish-triplets-and-hip-composition-t084) | Accepted | procedural near-field finish triplets and hip composition (T084) |  |
| [ADR-054](#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085) | Accepted | bot footstep hearing, wider vision and a readable lethality envelope (T085) |  |
| [ADR-055](#adr-055--accepted-generated-pcm-combat-sample-banks-t086) | Accepted | generated PCM combat sample banks (T086) | source policy amended by ADR-096 |
| [ADR-056](#adr-056--accepted-last-weapon-quick-switch-q-and-local-only-muzzle-overlay) | Accepted | last-weapon quick switch (Q) and local-only muzzle overlay |  |
| [ADR-057](#adr-057--accepted-knife-and-grenade-loadout-slots-t087) | Accepted | knife and grenade loadout slots (T087) |  |
| [ADR-058](#adr-058--accepted-minimap-self-marker-and-temporary-gunfire-reveal) | Accepted | minimap self marker and temporary gunfire reveal | north-up map replaced by ADR-104 |
| [ADR-059](#adr-059--accepted-personal-scoreboard-score-and-combat-score-badges) | Accepted | personal scoreboard Score and combat score badges |  |
| [ADR-060](#adr-060--accepted-local-authoritative-tightness-stamina) | Accepted | local authoritative tightness (stamina) |  |
| [ADR-061](#adr-061--accepted-geometryweapon-102-mechanical-parts-and-denser-fire-fx-t088) | Accepted | geometry.weapon 1.0.2 mechanical parts and denser fire FX (T088) |  |
| [ADR-062](#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089) | Accepted | stereo found/authored combat WAV bank and per-kind distance (T089) | source policy amended by ADR-096 |
| [ADR-063](#adr-063--accepted-hit-region-damage-coefficients-one-shot-head) | Accepted | hit-region damage coefficients (one-shot head) |  |
| [ADR-064](#adr-064--accepted-knifegrenade-fp-special-actions-and-throw-presentation-t087-follow-up) | Accepted | knife/grenade FP special actions and throw presentation (T087 follow-up) |  |
| [ADR-065](#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery) | Accepted | screen-space PiP rifle scope (blurred periphery) | replaced by ADR-103 |
| [ADR-066](#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed) | Accepted | non-commercial UI typeface bank (Barlow Condensed) |  |
| [ADR-067](#adr-067--accepted-soldier-motionv5-slide-pose-gait-stability-and-motor-corner-anti-launch) | Accepted | soldier motion.v5 slide pose, gait stability and motor corner anti-launch |  |
| [ADR-068](#adr-068--accepted-procedural-arsenal-visual-profiles-and-finishes) | Accepted | procedural arsenal visual profiles and finishes |  |
| [ADR-069](#adr-069--accepted-articulated-near-field-hands-and-global-analytic-reflections) | Accepted | articulated near-field hands and global analytic reflections |  |
| [ADR-070](#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090) | Accepted | bot tactics tranche (cover peek, wall hearing, ledge strafe, route spread) (T090) |  |
| [ADR-071](#adr-071--accepted-oil-depot-third-map-and-limited-elevated-routes) | Accepted | Oil Depot third map and limited elevated routes |  |
| [ADR-072](#adr-072--accepted-definitionid-weapon-profiles-catalog--visual-bind) | Accepted | DefinitionId weapon profiles (catalog + visual bind) | data form in ADR-077, ADR-102 |
| [ADR-073](#adr-073--accepted-life-scoped-multi-kill-and-headshot-hud-badges) | Accepted | life-scoped multi-kill and headshot HUD badges |  |
| [ADR-074](#adr-074--accepted-compact-64--80-m-arenas-with-distinct-per-map-layouts) | Accepted | compact 64 × 80 m arenas with distinct per-map layouts | layouts replaced by ADR-109 |
| [ADR-075](#adr-075--provisional-third-party-code-libraries-for-noise-runtime-rigging-and-spatial-audio) | Provisional | third-party code libraries for noise, runtime rigging and spatial audio |  |
| [ADR-076](#adr-076--accepted-offline-only-scope-and-bounded-local-evidence) | Accepted | offline-only scope and bounded local evidence | Networking build/test isolation amended by ADR-120 |
| [ADR-077](#adr-077--accepted-weapon-definition-files-generate-both-weapon-catalogs) | Accepted | weapon definition files generate both weapon catalogs |  |
| [ADR-078](#adr-078--accepted-shared-weapon-package-separate-from-map-packages) | Accepted | shared weapon package separate from map packages |  |
| [ADR-079](#adr-079--accepted-engine-toolchain-lock-separate-from-the-build-lock) | Accepted | engine toolchain lock separate from the build lock |  |
| [ADR-080](#adr-080--accepted-code-built-post-processing-hdr-and-ssao-per-quality-tier) | Accepted | code-built post-processing, HDR and SSAO per quality tier |  |
| [ADR-081](#adr-081--accepted-baked-ambient-occlusion-volume-per-map) | Accepted | baked ambient-occlusion volume per map |  |
| [ADR-082](#adr-082--accepted-geometryweapon-110-parametric-rifle-parts) | Accepted | geometry.weapon 1.1.0 parametric rifle parts |  |
| [ADR-083](#adr-083--accepted-map-registry-arena-layout-documents-and-the-generic-maparena-recipe) | Accepted | map registry, arena layout documents and the generic map.arena recipe |  |
| [ADR-084](#adr-084--accepted-analytic-sdf-ui-primitives-and-ui-motion-model) | Accepted | analytic SDF UI primitives and UI motion model |  |
| [ADR-085](#adr-085--accepted-generated-sdf-icon-atlas-with-mesh-traced-weapon-silhouettes) | Accepted | generated SDF icon atlas with mesh-traced weapon silhouettes |  |
| [ADR-086](#adr-086--accepted-animated-debrief-and-front-end-motion) | Accepted | animated debrief and front-end motion |  |
| [ADR-087](#adr-087--accepted-tactical-hud-craft-first-tranche) | Accepted | tactical HUD craft, first tranche |  |
| [ADR-088](#adr-088--accepted-seeded-noise-material-family) | Accepted | seeded noise material family |  |
| [ADR-089](#adr-089--accepted-weathered-map-surface-shading) | Accepted | weathered map surface shading |  |
| [ADR-090](#adr-090--accepted-packaged-procedural-decals-and-versioned-visual-layout-operations) | Accepted | packaged procedural decals and versioned visual layout operations |  |
| [ADR-091](#adr-091--accepted-numeric-sky-atmospheres-map-grades-and-actor-grounding-light) | Accepted | numeric sky atmospheres, map grades and actor grounding light |  |
| [ADR-092](#adr-092--accepted-numeric-volume-noise-combat-flipbooks) | Accepted | numeric volume-noise combat flipbooks |  |
| [ADR-093](#adr-093--accepted-bounded-particle-combat-presentation) | Accepted | bounded particle combat presentation |  |
| [ADR-094](#adr-094--accepted-bounded-soldier-presentation-revision-and-p05-reopening) | Accepted | bounded soldier presentation revision and P05 reopening | replaced by ADR-095 |
| [ADR-095](#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening) | Accepted | structural weapon-from-eye shooting posture (P05 reopening) |  |
| [ADR-096](#adr-096--accepted-music-channel-combat-sample-bank-v5-and-owner-audio-source-relaxation) | Accepted | music channel, combat sample bank v5 and owner audio-source relaxation |  |
| [ADR-097](#adr-097--accepted-headshot-kill-score-bonus) | Accepted | headshot kill Score bonus |  |
| [ADR-098](#adr-098--accepted-per-weapon-first-person-recoil-presentation-camera-climb--per-shot-kick) | Accepted | per-weapon first-person recoil presentation (camera climb + per-shot kick) |  |
| [ADR-099](#adr-099--accepted-per-weapon-first-person-hand-and-weapon-choreography) | Accepted | per-weapon first-person hand and weapon choreography |  |
| [ADR-100](#adr-100--accepted-third-person-directional-gait-airborne-pose-and-bot-hopsslides) | Accepted | third-person directional gait, airborne pose and bot hops/slides |  |
| [ADR-101](#adr-101--accepted-selectable-team-sizes-5--8--10--15) | Accepted | selectable team sizes 5 / 8 / 10 / 15 |  |
| [ADR-102](#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix) | Accepted | named real-world arsenal, random spawn loadouts and muzzle flash fix | human loadout replaced by ADR-107 |
| [ADR-103](#adr-103--accepted-optical-scope-lens-camera-balancedhigh-quality-uplift-flicker-geometry-fixes-and-damage-feedback-v2) | Accepted | optical scope lens camera, Balanced/High quality uplift, flicker geometry fixes and damage feedback v2 |  |
| [ADR-104](#adr-104--accepted-circular-heading-up-minimap-with-ring-compass) | Accepted | circular heading-up minimap with ring compass |  |
| [ADR-105](#adr-105--accepted-dropped-primary-weapons-and-g-pickup) | Accepted | dropped primary weapons and G pickup |  |
| [ADR-106](#adr-106--accepted-kernel-built-weapons-gloves-soldiers-and-structured-map-surfaces) | Accepted | kernel-built weapons, gloves, soldiers and structured map surfaces |  |
| [ADR-107](#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights) | Accepted | Armory loadouts and reusable rail-mounted sights |  |
| [ADR-108](#adr-108--accepted-career-ranks-unlocks-buffs-and-local-service-record) | Accepted | career ranks, unlocks, buffs and local service record | ladder extended by ADR-119 |
| [ADR-109](#adr-109--accepted-site-planned-maps-old-town-harbour-rebuilt-oil-depot) | Accepted | site-planned maps (Old Town, Harbour, rebuilt Oil Depot) |  |
| [ADR-110](#adr-110--accepted-body-armor-100-points-torso-and-limbs) | Accepted | body armor (100 points, torso and limbs) |  |
| [ADR-111](#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models) | Accepted | knife and grenade overhaul (one-handed hold, cut and throw, half-map grenade range, sound, explosion, models) |  |
| [ADR-112](#adr-112--accepted-crosshair-true-hits-rig-aligned-hit-volumes-and-bots-that-use-map-cover-flank-and-move-smoothly) | Accepted | crosshair-true hits, rig-aligned hit volumes, and bots that use map cover, flank and move smoothly |  |
| [ADR-113](#adr-113--accepted-career-progress-line-layered-rank-emblems-and-reward-art) | Accepted | career progress line, layered rank emblems and reward art |  |
| [ADR-114](#adr-114--accepted-corner-bob-motor-fix-nav-bake-that-respects-walls-and-a-tabbed-career-page) | Accepted | corner-bob motor fix, nav bake that respects walls, and a tabbed Career page |  |
| [ADR-115](#adr-115--accepted-open-reflex-collimator-with-a-clear-glass-pane) | Accepted | open reflex collimator with a clear glass pane |  |
| [ADR-116](#adr-116--accepted-realistic-map-colour-texture-and-shadows-materialnoise-120) | Accepted | realistic map colour, texture and shadows (material.noise 1.2.0) |  |
| [ADR-117](#adr-117--accepted-single-point-collimator-dot-and-12x-collimator-zoom) | Accepted | single-point collimator dot and 1.2x collimator zoom |  |
| [ADR-118](#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150) | Accepted | M4A1, Mk 16, M24 SWS and M870: pellets, shell reloads and geometry.weapon 1.5.0 | M870 damage retuned by ADR-127 |
| [ADR-119](#adr-119--accepted-30-rank-career-ladder-and-four-new-perks) | Accepted | 30-rank career ladder and four new perks |  |
| [ADR-120](#adr-120--accepted-parked-networking-behind-a-define-and-build-once-pure-lanes) | Accepted | parked networking behind a define and build-once pure lanes | amends ADR-076 |
| [ADR-121](#adr-121--accepted-feature-lock-hashes-c-code-tokens-and-every-lock-has-a-refreeze-command) | Accepted | feature lock hashes C# code tokens; every lock has a refreeze command |  |
| [ADR-122](#adr-122--accepted-mode-rules-as-data-and-team-indexed-kills-game-v1-additive) | Accepted | mode rules as data and team-indexed kills (Game-v1 additive) | amends ADR-034 |
| [ADR-123](#adr-123--accepted-unified-front-end-visual-system) | Accepted | unified front-end visual system | amends ADR-084, ADR-086, ADR-087 |
| [ADR-124](#adr-124--accepted-licensed-authored-art-track-characters-first) | Accepted | licensed authored art track (characters first) | amends ADR-002, ADR-007 |
| [ADR-125](#adr-125--accepted-bolt-action-snipers-kill-on-any-hit) | Accepted | bolt-action snipers kill on any hit | amends ADR-063, ADR-110 for flagged guns |
| [ADR-126](#adr-126--accepted-licensed-fire-and-smoke-flipbooks-and-burning-wrecks) | Accepted | licensed fire and smoke flipbooks and burning wrecks | second application of ADR-124; amends ADR-092, ADR-093, ADR-111 |
| [ADR-127](#adr-127--accepted-m870-lethal-up-close-and-per-pellet-tracers) | Accepted | M870 lethal up close and per-pellet tracers | retunes the ADR-118 M870 |

## ADR-001 — Accepted: Unity 6 URP rasterization

Context: realistic procedural art must run on integrated Radeon. Decision: Unity 6 URP, C#, HLSL; conventional meshes and generated PBR caches, Forward baseline D3D11. Reject HDRP baseline and full-scene ray marching for compatibility/cost. Consequence: constrain lighting/overdraw and measure from M0. Revisit renderer only on measured evidence without abandoning iGPU gate. [Architecture](architecture.md), [performance](performance.md).

## ADR-002 — Accepted: zero authored visual source assets

Code/numeric recipes are source of meshes, textures, rigs and movement. Generated caches allowed with provenance, engine font exception explicit. Imported models/photos/painted textures/sprites/mocap/hand-keyed joint clips disallowed. Consequence: soldier feasibility is a milestone gate, not an outsourcing task. [Vision](vision.md).

## ADR-003 — Accepted: compiler/runtime separation

Expensive geometry/material/LOD/nav/weight generation runs in tools; runtime loads immutable versioned artifacts and runs cheap motion. Alternative generate everything on demand rejected for CPU/GPU contention and unpredictable hitches. Consequence: cache schema, migrations and cold-build CI are first-class. [Generation](generation.md).

## ADR-004 — Accepted: gameplay independent of visual quality

Canonical collision/nav/hit profiles and weapon definitions identical across profiles. No render-mesh hit detection or AI visibility derived from absent low-tier foliage. Consequence: maintain separate gameplay artifact hash and fixed gameplay pose evaluator. [Contracts](contract-details.md).

## ADR-005 — Accepted: 60 Hz authoritative simulation

30 FPS display baseline with 60 Hz simulation; command/snapshot architecture from M0, offline first. Reject variable-delta combat and physics lockstep. Consequence: interpolation, replay and bounded catch-up needed; simulation determinism limited to tested envelope. [Combat](features/combat.md).

## ADR-006 — Provisional: GameObjects and kinematic motor

Begin with managed modular simulation and engine adapters, not full ECS. Kinematic motor supports explicit replay; rigidbody movement rejected as first prediction target. Revisit after M0 motor spike/B03 CPU measurements; converting to DOTS is a scoped migration, not a parallel-agent local choice.

## ADR-007 — Provisional: generated humanoid plus analytical IK

Parametric ring meshes, distance-based weights and gait/IK motion; no imported fallback. Reject full muscle/cloth simulation and learned motion datasets for slice complexity/provenance. M1 exits with reviewed poses/perf; failure reduces visual complexity through a recorded scope change. [Soldiers](features/soldiers-animation.md).

## ADR-008 — Provisional: transport behind pure codec port

Unity Transport candidate, evaluate NGO adapter at M5; custom FPS prediction not assumed free from middleware. NFE requires explicit ECS choice. Consequence: codec/authority work stays framework independent; choose exact package only after pinned-version spike. [Networking](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence).

## ADR-009 — Accepted: reproducible source, not universal deterministic physics

PCG32 named streams, canonical encoding, semantic versions, content addressing. Reject cross-platform lockstep promise for PhysX/nav/GPU. Server supplies authoritative state and gameplay hashes. Consequence: golden topology tests plus tolerance tests and version handshake. [Generation](generation.md).

## ADR-010 — Provisional: bounded compound and offline-first scope

256 m map, eight bots, rifle/pistol, static cover before four-player network slice. Reject open world/destruction/vehicles at foundation. Entire gameplay collision/nav loads initially; visuals stream. Consequence: later capacity and gameplay streaming need new evidence, not hidden expansion.

## ADR-011 — Provisional: exact toolchain resolved at M0

Use available supported Unity 6 LTS patch with compatible URP, pin all packages before implementation contracts freeze. No invented patch/version. M0 owner records lock and proves client/headless builds; [references](references.md) are guidance to check against that lock, not package compatibility guarantees.

## ADR-012 — Accepted: review closure defaults (revision 1.1)

Separate LogicalAssetId/Unity GUID from versioned request ContentId/payload hashes; exclude build telemetry from deterministic manifests. Explicit result/lease types replace ambiguous integer query returns and direct snapshot reads. Post-movement hit poses, batched same-tick shot intents and delayed AI commands define tick order. Compound Clear/no-respawn cooperative slice and collision matrix are fixed in [gameplay baseline](gameplay-baseline.md). Analytic death collapse is required; physics ragdoll optional. Implementation start links exact specifications and fixtures. These close ambiguity without claiming implementation or empirical feasibility.

## ADR-013 — Accepted: initial M0 toolchain and explicit engine resource curation

Date 2026-11-11. **Context:** M0 had to pin an installed, supported toolchain, and Core RP shipped authored debug UI sprites.

**Decision.** The toolchain is editor 6000.3.28f1 (8f082d2fe476), URP/Core RP 17.3.0, Input System 1.20.1, Test Framework 1.6.0 and AI Navigation 2.0.0, recorded in `toolchain-lock.json`. Input System 1.20.1 replaces the template's 1.12.0, which failed to compile; that was initial resolution, not a later upgrade. This resolves ADR-011.

`Tools/prepare_unity_packages.py` packs the installed Core RP source into an immutable local UPM tarball (`.local/engine-packages/com.unity.render-pipelines.core-17.3.0-shooter.tgz`). The tarball omits `DebugUICanvas.prefab` and `DebugUIPersistentCanvas.prefab`. It has canonical tar ordering and gzip, and it is verified against the pinned upstream and curated inventories and the archive SHA-256 (curation schema 2; a mutable folder dependency was rejected because Unity rewrote importer metadata).

URP dithering slots get code-generated Bayer textures, and the debug-font slot gets a generated one-pixel asset. Splash and Unity-logo are both disabled. Settings, scene, material and lookup outputs are hash-recorded in `Assets/Shooter/Settings/provenance.json`.

**Consequences.** A fresh setup runs `Configure-Unity.ps1` before package resolution. Installed packages and the Library cache are never edited. Any curation change needs a new inventory and a review.

## ADR-014 — Accepted: initial C0 source freeze and failure capture lifecycle

Date 2026-11-12. **Decision.** The reviewed contract candidate is frozen as C0-v1 (`ContractVersion.Current="C0-v1"`). `contract-lock.json` pins:
- the contract sources and metadata;
- the JSON and binary specification fixtures;
- the toolchain.

`Tools/verify_contract_lock.py` runs before the pure and Unity wrappers, and CI tests rejected drift. There is no automatic refresh. Future changes need integrator review, plus an ADR and migration where semantics change.

**Failure capture.** A retained player stalled during native shutdown after a correctly detected offline overload. Now:
- `FoundationEntry.Fail` notifies cleanup observers once.
- `FoundationBenchmark` disposes recorders before shutdown and writes `failure.json`.
- A partial capture never becomes success.
- `Test-FailureShutdown.ps1` injects a diagnostic 200 ms stall and requires exit code 1 within 30 s.

The four-tick cap and debt retention are unchanged.

## ADR-015 — Accepted: calibrated release measurement and immutable evidence review

Date 2026-11-12. **Context.** The locked IL2CPP `GetAllocatedBytesForCurrentThread` is a stub that returns zero. Historical zero-allocation readings therefore prove nothing, and their interpretation is superseded. Native GPU timings are sometimes missing on the reference driver.

**Allocations.** `Bootstrap/Runtime/AllocationMeter.cs` registers one IL2CPP profiler allocation callback, without editing the engine and without a development player. Object, string and array controls on the main and worker threads must be detected before warmup and after sampling. If they are not, the counters are unavailable, never zero. `Test-Measurement.ps1` also requires known injected allocations to be detected.

**Frame timing.** `Bootstrap/Core/FrameTimingLedger.cs` joins delayed native timings to frame brackets by exact QPC timestamp. Ambiguous or missing values stay unavailable and are never filled in.

**External GPU timing.** PresentMon 2.6.0 is pinned by hash in `Tools/presentmon-lock.json` and is an ignored measurement tool. `Benchmark.ps1 -PresentMon` records its own ETW session for the exact Shooter PID. `Tools/external_gpu.py` requires:
- the exact measured frame count;
- one swapchain;
- a finite GPU duration for every present;
- the player's own proof that VSync is off.

PresentMon's SyncInterval column is not treated as application VSync.

**Hardware review.** `Tools/hardware_evidence.py` validates dated reviewer rationale and hash-proven RAM and thermal evidence. `Review-Benchmark.ps1` writes fresh derived results, and raw files stay immutable.

**Migration.** Collector metadata moves to v2. The result schema, workloads, C0 and the toolchain are unchanged.

## ADR-016 — Accepted: conservative render-budget proof and continuous thermal conditioning

Date 2026-11-12. **Context.** The native active-render counter sometimes reports zero. Filling, zeroing or relaxing the budget were all rejected.

**Decision.** Collector v3 keeps both native FrameTiming endpoints: CPU frame start and Present called. Render work for a frame lies inside that interval, so the interval's p95 is a conservative upper bound on render p95. The ≤8 ms limit is unchanged and is applied to that larger value.

`Tools/render_budget.py` requires all of the following:
- clock identity;
- complete monotonic endpoints;
- every positive native render duration contained in its interval.

It writes `render-budget-proof.json`. Raw `frames.csv` keeps the native values. Older collector reports keep their original meaning.

**Thermal.** `Benchmark-Series.ps1` runs the three prescribed captures back to back after a bounded same-player preconditioning phase, recorded in metadata. Each capture still measures 180 s / 10800 ticks. Thermal acceptance stays a dated review of the actual curves, never elapsed time alone.

## ADR-017 — Accepted: M0 owner waiver of remaining hardware validation

Date 2026-11-12. **Decision.** The owner (Max) closed M0 as **CLOSED WITH USER WAIVER**, on the strength of no perf glitches during daily use on the reference PC.

The waiver covers the outstanding render-budget and thermal-stabilization acceptance and further monitored reruns. It is acceptance rationale, not measured evidence. Native render-zero validity, thermal stability and two 93/104 ms preconditioning pauses remain unresolved.

**Consequences.**
- Historical results stay immutable.
- No automatic gate returns PASS because of this waiver.
- No threshold changes.
- Future performance gates keep their full requirements.

## ADR-018 — Accepted: initial P02 compiler format and byte-cache boundary

Date 2026-11-12. **Decision.** P02 introduces:
- SGAF v1 envelopes;
- five bounded field-by-field typed bodies;
- `shooter.content.manifest.v1`;
- `shooter.recipe.box-collision.v1`.

They are specified in [content format](content-format.md) with a [manifest schema](content-schemas/manifest.schema.json), a [recipe schema](content-schemas/box-collision.schema.json) and golden vectors. `content-format-lock.json` pins them, and `Tools/verify_content_format.py` rejects drift.

`Shooter.Content.Runtime` depends on the BCL-only `Shooter.Generation.Core` format and identity helpers, so there is no second codec. Runtime never references the Editor compiler.

**Publication.**
- A per-key OS-exclusive lock.
- Durable staging.
- An atomic same-volume rename, followed by an atomic index switch.
- Corrupt or conflicting caches fail and are preserved.

**Runtime leases** hold complete verified SGAF files with bounded, coalesced residency.

**Consequences.** Incompatible format changes need a version bump, migration and fixtures first.

## ADR-019 — Accepted: additive M1 feature recipes and reproducible source-byte lock

Date 2026-11-12. **Decision.** Feature generators (weapon, material) live outside the frozen P02 folders. `ValidatedRecipe` validates against an explicitly composed schema and checks that the canonical source matches its document before hashing. An additive compiler overload accepts validated feature recipes, and the box constructor is unchanged. Weapon and material schemas and generator versions are registered independently of the disk format ([weapon baseline](weapon-recipe-baseline.md)). The P04 mask A channel stays 255.

**LF lock correction.** Eight P02 lock hashes had been computed from CRLF working files. They were corrected to the committed LF bytes after a byte-for-byte check, and no content changed. Locks hash LF repository bytes.

## ADR-020 — Accepted: versioned first visual corrections for weapon and metal generators

Date 2026-11-12. **Context.** The first gallery renders showed a disconnected rifle magazine and heavy painted-metal wear, although the numeric tests had passed.

**Decision.** `geometry.weapon` and `material.surface` move to 1.0.1:
- the magazine and trigger connect numerically;
- collision proxies follow the magazine;
- metal wear becomes sparse fine scuffs.

Sockets, units, seeds and schemas are unchanged.

**Migration.** Fixtures record only the generator-version change ([rifle 1.0.0](feature-fixtures/rifle-1.0.0.json) → [1.0.1](feature-fixtures/rifle-1.0.1.json), [metal 1.0.0](feature-fixtures/metal-1.0.0.json) → [1.0.1](feature-fixtures/metal-1.0.1.json)). Validators reject 1.0.0. Old caches stay immutable.

## ADR-021 — Accepted: initial material benchmark camera and central-cube clarification

Date: 2026-11-13. Owner: M1 integrator under the renewed M1 assignment. B01-materials-v1 has no accepted production captures yet. Its original description left the seven camera views unspecified and placed an added cube exactly over the original B01-minimal cube. Resolve this before the first implementation: retain the original 10×10 m floor and original gameplay cube collider, replace the original cube visual with the central plaster cube, and render seven material cubes total at X−6..6 step2, Y0.5, Z0. This prevents coplanar overlap. The workload renders96 triangles (seven cubes and floor); it adds no weapon or soldier. B01-minimal-v1 remains unchanged.

Each view holds for360 fixed60Hz ticks at camera (familyX,1.65,−6)m, yaw0/pitch0, then cuts to the next family in the documented order. The cycle is2520 ticks/42 seconds with no interpolation. Camera configuration and exact content manifests enter route/content identity. Only verified compiled material caches may be used; the gallery is still not a benchmark substitute. Existing C0 fixtures, old results, SGAF payloads and toolchain remain immutable. Full three-run release/resource/thermal acceptance remains mandatory; implementing this route does not pass it.

## ADR-022 — Accepted: versioned soldier spike and second numeric motion baseline

Date 2026-11-13. **Decision.** The 23 generation-local semantic joints are frozen as `soldier.semantic23.v1` before any dependent motion work. SGAF skeletons are visual artifacts only: the compiler and the soldier generator both reject a gameplay-profile skeleton. C0 and SGAF v1 are unchanged.

**Iteration 1** (`soldier.loft` 1.0.0 / `motion.spike.v1`) failed on foot reach/slide and aimed reload, even though the transforms were finite. Image capture was corrected to bake the real skinned mesh.

**Iteration 2** (`soldier.loft` 1.0.1 / `motion.spike.v2`) changed only versioned procedural parameters:
- walk/run duty 0.50/0.35;
- moving pelvis 0.78/0.76 m;
- reload belt target Y +0.075 m;
- a neutral-down shoulder task frame;
- fuller body transitions.

It was not a weakened threshold. Version 1.0.0 is rejected, and its caches stay immutable.

**Consequences.** Two failed iterations require a scope review ([soldier baseline](soldier-baseline.md)).

## ADR-023 — Accepted: owner-authorized third bounded body-clearance iteration

Date 2026-11-13. **Context.** Iteration 2 failed on persistent trigger-arm/torso penetration, even though the grip target was met exactly.

**Decision.** The owner authorized exactly **one** more focused iteration. It covers:
- weapon placement;
- stock and shoulder;
- eye/sight alignment;
- arm/body clearance.

Joints, sockets, ranges, limits and the art rules stay unchanged. This is not acceptance and not unlimited expansion.

**Outcome.** The candidate (`soldier.loft` 1.0.2 / `motion.spike.v3`) still **FAILED**: about 2.18 mm forearm/strap contact, and eye alignment unresolved. Its parameters and residuals are in the [soldier baseline](soldier-baseline.md#third-bounded-clearance-candidate).

## ADR-024 — Accepted: P05 soldier visual scope stop after exhausted clearance iterations

Date 2026-11-13. Scope decision.

**Decision.** After iteration 2 and the authorized third candidate both failed, the owner directed the remaining M1 work to proceed without a fourth iteration. **P05 soldier clearance/parameter search stops.**
- All `soldier.loft` 1.0.0–1.0.2 and `motion.spike.v1`–v3 outputs stay as non-accepted diagnostic evidence.
- P05 visual/clearance acceptance stays **FAIL**. The numeric pose, grip and harness passes do not override it.
- No tolerance is relaxed.

Unlimited iteration, treating numeric passes as visual PASS, and deleting evidence were all rejected.

**Consequences.** Future soldier work needs a new explicit owner authorization and a new versioned candidate ([soldier baseline](soldier-baseline.md#scope-stop)). ADR-094 later reopened the search; it failed. ADR-095 then resolved the numeric gate, and visual review is still Pending.

## ADR-025 — Accepted: M1 owner waiver of remaining hardware/resource validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M1 as **CLOSED WITH USER WAIVER** and asked the integrator to stand in for the outstanding human review.

Dispositions:
- **P01/P02:** PASS.
- **P03 silhouette/ADS:** PASS-with-limits. A greybox prototype, with no realism claim.
- **P03 LOD transition:** PASS on the numeric hysteresis harness.
- **P04 material series:** counters and thermal review were PendingHardware, waived for closure only.
- **P05:** stays FAIL (ADR-024).

`feature-content-lock.json` was corrected only for the committed LF bytes of `soldier-baseline.md`.

**Consequences.** No hardware gate returns PASS because of this waiver, and no threshold changes.

## ADR-026 — Accepted: M2 owner waiver of remaining hardware/reference validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M2 as **CLOSED WITH USER WAIVER**.

What passed:
- the C01–C04 motor/weapon/pose/view adapters;
- the pure and Unity V05/V06/V09 scenarios;
- client/server builds and smoke.

A PresentMon diagnostic stayed PendingHardware. The full three-run capture could not run because no live sensor log was available. Reference measurement is waived for closure only.

**Consequences.** No eight-bot B03, thermal or performance claim follows. P05 stays FAIL.

## ADR-027 — Accepted: M3 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M3 as **CLOSED WITH USER WAIVER**. The pure gates passed:
- W01 planner connectivity, borders, spawn LOS and routes;
- W02 nav/revisioned adapter and profile independence;
- W03 streaming;
- W04 eight-bot AI within the 16-ray / 2-path budget;
- W05 30-minute pure soak;
- the B02/B03 freeze (`Benchmarks/b02-b03-m3-freeze.json`).

The locked editor was missing on that worker. Unity tests and builds stayed Pending, and reference B02/B03 stayed PendingHardware. Both are waived for closure only.

## ADR-028 — Accepted: M4 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M4 as **CLOSED WITH USER WAIVER**. These passed (pure):
- O01: Compound Clear objective and restart.
- O02: HUD 720p at 150%, focus, rebind, contrast and reduced motion.
- O03: analytic FX pool and ≤24-voice synth with limiter.

O04/O05, the native-720p three-run captures, the 30-minute release soak and signed player evidence, stayed PendingHardware. They are waived for closure only, because the locked editor was missing on that worker.

## ADR-029 — Accepted: M5 Unity Transport behind INetworkChannel; custom prediction retained

Date 2026-11-13; recorded by the Cursor M5-M7 integrator (bc-05dea339). Spike compared the existing pure INetworkChannel port surface against Unity Transport (UTP) and NGO-style anticipation.

Decision: pin **Unity Transport behind INetworkChannel** for remote peers. Keep **custom** 256-tick prediction/reconciliation and remote interpolation in Shooter.Networking. **Reject** treating NGO/Netcode anticipation as FPS rollback/replay. Netcode for Entities remains a separate ECS migration ADR if ever pursued.

Measured software evidence (pure): loopback duplex, seeded impairment simulator, codec fuzz/truncation, handshake reject-before-spawn, command ownership/tick/rate validation, prediction p95 ≤0.1 m at 100 ms/1% loss on static plane, bounded snapshot interpolation/resync. Wire protocol **N1-v1**. Shipping composition rejects the impairment fake; LoopbackNetworkChannel is a real local/dedicated adapter.

Exact UTP package version remains **null** in 	oolchain-lock.json until the locked Unity 6000.3.28f1 editor verifies and pins it; no package upgrade is introduced by this ADR. C0-v1 unchanged.

## ADR-030 — Accepted: M5 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M5 (network foundation) as **CLOSED WITH USER WAIVER**. These passed (pure):
- N01: transport selection (ADR-029); the UTP version was not verified.
- N02: session, commands and codec fuzz.
- N03: prediction and reconciliation.
- N04: snapshots and interpolation.
- N05: two-peer impairment.

Unity process-level and hardware evidence was waived for closure only. Networking was later parked by ADR-076.

## ADR-031 — Accepted: M6 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M5-M7 integrator (bc-05dea339). After pure software gates for S01–S05 / T021–T022 PASS, Max’s standing direction (“do all next milestones”; no confirmations; hardware PendingHardware may be waived with an explicit ADR) authorizes administrative M6 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M7 before this close is recorded, reopening P05, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity 6000.3.28f1 editor binary remained absent; EditMode/PlayMode, IL2CPP four-peer builds, 30-minute B04 PresentMon soak could not run. Status **Pending / PendingHardware**. Evidence: Artifacts/m6-network-slice/owner-closure-20261113/hardware-attempt.json.
- Pure B04 software soak (30s proxy at 150 ms/3% loss, four peers) PASS; full 30-minute separate-host B04 remains PendingHardware.

Close M6 administratively as **CLOSED WITH USER WAIVER**. Seven of eight milestones are closed. Actual dispositions retained:

- S01 historical hit query PASS (pure).
- S02 authoritative fire ordering/trade PASS (pure).
- S03 late join/resync/disconnect/fresh session PASS (pure).
- S04 four-peer B04 software soak PASS; 30-minute hardware soak PendingHardware.
- S05 exposure review PASS (LAN/private; N4 public deferred).
- Unity/hardware remain **Unverified / waived for M6 closure only**.

Historical failed/pending results immutable. ADR-017/024–030 unchanged; P05 FAIL retained. M7 may start after this close.

## ADR-032 — Accepted: M7 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13. **Decision.** The owner closed M7 as **CLOSED WITH USER WAIVER**, the last of the original eight milestones. Dispositions:
- **H01** reproducibility locks/provenance: PASS.
- **H02** regression matrix: PASS_WITH_PENDING.
- **H03** rollback ledger: PASS.
- **H04** 60 FPS stretch: PendingHardware, with baselines not relaxed.
- **H05** deferred-scope ledger: recorded.

The clean Unity build and player rehearsal stayed Pending (no editor on that worker). T025 stays out of scope, and P05 stays FAIL.

## ADR-033 — Accepted: next offline playable product scope and staged delivery

Date 2026-11-13. **Context.** The original milestones closed under waivers without an integrated team-match player.

**Decision.** Plan the following:

| Milestone | Delivers |
|---|---|
| M8 | Runtime composition |
| M9 | Team Deathmatch |
| M10 | Maps and shell/settings |
| M11 | Playable release acceptance |

The product:
- one local human plus bots on both teams;
- two generated maps (later three, ADR-071);
- 150 enemy kills or 15 minutes;
- immutable results;
- persistent applied settings.

Team sizes were 5v5/6v6, later 5–15 (ADR-101). The rules are in [Team Deathmatch](features/team-deathmatch.md), [maps](features/tdm-map-layouts.md) and the [shell](features/game-shell.md).

A separate pure Game-v1 boundary holds product, session, results and settings semantics. It was frozen by T035 (ADR-034). C0, content v1 and N1 stay intact.

**Consequences.** Offline play needs no transport, imported art or package upgrade. Old waivers and the P05 FAIL stay. New exits need real player, visual and hardware evidence ([acceptance](acceptance.md)).

## ADR-034 — Accepted: initial Game-v1 freeze (T035)

Date 2026-11-13; accepted by the Cursor M8 integrator (bc-b005c261) under owner Max direction to implement next milestones. Freezes compiling `Shooter.Game.Contracts` (`Game-v1`) separately from C0-v1: session/map/settings ports, participant/life/results DTOs, TDM score/time constants (150 / 54000), and executable fixtures under `docs/game-fixtures/` locked by root `game-contract-lock.json`. C0-v1, SGAF/content-v1 and N1-v1 remain unchanged. New workloads `TDM-5v5-v1` / `TDM-6v6-v1` / `RUN-compound-vertical-v1` do not replace B01–B04.

Also records RUN00 repair: missing Unity `.meta` for M3–M7 sources generated with unique GUIDs; unapproved Assets Markdown relocated to `docs/notes/procedural-viewmodel-editmode.md`. Source provenance audit PASS after repair. This ADR does **not** accept Unity/player/hardware gates, close M8–M11, reopen P05, or authorize package upgrades.

## ADR-035 — Accepted: verified release map packages (T055 boundary)

Date 2026-11-14. **Context:** release players regenerated maps from seeds, SGAF Collision v1 carried no stable collider IDs, navigation had no shipping format, and catalog visual hashes hashed labels.

**Decision.**
- **Stable IDs.** SGAF Collision v1 and C0 stay frozen. Collider and surface identity is carried in canonical JSON `shooter.map-metadata.v1`, which binds every decoded primitive (in body order) to a nonzero `colliderStableId`, a `surfaceId` and a matching `surfaceTag`. Mismatched or duplicate metadata rejects the package.
- **Package root.** `shooter.map-package.v1` is the catalog root. Catalog gameplay and visual roots are SHA-256 of the canonical root JSON, which enumerates exact dependency hashes. Unknown, incomplete or toolchain-mismatched packages are rejected.
- **Navigation.** Shipping nav is adapter format `unity.navmesh.bake.v1`: an explicit little-endian triangulation blob with provenance ([content format](content-format.md)), baked against installed authoritative collision. Release players load it and never re-bake.
- **Recipes.** `map.compound`, `map.depot` and `map.package` are registered in `content-format-lock.json`, with schemas in `docs/content-schemas/`.
- **Depot seed.** The catalog seed is `460072031`. The seed `42` is robustness corpus only.
- **Publication.** ADR-018 atomic rules apply, and cold, warm and forced builds reproduce identical bytes. Corruption never substitutes another map. Collision, nav and metadata are installed before any actor spawns.

**Consequences.** Generator or seed changes need a new version, new roots and reviewed fixtures ([maps](features/tdm-map-layouts.md)).

## ADR-036 — Accepted: UserSettings schema 2 shell coverage (T060 / FIX09)

Date 2026-11-14. **Decision.** `GameContractVersion.SettingsSchema` moves to 2 (`shooter.user-settings.v2`, bounded UTF-8 JSON ≤64 KiB). It covers every [shell](features/game-shell.md) preference:
- **audio:** master, effects and UI;
- **video:** quality, resolution ≥1280×720, display mode, VSync, FPS cap, hip FOV 70–100°;
- **controls:** sensitivity 0.01–1.00, invert Y, and a complete binding map with Escape reserved and duplicates rejected;
- **accessibility:** UI scale 1.0–1.5, reduced motion and flash, contrast 0.8–1.5, subtitles.

Schema-1 files migrate once.

**Storage:**
- missing file: defaults;
- corrupt file: quarantine plus defaults;
- newer schema: in-memory defaults, never overwriting the file;
- writes are atomic with `.last-good`;
- read-only storage: works in memory with a "could not save" message.

`IClientSettingsApplier` applies the full DTO and stages display changes with a 15-second Keep/Revert. `UserSettingsEditor` owns Apply, Cancel and defaults. Fixtures are refrozen in `game-contract-lock.json`.

## ADR-037 — Accepted: product TDM measurement workloads (T063 / FIX12)

Date 2026-11-14. **Decision.** `game-fixtures/tdm-workloads.json` (schema 2) gets reviewed workload identities for every map. Depot is never an alias of the compound IDs. The original rows were `TDM-5v5-v1` / `TDM-6v6-v1` on `training-compound` (seed 460072030) and `-depot-v1` on `depot-yard` (460072031), plus 30-minute soak IDs. Sizes were later extended by ADR-101 and the oil map by ADR-071.

Measurement rows require these counters:
- kills;
- active ticks;
- respawns;
- AI rays;
- path requests;
- shots;
- damage;
- leases;
- queries;
- content bindings;
- render frames.

`ProductTdmWorkloads` mirrors the fixture. Collector v4 captures the product composition, and `Tools/analyze_tdm_benchmark.py` analyses it. Match rules are never changed to improve scores. Reference captures stay PendingHardware without proof.

## ADR-038 — Accepted: persisted release catalog (T067 / B03)

Date 2026-11-14. **Context.** Release startup still rebuilt gameplay through planners and `MapPackageBuilder`.

**Decision.**
- `shooter.release-catalog.v1` is written atomically as `catalog.json` beside `index.json` at publish time. It holds:
  - the toolchain hash;
  - per-map display, mode, size and recipe identity;
  - the package roots;
  - the gameplay sidecar: collision/nav revisions and hashes, bounds, spawns and destinations.

  Schemas are in `docs/content-schemas/release-catalog.*`.
- Release Bootstrap loads verified bytes only and rejects mismatches. It never calls planners.
- Players resolve `Content/map-packages` and `toolchain-lock.json` relative to the install root. Missing or corrupt content enters a recovery screen with Retry and Quit, never a silent substitution.

## ADR-039 — Accepted: local authoritative sprint slide (T072)

Date 2026-11-14. **Decision.** Slide (подкат), on owner request, defaults to Left Shift + F. A local `LocalPlayerCommand` envelope (the C0 `InputCommand` plus SlidePressed) is consumed only alongside a fresh accepted command for the current human life. C0, N1, SGAF and the fixtures stay frozen. Presentation sends intent only.

**Motor:**
- **Entry conditions:** grounded, sprinting forward at ≥4.5 m/s, a fresh press with Sprint held, and no aim, crouch, fire, reload or jump conflict.
- **Motion:** 6.5 m/s entry, 4 m/s² deceleration, at most 45 ticks, a 60-tick cooldown, and the entry direction locked.
- **Shape:** the 1.2 m crouch capsule.
- **Ending:** Jump cancels, and leaving the ground or a wall ends the slide. It stands up only with headroom. Holding F never retriggers. Death or reset clears it.

**Settings:** an additive optional `Slide` binding (F). Legacy files gain it when F is free, and Unbound otherwise. The schema stays 2, and a new fixture is refrozen.

## ADR-040 — Accepted: LF line-ending identity for frozen locks

Date 2026-11-14. **Context.** Thirteen lock entries in `content-format-lock.json` and `game-contract-lock.json` had been frozen from CRLF working copies, so every LF checkout failed.

**Decision.** Lock hashes are computed over canonical repository LF bytes. Only entries whose old hash equalled the SHA-256 of the LF→CRLF file were replaced; no content changed. Working copies must stay LF. A tool that writes CRLF must be fixed, not refrozen.

**Addendum (2026-11-15).** `user-settings-last-weapon.valid.json` was refrozen to its LF hash under the same guarded rule.

**Addendum (2026-11-22).** `7c0aa69` (ADR-122) froze `docs/game-fixtures/tdm-workloads.json` in `game-contract-lock.json` and the docs manifest from a CRLF working copy (`c2a5bc56…9512`). As a result, the first hosted CI run on a clean clone failed `verify_docs.py` and `verify_game_contract_lock.py`. The old hash equalled the LF→CRLF hash of the committed blob, so the entry was refrozen to the LF value `8342cdb7…6908` with `verify_game_contract_lock.py --refreeze ADR-040`. Only that entry changed. The 33 working copies left as CRLF were normalized to LF, and none of their blobs changed.

## ADR-041 — Accepted: Inno Setup per-user installer and in-place upgrade (T073)

Date 2026-11-14. **Decision.** The installer ([contract](features/windows-installer.md)):

- **Toolchain.** Inno Setup 6.7.3, pinned by `ISCC.exe` SHA-256 in `Tools/installer/installer-policy.json`.
- **Identity.** AppId `{B01E0F69-2123-4D91-AB59-88864A944E39}` is frozen forever.
- **Scope.** Per-user, x64, `{localappdata}\Programs\Shooter`, with a Start Menu shortcut and an optional desktop shortcut.
- **Versions.** The integrator supplies a monotonic `-ReleaseVersion`. Identities are installed as `installer-identity.json`. A downgrade is refused unless `/ALLOWDOWNGRADE=1`, which is reserved for rollback.
- **Payload.** The wrapper consumes a validated Package-Release directory with every inventory hash, and never rebuilds the game. Repair means rerunning the same installer. Stale shipped files are removed.
- **User data.** Settings in LocalLow are never touched.
- **Running game.** Interactive runs use the Restart Manager prompt. A silent run exits 1 if the game is running and 7 on locked files, before changing anything.

**Limits.** There is no atomic side-by-side swap, so a mid-copy crash can leave a mixed pair; rerunning the installer recovers it. The installer is unsigned. Evidence is under INS01–INS06.

## ADR-042 — Accepted: native C++ allocation counter for benchmark runs

Date 2026-11-14. Supersedes the ADR-015 managed allocation hook; its positive-control rule is kept.

**Context.** The managed IL2CPP allocation callback recursed when Unity attached the audio thread, which overflowed the stack (APPCRASH about 4 s after start).

**Decision.**
- Normal play installs no allocation hook. Only `-shooterBenchmark` runs do.
- The C++ plugin `Assets/Plugins/x86_64/ShooterAllocationMeter.cpp` is compiled into GameAssembly for Win64 IL2CPP only. Its callback only adds to atomic and thread-local counters, so it cannot recurse.
- C# reads it through `__Internal`. A failure gives -1, never zero.
- It must stay under root `Assets/Plugins/`.
- `Tools/provenance-allowlist.json` `namedNativeSources` pins its path and SHA-256.

**Consequences.** Any editor migration needs the signatures revalidated and the controls rerun. Never restore the managed hook.

## ADR-043 — Accepted: two-stage native navigation publication

Date 2026-11-14. **Context.** Publishing whole map packages from the Editor's Mono changed float rounding in `skeleton-soldier.bin`, so the IL2CPP player rejected every native package.

**Decision.** Mono never generates map content.
1. `Compile-GameContent.ps1 -Native` publishes pure .NET content first.
2. `ShooterBuild.CompileNativeGameContent` reads only the .NET `collision-gameplay.bin`. It bakes the native NavMesh twice and requires byte-identical results.
3. It writes `<map>.nvb1` with a `nav-stage.json` descriptor.
4. MapContent republishes with those bakes through `NavBakeStage`. `NavBakeStage.InputKey` binds each bake to the exact gameplay box bits and nav parameters, and any mismatch fails.

**Guard.** Native packages must equal the pure ones byte-for-byte except for the navigation entries. Release checks start a real match on native content.

## ADR-044 — Accepted: versioned procedural map surface publication

Date 2026-11-15. **Context.** Map packages published flat 1×1 albedos and dropped the normal and mask companions.

**Decision.** Publish four 128 px, two-metre material sets (concrete, metal cover, soil, asphalt) through `MaterialBaker`, with full mips. All companion dependencies are required in visual packages. URP Lit takes metallic from R, occlusion from G and smoothness from 1−B.

`map.compound` and `map.depot` move to 1.0.1, so package and catalog roots change while collision and nav stay the same. Strict readers reject 1.0.0. The old binary and content pair is kept as the rollback. Superseded for surfaces by `material.noise` (ADR-088, ADR-106, ADR-116).

## ADR-045 — Accepted: compact TDM arena layouts for catalog maps (T076)

Date 2026-11-15. Owner assignment: redesign maps for 5v5/6v6.

Context: both catalog maps used the full 256 × 256 m frame (Compound: 12 identical shells and 120 1 m cubes on a grid; Depot: six bays and perimeter fences). Spawn-to-spawn distance exceeded 200 m with no lane structure, which is far too sparse for 10–12 players.

Decision: catalog gameplay comes from new arena planners (`CompoundArenaPlanner`, `DepotArenaPlanner`) through `ProductMapPlanner`. Each map is a mirror-symmetric (z = 0) three-lane arena of 88 × 104 m and 80 × 112 m inside a closed 5 m perimeter, with walled staging, three gates and baffles. `ArenaRules` enforces the numeric rules in [compact TDM map layouts](features/tdm-map-layouts.md); `MapPackageBuilder`, `ReleaseCatalogDocument` and `CompoundCatalogFactory` throw when rules or `MapTopologyValidation` fail. Gameplay collision/nav hashes come from the actual box list. Map ids, display names, seeds, the recipe/package/metadata schemas, `MapRecipe.GeneratorVersion` (1.0.1 after ADR-044), SGAF Collision v1, nav settings, the 256 m world/32 m chunk frame and every lock are unchanged; arena planner hash versions are 2.0.0. The legacy W01 `CompoundPlanner`, `DepotPlanner` and their goldens stay for historical evidence only.

Consequences: all map package identities (collision, navigation, metadata, package roots) change and must be republished with `Compile-GameContent.ps1 -Native`; earlier packaged installers carry the old layouts. Rule validation proves geometry constraints only; route feel, spawn pressure under `SpawnSafetyPolicy` and bot behaviour on the new layouts are Pending manual playtest. Rollback: revert the T076 merge and republish.

## ADR-046 — Accepted: offline product client recovers from frame hitches

Date 2026-11-15. Context: the installed 0.3.5 player quit after Resume with `Offline tick overload; simulation paused` because one frame of about 83 ms or more exceeded the four-tick cap and `FoundationEntry.Fail` exits the player. Resume also gave no rebase frame.

Decision: `FixedTickClock` gains an opt-in `dropHitchDebt` mode, enabled by `FoundationEntry` only for the non-server, non-diagnostic, non-benchmark product client. When a frame leaves whole-tick debt after four ticks, the clock drops that debt, keeps the fractional remainder, never sets `Overloaded`, and `FoundationEntry` logs one `SHOOTER_HITCH` warning (elapsed, dropped debt, serviced, nextTick). Tick numbers stay contiguous and ordered. Diagnostic, smoke, gallery, benchmark and failure-injection runs and the server keep the ADR-prior retained-debt fatal overload, so `Tools/Test-FailureShutdown.ps1` semantics and benchmark validity are unchanged. After a suspend-to-run transition (including `SetSuspended(false)` from Resume) the first `SampleActiveElapsed` returns zero.

Consequences: a long hitch costs simulated time (the game runs slower for that moment) rather than the session. Hitch frequency is recorded only in the player log; the cause of slow frames remains unexplained and benchmark evidence is unaffected. Rollback: construct the clock with `dropHitchDebt` false.

## ADR-047 — Accepted: relaxed spawn fallback and recoverable spawn failure

Date 2026-11-15. **Context.** Stationary bots could keep every spawn slot exposed. The player then sat on "Awaiting a safe spawn", and when the 600-tick deadline expired the match stopped silently while still Playing, which soft-locked the game.

**Decision.** After `SpawnSafetyPolicy.SpawnRelaxAfterTicks` (120 active ticks) of failed safe search, the 40 m enemy line-of-fire rejection is dropped. The policy then picks a navigable, unoccupied slot with 2 m spacing, ranked by:
1. farthest nearest enemy;
2. least recently used;
3. lowest slot ID.

The 30-tick retry and the 600-tick deadline are unchanged. The match aborts only when no navigable, unoccupied slot exists. The abort records `GameErrorCode.SpawnFailure` in the non-frozen `TerminalFault`, and the shell returns to the menu with a message. Game-v1 is unchanged.

**Consequences.** A relaxed spawn may be in enemy line of fire, accepted in exchange for no soft-lock ([Team Deathmatch](features/team-deathmatch.md#death-and-respawn)).

## ADR-048 — Accepted: session-internal pre-match countdown

Date 2026-11-15. Status: accepted for the offline product build; Unity EditMode/PlayMode and real-player confirmation are Pending.

Context: the product shell needs a short readiness window after a match begins, so the player can orient before bots act and the clock runs. Adding a `GameMatchPhase` value would change the frozen Game-v1 surface and every consumer of the phase.

Decision: `SessionDefaults.PreMatchCountdownTicks` = 300 (5 s at 60 Hz), read through `OfflineGameSession.PreMatchTicksRemaining`. The countdown is internal to the session. The phase stays Playing, and no `GameMatchPhase` value or Game-v1 change is added. During the countdown the human's local input gate is closed, bots are not planned and issue no commands, no respawns are scheduled, and `EvaluateEndOfTick` does not run, so `ActiveTicksElapsed`, the match clock and the timeout stay at 0. The harness still steps with no commands, so gravity and settling continue. The HUD shows "MATCH STARTS IN N" with N = ceil(remaining/60), then "FIGHT!" during the first second of the live clock. The `OfflineGameSession` default is 0 ticks. Only `ProductShellHost` sets 300, so tests, benchmarks, `ProductTdmHost` and `ProductTdmBenchmark` are unchanged.

Consequences: the countdown adds 300 ticks to CompletedTick before the 54000-tick match clock starts, so tick numbers for a match are offset by 300 relative to the old start. Consumers that assume CompletedTick equals active time must use ActiveTicksElapsed. Tests: `PreMatchCountdownTests` (4). Rollback: set the product value to 0. Real-player timing and HUD readability are Pending.

## ADR-049 — Accepted: non-gameplay visual architecture boxes for map solids (T078)

Date 2026-11-15. Status: accepted; source merged to main at `ddce776`; package republish and player visual review Pending.

Context: catalog arenas already emit non-gameplay roof slabs, but `MapPackageBuilder.BuildSolidsMesh` skipped `!Gameplay` boxes, so roofs never appeared. Places still read as featureless collision boxes versus the VD5 / Battlefield V environment bar. Expanding gameplay boxes would churn collision/nav identities and ArenaRules ceilings.

Decision: planners may append **non-gameplay** `CollisionBox` architectural details (eaves, door jambs/lintels, window frames, baseboard/caps, curbs, canopy lips, thin industrial trim) with dedicated stable-id ranges that do not collide with gameplay ids. `BuildSolidsMesh` includes every box (gameplay and visual) so roofs and details render. Gameplay boxes (`Gameplay == true`) remain byte-identical in order, center, size, tag and stable id; collision SGAF, nav settings/bake inputs derived from gameplay boxes, spawn/topology rules and ArenaRules ceilings are unchanged. Presentation may strengthen packaged material contrast and cover overlays without importing art. Weapon generator `geometry.weapon` 1.0.1 and soldier loft/P05 stay frozen under ADR-024.

Consequences: packaged map-solids mesh and package/catalog visual identities change and must be rebuilt with `Compile-GameContent -Native` after merge; prior installers remain valid rollback pairs. Visual box count is bounded in tests (<800 extras/map) but is not an ArenaRules ceiling. Rollback: remove visual append + restore gameplay-only `BuildSolidsMesh` filter. Named environment acceptance (VD5) remains Pending.

## ADR-050 — Accepted: packaged architectural surface construction (T081)

Date 2026-11-15. **Decision.** `MapSolidsMesh` adds offline packaged visual detail around the existing gameplay boxes:
- 15 mm edge radii;
- wall footing, coping, end strips and panel joints, at most 20 mm proud;
- cargo corrugation, rails and door hardware, at most 51 mm proud.

Openings are never bridged. The budget is 180,000 vertices per map. Gameplay boxes, collision, nav, seeds, spawns and hit poses stay byte-identical. Map material roles 3–5 (DepotBlue, OxidePaint, Foundation) are added; SGAF already carries ushort slots. Unknown roles fail closed. Visual hashes change, so a native republish is required.

## ADR-051 — Accepted: runtime cover probes share the AI ray budget (T082)

Date 2026-11-15. **Context.** Product map packages carried no cover data, so `SeekCover` never found any. Adding cover data would have changed Game-v1 and every content identity.

**Decision.** `Simulation/AI/CoverFinder.cs` generates cover at runtime:
- **Sampling.** Up to 8 points 4–7 m away, within ±80° of the direction away from the threat.
- **Qualifying.** The first point where a ray from the threat's eye to a crouched chest is blocked wins.
- **Budget.** Probe rays use `PerceptionSystem.TryBudgetedRay`. They share the global 16-ray budget, run after vision, and are capped at 4 per tick.
- **Reachability.** It comes from the normal path request.
- **Claims.** Quantized ids in `CoverReservationBoard`, released on leaving, on path failure, on death and on cancel.

Explicit `CoverSite` lists still take precedence. Authored map cover later arrived with ADR-112.

## ADR-052 — Accepted: packaged first-person glove and sleeve props (T083)

Date 2026-11-15. **Decision.** Each visual closure gets four required SGAF meshes: rifle/pistol × trigger/support glove and sleeve. They are built offline by `FirstPersonHandGeometry` from numeric palms, fingers and cuffs (≤6,000 vertices per hand), and they bind the existing weapon grips. Material slots: 0 sleeve, 1 glove, 2 trim. No soldier joint, hit pose or socket changes. Missing hand dependencies fail closed. Server profiles have no hand assets.

Support-hand reach is an analytic offset that follows the reload phase, with continuous endpoints. Superseded for geometry by the articulated `fp.hand.v2` (ADR-069) and the kernel gloves (ADR-106).

## ADR-053 — Accepted: procedural near-field finish triplets and hip composition (T084)

Date 2026-11-15. **Decision.** `FirstPersonSurfaceBaker` builds four 128 px, 100 mm tiling PBR triplets: coated metal, stippled polymer, woven cloth and glove grain. They use `MaterialBaker.BuildPlane` mips, and `material.surface` is unchanged. They are required visual dependencies, bound through `ProductSurfaceMaterial` at 10 repeats per metre.

The sleeve becomes a continuous ring loft with folds. Hip composition gets a bounded yaw and roll that fades to exactly identity at full ADS, which keeps the sight ray. Gameplay, weapon and soldier identities are unchanged.

## ADR-054 — Accepted: bot footstep hearing, wider vision and a readable lethality envelope (T085)

Date 2026-11-15. **Context.** A playtest found that bots trailed each other, missed enemies behind them and killed too fast to read. Weapon numbers are shared with the player, so lethality has to be tuned in bot aim.

**Decision.**
- **Footstep hearing**, from planner-derived speed: 24 m sprinting, 13 m running, 5 m walking, and always within 2.5 m. It uses no rays.
- **Vision cone:** 120°.
- **Unseen attackers.** Two hits within 1.5 s from an enemy the bot is not fighting, or one hit below 50 health, turn the bot onto that attacker.
- **Accuracy** recovery starts at the end of the reaction delay. A regression test pins the median time-to-kill on a still target: 1.25–2.3 s at 8 m, 1.8–4 s at 20 m, 2.7–7 s at 40 m, and no kill within 0.67 s of the first shot.
- **Spread.** Destinations are chosen by a deterministic spread score. At most 2 allies respond to one noise.

No contract, budget or weapon change ([combat AI](features/ai.md)).

## ADR-055 — Accepted: generated PCM combat sample banks (T086)

Date 2026-11-15. **Context.** Shipping audio was a sine and noise synth that sounded like beeps.

**Decision.** Presentation may ship a deterministic PCM combat sample bank. Found PCM is allowed when the manifest records its source and hash. Commercial licensing is not required for this non-commercial build. Audio extracted from commercial games is forbidden.

`SynthAudioMixer` plays samples with:
- distance attenuation;
- pan;
- HF rolloff;
- a limiter;
- master, effects and UI gains;
- ≤24 voices.

A degraded analytic fallback keeps play going if the bank fails. Extended by ADR-062 and ADR-096 ([audio](features/audio.md)).

## ADR-056 — Accepted: last-weapon quick switch (Q) and local-only muzzle overlay

Date 2026-11-15. **Decision.**
- **Muzzle overlay.** The full-screen muzzle overlay and the "Shot" subtitle apply only to the local human's `ShotAccepted` (`FirstPersonHitFeedback.IsLocalShot`). World muzzle proxies and audio still play for everyone.
- **Last weapon.** An additive optional `LastWeapon` binding (Q) swaps to the previous slot through the existing C0 `SelectedSlot`. On life reset the previous slot starts as the opposite slot. A held absolute slot key wins in the same frame. Legacy settings gain Q when it is free, and Unbound otherwise. The schema stays 2.

## ADR-057 — Accepted: knife and grenade loadout slots (T087)

Date 2026-11-15. **Decision.** C0 stays `C0-v1`, but `PredictionSchema` moves 1→2.
- `Require.Slot` accepts 0..3: rifle, pistol, knife, grenade.
- Prediction holds four `WeaponState` records.
- `WeaponAction.Cooking` = 5 is added.
- The fixtures (`prediction-state.json`, `gameplay-defaults.json`) and `contract-lock.json` are migrated.

**Knife:** 1.8 m hitscan, 55 damage, a 24-tick interval, no ammo, can fire while sprinting.

**Grenade:**
- one per life;
- cook on press, throw on release;
- 120-tick fuse from the cook, and a held cook detonates at the thrower;
- a gravity arc using `ProjectileSpawned`;
- a 5 m falloff blast, up to 100 damage;
- self damage on, friendly damage zero;
- respawn refills it.

**Bindings and bots.** Slot3 and Slot4 (3 and 4) are additive bindings. Bots use the knife when dry at close range and their grenade once at medium range.

**Consequences.** The parked N1 wire format makes no claim of schema-2 compatibility.

## ADR-058 — Accepted: minimap self marker and temporary gunfire reveal

Date 2026-11-15; owner playtest reported the tactical map did not show the local player and asked that enemies who are shooting appear on the minimap. Sole integrator owns the bounded presentation/HUD/docs fix. No contract, sensing, AI, content-format or acceptance waiver.

Context: T074/brandbook required a north-up friendly-only map. The local marker was a `TacticalUi.Line` chevron inside `GUI.BeginGroup`, which commonly clips under IMGUI matrix draws so the self pip vanished while ally `Fill` squares remained. Enemies were never plotted.

Decision:
- Draw the local player with Fill-based center pip and Fill facing tick (no Line inside the map group).
- On enemy-team `ShotAccepted` (not local, not ally), store a short-lived presentation blip from `ShotPayload.Origin` XZ in a fixed 8-slot ring (2.5 s hold, 0.5 s fade). Refresh on repeated shots. Plot coral plus/diamond Fills inside the existing 42 m plate only while the blip is live.
- Silent enemies stay hidden. Do not stream live enemy poses, do not use AI Heard/LKP, and do not change combat authority or sensing.
- Brandbook / visual brief language becomes “friendly + temporary gunfire reveal” rather than absolute friendly-only.

Validation: pure `CombatHudVisual` filter/TTL/ring tests, Simulation suite. Interactive player review remains Pending. Rollback reverts the presentation ring and restores the prior Line chevron / friendly-only wording.

## ADR-059 — Accepted: personal scoreboard Score and combat score badges

Date 2026-11-15. **Decision.** An additive `uint Score` is added to Game-v1 `ParticipantState`, for the scoreboard and results only. Team kills and win rules are unchanged.

| Situation | Score |
|---|---|
| Valid enemy killer | +100 (`PersonalKillPoints`), and no damage sum |
| Other damagers of that life | Their accumulated HP |
| Environmental death | Damagers get HP points only |

Sort order: Score, then kills, then deaths ascending, then ParticipantId. A session-local award ring drives the procedural kill ribbon, centre feed and ammo +N badges. C0 payloads are unchanged, and `game-contract-lock.json` is refrozen.

## ADR-060 — Accepted: local authoritative tightness (stamina)

Date 2026-11-15; owner reported infinite sprint on held Shift and asked for a tightness scale that gates run, slide and jump, restoring fully in 5 s while walking. Sole integrator owns bounded motor / local movement view / HUD / tests / docs. No C0/N1/Game-v1 freeze, online prediction, settings binding or acceptance waiver.

Context: `docs/features/combat.md` deferred stamina until movement was stable. ADR-039 slide already proved the offline pattern: authority state on the motor, readout on `LocalMovementView`, C0 snapshots unchanged. Sprint remained uncapped.

Decision:
- Add authoritative `MotorBodyState.Tightness` in `[0, 1]` (spawn/respawn = 1). Recipe constants on `MotorConfig` (ADR-039 style): continuous sprint empties in **8 s**; walk regen fills in **5 s**; jump costs **0.25**; slide entry costs **0.40**. Slide cooldown from ADR-039 stays independent.
- Gate in `KinematicMotor.Step`: sprint speed/`SprintActive` only while tightness &gt; 0 after this tick’s jump/slide costs; jump and slide entry require their flat costs; empty pool → walk only.
- Regen when grounded, not sliding, and Sprint is not held (walk/idle/crouch-walk). Holding exhausted Shift stays at empty until release. No regen mid-air or during an active slide.
- Expose `LocalMovementView.Tightness01` for a compact combat HUD bar. Do not extend `EntitySnapshot` / `InputCommand` / settings schema.
- Bots share the motor gate automatically; no AI blackboard change required for correctness.

Validation: pure motor drain/regen/cost/exhaust cases in `Tools/Test-Simulation.ps1`; docs/manifest. Interactive feel and installer identity remain Pending. Rollback removes the motor field/gates and HUD bar.

## ADR-061 — Accepted: geometry.weapon 1.0.2 mechanical parts and denser fire FX (T088)

Date 2026-11-15. **Decision.** `geometry.weapon` moves 1.0.1→1.0.2, and 1.0.0 and 1.0.1 are rejected. Body LODs gain silhouette detail. The **magazine** and **cycle** (bolt or slide) become separate packaged meshes (`mesh-*-magazine.bin`, `mesh-*-cycle.bin`). The magazine follows the reload phase, and the cycle opens on `ShotAccepted` and closes in about 90 ms. Sockets, collision dimensions and C0/SGAF are unchanged.

`AnalyticFxPool` adds shell casings and tracers and a denser muzzle. Reduced flash suppresses the muzzle and the tracers.

## ADR-062 — Accepted: stereo found/authored combat WAV bank and per-kind distance (T089)

Date 2026-11-15; owner reported synthetic/beep combat audio and map-wide bot footsteps. Sole integrator owns Presentation/Audio, Content/Audio packaging, pure tests and docs. Extends [ADR-055](#adr-055--accepted-generated-pcm-combat-sample-banks-t086). No C0/Game-v1 freeze, no FMOD/Wwise, no commercial-game extraction, no acceptance waiver.

Context: T086 shipped a mono recipe bank with a soft `1/(1+0.05d)` atten and a single 80 m cull for every cue, so footsteps stayed audible across the map. Owner directed **stereo real-sample quality** (not MIDI/oscillator focus).

Decision:
- **Product path.** Ship interleaved **stereo 48 kHz** WAV clips under `Content/Audio/combat/` with `manifest.json` (kind, variation, file, sha256, source, license). Bank id `combat-audio-bank-v3-stereo`. Client/Package-Release copy the folder beside the player; WAVs stay outside `Assets` so they are not treated as imported art.
- **Mixer.** Play authored L/R with distance gain + HF rolloff and a **mild azimuth balance** (do not mono-collapse then pan). Generated stereo Foley remains emergency fallback when manifest/files/hash fail.
- **Distance.** Per-kind ceilings: footsteps **18 m** with steep atten + 0.45 base gain; reload/death/impact **35 m**; gunfire/headshot **80 m**; UI/ambient listener-local.

Validation: `Tools/Test-Simulation.ps1` audio cases; docs/manifest. FIX10 named device hearing and installer identity remain Pending. Rollback: revert T089 and restore the T086 mono/soft-distance path.

## ADR-063 — Accepted: hit-region damage coefficients (one-shot head)

Date 2026-11-15; owner requested clearer body-part lethality: headshot = death, chest needs several hits, limbs need more. Sole integrator owns `Simulation/Combat/DamageMath`, combat docs/fixtures sync and pure combat tests. No C0 region-ID change, no base rifle/pistol/knife/grenade damage or fire-rate change, no T085 bot-aim retune, no new hand/foot hit volumes.

Context: prior defaults were head×2 / torso×1 / limb×0.75 (rifle head 60 → two shots; limbs ~5). Owner feel target for rifle: **1** head / **3–4** torso / **6–7** limb shots on 100 HP. Region IDs remain Head=1, Torso=2, Arms=3–4, Legs=5–6; hands/feet share limb capsules.

Decision:
- Set `DamageMath.RegionMultiplier` to **head×4**, **torso×1**, **limbs×0.5**. Rounding rule unchanged (`floor(base×mult+0.5)`, min 1).
- Rifle outcomes: head 120 (one-shot), torso 30 (~4 shots), limb 15 (~7 shots). Pistol head also one-shots (100); pistol limbs take more shots — acceptable; primary feel is rifle.
- Sync [gameplay baseline](gameplay-baseline.md) and [combat](features/combat.md). Do not edit frozen C0/Game-v1 locks or `gameplay-defaults.json` weapon base damage.

Validation: `Tools/Test-Simulation.ps1` combat rounding cases; docs/manifest. Named player feel and matching installer remain Pending. Rollback restores ×2 / ×1 / ×0.75.

## ADR-064 — Accepted: knife/grenade FP special actions and throw presentation (T087 follow-up)

Date 2026-11-15; owner reported knife/grenade reused gun animations and grenades could not be thrown visually. Sole integrator owns bounded combat polish + presentation/FX/audio/Game.Contracts lifecycle field. No C0 event-kind change, no SoldierMotion melee choreography, no imported clips.

Context: ADR-057 shipped authority knife melee and cook/throw grenade, but FP still applied gun feedback (muzzle/rifle audio) and never rendered `ProjectileSpawned`, so the held grenade stayed in hand.

Decision:
- Authority: rising-edge cook/melee after equip (`fireHeld && !PreviousFireHeld`); clear `PreviousFireHeld` on equip→Ready so hold-through-draw still cooks/slashes once.
- Game-v1 additive `ActorLifecycleState.WeaponAction` mirrored from `EntitySnapshot.Weapon.Action` (refreeze `game-contract-lock.json`).
- Presentation: `FirstPersonSpecialActions` slash vs close-range stab (same-tick body Impact/Damage); grenade cook raise from `WeaponAction.Cooking`, throw arc + hide held prop on `ProjectileSpawned` / empty magazine; `CosmeticGrenadeFlight` gravity arc until nearby Impact.
- FX/audio: skip knife muzzle/overlay/gunfire reveal; map knife ShotAccepted to impact cue; soft UI cue on ProjectileSpawned.

Validation: Simulation knife/grenade/view/FX tests; Game lock verify; docs/manifest. Interactive player/installer Pending. Rollback restores prior lifecycle DTO/lock and gun-path FX for knife.

## ADR-065 — Accepted: screen-space PiP rifle scope (blurred periphery)

Date 2026-11-15; owner reported rifle ADS covered the whole screen with an opaque black tube. Sole integrator owns Presentation optic math, a lightweight blit presenter/shader, IMGUI housing/reticle and docs. No C0/Game-v1 change, no second optic camera, no URP Volume/DOF stack, no acceptance waiver.

Context: T088 shipped a procedural circular mask that filled outside the lens with near-black while blending the whole gameplay camera FOV tighter. That matched an analytic tube but not the desired picture-in-picture look (sharp zoomed lens, live blurred unzoomed world outside). Brandbook still forbids recursive render-to-texture optics.

Decision:
- **Single-camera screen-space PiP.** Keep hip FOV on the gameplay camera for rifle ADS. Magnification is digital UV zoom inside the lens (`ads/hip` ratio, same 42/80 cosmetic). Outside the circle: separable blur of the hip-FOV frame. Soft analytic housing rim in the composite + thin IMGUI ring/mil-dot. No opaque full-screen black fill.
- **Pistol unchanged.** Iron sights keep real camera FOV blend + edge vignette.
- **Accessibility.** `ReducedCameraMotion` keeps hip FOV and skips blur/digital zoom; housing/reticle may still fade in.
- **Shipping.** `Shooter/AdsScopePip` is preloaded beside `ProceduralSurface`; ADR-013 null `postProcessData` stays. Not a stock URP Volume feature.

Validation: Simulation pure optic tests; provenance for new shader/presenter; docs/manifest. Named player optic review remains Pending. Rollback restores black-tube `AdsScope` fills and whole-camera rifle FOV blend.

## ADR-066 — Accepted: non-commercial UI typeface bank (Barlow Condensed)

Date 2026-11-15. **Context.** ADR-002 allowed only the engine IMGUI font.

**Decision.** The non-commercial build ships Barlow Condensed (OFL) as Unity TrueType fonts in `Presentation/Fonts/Resources/`, loaded by `UiFonts`:

| Use | Weight |
|---|---|
| Display (≥22 px) | Bold |
| Body | Regular |
| Buttons | SemiBold |

The engine font is the fallback. `Tools/provenance-allowlist.json` `namedFontSources` pins the TTF hashes. The OFL text lives at `Content/Fonts/OFL-BarlowCondensed.txt` and in the release `LICENSES.txt`. The map visual closure records `fontException: ui-typeface-bank-v1`. Player-facing copy uses short military FPS language (`ShellUiCopy`, `InsertionScreenCopy`).

## ADR-067 — Accepted: soldier motion.v5 slide pose, gait stability and motor corner anti-launch

Date 2026-11-15. **Context.** Models flickered, sprint feet were bad, slide had no animation, and sprinting into wall/floor corners launched the actor upward.

**Decision.**
- **Motor.** After `SlideMove`, vertical velocity is clamped to ≤0 unless a jump ascent owns the tick. Non-walkable contact never adds upward velocity. `TryStep` requires a real rise.
- **Motion.** Motion moves to `soldier.motion.spike.v5`, which adds a cosmetic `Slide` dive pose and walk/run hysteresis (3.8 / 3.2 m/s).
- **Presentation.** Gait is driven from the authoritative `LocalMovementView` speed and distance, and the displayed feet blend toward the authority feet. Hit capsules stay tick-snapped.

## ADR-068 — Accepted: procedural arsenal visual profiles and finishes

Date 2026-11-15. **Decision.** `geometry.weapon` moves 1.0.2→1.0.3. Rifle and pistol use numeric section lofts, a slotted cage, fasteners, recesses, a muzzle collar and a faceted slide. The knife gets a ground blade, and the grenade an ovoid shell with spoon and ring.

Weapon material slots are:

| Slot | Material |
|---|---|
| 0 | Coated metal |
| 1 | Polymer |
| 2 | Machined steel |
| 3 | Recess/rubber |
| 4 | Olive enamel |

Three new 128² PBR triplets supply them through the visual closure. Sockets, collision, seeds and LOD ceilings are unchanged. Old recipes are rejected.

## ADR-069 — Accepted: articulated near-field hands and global analytic reflections

Date 2026-11-15. **Decision.**
- **Hands.** `fp.hand.v2` gives each of the four slots its own 17-joint, camera-only glove and sleeve rig. These are 8 skinned meshes plus skeletons in the visual closure, using the existing SGAF skin codecs. The fingers release and regrip through reloads, and the trigger finger follows the shot cycle. The magazine moves only after the support hand reaches it.
- **Weapons.** `geometry.weapon` moves 1.0.3→1.0.4 for selective bevels and a rounder barrel.
- **Reflections.** One generated 64² RGBAHalf mip-chained cubemap is the global reflection fallback outside the local probe. It is approximate, not a GGX prefilter.

## ADR-070 — Accepted: bot tactics tranche (cover peek, wall hearing, ledge strafe, route spread) (T090)

Date 2026-11-15. **Decision.** Tactics improve within the same budgets:
- **Cover peek.** Probes prefer a hidden point with a clear left/right peek. A blind but hidden point is the fallback. They share the ≤4 probe / ≤16 total ray budget.
- **Hearing through walls.** Gunfire uses a direct ear→source occlusion ray, outside the vision budget. Footsteps beyond 2.5 m use leftover budgeted rays at most once per bot every 12 ticks, failing open.
- **Ledge strafe.** A downward probe at the look-ahead foot flips or cancels the strafe on a drop of more than about 0.55 m.
- **Route spread.** Destinations within about 9 m of an ally's remaining path corners are penalized.

Budgets, determinism and the planner seam are unchanged ([combat AI](features/ai.md)).

## ADR-071 — Accepted: Oil Depot third map and limited elevated routes

Date 2026-11-15. **Decision.** A third catalog map is added: `oil-depot` (Oil Depot), using generator `map.oil` / `shooter.recipe.map-oil.v1` with seed `460072032`. Game-v1 is unchanged (`MapId` is a string). `WellKnownMaps.OilDepot` and the oil workloads are registered.

**Limited verticality:**
- Thin gameplay slabs (≤0.40 m) are walkable stairs and catwalks.
- `CompoundConnectivity` occupancy is Y-aware, so decks do not seal underpasses.
- Climbing uses layered pathfinding.
- Native and software nav bakes emit those slabs.
- The motor step and slope limits are unchanged.

**Addendum.** Compound roof slabs became walkable under the Y-aware occupancy. Only the B02/B03 `routeHash` in `Benchmarks/b02-b03-m3-freeze.json` was refrozen, with owner approval. The layout was later rebuilt by ADR-109.

## ADR-072 — Accepted: DefinitionId weapon profiles (catalog + visual bind)

Date 2026-11-15. **Decision.** **The slot is not the identity.** Slots 0..3 stay primary, sidearm, melee and throwable. Behavior and look resolve from `DefinitionId`:
- `WeaponCatalog` (Simulation) owns the combat definitions; `WeaponDefinition.FromId` delegates to it.
- `WeaponVisualProfiles` (Presentation) binds meshes and optics.
- `WeaponLoadoutPolicy` builds the four `WeaponState` records.
- Game-v1 `ActorLifecycleState` gains an optional `ActiveWeaponDefinition`.

`geometry.weapon` 1.0.5 requires `parameters.silhouette` (`standard` | `carbine`). There is still no fifth slot. Superseded in data form by ADR-077 (definition files) and ADR-102 (named arsenal).

## ADR-073 — Accepted: life-scoped multi-kill and headshot HUD badges

Date 2026-11-15. **Decision.** The session tracks a kill streak for the local human's current life. It resets on death and has no Score bonus. The internal `LocalScoreAward` ring carries the streak plus an optional `Headshot` award (0 points) when the fatal hit was to the head. The HUD shows:
- `ENEMY KILLED`, `DOUBLE KILL`, `TRIPLE KILL` or `MULTI KILL` (4+);
- an extra headshot feed row.

Score math, team rules, Game-v1 and C0 are unchanged. A +20 headshot Score bonus came later in ADR-097.

## ADR-074 — Accepted: compact 64 × 80 m arenas with distinct per-map layouts

Date 2026-11-15. **Context.** The owner found the maps too big, with every fight in the middle, and too alike.

**Decision.** Every arena is 64 × 80 m with anchors at z = ±34. `ArenaRules` thresholds shrink:
- playable 56–72 × 72–88 m;
- anchor separation 60–76 m;
- spawn→centre path 26–44 m;
- spawn→enemy path 60–100 m;
- sightline ≤48 m.

`ArenaLayout` gains `ArenaSymmetry` (MirrorZ or Rotate180). Free-standing separator walls are retired. Blocking geometry must be readable objects (`ArenaFeature` with `ArenaFeatureDressing`). The planners move to `environment.*.arena` 3.0.0 (oil 2.0.0). Map ids, seeds, schemas, SGAF and nav settings are unchanged, but package identities change.

Superseded for layout by ADR-109 site plans, which keep the 64 × 80 m frame.

## ADR-075 — Provisional: third-party code libraries for noise, runtime rigging and spatial audio

Date 2026-11-16. **Context.** ADR-002 bans imported visual *content*, not third-party *code* that turns numeric recipes into output. Pure folders must stay C# 9 / .NET Standard 2.1 and Unity-free, which rules out .NET 6+ physics and MonoBehaviour character assets.

**Decision.** Three libraries were considered. Each must pass its own pinned-version spike before it is adopted.

| Library | Use | Outcome |
|---|---|---|
| **FastNoiseLite** (MIT) | Noise sampling primitive in `Generation/Core` | **Accepted** |
| **Unity Animation Rigging** | Runtime IK correction | **Rejected** |
| **Steam Audio** (Apache-2.0) | Presentation-only HRTF and occlusion | **Provisional** (T094) |

**FastNoiseLite.** Accepted as a sampling primitive (T091/T092).
- **Precision.** Upstream `float` math differed by about 1 ulp between Mono and .NET. The vendored `Generation/Core/Noise/ThirdParty/FastNoiseLite.cs` is therefore a mechanical double-precision port, generated from the pinned upstream bytes by `Tools/port_fastnoiselite_double.py`. `--check` re-derives it, and hand edits are not allowed.
- **Usage.** Recipes use only `Generation/Core/Noise/NoiseField.cs`, with seeds from `NamedRandomStream` and every setting explicit. Tileable sampling blends lattice-shifted copies.
- **Provenance.** `Tools/provenance-allowlist.json` gains `namedThirdPartyCodeSources` and `namedNativeBinaries`. Any unlisted `ThirdParty/` file fails.
- **Evidence.** Golden values match bit-for-bit across lanes.

**Animation Rigging.** Rejected (T093). The spike worked, but adoption would have refrozen the toolchain hash in every lock. Pose corrections stay in the pure `TwoBoneIk` inside `SoldierMotion`. The follow-up gaps are:
- collision-backed `ISoldierGround` for remote soldiers;
- per-profile support grips;
- a bounded head look-at.

Corrected poses never feed Simulation, hit volumes or AI.

**Steam Audio.** Still Provisional (T094). It needs per-voice spatialization against gameplay collision, native binaries in the allowlist and the stereo mixer as fallback.

**Spike gates for any library:**
- the version is pinned;
- provenance passes;
- the pure suites and cross-lane goldens pass;
- the Client build passes;
- reference-laptop CPU is measured;
- a named review is done.

## ADR-076 — Accepted: offline-only scope and bounded local evidence

Date 2026-11-16. **Context.** The C: drive filled. `Artifacts/` held 37 GB of retained players, servers, packages and installers, and nothing pruned them.

**Decision.**
1. **Offline scope.** The product is offline TDM against bots until the owner decides on multiplayer.
   - `Networking` stays in git but is parked: no new code, and no Unity assembly references it.
   - Neither CI nor the release flow builds the Dedicated Server; `Build.ps1 -Target Server` still works on request.
   - No new `UNITY_SERVER` guards or transport hooks are added.
   - Simulation stays the single authority and Presentation stays read-only.
   - Server gates read as **Parked**, not Pending.
2. **One output per pipeline.** After a non-FAIL run, `Save-ShooterRun` (`Tools/Unity.Common.ps1`), `Package-Release` and `Build-Installer` delete older runs of the same action. `SHOOTER_KEEP_RUNS` raises the count. Small JSON reports are first copied to `Artifacts/reports/<action>/<commit>/<run>/`. A failed run never deletes the last good one.
3. **Scratch deleted.** Package rehearsal trees are deleted after success.
4. **Free-space floor.** Heavy wrappers refuse to start below `SHOOTER_MIN_FREE_GB` (default 5). `Tools/Disk-Report.ps1` reports usage.
5. **One-time cleanup.** About 33 GB was deleted.

**Consequences.** Handoffs cite reports and commits, not stored players. Run heavy wrappers one at a time.

**Architecture review addenda (2026-11-16):**
- **`material.surface` 1.1.0** samples its fields through `NoiseField`. Shipped recipes stay on 1.0.1 until reviewed.
- **IK.** `TwoBoneIk` is the only IK solver.
- **JSON readers.** The four readers (`JsonValue`, `MapJsonValue`, `UserSettingsJson`, `CombatAudioManifest`) stay separate on purpose, because each enforces a different policy.
- **Shared helpers.** Tools share helpers through `Tools/TestResults.ps1` and `Tools/shooterlib.py`.
- **Asmdef mirroring.** Each pure asmdef has its own `Tools/<Assembly>` project. `Tools/verify_asmdef_graph.py` fails on drift.
- **Moved code.** `Presentation/Model` holds the Unity-free presentation code. `World/Navigation` and `World/Streaming` moved under `World/Runtime`.
- **Pruning.** `MapPackagePublisher.PruneSuperseded` deletes package directories the new index no longer references, but only for maps, profiles and weapons included in that publish.
- **Deferred:** splitting the large classes, and moving map packaging out of `Generation/Core/Environment`.

## ADR-077 — Accepted: weapon definition files generate both weapon catalogs

Date 2026-11-16. **Context.** After ADR-072, adding a weapon still meant editing two parallel catalogs plus id switches for the kill feed and audio.

**Decision.** There is one file per weapon: `Content/Weapons/<name>.weapon.json` (`shooter.weapon.v1`). It holds:
- the id and order;
- the label and family;
- `combat`;
- `visual` (mesh key, recipe link, optic, magazine cycle, handling);
- `audio.shot`;
- `hud.killFeedGlyph`.

`Tools/generate_weapon_catalog.py` validates every file and writes `WeaponCatalog.g.cs` and `WeaponVisualProfiles.g.cs`. Numbers keep their exact JSON literal text, so the output is bit-identical. Enum values are read from the C# sources. `--check` runs in CI.

Profiles route the shot voice (`ShotVoice`) and the kill-feed glyph. Unknown ids fall back to the rifle ([weapons](features/geometry-weapons.md#adding-or-tuning-a-weapon-adr-077)).

## ADR-078 — Accepted: shared weapon package separate from map packages

Date 2026-11-16. **Context.** Every map visual package embedded every weapon mesh, so a new weapon touched many files and every map package.

**Decision.**
- The generator also emits `WeaponMeshCatalog.g.cs`, with one source per mesh key: a `geometry.weapon` recipe or a built-in prop.
- `WeaponPackage` builds one `shooter.weapon-package.v1` package, published beside the maps, and `weapons.json` (`shooter.weapon-package-index.v1`) names it. Gun bytes equal the former embedded artifacts. Props use `geometry.loadout-prop`.
- Map visual packages drop weapon meshes, so their roots change once.
- `VerifiedMapPackageStore` verifies the weapon package once and attaches it (`WithWeapons`).
- `Package-Release` ships and closure-hashes it. The native guard requires identical weapon packages.

**Consequences.** A weapon that reuses an existing mesh key needs only its definition file and a content republish.

## ADR-079 — Accepted: engine toolchain lock separate from the build lock

Date 2026-11-16. **Context.** `toolchainHash` (the SHA-256 of `toolchain-lock.json`) is embedded in all content and frozen in three locks, yet it also covered packages that never affect generated bytes.

**Decision.**
- **`toolchain-lock.json` schema 2** keeps only content-shaping inputs: editor and revision, URP/curated Core RP, AI Navigation, graphics API and the .NET SDK.
- **`build-lock.json`** (`shooter.build-lock.v1`) holds the other packages (Input System, Test Framework), backends, targets, modules, the allowlist version and the transport version. Changing it leaves `toolchainHash` unchanged.

**Enforcement.**
- `Assert-ShooterLockedPackages` requires every direct manifest dependency to match one of the two locks.
- `ShooterBuild.CheckToolchain` merges them.
- Build manifests record `buildLockHash`, and `Package-Release` and `Benchmark` check it.

There was a one-time refreeze to `toolchainHash` `a8331fe2…0500`. Only that field changed in the three locks. Settings provenance was regenerated, and all content was republished.

## ADR-080 — Accepted: code-built post-processing, HDR and SSAO per quality tier

Date 2026-11-16. **Context.** URP post data references stock images that the art audit forbids, so the image was untonemapped LDR with no AO.

**Decision.**
- `ShooterPostProcessingBuild.ConfigureAssets` builds `Settings/ProductPostProcessData.asset` with stock post shaders and generated lookups only: a 16 px ordered dither and one-pixel zero lookups. Film grain and SMAA are never enabled.
- Balanced and High get their own renderers with InterleavedGradient SSAO, so URP strips the stock noise images. HDR is on for Balanced and High.
- `ProductPostProcessing` builds one code-created global volume from `PostProcessingTier`. Only the product camera uses it, and a quality change reapplies it.

Per-tier values are in [materials and rendering](features/materials-rendering.md#shaders-and-lighting). Baked AO followed in ADR-081.

## ADR-081 — Accepted: baked ambient-occlusion volume per map

Date 2026-11-16. **Decision.**
- **Bake.** `MapAmbientOcclusionVolume` bakes sky visibility over an 80 × 16 × 96 m box at 0.5 m × 1 m × 0.5 m (160 × 16 × 192 cells). Each cell casts 17 lattice directions up to 6 m against the collision boxes, with upward rays weighted more. Solid cells are dilated from their open neighbours. The math is IEEE double only, with no trigonometry.
- **Packaging.** It ships as `lighting-ambient-occlusion.bin`, an SGAF `rgba8-linear` atlas required in every map visual package.
- **Shading.** `Shooter/MapSurface` keeps URP Lit's forward pass, but multiplies indirect occlusion by the volume sampled 0.35 m along the normal.
- **Strength.** 1.0 on Low and 0.8 on Balanced/High (`PostProcessingTier.AoVolumeStrength`).
- **Determinism.** Golden hashes match in `AmbientOcclusionVolumeTests` (.NET) and `AmbientOcclusionVolumeEditModeTests` (Mono).

## ADR-082 — Accepted: geometry.weapon 1.1.0 parametric rifle parts

Date 2026-11-16. **Decision.** `geometry.weapon` 1.1.0 sits beside the frozen 1.0.5. A rifle states `parameters.parts` in integer millimetres:
- `handguardFrontMm`, `barrelFrontMm`, `topRailTeeth`;
- the gas block, gas tube and front sight positions;
- `stock` (`fixed`/`none`) and `optic` (`compact`/`none`).

`WeaponParts` enforces ranges and order. The 1.0.5 `standard`/`carbine` presets build byte-identical output under 1.1.0. Sockets, collision, magazine and cycle part keep the baseline.

Fingerprints use each recipe's own version. A definition may give `visual.geometryParts` inline instead of a recipe. `feature-content-lock.json` was refrozen.

**Consequences.** A new rifle shape is one file plus a content republish ([recipe baseline](weapon-recipe-baseline.md)).

## ADR-083 — Accepted: map registry, arena layout documents and the generic map.arena recipe

Date 2026-11-16. **Context.** A map id was hard-coded in about fifteen places.

**Decision.**
- **Registry.** Each map is one file, `Content/Maps/<id>.map.json` (`shooter.map-definition.v1`), holding:
  - id, order, name, description and seed;
  - the recipe generator and the planner;
  - presentation hints (`sky`, `groundSurface`).

  `Tools/generate_map_registry.py` writes `MapRegistry.g.cs` (`--check` in CI). Recipes, planners, the release catalog, `MapContent`, `Compile-GameContent -Map`, the sky, the yard surface and the menu cards all read it. Release policy lists stay deliberate, and `MapRegistryReleasePolicyTests` fails until they match.
- **Layout documents.** `ArenaLayoutDocument` is a planner written as data: identity, layout and ordered kit operations in integer millimetres.
- **Generic recipe.** `map.arena` / `shooter.recipe.map-arena.v1` is the recipe for every new map. `content-format-lock.json` was refrozen (source hashes only).

Adding a map is described in [maps](features/tdm-map-layouts.md#adding-a-map-adr-083).

## ADR-084 — Accepted: analytic SDF UI primitives and UI motion model

Date 2026-11-16. **Context.** `TacticalUi` drew everything as flat white rectangles. ADR-002 rules out sprite art.

**Decision.**
- **Shader.** `Shaders/UiSdf.shader` (`Shooter/UiSdf`) evaluates analytic signed distances:
  - shapes: rounded rect, circle/ring, arc, triangle, diamond, vignette;
  - styling: fill or stroke, gradients, soft shadow, glow and a glint.
  - Output is premultiplied, it respects the IMGUI clip mask, and it samples no texture.
- **Drawer.** `Presentation/Session/UiSdf.cs` draws one quad per shape during Repaint, with one-pixel AA at any scale. If the shader is unavailable, `TacticalUi` falls back to its flat path.
- **Motion.** The pure `UiMotion` provides easing, a damped follow and stamp, slide, stagger, count-up and pulse envelopes. Reduced motion snaps; reduced flash removes the glint and softens glows.
- **Tokens.** `UiShapeParams` ([UI design system](brandbook.md)).
- **Build.** `ShooterBuild` preloads the shader in client players.

**Addendum (HUD review):**
- The ribbon, ammo plate, tracks and weapon silhouettes were improved, and hip reticle arms are 1.5 px.
- Rifle hip framing was moved so the muzzle sits below and off the reticle (`hipRotationDegrees`, `nearFieldHipBias`), guarded by `FirstPersonHandlingTests.HipMuzzleStaysBelowAndOffTheReticle`.
- A review-only `-shooterUiGallery 1` flag shows the medals and controls.

## ADR-085 — Accepted: generated SDF icon atlas with mesh-traced weapon silhouettes

Date 2026-11-16. **Decision.** The icon generator lives in `Generation/Core/Icons`:
- **`IconShape` / `IconRecipe`.** Analytic SDF shapes in integer units, combined by union and subtraction. Math is IEEE only.
- **`IconLibrary`.** Original glyphs:
  - headshot, elimination, assist, ammo, magazine, health and reload;
  - markers;
  - ranks and stars;
  - two fictional faction emblems that differ in shape;
  - medal and crown;
  - the settings categories.
- **`MeshSilhouette`.** Traces each weapon's side view from its packaged meshes (4× supersampled coverage and an exact distance transform), so the icons match the 3D models.
- **`IconAtlas`.** Packs one R8 SDF atlas: 64 px cells with an 8 px spread.

**Runtime.** `UiIconAtlas` builds the atlas in memory at first use, and again when the weapons register (`RegisterWeapons`). It is never written to disk, so the art audit and the content format are unchanged. `Shooter/UiSdf` gains `UiShapeKind.Glyph`.

**Used by:**
- kill-feed icons;
- the ammo-plate silhouette;
- settings tabs;
- team tables with emblems and the top-scorer crown;
- career insignia (ADR-108).

If the build fails, the ADR-084 shapes are used instead. A cross-lane golden hash matches between .NET and Mono.

## ADR-086 — Accepted: animated debrief and front-end motion

Date 2026-11-16. **Decision.**
- **Debrief model.** The pure `ResultsPresentation` derives up to four awards from the final rows:

  | Award | Winner |
  |---|---|
  | MVP | Highest score |
  | Top Fragger | Most kills |
  | Survivor | Fewest deaths, with at least 1 kill |
  | Sharpest | Best K/D, with at least 2 kills |

  It also owns the reveal timeline: banner sweep, score count-up, staggered award cards, then tables. Reduced motion shows the final state at once.
- **Results screen.** `ProductShellHost.DrawDebrief` draws the team-tinted banner, counting scores with emblems, medal award cards and the tables.
- **Screen entry.** `ShellMotion` makes full-screen pages rise 18 px out of a short veil, or only fade under reduced motion. Gameplay and overlays are never veiled.
- **Menu camera.** `ProductMenuScene.ApplyCameraDrift` adds slow bounded sway, a one-time push-in and pointer parallax, on real elapsed time. Reduced motion holds it still.
- **Review preview.** `-shooterUiGallery results` previews the debrief over sample rows.

## ADR-087 — Accepted: tactical HUD craft, first tranche

Date 2026-11-16. **Decision.** HUD craft, read-only and with no authority change.

**First tranche:**
- SDF damage-direction arcs;
- an authority-tick reload ring;
- up to 30 round glyphs;
- rotated ally/self arrows and enemy gunfire glyphs (ADR-058 rules);
- team-plated elimination rows with smooth placement;
- a hit-marker pop with a gold headshot variant;
- a bounded low-health pulse.

Reduced motion removes pop, slide and row travel. Reduced flash holds the vignette and softens the arcs. Tokens are in the [UI design system](brandbook.md).

**Second tranche:**
- **Compass.** The chevron points to the team-2 spawn from verified map metadata, never live enemy poses.
- **Insertion screen.** Draws collision footprints and spawn markers once verified data exists.
- **Rotating content.** Tips rotate. Media and tips freeze under reduced motion.
- **Branding.** A stencil wordmark, lifting operation cards and outlined keycaps.
- **Binding phases.** Verified binding is split into cancellable phases (map, weapon, soldier, FX, session) with a paint yield after each. Indivisible uploads over 33 ms log an overrun.

## ADR-088 — Accepted: seeded noise material family

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add material.noise 1.0.0 / shooter.recipe.material-noise.v1 with concrete, plaster, rust, painted-steel, asphalt, gravel, corrugated-metal and camo-fabric roles. Named NoiseField streams supply periodic cellular cracks, broad staining, warped streaks, chipped coating, aggregate/tar and cloth patterns. Bake physically scaled normals and linear RGBA albedo/mask mip chains offline at 128 pixels (256 on High). Existing material.surface 1.0.1/1.1.0 remains frozen. Extend map presentation metadata with opt-in material roles; retain collision/nav/spawn identities. Refreeze only the new registry inventory and explicitly add the generator row, schema and fixture.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

## ADR-089 — Accepted: weathered map surface shading

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Retain the URP Lit forward/shadow/depth paths and the one normal/mask keyword set. Add AO-linked grime/roughness, low height-band dirt, 20 m macro variation and triplanar detail normal sampling. All additions are visual only; measure Low cost in the release player. The detail uses already packaged numeric normal maps; no new imported textures or geometry.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

## ADR-090 — Accepted: packaged procedural decals and versioned visual layout operations

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Bake an eight-cell numeric decal atlas (stencil letters/numbers, hazard stripe, unit marking, oil, scorch and bullet damage) into SGAF. Arena layout documentVersion 2 adds visual-only decal operations and retains version-1 parsing; ignored visual operations never advance collision stable IDs or change navigation/spawns. Publish static decal placements in the verified map closure. Balanced/High configure the locked URP screen-space Decal renderer feature and projectors; Low uses depth-biased mesh quads. Impact marks use a bounded 64-slot pool and authority Impact events; no new collision queries. New shader passes use only the generated atlas. Refreeze the affected format registry source inventory explicitly without changing SGAF v1.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

## ADR-091 — Accepted: numeric sky atmospheres, map grades and actor grounding light

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add a code-only cube sky with a once-per-map NoiseField cloud bake, morning haze / overcast / golden-hour presets from registry sky hints, matching reflected environment and height fog. Per-map integer grade parameters generate a 32-cube linear lookup for Balanced/High; Low uses only ColorAdjustments in LDR. Actor root samples of the verified AO volume drive one cosmetic visibility value per actor, plus a code-only contact shadow on Low. New sky/grade bytes have matching .NET and Mono goldens; no gameplay light/visibility changes or runtime geometry generation is introduced.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

## ADR-092 — Accepted: numeric volume-noise combat flipbooks

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add fx.flipbook 1.0.0: an 8 by 8 frame sequence for smoke puff, dust puff, fireball, muzzle star and spark streak. Integrate three-dimensional FBm and finite-difference curl advection along a volume ray with polynomial density envelopes; use IEEE operations only in hashed output. Store complete linear RGBA atlas mip chains through SGAF, cache immutable offline bakes and verify matching .NET/Mono aggregate goldens. No images or sprites are imported.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

## ADR-093 — Accepted: bounded particle combat presentation

Date 2026-11-16. **Decision.** Combat mesh proxies are replaced by code-built, manually advanced ParticleSystems that use the verified SGAF flipbooks (ADR-092). The bounded `AnalyticFxPool` and the event timing stay.
- **Low** emits fewer particles and skips depth softening.
- **Weapon definitions** gain a validated `muzzleFx` profile, plus a smoke wisp and HDR tracers.
- **Grenades** add flash, fire, smoke, a shockwave, debris and a scorch mark, with cosmetic camera shake gated by reduced motion.
- **Movement** produces bounded footstep and slide dust.
- **Reduced flash** suppresses flash, fire and tracers.

No simulation change.

**Addendum (surface impacts).** The pure `ImpactSurfaceFx` resolves the impact type read-only from the verified packaged boxes (`MapSolidsMesh.Classify`) and the registry `groundSurface`:

| Surface | Effect |
|---|---|
| Metal | Sparks |
| Concrete | Dust and chips |
| Dirt | A brown kick-up |

Feature envelopes (vehicles, sandbags) are classified by box alone.

## ADR-094 — Accepted: bounded soldier presentation revision and P05 reopening

Date 2026-11-16. **Decision.** The visual roadmap reopened ADR-024 for at most three measured candidates or 90 minutes of P05 clearance and eye-solver work. The P05 gate stayed unchanged.

The new soldier kit, `soldier.loft` 1.0.3, adds:
- lofted uniform and equipment modules;
- camo and fabric;
- numeric insignia;
- a 10-µm output lattice for Mono/.NET portability.

1.0.2 stays buildable. Cosmetic ground reads the gameplay collision boxes, and each weapon profile supplies its own grip targets.

**Outcome.** All three candidates **FAILED**: arm clearance −55.6, −57.7 and −11.5 mm. The product kept the baseline solver, and the failed correction stayed opt-in (`-shooterP05Candidate`). Superseded by ADR-095.

## ADR-095 — Accepted: structural weapon-from-eye shooting posture (P05 reopening)

Date 2026-11-17. **Context.** All three ADR-094 candidates only pulled the head onto the sight line of a weapon whose stock stayed on an unraised shoulder. The sight line runs just 55 mm above the stock socket, so the head landed on the trigger arm.

**Decision.** The owner reopened P05 once more for one structural revision of the `SoldierMotion` presentation posture (`ShootingPosture`). It may change:
- the right-clavicle shooting posture, which moves the stock socket;
- a head pose that puts the right eye on the sight line by construction, with the neck solved to that target;
- the spine distribution of aim yaw;
- the elbow solve after the head;
- an outward support-hand reload bow.

The skeleton, limbs, recipes, sockets, the `ArmBodyClearance` proxy, gameplay hit volumes and Simulation stay unchanged.

**Gate**, fixed in advance on the unchanged `SoldierClearanceTests` envelope:
- arm clearance ≥0;
- eye/sight residual ≤5 mm;
- grip ≤2 cm;
- stock error ≤1e-5 m.

The bound was 180 minutes.

**Outcome.** PASS. Clearance +3.0 mm and eye residual 0 mm. `soldier.motion.v6.shooting` became the product default (later v7, ADR-100), and `-shooterP05Candidate` was retired. P05-D01–D03 are numerically resolved. P05 visual acceptance stays **FAIL** until a named visual review ([defect inventory](visual-defect-inventory.md)).

## ADR-096 — Accepted: music channel, combat sample bank v5 and owner audio-source relaxation

Date 2026-11-17. **Context.** Reload, knife and grenade cues sounded synthetic, and there was no music.

**Decision.**
- **Source policy.** In this non-commercial build, a source's license no longer gates selection. It is still recorded with the SHA-256. This amends ADR-055/062. Audio extracted from commercial games stays forbidden.
- **Bank v5.** Adds sidearm reload, knife hits, grenade pin/bounce/far blast, dry fire, weapon switch, hit marker, kill confirm, flyby, distant battle and UI hover/click/back. Each cue has 1–8 variations. It is built by `Tools/build_audio_bank.py` from `Tools/audio-sources.json`.
- **Routing.** Presentation only:
  - reload Foley is time-fitted to the reload window;
  - knife hit pairing;
  - local hit and kill ticks;
  - flybys within 2.5 m;
  - analytic grenade bounces;
  - a distant-battle bed.

  No event contract changes.
- **Music.** `MusicDirector` drives `UnityMusicOutput`: menu → silence while loading → calm bed → tension bed → stinger → results loop, with ducking. Tracks are OGG from `Content/Audio/music`, with hashes. The built-in UnityWebRequest audio modules are lock-exempt.
- **Menu sound.** A UI mixer gives the menu sound.
- **Settings.** An additive `musicVolume` (default 0.6). Schema stays 2, and the Game-v1 lock is refrozen.

See [audio](features/audio.md).

## ADR-097 — Accepted: headshot kill Score bonus

Date 2026-11-17. Context: owner playtest of 0.3.18 asked that a headshot kill be worth more than a body kill, for example 100 for the kill plus 20 for the head. ADR-059 fixed the killer's personal Score at exactly 100 and ADR-073 made the headshot badge worth 0 points.

Decision: an enemy kill whose fatal `DamageApplied` from the killer to the victim in the same tick batch has region 1 (head), as decided by `LocalKillBadgeLogic.IsFatalHeadshot`, adds `LocalKillBadgeLogic.HeadshotBonusPoints` = 20 personal Score on top of the ADR-059 kill points. It applies to every participant, bots included. `OfflineGameSession` adds it through `ITeamMatch.AddScore` directly after `ProcessDeath` and before `EvaluateEndOfTick`, so a match-ending headshot keeps its bonus. The local HUD Headshot award carries Points = 20: the feed shows `+20 HEADSHOT`, and the ribbon/ammo popup show the kill total (120). Team kills, the 150-kill limit, assists, environmental deaths and the C0/Game-v1 contracts are unchanged; the constant lives in `Game/Session`, so no lock refreeze is needed. This supersedes "exactly 100" in ADR-059 and "(0 points)" for the headshot badge in ADR-073.

Consequences: personal Score and results ordering can differ from earlier builds for the same play; team outcome cannot.

Rollback: set the bonus to 0 or revert this change.

## ADR-098 — Accepted: per-weapon first-person recoil presentation (camera climb + per-shot kick)

Date 2026-11-17. **Context.** Simulation deflected shots by authoritative recoil, but neither the camera nor a scoped view showed it. The viewmodel spring piled up into drift at 600 RPM.

**Decision.**
- **Recoil is weapon data.** The generator copies `combat.recoilPitchDegrees` and `recoilYawHalfDegrees` into `WeaponVisualProfile`. Presentation has no slot or id switches.
- **Camera** (`FirstPersonCameraRecoil`):
  - The *climb* mirrors the authoritative pitch tick for tick, so the screen centre and the reticle stay where rounds go.
  - A cosmetic *punch* spring kicks every report: about 31 ms to peak, about 85% settled by 100 ms.
  - Reduced motion drops the punch.
  - Authoritative yaw is seeded and not mirrored.
  - These values touch only the camera rotation. Events are leased before the camera is posed.
- **Viewmodel** (`ProceduralViewModel`). Each local shot adds an impulse sized from the profile to a stiff exact `DampedSpring`, so automatic fire pulses once per round.

## ADR-099 — Accepted: per-weapon first-person hand and weapon choreography

Date 2026-11-17. **Context.** One generic reload arc served every weapon, switches took a fixed 0.34 s, and the knife and grenade had poor or cut motion. There was no movement on the viewmodel.

**Decision.** Timing comes from weapon data: the generator copies the equip, fire, reload and fuse ticks into `WeaponVisualProfile`, plus an optional `visual.adsSeconds`. Choreography is numeric `MotionCurve` key tables of equipment-relative waypoints in pure `Presentation/Model/View`, not clips.
- **Reload** (`FirstPersonReloadChoreography`):
  - **Rifle:** tilt, release, strip or drop the magazine, fetch a fresh one from the belt, insert, slap. An empty reload adds a bolt-catch press.
  - **Pistol:** cant, thumb drop, insert, palm seat. An empty reload adds a slide rack.
  - An `FP fresh magazine` child reuses the magazine mesh.
- **Shooting.** The cycle closes within min(90 ms, 0.8 × interval). On empty it stays locked back.
- **Switching** (`FirstPersonEquipChoreography`). A tuck of at most 0.10 s, then a per-weapon draw that fills the authoritative equip window.
- **Knife.** Swings fill the fire interval and alternate direction.
- **Grenade.** A pin pull, a cocked raise and a fuse tremble. The throw survives the authority's same-tick switch back.
- **Movement** (`FirstPersonLocomotionPose`):
  - sprint carry and bob;
  - jump lag and a landing dip;
  - crouch tuck and slide roll;
  - look inertia;
  - idle breathing;
  - eased eye height.

  Rotation layers fade out at ADS.

Contracts, timing, the hand rig and the packages are unchanged.

## ADR-100 — Accepted: third-person directional gait, airborne pose and bot hops/slides

Date 2026-11-17. **Context.** Strafing bots had floating legs because foot targets ran along the aim. There was no airborne pose, and bots rarely jumped or slid.

**Decision.** `soldier.motion.v7.locomotion` (ADR-095 posture plus the following):
- **Directional gait.** `MoveHeadingRadians` sets the stride along the travel heading while the torso and weapon keep the aim. Stance width turns toward the travel perpendicular. Side-step and backpedal use 1.0 / 1.6 m cycles.
- **Reversals.** A turn of more than 120° mirrors the gait phase. A turn of 60–120° re-aims a swing that is less than halfway done. Both stay within the two-ground-query budget.
- **Airborne.** A tuck pose with no ground samples. Feet lift while rising and reach down while falling.
- **Landing.** The pelvis dips up to 7 cm.
- **Slide.** Faces the direction of travel, with the spine twisted back to the aim.
- **Presentation.** `LocalMovementView` gains velocity. `ThirdPersonLocomotionBlend` derives heading hold, airborne hysteresis, landing and slide easing.
- **Bots:**
  - more and sooner slides (300-tick cooldown);
  - a knee-height traversal hop over low obstacles (`TraversalJumps`);
  - a 25% hop on safe strafe reversals (`CombatHops`).

`feature-content-lock.json` was refrozen. Contracts, motor and hit volumes are unchanged.

## ADR-101 — Accepted: selectable team sizes 5 / 8 / 10 / 15

Date 2026-11-17. **Decision.**
- **Allowlist.** Team sizes are {5, 8, 10, 15}, shown as a 2×2 grid in Setup. Unknown or legacy values (6) clamp to 5.
- **Capacity.**
  - `GameContractVersion.MaxParticipants` / `MaxActiveActors` = 30.
  - `CombatAiDirector.MaxBots` = 29.
  - `ruleVersion` stays 1.
- **AI budgets.** The global limits stay at ≤16 rays and ≤2 paths per tick, and every bot is sensed within `MaxBots` ticks. There is no per-size scaling.
- **Spawns.** 15 slots per team (`MapSpawnLayout.SlotsPerTeam`), so packages are republished.
- **Names.** At least 29 bot names.
- **Workloads.** `TDM-5v5/8v8/10v10/15v15-*` and `TDM-soak-15v15-*`. The 6v6 evidence is archival.
- **Locks.** `game-contract-lock.json` and `content-format-lock.json` are refrozen.

## ADR-102 — Accepted: named real-world arsenal, random spawn loadouts and muzzle flash fix

Date 2026-11-17. **Context.** Every gun was generic, and the muzzle flash never showed.

**Decision.**
- **Roster.** Seven named guns, one definition file each:

  | Gun | Fire mode | Damage | Magazine | Optic |
  |---|---|---|---|---|
  | AK-47 | Auto | 36 | 30 | Irons |
  | AK-102 | Auto | 28 | 30 | Irons |
  | M16A4 | 3-round burst | 32 | 30 | 2x |
  | Mk 14 EBR | Semi | 55 | 20 | 4x |
  | M-TAC 338 | Bolt | 120 | 5 | 8x |
  | G17 | Semi | 24 | 17 | Irons |
  | Golden Eagle .50 | Semi | 55 | 7 | Irons |

  Each gun has its own falloff. The generic rifle, carbine and pistol stay with `spawnWeight 0` for tests and fixtures.
- **Simulation.** Optional burst, falloff, `moveSpeedScale` and `spawnWeight` fields. Burst progress is derived from `ShotIndex`, so C0 `WeaponState` is unchanged. Falloff scales damage before the region multiplier.
- **Random loadout.** `WeaponLoadoutPolicy.CreateRandomLoadout(seed, actor, tick)` draws a weighted primary and sidearm, so replays draw the same guns. Bots read their issued guns. The human uses the Armory since ADR-107.
- **Geometry.** `geometry.weapon` 1.2.0, with archetypes `ak`, `ar`, `dmr`, `bolt`, `pistol-striker` and `pistol-eagle`, and parts for barrel, furniture, muzzle, optic and stock. Wood (slot 5) and gold (slot 6) are tints.
- **Handling.** `handlingStyle`, `adsFovDegrees` and muzzle flash scale and side jets.
- **Muzzle flash fix.** Flipbooks played backwards because of the row order. The presenter now flips rows on upload (`AnalyticFxPool.FlipTextureRows`) and places the flash on the visible muzzle.
- **Audio.** Bank v6 adds per-gun shots, `bolt_cycle`, `reload_ak` and `reload_bolt`.

`feature-content-lock.json` is refrozen.

## ADR-103 — Accepted: optical scope lens camera, Balanced/High quality uplift, flicker geometry fixes and damage feedback v2

Date 2026-11-17. **Context.** At 1080p High, maps flickered, scopes looked poor at range and damage gave weak feedback. This supersedes ADR-065 (no second optic camera) and parts of ADR-080 and ADR-087.

**Decision.**
- **Lens camera.** `AdsScopePipPresenter` renders a child lens camera at the lens FOV into a square target sized to the lens (capped at 512 / 768 / 1024 per tier), and samples it 1:1. Targets use D32 depth.
- **Anti-aliasing.** `ProductPostProcessing.Apply` owns AA: Low FXAA, Balanced MSAA 2x, High MSAA 4x.
- **Tier budgets** (`ShooterBuild.Profile*`):
  - shadows 40 / 80 / 150 m with 2 / 3 / 4 cascades, soft above Low;
  - anisotropic filtering 2 / 8 / 16;
  - a mipmapped decal atlas;
  - fog scaled per tier.

  Low is unchanged apart from FXAA.
- **Flicker.** `MapSolidsMesh.AddWallDetails` keeps an 8 mm minimum separation. Coplanar slabs are lifted, and decal quads are offset.
- **Damage feedback.**
  - `FirstPersonDamagePunch` flinches the view.
  - The edge flash scales with damage.
  - A second global volume adds a low-health grade with a heartbeat pulse below 40 HP, and a hit grade.
  - `LowHealthAudioFx` adds a heartbeat, a thud and a muffle below 25 HP.

Reduced motion and reduced flash gate all of it.

## ADR-104 — Accepted: circular heading-up minimap with ring compass

Date 2026-11-17. **Context.** The minimap did not show the player or teammates: rotated glyphs inside a `GUI.BeginGroup` were clipped away. The owner asked for a round, rotating minimap ringed by the compass. This supersedes ADR-058's north-up clause and removes the top compass strip. The gunfire-reveal rules are unchanged.

**Decision.** The minimap is drawn in absolute HUD space through a `_ClipCircle` SDF mask (`UiSdf.BeginCircleClip`). It is a heading-up disc with a 75 px radius covering 50 m, showing:
- the range ring and view cone;
- structures;
- ally arrows, with rim pips when out of range;
- gunfire blips;
- the self arrow.

A 16 px compass bezel rings the disc, with letters placed by `CompassRingAngle`, the objective marker and a heading readout. The pure math lives in `CombatHudVisual`.

## ADR-105 — Accepted: dropped primary weapons and G pickup

Date 2026-11-18. **Decision.** When a soldier dies, their primary drops for 15 s, and the human can take it with G.

**Authority.**
- `Simulation/Combat/DroppedWeaponField` is a bounded list (32 entries) of dropped primaries.
- Expiry counts **active match ticks** (`LifetimeActiveTicks` = 900), so pause and the countdown do not run the clock.
- `OfflineGameSession.Step` captures slot 0 and the dying actor's feet inside the death batch.
- Out-of-world deaths drop nothing.

**Pickup.** `TryFindPickup` finds the nearest drop within 1.75 m horizontally and 1.5 m vertically.
- **A different gun** is a swap through `ReplacePrimary`. The old primary drops in its place.
- **The same gun** tops up the reserve to capacity (`SetPrimaryReserve`), and is offered only if it adds rounds.
- The pickup resolves after the death batch.

**Input.** `LocalPlayerCommand.PickupPressed` travels beside the frozen C0 command. An additive Game-v1 binding `Pickup` (G) uses the ADR-056 legacy rule, and the lock is refrozen.

**Presentation.** `SyncDroppedWeapons` shows each drop, blinking for its last 3 s. `DrawPickupPrompt` shows the prompt using the same query as the authority. Bots do not pick up.

## ADR-106 — Accepted: kernel-built weapons, gloves, soldiers and structured map surfaces

Date 2026-11-18. **Context.** At 1080p High, textures looked over-sharpened, hands and bots looked like puppets, and weapons looked like boxes. The cause was content, not settings. The owner allowed rule bends and chose gloved hands and covered faces.

**Decision.**
- **Kernel.** `Generation/Core/Geometry/ShapeKit.cs` provides `RoundedOutline` + `Extrude`/`Slab`, `Lathe`, superellipse `Loft`, and `Spline` + `Tube`. Every part is validated as a closed outward solid.
- **Weapons.** `geometry.weapon` 1.3.0 (`ProfileArsenalGeometry`) rebuilds the seven named guns from profiles, turned parts, lofts and grip lofts. Sockets, collision and slots reuse 1.2.0. LOD ceilings rise.
- **Gloves.** One swept tube per finger with smoothstep knuckle blending (`ChainWeights`). Rig `fp.hand.v2` is unchanged.
- **Soldiers.** `soldier.loft` 1.1.0 is the full covered-face kit (≈14.7k triangles). `SelectBodyLod` switches at 15 / 40 m.
- **Map surfaces.** `material.noise` 1.1.0: metre-sized features over a 4 m tile, joints, tie holes, cracks and Toksvig mask mips. 256 px on Low, 512 px above.
- **Shader.** Detail at 0.55/m, and bumps lower by about a quarter.
- **Polish.** HDR grading, decals out to 200 m, a 256 px probe and pooled flash lights on Balanced/High.

The owner authorized these rule bends: a feature-lock refreeze, ADR-088's 128/256 limit, and higher LOD ceilings.

## ADR-107 — Accepted: Armory loadouts and reusable rail-mounted sights

Date 2026-11-18. **Decision.** This replaces ADR-102's random loadout for the human only.

- **Bare-rail primaries.** `geometry.weapon` 1.4.0 (`optic: "mount"`) gives long guns without an optic. They keep their integral irons, and the body's `sight-eye` is the iron line. Pistols stay on 1.3.0.
- **Sights** (`WeaponSights.cs`, `BuildSight`). `irons`, `collimator`, `scope2`, `scope4` and `scope8` are built per archetype rail, inside a 0.27 m envelope. `EyeFor(kind, sight)` gives the line of sight. They ship as `sight-<sight>-<kind>` meshes in the weapon package.
- **Definitions.** `visual.sights {default, allowed}`. The generator validates it and writes `SightCatalog.g.cs` and `WeaponSightChoices.g.cs`.
- **Authority.** Sights never change combat. Each actor carries a primary sight. Pickups keep the dropped gun's sight. Bots roll `WeaponLoadoutPolicy.RandomSight` on its own stream.
- **Human loadout.** `PlayerLoadout` is pinned with `HarnessActorLifecycle.PinLoadout` and `OfflineGameSession.UseArmoryLoadout`. Fields fall back to the M16 with prism and the G17.
- **Armory file.** `shooter-armory.json` (`shooter.armory.v1`), stored separately from the frozen settings, with fixtures in `docs/armory-fixtures/`.
- **Shell.** `ShellScreen.Armory` ([game shell](features/game-shell.md#armory-adr-107)).
- **Presentation.** `OpticMode.Collimator` and sight meshes parented to the first-person, held and dropped guns. ADS aligns on the mounted sight.

The feature-content lock is refrozen.

## ADR-108 — Accepted: career ranks, unlocks, buffs and local service record

Date 2026-11-18. **Decision.** A local career. This supersedes the "no progression" lines in the vision, the brandbook, the TDM page and ADR-059/073/085.
- **Career points** per finished match: personal score, +500 for finishing, +1000 for a win (+500 for a draw). Aborted matches earn nothing.
- **Ladder** (`Game/Settings/CareerLadder`). Rank *n* needs `1000·(n²−1)` CP. Each rank above Private unlocks one reward: a gun, a sight, or a small human-only buff for all firearms. Every gun's factory optic unlocks before the gun. The ladder was originally 20 ranks; ADR-119 extended it to 30 ([career](features/career.md)).
- **Locks.** `ArmorySelection` takes a lock rule, and locked saved choices fall back.
- **Buffs.** `Simulation/Combat/WeaponModifiers` uses integer per-mille scales. They never change damage, fire rate or movement. They ride on `PlayerLoadout.Modifiers` and are applied where `WeaponSimulator.Step` reads a definition. `Identity` is bit-identical for bots and replays. Presentation mirrors timing.
- **Service record.** `shooter-career.json` (`shooter.career.v1`) with the same store rules plus "an unreadable file is never overwritten". Fixtures are in `docs/career-fixtures/`. `OfflineGameSession.LocalTally` supplies assists, headshots and streaks. `CareerRecorder` applies each match once.
- **Insignia.** Procedural `rank-NN` glyphs tinted by band.
- **Bots.** `BotRanks.Resolve` gives a cosmetic seeded rank within ±3 of the player's.
- **Shell.** The Career page, the Armory header, the menu badge, insignia before every name, and the debrief CP strip.

No C0, Game-v1 or content change.

## ADR-109 — Accepted: site-planned maps (Old Town, Harbour, rebuilt Oil Depot)

Date 2026-11-18. **Context.** The ADR-074 planners dropped boxes into empty rectangles. The validator rewarded scatter, and spawn safety ignored the exits.

**Decision.**
- **Site-plan layer** (`Generation/Core/Environment/Site/`). `SiteBuilder` describes a real place:
  - a boundary;
  - buildings with doors, windows, partitions, upper floors and stairs;
  - named zones;
  - props that must name a host;
  - a few landmarks.

  It compiles to `ArenaBoxBuilder` boxes, with a `SiteLayout` on `CompoundPlan.Site`. There is no API for an unowned box.
- **`SiteRules`.** They run alongside `ArenaRules` and check:
  - every box has a role;
  - built coverage is 25–60%;
  - no open void;
  - spawn-exit exposure;
  - first contact at ≥18 m;
  - sightlines ≤40 m outside the plaza/quay;
  - upper floors are reachable.

  Path bounds widen to 26–52 / 60–110 m.
- **Maps.** Ids, seeds, recipes and format are unchanged:

  | Map id | Becomes | Planner | Symmetry |
  |---|---|---|---|
  | `training-compound` | Old Town | `environment.town.site` | Rotate180 |
  | `depot-yard` | Harbour | `environment.harbour.site` | MirrorZ |
  | `oil-depot` | Oil Depot (rebuilt) | `environment.oil.site` | MirrorZ |

  Layouts are in [maps](features/tdm-map-layouts.md).
- **Dressing.** `SiteDressing` and `OilDepotDressing` are mesh-only.
- **Bots.** `RouteCorners` is a circuit through every lane.
- **Review aid.** `SiteMapSvg` renders the plans.
- **Retired:** the ADR-074 arena planners.

Gameplay package identities change and native nav is republished.

## ADR-110 — Accepted: body armor (100 points, torso and limbs)

Date 2026-11-18. **Decision.** Every actor has 100 armor (`DamageMath.MaxArmor`). It is set on spawn, restored by `ResetActor` and kept by `RelocateActor`.

**Rules:**
- **Coverage.** Armor covers regions 2–6: torso, arms and legs (`ArmorCovers`). Headshots go straight to health, so the ADR-063 one-shot is unchanged.
- **Split.** After rounding, armor takes `floor(d/2)` of a covered hit, capped by what remains, and health takes the rest (`SplitArmor`). Grenade blasts count as region 2.
- **Events.** `DamagePayload` still means health damage. Health always takes at least 1, so the C0 `amount > 0` rule holds.
- **Read path.** Armor is authority-only, read through `TryGetArmor`. There is no snapshot field.
- **HUD.** An AR number and bar beside HP. AI reads health only.

**Consequences.** Body time-to-kill rises: about 6 rifle torso hits instead of 4. No lock changes.

## ADR-111 — Accepted: knife and grenade overhaul (one-handed hold, cut and throw, half-map grenade range, sound, explosion, models)

Date 2026-11-18. **Context.** The knife and grenade sounded and looked cheap, the knife was held two-handed, and the grenade reached only about 18 m.

**Decision.**
- **Ballistics.**
  - Throw speed rises from 18 to 28 m/s, and gravity from −20 to −24.
  - `GrenadeSimulator.Loft` adds +8° to the launch, giving about 34 m at a 45° launch. The 120-tick fuse is kept.
  - Bots solve a low launch pitch and throw out to 32 m.
  - The C0 fixture `gameplay-defaults.json` (`throwMmPerSecond` 28000) is refrozen, with owner approval.
- **First person.**
  - Knife and grenade are one-handed (`SupportHandVisible` is for guns only), and never pulled to the sights.
  - The knife has a large diagonal cut and a stab with hit-stop, with a `KnifeBladeTrail`.
  - `GrenadeArcPresenter` previews the arc and the landing point.
  - `CosmeticGrenadeFlight` mirrors authority bounce and friction.
  - Third-person cut and throw overlays are added.
- **Explosion.** Presenter-only layers: flash, fireball, sparks, embers, shockwave, dust ring, a smoke column, a light and shake. Reduced flash and reduced motion gate them.
- **Audio.** Bank v7 re-mixes the knife and the explosion. Addendum (v8): peaks are normalized on the true float peak, so no clip reaches full scale.
- **Models.** `geometry.loadout-prop` 1.1.0 on `ShapeKit` (`ClosedTube`), with finish slots 7–10.

## ADR-112 — Accepted: crosshair-true hits, rig-aligned hit volumes, and bots that use map cover, flank and move smoothly

Date 2026-11-18. **Context.** Centred headshots sometimes did nothing, and bots stacked and moved jerkily.

**Decision: hits.**
- **Eye-ray resolution.** `HitscanResolver` resolves along the eye ray against static geometry and hostile capsules, nearest first. Previously the shot flew from the muzzle offset and missed by about 0.22 m. The eye→muzzle wall check stays, and the muzzle is only the tracer origin.
- **Teammates.** Same-team capsules are skipped. This supersedes "friendly bodies block bullets".
- **Hit volumes.** `GameplayPoseEvaluator` v2 follows the drawn ADR-095 posture (head radius 0.13 m), checked by `HitVolumeRigAlignmentTests` to within 5 cm.
- **Presentation.** Drawn bodies advance by velocity and never drift more than 0.25 m from authority. The hip reticle shows the real cone. F8 shows a dev hitbox overlay.
- **Bot aim.** The muzzle-parallax correction is removed. Bots aim at the chest, and recovery takes 66 ticks.

**Decision: bots** (same budgets):
- **Visibility hold.** A target seen within 12 ticks is held through starved ticks.
- **Smoother motion.** Aim error glides, direction changes ease, routes bend into corners, and the leash is ±3 m.
- **Map cover.** Authored package cover reaches the bots through `World.Runtime.IMapCoverSource`. Scoring uses no rays.
- **Fights move.** Bots go to cover after 2–4 s in the open and duck-peek behind low cover.
- **Flanking.** Bots over the crowd limit flank.
- **Destinations** avoid the own spawn and lean toward the enemy side.

No lock changes.

## ADR-113 — Accepted: career progress line, layered rank emblems and reward art

Date 2026-11-18. **Decision.** Presentation only, drawn with UiSdf and no imported art.
- **Rank emblems** (`CareerArt.RankEmblem`). An enamel plate (shield, hexagon, or a general's burst in a laurel wreath), a metal rim, and the embossed insignia in the band metal. Below 34 px only the insignia is drawn.
- **CP bars** (`CareerArt.ProgressBar`). Recessed bars with segment ticks and a lit gain.
- **Rewards.** `Game/Settings/CareerRewardCatalog` gives every rank an icon, title and detail. New plate, sight, perk, dog-tag, crown, lock and check glyphs. `CareerArt.RewardCard` uses kind colours.
- **Career page.** A hero band, a horizontal progress line with one node and card per rank, perk chips and service-record tiles.

The icon-atlas golden changes.

## ADR-114 — Accepted: corner-bob motor fix, nav bake that respects walls, and a tabbed Career page

Date 2026-11-18. **Decision.**
- **Corner bobbing (motor).** A PhysX `CapsuleCast` that starts in contact reports distance 0 with normal −dir. At box corners this lifted actors repeatedly. `KinematicMotor` now:
  - routes every sweep through `Sweep()`, which re-sweeps an inset capsule on a start contact;
  - lets `TryStep` require a measured step top;
  - settles `ProbeGround` from the true distance;
  - never has `SlideMove` lift a ground-following actor.

  `SlopeWorld.PhysxStartContact` and a real-PhysX PlayMode test cover it.
- **Bot navigation.**
  - The bake (`UnityNavMeshBakeEditorBridge`) gives each solid a Not-Walkable volume from one agent height below it up to 0.1 m under its top, padded by the clearance margin.
  - The pure `NavCollisionAudit` rejects nav inside or too close to blocking solids. It runs on every native bake and logs `SHOOTER_NAV_COLLISION_AUDIT`.
  - `UnityNavMeshBakeInstaller` snaps heights to 5 cm, so the whole bake installs. Previously about 1% did.
  - The fallback snaps ≤1.5 m and thins corners only when `NavMesh.Raycast` is clear.
  - The bot corner bend requires clear probes.
- **Career page.** PROGRESS and STATISTICS tabs (`TacticalUi.TabStrip`, Q/E). The pure `CareerLayout` / `CareerLineLayout` remove overlaps at 720p–1080p and 100–150%. `TacticalUi.LabelFit` keeps labels on one line. Opening with `-shooterUiGallery career` is for captures.

Native packages are republished. About half of bot goal points still miss the NavMesh and use the wall-safe fallback.

## ADR-115 — Accepted: open reflex collimator with a clear glass pane

Date 2026-11-18. **Context.** The collimator showed an opaque black disc behind the dot.

**Decision.** `geometry.weapon-sight` 1.1.0 (`ProfileArsenalGeometry.Collimator`) builds an open reflex sight:
- a 28 mm box base on a rail clamp, with an emitter, buttons and a battery cap;
- a rounded-rectangle frame leaning back 8°;
- one 1 mm glass pane.

The eye line stays 32 mm above the rail. Slot 11 is a clear `Shooter/SightGlass` material: transparent, about 7% opacity, a Fresnel rim and sheen, and no textures. `SightGeometryTests.Collimator_eye_line_passes_only_through_clear_glass` checks the eye line. The weapon package and the feature-lock source hashes change.

## ADR-116 — Accepted: realistic map colour, texture and shadows (material.noise 1.2.0)

Date 2026-11-18. **Context.** Maps read as almost grey. Four causes stacked:
- dark, neutral albedo;
- tints that only darkened;
- desaturating grades;
- early grey fog, a cool Harbour sun and hard Low shadows.

**Decision.**
- **`material.noise` 1.2.0** (`NoiseMaterial.cs`). It reuses the 1.1.0 relief and adds colour fields:
  - warm concrete with pour jitter and damp;
  - plaster with ochre wash and brick spalls;
  - steel chipped to red-oxide primer with rust;
  - galvanized corrugated sheet;
  - asphalt with aggregate;
  - mixed gravel.

  Fixture `material-noise-1.2.0.json`.
- **Tints** (`ProductSurfaceResponse`) sit near 1.
- **Accents** (`ArchitecturalAccents`) divide the target paint colour by the plane's mean albedo.
- **Shader.** Warm/cool hue drift, rain streaks, a warmer dirt tint and lighter fog.
- **Light.** Warm suns (1.25–1.35), and fog that starts at about 40 m.
- **Grade.** +10 saturation on every tier.
- **Shadows.** This amends ADR-103's "Low unchanged". Low gets soft 50 m / 2048 px shadows. SSAO is 1.4 / 1.6.

If Low misses its GPU budget, drop the Low atlas back to 1024 first.

## ADR-117 — Accepted: single-point collimator dot and 1.2x collimator zoom

Date 2026-11-19. Context: owner report. The collimator should show one small red point in the middle without the red area around it, and should magnify 1.2x rather than 1x. The ADR-107 dot was three HUD circles: a 3.4× radius bloom disc at 22% alpha (the red area), the core dot and a pale centre. The ADR-107 field of view was 56/80 of the hip FOV, which is about 1.43x, while the Armory text said "1x".

Decision:
- `TacticalUi.CollimatorDot` draws one solid red circle. The bloom disc, the pale centre and the reduced-flash variant are removed. The square fallback stays for when the SDF shader is unavailable.
- `AdsOpticVisual.CollimatorDotRadiusPixels` = max(1.5, 0.0030 × screen height), about 3 px at 1080p (was 0.0042, about 4.5 px).
- The collimator magnifies 1.2x: its ADS field of view is 80/1.2 ≈ 66.7° at the 80° reference, scaled with the hip FOV as before (`AdsOpticVisual.CollimatorAdsFovDegrees`). The sight row's magnification is 1.2 and its text reads "Red dot, 1.2x".
- This supersedes ADR-107's "56/80" and ADR-115's "dot unchanged". Irons, the prism and the scopes are unchanged.

Consequences: Presentation only. No lock, contract, fixture or content change. `ArmorySelectionTests` pins the 1.2x field of view and the dot size.

Rollback: revert the commit.

Evidence: [M11 handoff](milestones/m11-playable-release/handoff.md#adr-117-collimator-dot-and-zoom--2026-11-19). The in-game look is Pending owner playtest.

## ADR-118 — Accepted: M4A1, Mk 16, M24 SWS and M870: pellets, shell reloads and geometry.weapon 1.5.0

Date 2026-11-19. **Decision.** Four primaries are added: M4A1, Mk 16 (SCAR-L, under its US designation), M24 SWS and M870.

**Pellets and shell reloads.** `WeaponDefinition` gains `PelletCount`, `PelletSpreadRadians` and `ReloadPerRound`.
- A multi-pellet shot is one `ShotAccepted` and one recoil impulse. Each pellet has its own channels (16 + 2p, 17 + 2p), impact and falloff. Pellet 0 decides whether the muzzle is blocked.
- Single-projectile guns are bit-identical.
- A tube-fed gun loads one round per phase, and a press with a round loaded fires at once.
- C0 `WeaponState` is unchanged, and `WeaponModifiers.Apply` copies the new fields.

**Geometry.** `geometry.weapon` 1.5.0 builds every 1.4.0 recipe byte-identically and adds:
- `scar`;
- `pump` (forend cycle part, shell magazine part, ghost ring);
- the `collapsible` stock.

`Tools/verify_feature_registry.py --refreeze ADR-NNN` is the reviewed refreeze path.

**Content:**

| Gun | Fire | Damage | Magazine |
|---|---|---|---|
| M4A1 | Auto, 720 rpm | 29 | 30 |
| Mk 16 | Auto, 600 rpm | 31 | 30 |
| M24 | Bolt | 100 | 5 |
| M870 | Pump | 8 × 25 in a 2.6° cone, falloff 8–30 m | 7 |

Spawn weights are 3 / 2 / 1 / 2, which changes seeded bot loadouts.

**Presentation and bots.**
- A `Pump` handling style.
- New sounds (bank v9).
- Bots treat shotguns as close range.

## ADR-119 — Accepted: 30-rank career ladder and four new perks

Date 2026-11-19. **Decision.** The ladder grows to 30 ranks on the same scale (`1000·(n²−1)`, rank 30 = 899,000 CP):

| Band | Ranks |
|---|---|
| Enlisted | PVT to SMA (1–13) |
| Warrant | WO1 to CW5 (14–18) |
| Officer | 2LT to COL (19–24) |
| General | BG to General of the Armies (25–30) |

Rewards add the ADR-118 guns, and every factory optic still precedes its gun ([career](features/career.md)).

**New perks.** This amends ADR-108's "fire rate and movement never change".
- Hip Shooter: hip spread −8% / −15%.
- Choke: pellet cone −15%.
- Slick Action: manual-action cycle −8% / −15%.
- Light Load: +3% move speed with a gun out.

They are per-mille fields of `WeaponModifiers`. Identity is unchanged for bots and replays.

**Rank count.** `CareerLadder.RankCount` is the single source. `CareerRankCountAgreementTests` pins the mirrors:
- `IconLibrary.CareerRankCount`;
- `BotRanks.MaxRank`;
- `ScoreboardRow.MaxRankIndex`;
- the reward art.

**Art.** New insignia and perk glyphs; the icon golden is `b5f08aab…`.

Saves keep CP only, so some unlocks re-lock. The owner accepted this, and there is no migration.

## Decision change template

ID/title; date; owner; status; requirement affected; measured problem; alternatives; chosen behavior; contract/schema impact; migration; affected work packages; validation and rollback; supersedes ID. Integrator approves compatibility changes; an agent cannot relax art or performance requirements by silently updating a baseline.

## ADR-120 — Accepted: parked networking behind a define and build-once pure lanes

Date 2026-11-19 (T195 phase 2). **Context.** Parked Networking (ADR-076) was still compiled into every client player, referenced by every pure test project and rebuilt by every pure lane, so motor and session refactors had to keep it compiling. Each pure wrapper also restored and built the shared project graph on its own.

**Decision.**
- `Shooter.Networking.asmdef` declares `defineConstraints: [SHOOTER_NETWORKING]`. No build defines it, so Unity neither compiles nor ships it. Resuming multiplayer means defining the symbol in an ADR.
- Its 21 tests live in `Tools/Networking.Tests` and run only in `Tools/Test-Networking.ps1`. No other test project may reference `Shooter.Networking`; `Tools/verify_asmdef_graph.py` enforces both rules.
- `Tools/Shooter.Pure.slnx` holds every pure project except the parked pair. `Tools/Test-All.ps1` restores it once in locked mode (with the NuGet advisory check), builds it once, and runs Test-Contracts, Test-Generation, Test-Simulation and Test-Game with `-NoBuild` in parallel. Each suite still checks its own locks and writes its own evidence.
- `ShooterBuild.CompileNativeGameContentAndBuildClient` bakes native navigation and builds the client in one editor session (`Compile-GameContent -Native -AndBuildClient`); `Tools/Deliver.ps1` chains content, build, smoke, package and the optional installer.

**Consequences.** ADR-076's "no new Networking code" still holds: the code is unchanged and only moved out of the default lanes. A change that breaks Networking now fails only `Test-Networking.ps1`, which remains the owner's call to run while networking is parked.

## ADR-121 — Accepted: feature lock hashes C# code tokens and every lock has a refreeze command

Date 2026-11-19 (T195 phase 3). **Context.** `feature-content-lock.json` hashed the raw bytes of every generator source, so a comment or formatting edit in weapon, soldier, motion or material code needed an ADR and a refreeze. Three of the four locks had no refreeze command, so they were edited by hand. Fingerprinting generated outputs instead was considered and rejected: soldier motion and several material and geometry paths run in the player, not in packages, so an output fingerprint would silently stop protecting them.

**Decision.**
- The feature lock records `sourceHashPolicy: cs-tokens-v1`. C# generator sources are hashed by code tokens: comments dropped, whitespace runs collapsed, spaces next to `( ) { } [ ] , ;` removed, string/char literals byte-exact. Recipes, schemas, fixtures and baseline docs stay raw-byte hashed.
- The switch was made with `verify_feature_registry.py --migrate-policy ADR-121`, which refuses unless the tree still matched the raw-byte lock exactly.
- `verify_contract_lock.py`, `verify_game_contract_lock.py` and `verify_content_format.py` gain `--refreeze ADR-NNN`. It records the ADR in `refreezeDecisions`, updates `reviewDate`, and rewrites the source, fixture and toolchain hashes from the tree.

**Consequences.** Any change to generator code, numbers or strings still fails until a reviewed refreeze names its ADR. Comment and layout edits no longer need one. No lock is edited by hand any more.

## ADR-122 — Accepted: mode rules as data and team-indexed kills (Game-v1 additive)

Date 2026-11-19 (T195 phase 3). **Context.** Game-v1 baked Team Deathmatch into the contract: limits were constants, snapshots and results had exactly `Team1Kills`/`Team2Kills`, and the session hardwired `TeamDeathmatchMatch` even though the `ITeamMatch` port existed. A second mode would have touched the contract, the session and four Presentation sites.

**Decision.** An additive amendment, with details in [game session contracts](game-session-contracts.md#adr-122--mode-rules-as-data-and-team-indexed-kills):
- `ModeRules` registry. Requests read limits and team count from their mode.
- `TeamKills` on snapshots and results; the two-team members become views.
- `GameContractVersion.MaxTeams = 2`.
- `Simulation.Match.IMatchRules` (the `ITeamMatch` port plus identity, start and abort). The session builds the match from the request's mode through a factory.
- Presentation reads the active mode's limits and name instead of `150`, `GameContractVersion.ScoreLimit` and the `team-deathmatch` literal.

A full version bump to `Game-v2` was rejected. Every change is additive, and the identity string is persisted in settings and installs (ADR-041 in-place upgrades).

**Consequences.**
- `game-contract-lock.json` was refrozen with `verify_game_contract_lock.py --refreeze ADR-122`. Game fixtures are unchanged, since none carries a snapshot or results.
- Team Deathmatch behaviour and limits are identical.
- More than two teams still needs participant/spawn team ids and a team-indexed outcome before `MaxTeams` rises.

## ADR-123 — Accepted: unified front-end visual system

Date 2026-11-19 (T196, owner-assigned). **Context.** Each screen had grown its own look. Only part of the palette was tokenised: about 190 colour literals sat in `TacticalUi`, `CareerArt`, the HUD and the shell pages, and Career art kept a second palette. Type sizes were raw numbers. Most pages used inline magic-number layouts. The main menu backdrop was a diorama of flat tinted boxes. The app icon used olive, sand and red, unrelated to the UI. The installer showed the default Inno Setup art. Subtitles used the engine font in white.

**Decision.** The owner chose to evolve the existing charcoal, amber and Barlow look into one strict system:
- **Tokens.** `Presentation/Model/Style/UiTokens.cs` (pure) holds every colour role, the type scale (`UiType`), spacing and frame anatomy (`UiSpace`, `FrameKind`). `Presentation/Session/UiInk.cs` is the only bridge to Unity colours. `UiTokensTests` fails on any new `new Color(<number>` in the UI files. It also checks WCAG 4.5:1 text contrast on every surface, HUD text over a bright scene, and ally/enemy luminance separation. Enemy coral moved from `#E47B70` to `#E68074` to reach 4.5:1 on raised plates.
- **Frame language.** `TacticalUi.Frame` gives four weights: Plate, Card, Hero and Modal. All are hard-edged (2 px radius) with a tone gradient, 1 px top highlight, hairline rim and soft shadow. Hero and Modal frames carry amber corner brackets. Every page shares `Header` (eyebrow tick, title, rule, emblem) and `KeyHints` (keycap footer). The flat fallback draws the same geometry.
- **Layouts.** Main, Settings and Armory are placed by pure `MainMenuLayout`, `SettingsLayout` and `ArmoryLayout` (`Presentation/Model/Menus/ShellLayouts.cs`). They are tested for overlap at 720p–1080p and 100–150% UI scale, like `CareerLayout`.
- **Menu vista.** `ProductMenuScene` drops the box diorama. The showcase squad stands on a rooftop overlook, and the focused map's packaged solids lie below it under that map's sky gradient, linear fog and trilight ambient, graded by the tier post-processing. These are the same root and mesh the operation card was rendered from. The pure `MenuVistaShot` holds one reviewed shot per registered map. The vista follows the hovered or focused card, else the selected operation, and cross-fades over 0.4 s (snaps under reduced motion). Low renders it at 15 Hz, other tiers at 30 Hz. Loading and Results show its last frame, so the stage never renders during a match.
- **Brand emblem.** `Generation/Core/Icons/BrandEmblem.cs` is one numeric recipe: a reticle ring, four ticks and a chevron. It is used by:
  - `AppIcon`, now a charcoal tile with an amber emblem, rendered natively at 16–256 px with thicker strokes at small sizes;
  - `TacticalUi.Emblem` and `BrandMark`;
  - `InstallerArt`, which writes the multi-size setup `.ico` and the 164×314 and 55×55 wizard BMPs that `Tools/InstallerArt` produces for `Build-Installer.ps1`.
- **Copy and type.** Settings, binding and match-banner strings moved into `ShellUiCopy`. Subtitles draw on a HUD plate in the brand face.

**Consequences.**
- This is presentation only: no C0, Game-v1, content-format or gameplay change. Fonts stay the ADR-066 exception, and there is still no imported art.
- `AppIcon`, `BrandEmblem` and `InstallerArt` are outside the feature lock's source registry, so no lock refreeze was needed. The icon atlas and its golden are unchanged. `AppIconTests` and `InstallerArtTests` pin their behaviour.
- The ADR-041 AppId and install behaviour are unchanged. The setup exe now carries the brand icon and wizard art.
- VD7, REL03 and UI02 owner review stays Pending. AI review of captures does not count.

## ADR-124 — Accepted: licensed authored art track (characters first)

Date 2026-11-27 (T232, owner decision). **Context.** ADR-002 allowed only generated visuals. After many iterations the procedural soldier (`soldier.loft`, `soldier.semantic23.v1`, `SoldierMotion`) still reads as a puppet in third person. The owner chose to step out of the procedural-only rule, starting with player and bot bodies, and to use authored animation clips as well as authored meshes.

**Decision.**
- **Allowed sources.** Models, textures and animation clips under a redistributable license only: CC0-1.0, CC-BY-4.0 or CC-BY-3.0. The repository is public, so paid store assets, Mixamo downloads and any non-redistributable file stay out.
- **Location and pins.** Authored art lives only under `UnityProject/Assets/Shooter/Art/`. Every file is hash-pinned in `Tools/art-sources.json` with its pack, and each pack records author, source URL and license; CC-BY packs also record their attribution line. `Tools/Test-SourceProvenance.ps1` rejects unlisted, drifted, unlicensed or misplaced files, and the player build rejects any art outside that manifest.
- **Import settings are code.** An editor `AssetPostprocessor` owns the model, clip and texture import settings, so the committed `.meta` files are reproducible.
- **First application: soldiers.** The owner reviewed renders and chose the CC0 Quaternius Ultimate Modular Men Swat body, with heads borrowed from the same pack. Clips come from that pack's animation set (four-way run) and the CC0 Universal Animation Library (crouch, jump, death), retargeted through Unity Humanoid. Team colour, helmets, backpacks and other kit stay generated by our own kernels and ride the authored bones. Several variants per team are picked deterministically per actor. A generated clothing layer for bare base bodies exists but is unused, since the Universal Base Characters free tier read as body paint.
- **Presentation only.** Hit volumes stay the analytic capsules of `GameplayPoseEvaluator`; Simulation, C0 and Game-v1 do not change. The procedural soldier stays in the map packages for the gallery and diagnostics. The product uses the authored body and fails loudly if its catalog is missing: there is still no silent fallback in either direction.
- **Credits.** CC-BY attributions ship in the release credits; CC0 packs are credited by courtesy.

**Consequences.**
- AGENTS.md, vision R02/R10 and the art pages now describe generated art plus this named track. Fonts and the audio bank stay their own exceptions (ADR-066, ADR-062/096).
- `build-lock.json` `provenanceAllowlistVersion` moves to 2. The `com.unity.modules.animation` engine module joins the manifest (modules are exempt from the package lock).
- An authored-body hit-volume alignment test replaces the generated-rig one as the visual guard for the head and torso capsules. P05/REL03 soldier sign-off reopens for owner review; AI review of captures does not count.

## ADR-125 — Accepted: bolt-action snipers kill on any hit

Date 2026-11-27. **Context.** Owner request: the bolt-action sniper rifles should kill with one shot to any part of the body at any distance. Under ADR-063 and ADR-110 the M24 did 50 health on an armored torso and 25 on a limb.

**Decision.**
- Weapon definition files gain an optional combat flag `lethalOnHit` (default `false`). `WeaponDefinition.LethalOnHit` carries it, and `WeaponModifiers.Apply` copies it, so perks never drop it.
- A hit from a flagged gun takes the target's remaining health, whatever the region (head, torso or limb), range or armor. Armor is neither consulted nor spent. It is still one `DamageApplied` (amount = remaining health, remaining 0) followed by `Death`.
- The generator accepts the flag only on single-projectile guns (no pellets, no knife or grenade). Unflagged rows in `WeaponCatalog.g.cs` stay byte-identical.
- Flagged: M24 SWS and M-TAC 338. Neither has falloff, and the 300 m hitscan reach (ADR-112) covers every map, so "any distance" needs no range change.

**Consequences.** Simulation and content only. C0, Game-v1, fixtures and locks are unchanged. Base damage, spread, recoil and cadence are unchanged, so the guns still need the shot to land: hip fire stays wide and ADS stays precise. Bot snipers kill faster in random-loadout matches.

Rollback: remove the two flags from `Content/Weapons` and regenerate.

Evidence: [M11 handoff](milestones/m11-playable-release/handoff.md#adr-125-bolt-action-snipers-kill-on-any-hit--2026-11-27). The in-game feel is Pending owner playtest.

## ADR-126 — Accepted: licensed fire and smoke flipbooks and burning wrecks

Date 2026-11-27 (T233, owner decision). **Context.** After the authored soldiers (ADR-124) the generated `fx.flipbook` fire and smoke (ADR-092) read as soft blobs next to them. The owner chose realistic, Houdini-rendered flipbooks for grenade explosions, muzzle fire and gun smoke, bullet impacts, and burning fires on the maps.

**Decision.**
- **Second application of the ADR-124 track.** Effect flipbook sheets are licensed textures under the same rules: redistributable license only, files only under `UnityProject/Assets/Shooter/Art/`, every file hash-pinned in `Tools/art-sources.json`. A flipbook sheet is a sprite atlas; inside this track that is allowed, outside it the AGENTS.md ban on sprites stands.
- **Source.** Unity Labs "Free VFX image sequences and flipbooks" (2016), stated CC0 on the post (no license file in the zips). Six sheets: Explosion01 and Explosion02 (5×5), FireBall01 and WispySmoke01 (8×8), Flame02 and DiscSmoke01 (16×4). The TGA flipbooks are re-saved as lossless PNG with identical pixels, under `Art/Fx/unity-labs-vfx/Resources/FxArt/`.
- **Import settings are code.** `FxArtPostprocessor` imports them as sRGB with their own alpha, mip-mapped, clamped and capped at 1024 (the 2048-wide sheets cost the same as the 1024 ones), compressed HQ.
- **Catalog, no fallback.** `Art/Fx/Resources/FxArt/fx-art.json` maps each `FxArtRole` to a sheet (grid, blend, frame rate, brightness, scene light, fire glow). `FxArtCatalog` throws if a role, the catalog or a sheet is missing; there is no silent return to the generated sheets.
- **What changes look.** `FxArtRoles.For` picks the sheet per pool kind:

| Effect | Sheet |
|---|---|
| Grenade fireball (two per blast on Balanced/High) | Explosion01 / Explosion02, played once, fire burning out into dark smoke |
| Grenade fire puffs, new `MuzzleFire` ball under the muzzle star | FireBall01, additive |
| Gun smoke, blast smoke column, blast dust ring, impact and footstep dust | WispySmoke01, tinted per surface (concrete grey, dirt brown, footsteps tan) |
| Burning wrecks | Flame02 on the roof plus a DiscSmoke01 plume |

  The muzzle star, tracers, casings, spark streaks, embers, debris chips, flash core, shockwave ring and decals keep their generated sheets. `FxFlipbook` and its goldens and lock entries are unchanged, and the generated atlases stay in the map packages.
- **Shader.** `CombatParticle` takes the grid from the particle system, blends adjacent frames through the UV2 and AnimBlend vertex streams, lights the non-glowing texels with the sky ambient plus part of the sun colour, and boosts fiery (red over blue) texels in HDR.
- **Pool.** `FxKind.MuzzleFire` (15, 0.1 s) spawns with the primary muzzle lobe, counts as a flash (hidden by reduced flash, may evict ambient proxies like the flash), and the grenade fireball lives 1.6 s instead of 0.9 s so the sheet can burn out. Gun smoke drops to 2/3 particles (Low/other) and impact dust to 3/5; caps are unchanged.
- **Burning wrecks.** `AmbientFireAnchors` picks up to four vehicles (truck, tanker, trailer, tank) of the registered map plan by a stable hash of the map id, at least 18 m apart; Low shows the first two. Each burns a flame column on the roof toward its front and a dark plume starting well above the roof, so the smoke never hides a standing soldier on one side only. One flickering light follows the nearest fire within 40 m on Balanced/High (steady under reduced flash), none on Low.
- **Presentation only.** No damage, collision, navigation, bot sight, Simulation, C0, Game-v1 or map package change.
- **Credits.** CC0, credited by courtesy in the release licenses text (from the manifest) and the README.

**Consequences.** ADR-092's "no images or sprites are imported" and the ADR-093/ADR-111 material choices are amended for the kinds above. vision, materials-rendering and combat pages describe the licensed sheets. The fill cost of larger soft puffs on the integrated GPU (RSK03) needs the Low TDM benchmark on the reference machine; that and owner P05 review of the effects stay Pending until recorded.

Rollback: revert the presenter and pool changes, remove the pack and its files from `Tools/art-sources.json`, and delete `Art/Fx`.

Evidence: [M11 handoff](milestones/m11-playable-release/handoff.md#adr-126-licensed-fire-and-smoke-flipbooks--2026-11-27).

## ADR-127 — Accepted: M870 lethal up close and per-pellet tracers

Date 2026-11-27. **Context.** Owner report: the shotgun is weak. A shotgun should throw a bunch of pellets per shot, and up close one or two pellets on the body should kill. The M870 already fired 8 pellets (ADR-118), but each did 25 with full damage only to 8 m. ADR-110 armor takes half of every torso or limb hit, so a torso pellet cost 13 health and a fresh soldier died only when all 8 pellets hit the torso inside 8 m. A blast also drew one tracer (pellet 0), so the spread was hard to read.

**Decision.**
- **Damage (content only).** `Content/Weapons/m870.weapon.json`: 100 per pellet (was 25), full damage to 10 m (was 8), falling linearly to 10 per pellet at 30 m (min scale 0.1, was 0.3). Pellet count, cone, spread, cadence, magazine and range are unchanged, and no Simulation code changes.

  | Range | Per pellet | Kills a fresh soldier (100 health, 100 armor) |
  |---|---|---|
  | to 10 m | 100 | 2 torso pellets, or 1 torso + 2 limb, or 4 limb, or 1 head; 1 torso pellet once the armor is gone |
  | 15 m | 77 | 3 torso pellets |
  | 20 m | 55 | 4 torso pellets |
  | 30 m and beyond | 10 | effectively never |

- **Armory.** The damage bar clamps damage per shot at 200 (full health plus full armor). The M870 (8 × 100) fills it, and every other bar keeps today's length.
- **Pellet tracers (presentation only).** `PelletTracerTracker` (Presentation.Model) finds every further `Impact` from the same shooter in the same tick that is off pellet 0's ray. `ProductSessionView` draws each one as a tracer from the visible muzzle toward that impact (`AnalyticFxPool.SpawnPelletTracer`). Single-projectile guns produce only the pellet-0 impact, so they are unchanged. Reduced flash hides pellet tracers like the primary tracer. A pellet that hits nothing within 60 m has no impact and draws no tracer.
- **FX caps.** `MaxTracers` 16 → 24 (one full blast beside ordinary gunfire), with `MaxInstances` 96 → 104 to keep the ADR-111 shared-cap slack. The presenter allocates 8 more pooled particle systems.

**Consequences.** Content, Presentation and tests only. C0, Game-v1, fixtures and locks are unchanged. Bots carrying the M870 are equally lethal up close; they still never fire it past its 30 m falloff end. Pending: owner in-game balance and tracer-look check, Unity lanes and a client build, and the TDM-5v5 Low benchmark with the larger tracer cap.

Rollback: restore the three combat values in `m870.weapon.json` and regenerate; revert the tracker, tracer helper, caps and Armory clamp.

Evidence: [M11 handoff](milestones/m11-playable-release/handoff.md#adr-127-m870-close-range-lethality-and-pellet-tracers--2026-11-27).

# Vision, scope and requirements

Status reviewed 2026-11-14 from 84befb9 / a54ed83: M0–M7 retain historical owner-waived closures. A clean `7cd7e81` test archive exists; later T066/T068/T069/T070 software is integrated. Manual gameplay, remaining native-navigation publication and M8–M11 acceptance stay open in the [current audit](product-state-audit.md). The [launch guide](playtest-guide.md) gives the immediate owner path and [remaining queue](next-playable-build.md) keeps original scope. Product requirements and Unity 6 URP toolchain remain fixed.

## Product

Owner quality goal, 2026-11-14: **the finished visual design should be as good as Battlefield V, Bodycam and Warface**. [VD0–VD9](design-milestones.md) turn this into art-direction, representative-slice, component and final comparative review gates. Current 0.3.1 is a software baseline; this quality is unproven. The procedural-art boundary, fixed gameplay/quality separation, reference Low30 budget and existing open acceptance remain binding. A demonstrated conflict requires an explicit owner decision instead of an automatic quality or performance reduction.

A grounded, modern-world military FPS whose geometry, surface detail and motion come from algorithms and numeric definitions. Build a small, convincing combined indoor/outdoor combat space before expanding content. Realism means believable scale, silhouettes, material response, motion, lighting and feedback; visual parity with commercial AAA shooters is an aspiration, not an acceptance claim.

The first playable slice is an offline 256 × 256 m training compound, one local player, eight hostile soldiers, one rifle and one pistol, a single objective, solid cover, repeatable lighting and a replayable benchmark. Networking follows the offline slice, but simulation boundaries and command IDs exist from the first milestone. Initial multiplayer validation is four players plus eight bots; eight players is a later capacity gate, not a promise of large battles.

That paragraph defines the historical M0–M7 slice. **The next owner-requested deliverable** is a Windows offline game with two distinct procedural maps; one human and allied bots versus enemy bots; 5v5 default and selectable 6v6; Team Deathmatch ends at 150 enemy kills or 15 active minutes, followed by team/player results. A main menu selects map, supported mode (only Team Deathmatch), team size and sound/video/control settings. [Exact rules](features/team-deathmatch.md), [maps](features/playable-maps.md) and [shell](features/game-shell.md) define defaults. This is new M8–M11 work, not a retroactive claim about M4 or a network-capacity expansion. No remote transport is needed to play this release.

## First-version scope: local bots only

Owner clarification, 2026-11-14: the first version is **one human playing locally with allied/enemy bots**, entirely on the player's PC. No online multiplayer, LAN/remote player support, external server process, rented server, hosting purchase/account or online service is required to launch or complete it. Online multiplayer and hosting are deferred beyond this release. Existing dedicated-server-compatible authority boundaries and historical network code may stay; they do not create a new online delivery gate. Retained Server build/isolation checks are architectural regression evidence, not a request to provision a server. Existing local gameplay, visual/provenance and reference-performance requirements remain unchanged.

## Windows installer delivery

Owner clarification, 2026-11-14: final delivery is a **Windows installer**, and running a newer version's installer must update the existing game in the system, preserving settings/controls and the installation identity/path/shortcuts. A ZIP is only a preview/internal artifact. [Installation and upgrade contract](features/windows-installer.md) defines the required install/update/repair/uninstall/rollback evidence; T073 is planned, not implemented here.

## Confirmed constraints

| ID | Requirement | Evidence of acceptance |
|---|---|---|
| R01 | Unity 6, URP, C# gameplay/generation, HLSL shading | Clean client/server builds and locked toolchain |
| R02 | No imported or manually modeled 3D meshes, painted/image-authored surface textures, sprites, authored skeletal clips or motion-capture animation | Provenance scan plus regeneration from source definitions |
| R03 | Generated meshes, PBR texture arrays, LODs, skeletons, skin weights and algorithmically sampled motion caches are allowed | Cache manifests identify generator and inputs |
| R04 | Ryzen 5 4600H Radeon integrated graphics, 16 GB DDR4-3200 with two slots occupied, NVMe SSD is minimum reference | Hardware report records adapter, driver, actual RAM channel mode and power conditions |
| R05 | 1280 × 720 internal render, Low, 30 FPS initial target; 60 FPS stretch | [Performance gates](performance.md) on actual reference hardware |
| R06 | Quality profiles and repeatable performance tests begin in foundation milestone | Profiles and benchmark artifacts exist at M0 |
| R07 | Collision, cover, hit volumes, AI visibility and weapon rules do not change with graphics profile | Cross-profile simulation tests |
| R08 | Asset generation is reproducible and versioned; expensive generation moves out of gameplay | [Generation contract](generation.md) and cache tests |
| R09 | Independent agents implement bounded modules against frozen shared contracts | [Coordination rules](agent-coordination.md) |
| R10 | Modern weapons, procedural soldiers, environments, AI, FPS combat and staged networking | Feature acceptance tests and [milestone gates](milestones.md) |
| R11 | Offline 5v5/6v6 with one human, allied/enemy bots, 150 kills / 15 minutes, safe respawn and immutable results | M9 TDM01–TDM08 and M11 REL02 |
| R12 | Two distinct packaged procedural maps, both supporting the team mode and sizes | M10 MAP01–MAP03; clean release REL01 |
| R13 | Actual main/setup/pause/settings/scoreboard/results UI; persistent applied sound/video/control/accessibility preferences | M10 UI01–UI02, SET01–SET02; M11 player review |
| R14 | Source-matched Windows installer, clean offline installation and in-place N→N+1 upgrade preserving user preferences; ZIP remains preview/internal only | M11 REL01–REL06, including new full-roster performance |
| R15 | Reference-comparable visual design against the approved Battlefield V / Bodycam / Warface comparison set | VD0–VD9, named owner review of all applicable categories plus unchanged technical/release gates; no parity claim from software tests alone |

Two occupied memory slots do not prove dual-channel operation. The temporary memory-usage screenshot is explicitly excluded from requirements, budgets and diagnosis.

## Art boundary

Permitted source: code, numeric measurements, analytic curves, rule grammars, JSON definitions, scalar/vector key parameters and test fixtures. Human-written code and dimensions are necessary and allowed. Not permitted: hand-edited mesh vertices, sculpted surfaces, photo/PBR libraries, scanned assets, downloaded character rigs, AI image textures, hand-keyed joint pose sequences, exported DCC models or third-party visual asset packs. AI coding tools may write generators but may not smuggle authored assets into output.

Generated textures are algorithmic raster caches, not painted art. Generated geometry may be serialized in Unity native form with provenance. Runtime animation uses gait/IK dynamics; optional sampled caches must be regenerated from those equations. Generated particles use analytic geometry/materials rather than sprite atlases. No mesh impostor/billboard texture requirement is assumed; low detail uses simplified mesh geometry.

Fonts and sound are not 3D/surface/animation assets. Baseline UI uses an engine-supplied licensed font with a recorded exception in the provenance allowlist. M0–M4 baseline sound was synthesized. [ADR-055](decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086) / [ADR-062](decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089) authorize a provenance-recorded stereo PCM combat sample bank (`Content/Audio/combat`) for non-commercial builds; see [features/audio.md](features/audio.md). Extracting audio from third-party commercial games remains out of scope.

## Non-goals for the first release slice

No open world, vehicles, playable aircraft, structural destruction, deformable terrain, weapon attachments marketplace, cinematic faces, lip sync, full cloth simulation, licensed brands, ranked matchmaking, persistent progression, console/mobile support, VR, full photorealistic skin or ray-traced lighting. No deterministic PhysX lockstep, whole-world ray marching, general-purpose DCC replacement or custom engine. Do not silently trade these constraints for better visuals.

## Defaults requiring validation, not a blocking user decision

Windows x64/D3D11 is the first client target; Windows headless is the first server target. Use fictional factions and unbranded weapons. Medium/high profiles are scalability probes. Player capsule defaults: 1.8 m standing, 0.32 m radius; walk 3.5 m/s, sprint 5.5 m/s; all tuning lives in versioned data. Networking transport selection and exact Unity patch are M0/M5 gates, recorded in [decisions](decisions.md). There is no commercial scope or schedule commitment.

## Definition of implementation-ready

Every package has an owner, permitted folders, input/output contracts, dependencies, failure behavior and acceptance checks. Unknowns have a spike, a measurable exit and a fallback within the art constraint. Each milestone produces an executable artifact and evidence; completion cannot be claimed from documentation or screenshots alone.

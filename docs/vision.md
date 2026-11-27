# Vision, scope and requirements

This page holds scope and requirements. Current state and remaining work are in [next playable build](next-playable-build.md). The visual quality target is in [visual direction](visual-direction.md).

## Product

A grounded, modern-world military FPS. All geometry, surface detail and motion come from algorithms and numeric definitions.

Realism means believable scale, silhouettes, material response, motion, lighting and feedback. The owner's quality target is Battlefield V, Bodycam and Warface ([VD0–VD9](visual-direction.md)). Parity is an aspiration that needs named review, not a claim. If the target conflicts with the art boundary or the Low budget, the owner decides. Nothing is reduced automatically.

## First release

The first release is **one human with local allied and enemy bots, entirely offline on the player's PC**. Online play, LAN, an external server, hosting and online services are deferred. Under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence) (2026-11-16):
- **Networking is parked.** The `Networking` code stays in git but is dormant, and no Unity assembly references it.
- **No server build.** The Dedicated Server build is not run by CI or by the release flow.
- **Server gates are Parked, not Pending.**

Simulation stays the single authority and Presentation stays read-only.

In the game, the main menu selects:
- a map: Old Town, Harbour or Oil Depot ([maps](features/tdm-map-layouts.md));
- Team Deathmatch;
- a team size: 5v5, 8v8, 10v10 or 15v15;
- settings.

The other systems:
- [Team Deathmatch rules](features/team-deathmatch.md) (150 enemy kills or 15 active minutes, then results);
- the [shell and Armory](features/game-shell.md);
- a local [career](features/career.md).

Delivery is a **Windows installer**. Running a newer installer updates the installed game in place and keeps settings, controls and the installation identity ([installer](features/windows-installer.md), ADR-041). ZIPs are internal artifacts only.

## Requirements

| ID | Requirement | Accepted by |
|---|---|---|
| R01 | Unity 6, URP, C# gameplay and generation, HLSL shading on the locked toolchain | Clean Client build |
| R02 | Generated art by default; authored models, textures and clips only through the licensed art track ([ADR-124](decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)), never painted, photo or AI image textures or sprites | Provenance scan, art-source manifest and regeneration from source |
| R03 | Generated meshes, PBR arrays, LODs, skeletons, skin weights and sampled motion caches are allowed | Cache manifests name generator and inputs |
| R04 | Reference: Ryzen 5 4600H integrated Radeon, 16 GB DDR4-3200 (two slots), NVMe | Hardware report records adapter, driver, channel mode and power |
| R05 | Native 1280×720 Low at 30 FPS; 60 FPS is a stretch | [Performance](performance.md) gates on reference hardware |
| R06 | Quality profiles and repeatable benchmarks | Profiles and workload fixtures |
| R07 | Collision, cover, hit volumes, AI visibility and weapon rules are identical across graphics profiles | Cross-profile simulation tests |
| R08 | Reproducible, versioned generation, done outside gameplay | [Generation](generation.md) and cache tests |
| R10 | Modern weapons, soldiers (licensed authored bodies and clips with generated gear), environments, AI and FPS combat | Feature acceptance tests |
| R11 | Offline TDM at every catalog size, with safe respawn and immutable results | [TDM01–TDM08](acceptance.md#m9--team-deathmatch), REL02 |
| R12 | Distinct packaged procedural maps, each supporting every catalog size | [MAP01–MAP03](acceptance.md#m10--maps-shell-and-settings), REL01 |
| R13 | Real menus, pause, settings, scoreboard and results; persistent settings that actually apply | UI01–UI02, SET01–SET02 |
| R14 | Source-matched installer with offline install and in-place upgrade | [REL01–REL06](acceptance.md#m11--release), INS01–INS06 |
| R15 | Reference-comparable visuals against the approved comparison set | VD0–VD9 named owner review |

Two occupied memory slots do not prove dual-channel operation.

## Art boundary

**Permitted sources:**
- code;
- numeric measurements;
- analytic curves;
- rule grammars;
- JSON definitions;
- test fixtures.

**Not permitted:**
- hand-edited mesh vertices or sculpts;
- photo/PBR libraries or scans;
- downloaded rigs;
- AI image textures;
- hand-keyed joint poses;
- DCC exports;
- third-party visual packs.

AI coding tools may write generators, but they may not slip authored assets into the output.

**Licensed art track** ([ADR-124](decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)). Authored models, textures and animation clips are permitted only when they are CC0 or CC-BY, live under `UnityProject/Assets/Shooter/Art/`, and are hash-pinned with author, source URL and license in `Tools/art-sources.json`. Soldiers are the first use: CC0 humanoid bodies and clips, dressed in generated military gear. Everything else in the list above still applies outside that track.

How generated output is treated:
- **Textures** are algorithmic raster caches.
- **Geometry** may be serialized in Unity native form with provenance.
- **Animation** uses gait, IK and dynamics.
- **Particles** use analytic geometry.
- **Low detail** uses simplified meshes, not billboards.

**Fonts and sound** are outside the 3D art rule. UI uses allowlisted fonts ([ADR-066](decisions.md#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed)). Combat audio is a provenance-recorded stereo PCM bank for non-commercial builds ([ADR-062](decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089), [audio](features/audio.md)). Audio extracted from commercial games is out of scope.

## Non-goals

The first release excludes:
- open world, vehicles, aircraft, structural destruction or deformable terrain;
- cinematic faces, lip sync or full cloth simulation;
- licensed brands or ranked matchmaking;
- online or account-bound progression (the local career of [ADR-108](decisions.md#adr-108--accepted-career-ranks-unlocks-buffs-and-local-service-record) is the one exception);
- console, mobile or VR;
- photoreal skin or ray tracing;
- PhysX lockstep, whole-world ray marching or a custom engine.

Factions and kit are fictional. Weapons use real-world names but no brand assets. Player capsule defaults are 1.8 m standing and 0.32 m radius, with walk at 3.5 m/s and sprint at 5.5 m/s. All tuning lives in versioned data.

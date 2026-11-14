# Milestones and release gates

Milestones are dependency gates, not calendar promises. [Backlog](backlog.md) gives ordered tasks. No milestone is complete until implementation exists and attached evidence passes [validation](validation.md).

Current disposition, 2026-11-14: M0–M7 retain historical owner-waived administrative closures (ADR-017/025–028/030–032), with original FAIL/Pending evidence and P05 FAIL unchanged. M8–M11 have software closeout records, but the [current audit](product-state-audit.md) leaves M8–M10 integration incomplete and M11 acceptance open. [T052–T065](release-remediation-plan.md) repair these same gates; no M12 or inherited waiver is created. Closure counts must not be presented as technical acceptance counts.

| Gate | Deliverable | Required exit evidence |
|---|---|---|
| M0 Foundation | Pinned Unity/URP, asmdefs, pure contracts, fake world, Low/Balanced/High settings, build scripts, benchmark shell | Clean client and headless builds, contract fixtures, B01 capture on reference machine, provenance policy, toolchain lock |
| M1 Feasibility | Compiler v1, primitive/material outputs, one soldier rig/gait and rifle | Cold/warm cache reproduction, rig/visual review and timing, key risks resolved or scoped ADR |
| M2 Combat harness | Motor, rifle/pistol, hit volumes, ammo/reload/death, procedural feedback | Tick tests, cover/muzzle cases, render-FPS independence, no authored assets |
| M3 Generated compound | Full bounded map, collision/nav, chunks, eight AI soldiers | Reachability/seam tests, query bounds, 30-minute offline soak, B02/B03 evidence |
| M4 Offline slice | Objective loop, UI/settings, polished constrained visuals, Low optimization | All R01–R09 offline obligations; native 720p 30 FPS gate, memory/load budgets, reviewed realism and provenance |
| M5 Network foundation | Framework decision, loopback/dedicated authority, prediction/interpolation | Client/headless isolation, packet fuzzing, command recovery, two-player impaired-network tests |
| M6 Network slice | Four players + eight bots, lag compensation, late join | B04 soak, 150 ms/3% loss correctness, security limits, bandwidth and server timing |
| M7 Hardening/stretch | Reproducible release, broader seed/profile coverage; 60 FPS experiment | Clean CI rebuild, all required gates, 60 FPS labeled passed/failed separately; capacity eight players optional new gate |
| [M8 Runtime composition](milestones/m8-runtime-composition/index.md) | Source-audit repair, Game-v1 freeze and real one-map input/authority/world/view/audio path | RUN00–RUN05; actual player and lifecycle evidence |
| [M9 Team Deathmatch](milestones/m9-team-deathmatch/index.md) | 5v5/6v6 team bots, score/time/respawn/results data | TDM01–TDM08; 9/11-bot capacity and complete matches |
| [M10 Maps and shell](milestones/m10-maps-shell/index.md) | Two distinct maps, menus, tables, applied persistent settings | MAP01–MAP03, UI01–UI02, SET01–SET02 in actual player |
| [M11 Playable release](milestones/m11-playable-release/index.md) | Clean installable offline archive with accepted content | REL01–REL06; full-roster native720p Low30, review and rollback |

## Slice acceptance checklist

The checklist below describes the historical Compound Clear slice. New release acceptance is the [M11 matrix](milestones/m11-playable-release/validation-acceptance.md): both maps, both team sizes, score and timeout finish, results/replay, menu/settings and clean independent build. Keep both rule sets explicit rather than overwriting historical evidence.

User can launch Windows release build, select Low, load deterministic compound, move/aim/fire/reload two weapons, fight eight soldiers, complete objective, restart and change settings. No missing colliders, corrupt meshes, persistent animation invalidity, unbounded memory or gameplay rules differing by quality. Content restores without local handcrafted assets. Reference-machine evidence meets [performance](performance.md), with failure honestly reported. R10 networking is met only after M6; M4 is explicitly offline.

## Checkpoints and rollback

Tag each passing gate with source, toolchain, content and benchmark manifest. Keep previous passing artifact. Failed visual spike keeps scope at M1 rather than expanding environments. Failed performance gate blocks M4 and prioritizes budget fixes. Failed network prediction keeps offline slice valid but blocks multiplayer claims. Failed provenance invalidates the affected generated cache and requires source correction/rebuild, not merely file renaming.

## Milestone implementation packs

[Open the milestone folders](milestones/index.md) for stage-specific implementation sequences, contracts, agent assignments, tests and handoff records. Each folder links to the shared specifications in the docs root. Root specifications remain authoritative; current product disposition and repair evidence are linked in the audit and FIX matrix.

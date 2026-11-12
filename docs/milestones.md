# Milestones and release gates

Milestones are dependency gates, not calendar promises. [Backlog](backlog.md) gives ordered tasks. No milestone is complete until implementation exists and attached evidence passes [validation](validation.md).

Owner disposition on 2026-11-12: **M0 closed with explicit user waiver; one of eight milestones closed.** This recorded exception accepts the implemented foundation while leaving render/thermal acceptance and observed preconditioning pauses unverified. It is not an all-gates-pass claim; see the [M0 handoff](milestones/m0-foundation/handoff.md). M1 has started with P01 deterministic foundations; compiler, geometry, materials and soldier acceptance remain pending in the [M1 handoff](milestones/m1-procedural-feasibility/handoff.md). M2-M7 have not started; future performance gates are unchanged.

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

## Slice acceptance checklist

User can launch Windows release build, select Low, load deterministic compound, move/aim/fire/reload two weapons, fight eight soldiers, complete objective, restart and change settings. No missing colliders, corrupt meshes, persistent animation invalidity, unbounded memory or gameplay rules differing by quality. Content restores without local handcrafted assets. Reference-machine evidence meets [performance](performance.md), with failure honestly reported. R10 networking is met only after M6; M4 is explicitly offline.

## Checkpoints and rollback

Tag each passing gate with source, toolchain, content and benchmark manifest. Keep previous passing artifact. Failed visual spike keeps scope at M1 rather than expanding environments. Failed performance gate blocks M4 and prioritizes budget fixes. Failed network prediction keeps offline slice valid but blocks multiplayer claims. Failed provenance invalidates the affected generated cache and requires source correction/rebuild, not merely file renaming.

## Milestone implementation packs

[Open the milestone folders](milestones/index.md) for stage-specific implementation sequences, contracts, agent assignments, tests and handoff records. Each folder links to the shared specifications in the docs root. Root specifications remain authoritative; implementation status is planned until evidence is reviewed.

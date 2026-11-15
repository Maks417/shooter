# Feature: compact TDM map layouts (T076)

Owner assignment 2026-11-15: redesign catalog maps as compact Team Deathmatch arenas sized for 5v5 and 6v6. ADR-045 covered `training-compound` and `depot-yard`; ADR-071 adds `oil-depot` with limited elevated routes. Map ids, display names, seeds, recipe/package/metadata schemas, SGAF Collision v1, nav settings and the 256 m world/32 m chunk frame stay on the existing package format; generated gameplay topology (and therefore package identities) changes per map. The legacy W01 `CompoundPlanner` and its canonical-seed goldens stay untouched for historical M1–M3 evidence; the catalog uses arena planners.

## Design principles applied

Sources: Valve Developer Community *Deathmatch Map Design Theory* and *Multi-Player Mapping Tips*; Epic *Gears of War Multiplayer Map Theory*; The Level Design Book (circulation, typology, combat balance); TF2 map theory (spawn exits: main choke + major/minor flank); Call of Duty three-lane practice as reported by GamesRadar.

1. **Three lanes, connected.** Each map has a left, middle and right lane running spawn-to-spawn, linked by at least two cross connectors so players can rotate without returning to spawn. Lanes have different characters (long sightline / close quarters / mixed) so loadout and playstyle choices matter.
2. **Size for player count.** 10–12 players need contact within ~8 s of spawning but room to flank. With sprint 5.5 m/s the spawn-to-centre path is 38–55 m and the playable area is at most 100 × 120 m (target ~85 × 105 m) — roughly a quarter of the old 256 m area.
3. **Symmetry for fairness.** Gameplay collision is mirror-symmetric across `z = 0` (team 1 stages at −Z, team 2 at +Z). Visual-only variation is allowed later; gameplay boxes must mirror exactly.
4. **Protected spawns with multiple exits.** Each staging zone sits behind a spawn wall with three exits (one per lane), no line of sight from mid or from the enemy staging zone, and no sharp U-turn on leaving.
5. **Controlled sightlines.** Long sightlines exist only in the designated long lane and are capped at 60 m; mid and the CQB lane break sight every ≤ 25 m. No spawn-to-spawn and no centre-to-spawn eye line.
6. **Cover rhythm.** Every open walkable point outside staging has cover within 8 m (≈1.5 s sprint). Half cover (1.1 m) hides a crouched eye (1.05 m) but not a standing one (1.65 m); full cover is ≥ 2.0 m. With jump apex ≈0.63 m and step 0.25 m, half cover is not climbable, so lanes cannot be shortcut by vaulting.
7. **Readable chokepoints, no dead ends.** Doors ≥ 2.0 m clear, corridors ≥ 3.0 m, lanes 6–12 m. Every enclosed room has ≥ 2 exits.
8. **Map control in the middle.** A central structure is the strongest position but is reachable from all three lanes, so holding it is contestable.

## Numeric rules (enforced by `ArenaRules` validation)

| Rule | Threshold |
|---|---|
| Playable rectangle (inside perimeter walls) | 70–100 m along X, 90–120 m along Z |
| Perimeter wall height | ≥ 4.0 m, closed except none (no gaps) |
| Team anchor separation along Z | 80–110 m |
| Nav path anchor → map centre (1 m grid, capsule clearance) | 38–55 m each team |
| Nav path anchor → enemy anchor | 85–140 m |
| Staging exits per team | ≥ 3, pairwise ≥ 8 m apart, each reaches the centre |
| Distinct routes (existing similarity rule, via points = lane anchors) | ≥ 3, ≥ 1 covered |
| Spawn LOS | none between any T1 and T2 slot; none from centre eye (0, 1.65, 0) to any slot |
| Longest clear eye-height sightline (4 m sample grid × 16 directions, inside playable) | ≤ 60 m |
| Cover within 8 m of every open sample outside staging | 100 % of samples; cover = gameplay box with height ≥ 1.0 m |
| Gameplay mirror symmetry across z = 0 | every box has a mirror twin (tolerance 1 mm) or lies on z = 0 symmetric |
| Ceilings | ≤ 400 gameplay boxes, ≤ 64 cover proxies |
| Spawn slots | 12 per team, inside own staging rectangle, ≥ 2 m spacing |

Ground is flat (`y = 0`, 2×2 zero height grid) for Compound and Depot. ADR-071 adds limited walkable elevation on Oil Depot only: thin gameplay slabs (≤0.40 m) for stairs/catwalks, Y-aware occupancy (pass-under), and layered climb when a path endpoint is elevated. Roofs stay non-gameplay.

## Map A — Training Compound (`training-compound`, mirror across z = 0)

Playable rectangle x ∈ [−44, 44], z ∈ [−52, 52] (88 × 104 m). Team 1 anchor (0, 0, −46), team 2 anchor (0, 0, 46).

- **Staging** z ∈ [−52, −38] (team 1). Spawn wall at z = −37 (3 m tall, 0.4 m thick) across x ∈ [−44, 44] with three 4 m gates centred at x = −30, 0, +30. Two 1.1 m half-cover crates in front of each gate.
- **Left lane — Range (long sightline)** x ∈ [−44, −24]: open firing range with staggered 1.1 m sandbag rows every ~10 m, alternating sides, and two 2.4 m concrete blocks at z = ±14 to cap the lane sightline at ≤ 60 m.
- **Middle — HQ building** centred (0, 0), shell 16 × 12 m (walls 3.5 m high, roof), doors 2.4 m wide on all four sides (N, S, E, W) plus two interior pillars; outdoor plaza around it with 1.1 m planters. HQ blocks spawn-to-spawn and centre-to-spawn sight.
- **Right lane — Barracks (close quarters, covered route)** x ∈ [24, 44]: two barracks buildings 12 × 22 m spanning z ∈ [−30, −8] and its mirror z ∈ [8, 30], each with doors at both short ends and one side door toward mid; interior partition walls create 2–3 rooms; gap z ∈ [−8, 8] is a small courtyard with full cover.
- **Lane separators** at x = ±18: low 2.4 m walls/sheds broken by cross connectors at z = ±18 and z = 0 (3 m openings), so the three lanes are distinct but rotations are short.

## Map B — Depot Yard (`depot-yard`, mirror across z = 0)

Playable rectangle x ∈ [−40, 40], z ∈ [−56, 56] (80 × 112 m). Team 1 anchor (0, 0, −50), team 2 anchor (0, 0, 50). Distinct grammar from the compound: industrial, linear, container-driven.

- **Staging** z ∈ [−56, −42]; gatehouse wall at z = −41 (3 m) with three gates at x = −28, 0, +26.
- **Left lane — Rail siding (long sightline)** x ∈ [−40, −20]: one track bed with three boxcars (3.0 × 3.2 × 12 m, long axis Z) staggered between x = −34 and x = −26 at z = −22, 0 (centred, symmetric) and +22, plus 1.1 m rail buffers; sightline capped ≤ 60 m.
- **Middle — Central warehouse** 22 × 26 m centred (0, 0), walls 5 m high with roof, two 3 m loading doors on each short end (N/S) and one 2.4 m door on each long side; interior shelving rows (2.2 m high, 1.2 m deep) running X with 3 m aisles; 1.2 m loading-dock lip outside each N/S door.
- **Right lane — Container stacks (CQB maze, covered route)** x ∈ [18, 40]: shipping containers 2.5 × 2.6 × 6 m in staggered rows forming two parallel twisting alleys (≥ 3 m wide) with short sightlines (≤ 20 m); a few double-stacked 5.2 m-tall containers as hard sight breaks.
- **Cross connectors** at z = ±30 (yard aprons between staging and warehouse) and z = 0 through the warehouse side doors.

## Map C — Oil Depot (`oil-depot`, mirror across z = 0) — ADR-071

Playable rectangle x ∈ [−42, 42], z ∈ [−54, 54] (84 × 108 m). Team 1 anchor (0, 0, −48), team 2 anchor (0, 0, 48). Petroleum grammar distinct from Compound and Depot Yard.

- **Staging** z ∈ [−54, −40]; gatehouse wall at z = −39 (2 m deep) with three gates at x = −28, 0, +28 and baffle chutes.
- **Left lane — Tank farm** x ∈ [−42, −18]: octagonal/drum tanks, pipe cover, sight chicanes; keep lane via (−28, 0) open.
- **Middle — Pump house** 16 × 14 m centred (0, 0), doors on all four sides, interior pillars, outdoor apron cover and axis sight blocks.
- **Right lane — Container / shed CQB** with a clear strip for the elevated flank.
- **Elevated flank** at x ≈ 32: walkable catwalk deck top 2.25 m (underpass), stair runs ≤0.25 m risers at both ends, mirrored across z = 0; half-cover rails.
- **Cross connectors** at z ≈ ±28 through separator gaps and pump-house side doors.

## Implementation boundary

- Pure files under `UnityProject/Assets/Shooter/Generation/Core/Environment/`: `ArenaLayout.cs`, `ArenaRules.cs`, `ArenaGeometry.cs` (including stair/catwalk helpers), `CompoundArenaPlanner.cs`, `DepotArenaPlanner.cs`, `OilArenaPlanner.cs`, `ProductMapPlanner.cs`. Each new `.cs` needs a committed `.meta` with a fresh GUID.
- `CompoundPlan` gains an optional `ArenaLayout? Arena` (null keeps legacy behaviour). `MapSpawnLayout` and `MapTopologyValidation` use the arena when present. `MapPackageBuilder`, `ReleaseCatalogDocument` and `CompoundCatalogFactory` dispatch through `ProductMapPlanner`.
- Collision/nav hashes are derived from the actual box list (not seed-only). Stable IDs: compound 10000+, depot 20000+.
- Must stay C# 9 / .NET Standard 2.1, Unity-free (linked by `Tools/Generation`, `Tools/Foundation` and test projects).
- Presentation (`ProductMapView`, leased to T075/surfaces) is not edited; it renders from the package as before.

## Validation and evidence

Pure: `Tools/Test-Generation.ps1`, `Tools/Test-Maps.ps1`, `Tools/Test-Simulation.ps1`, `Tools/Test-Game.ps1`, `Tools/Test-Contracts.ps1`, source provenance, docs/fixtures checks. Content: regenerate packages with `Tools/Compile-GameContent.ps1` (native nav with the locked editor where available) and record old/new package identities in the M11 handoff. Rule validation proves geometry constraints only; enjoyable flow, spawn pressure and bot behaviour on the new layouts remain Pending manual playtest of both maps at 5v5 and 6v6.

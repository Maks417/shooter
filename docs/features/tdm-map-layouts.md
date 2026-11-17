# Feature: compact TDM map layouts (T076, ADR-074)

Owner assignment 2026-11-15: redesign catalog maps as compact Team Deathmatch arenas sized for 5v5 and 6v6. ADR-045 covered `training-compound` and `depot-yard`; ADR-071 added `oil-depot` with limited elevated routes. ADR-074 (owner request 2026-11-15) shrinks all three maps to 64 × 80 m, because play showed every fight collapsing into the middle while the spawn-side halves stayed empty. It also replaces the shared "box building in the middle, free-standing walls around it" template with a layout grammar per map. Map ids, display names, seeds, recipe/package/metadata schemas, SGAF Collision v1, nav settings and the 256 m world/32 m chunk frame stay on the existing package format; generated gameplay topology (and therefore package identities) changes per map. The legacy W01 `CompoundPlanner` and its canonical-seed goldens (B02/B03) stay untouched for historical M1–M3 evidence; the catalog uses arena planners.

## Design principles applied

Sources: Valve Developer Community *Deathmatch Map Design Theory* and *Multi-Player Mapping Tips*; Epic *Gears of War Multiplayer Map Theory*; The Level Design Book (circulation, typology, combat balance); TF2 map theory (spawn exits: main choke + major/minor flank); Call of Duty three-lane practice as reported by GamesRadar.

1. **Three routes, each with its own character.** Every map has a left, middle and right route from spawn to spawn. Players rotate between them without going back to spawn. The routes differ (interior/CQB, open street or yard, elevated or long), so loadout and playstyle choices matter.
2. **Size for player count.** 10–12 players should meet within ~5–7 s of spawning. With sprint 5.5 m/s the grid path from spawn to centre is 26–44 m (grid paths overstate walked distance), and the playable area is 64 × 80 m, roughly half the ADR-045 area.
3. **Symmetry for fairness, not sameness.** Team 2's half is an exact image of team 1's: a z-mirror (Depot Yard, Oil Depot) or a 180° rotation about the centre (Training Compound). Rotation turns the compound's corner buildings into a pinwheel: each team's left route goes through a different building than its right route.
4. **Protected spawns with multiple exits.** Each staging zone sits behind parked vehicles or a building, with three exits. There is no line of sight from the centre or from the enemy staging zone. From the doorway of any exit at most four own slots are visible within 40 m: gate chutes are formed by parked trailers, and staging dividers have offset openings.
5. **Controlled sightlines.** The longest clear eye line is 48 m. There are no spawn-to-spawn or centre-to-spawn eye lines.
6. **Cover rhythm.** Every open walkable point outside staging has cover within 8 m (≈1.5 s sprint). Half cover (1.1 m) hides a crouched eye (1.05 m) but not a standing one (1.65 m); full cover is ≥ 2.0 m. With jump apex ≈0.63 m and step 0.25 m, half cover is not climbable, so routes cannot be shortcut by vaulting.
7. **Readable objects, no strange walls.** Every blocking box is a nameable object: a building wall, parked vehicle, boxcar, container, sandbag wall, tank, pipe rack or booth. Free-standing separator walls, U-pockets and spawn baffle walls are gone (validated by `ArenaRulesTests.No_free_standing_thin_walls`). Vehicles, rolling stock, sandbags and fences carry an `ArenaFeature` descriptor that the mesh-only dressing draws as the real object.
8. **Readable chokepoints, no dead ends.** Doors ≥ 2.0 m clear, corridors ≥ 3.0 m. Every enclosed room has ≥ 2 exits.
9. **A different centre per map.** Training Compound's centre is an open parade square, Depot Yard's is a train with a crossing, and Oil Depot's is a pump house. Holding the centre is reachable from all three routes and contestable.

## Numeric rules (enforced by `ArenaRules` validation)

| Rule | Threshold |
|---|---|
| Playable rectangle (inside perimeter walls) | 56–72 m along X, 72–88 m along Z |
| Perimeter wall height | ≥ 4.0 m, closed except none (no gaps) |
| Team anchor separation along Z | 60–76 m |
| Nav path anchor → map centre (1 m grid, capsule clearance) | 26–44 m each team |
| Nav path anchor → enemy anchor | 60–100 m |
| Staging exits per team | ≥ 3, pairwise ≥ 8 m apart, each reaches the centre |
| Distinct routes (existing similarity rule, via points = lane anchors) | ≥ 3, ≥ 1 covered |
| Spawn LOS | none between any T1 and T2 slot; none from centre eye (0, 1.65, 0) to any slot |
| Longest clear eye-height sightline (4 m sample grid × 16 directions, inside playable) | ≤ 48 m |
| Cover within 8 m of every open sample outside staging | 100 % of samples; cover = gameplay box with height ≥ 1.0 m |
| Gameplay symmetry (`ArenaLayout.Symmetry`) | every box has its `MirrorZ` or `Rotate180` twin (tolerance 1 mm) or is its own image |
| Ceilings | ≤ 400 gameplay boxes, ≤ 64 cover proxies |
| Spawn slots | 12 per team, inside own staging rectangle, ≥ 2 m spacing |

Measured at ADR-074 (pure `ArenaRules`, seeds unchanged):

| Map | Size | Anchor sep. | Path → centre | Path → enemy | Longest sightline | Worst cover gap | Boxes / covers |
|---|---|---|---|---|---|---|---|
| Training Compound | 64 × 80 | 68 | 40 | 84 | 46.5 | 5.05 | 113 / 59 |
| Depot Yard | 64 × 80 | 68 | 42 | 80 | 47.25 | 5.37 | 81 / 50 |
| Oil Depot | 64 × 80 | 68 | 40 | 88 | 46.0 | 5.7 | 120 / 36 |

Ground is flat (`y = 0`, 2×2 zero height grid). ADR-071 limited walkable elevation applies only on Oil Depot: thin gameplay slabs (≤0.40 m) for stairs/catwalks, Y-aware occupancy (pass-under), and layered climb when a path endpoint is elevated. Roofs stay non-gameplay.

## Map A — Training Compound "garrison square" (`training-compound`, `Rotate180`)

Playable rectangle x ∈ [−32, 32], z ∈ [−40, 40]. Team 1 anchor (0, 0, −34), team 2 anchor (0, 0, 34). Team 2's half is team 1's half rotated 180° about the origin. Planner `environment.compound.arena` 3.0.0.

- **Staging (motor pool)** z ∈ [−40, −30]:
  - A 14 × 7 m drive-through garage at (0, −27) has offset south/north doors and a tall parts rack between them. Two jeeps parked nose-in either side of the south door form a short chute.
  - Four parked trucks along z = −29 leave a 7 m exit on each side.
  - Parked trailers split the staging strip, with openings at the back (west) and in the middle (east).
- **Centre: parade square** (no building). A low flag plinth (1.0 m, flagpole dressing) stands on the origin, with planters and sandbags as half cover. A trailer and a booth on the square's edges break east–west eye lines.
- **Middle route:** through the garage, then through the offset guard-hut arch at z = −15. One hut stands on the garage-door column, and its rotated twin closes the other half.
- **Left route (interior):** a 12 × 14 m two-room barracks at (−21, −15) with doors to the spawn, the square and the west alley. A sandbag wall and a parked trailer cap the alley.
- **Right route (open street):** staggered parked trucks and sandbag walls, past a 9 × 10 m HQ office at (15.5, −14) that opens onto the square. After rotation, team 1's barracks route faces team 2's truck street, and vice versa.

## Map B — Depot Yard "rail crossing" (`depot-yard`, `MirrorZ`)

Playable rectangle x ∈ [−32, 32], z ∈ [−40, 40]. Team 1 anchor (0, 0, −34), team 2 anchor (0, 0, 34). Planner `environment.depot.arena` 3.0.0. Industrial, linear and container-driven; there is no central warehouse.

- **Staging** z ∈ [−40, −30]:
  - Four semi-trailers are parked nose to tail along z = −29.
  - A gate booth wider than the 5 m centre gate stands at z = −25.3. Inside the centre gate, two trailers form a chute.
  - Pallet stacks split the strip, with offset openings.
- **Centre: the train** on z = 0 (self-mirrored). Two strings of coupled cars run along the track: three 3.4 m boxcars plus one 1.2 m flatcar. You can shoot over the flatcar but not climb it, and a tarped load stands on its east end.
- **Crossings:** three, at x ∈ [−32, −28], [−3, 3] and [28, 32]. A signal box and a relay cabinet beside the centre crossing stop the eye line along the train and diagonals through the crossing.
- **Left route:** a container stack yard with staggered 6 m containers, two of them double-stacked, and aisles open at both ends. A relay cabinet against a stack caps the wall alley.
- **Middle route:** the open yard from the gate booth to the centre crossing, with a fuel truck, a signal box and pallets.
- **Right route (covered):** a 14 × 14 m loading shed at (20, −15), 4.5 m walls, four doors, shelving and a dock lip. East of it is an alley with a parked trailer and sandbags.

## Map C — Oil Depot (`oil-depot`, `MirrorZ`) — ADR-071, compacted by ADR-074

Playable rectangle x ∈ [−32, 32], z ∈ [−40, 40]. Team 1 anchor (0, 0, −34), team 2 anchor (0, 0, 34). Planner `environment.oil.arena` 2.0.0. ADR-074 keeps the map's distinctive parts (tank farm, pump house, catwalk) and drops the container lane and the thin separator walls.

- **Staging (loading rack)** z ∈ [−40, −30]:
  - Tanker trucks are parked along z = −29.
  - A pump island wider than the centre gate stands at z = −25.3. Inside the centre gate, two tankers form a chute.
  - Drum stacks split the strip, with offset openings.
- **Left route — tank farm:** drum tanks at (−22, ±19) r 3.4 and (−27.5, ±8) r 3.0, with pipe stands and valve pits. A 10.5 m pipe rack at x = −14 west of the pump house catches the diagonal eye lines across the apron. The lane via (−26, 0) stays open.
- **Middle — pump house** 14 × 12 m centred (0, 0): doors on all four sides, interior pillars and a console. The apron has valve manifolds, and valve towers stand against its east wall.
- **Right route — catwalk:** one continuous walkable deck (top 2.25 m, 3.4 m wide, z ∈ [−6, 6]) at x = 22, spanning z = 0, with half-cover rails. A 9-tread stair runs at each end. Both teams contest the same high ground, and there is an underpass beneath it.
  - East of the catwalk is a tanker on a siding with a tall pipe stand.
  - West of the catwalk is a pipe-rack stub and valve cover.
- **Visual dressing (mesh-only):**
  - `OilDepotDressing.cs` covers the round 32-sided white tank shells that enclose the square drum collision (the drum/buttress boxes are not drawn), strake bands, cone roofs, yellow roof rails and caged ladders.
  - It also draws wall-mounted pipe racks on all four perimeter walls, two overhead pipe bridges (y = 6.4 m) from the outer tanks to the pump-house roof on columns standing on the tank roofs and the x = −14 pipe rack, the pump-house roof plant, and an out-of-bounds skyline of large tanks plus a banded flare stack.
  - The ADR-071 loading gantry was removed together with its containers.
  - The accent roles `TankWhite` (6) and `SafetyYellow` (7) remain oil-only.
  - Known limitation: near the tank cardinal faces the visual shell sits up to ≈ 0.35 r − 0.35 m (≤ 0.85 m) outside the authoritative buttress collision.

## Feature dressing (ADR-074)

`ArenaBoxBuilder.AddVehicle`, `AddSandbagWall`, `AddFenceRun` and `RecordFeature` record an `ArenaFeature` (kind, envelope, facing) next to the collision box; `CompoundPlan.Features` carries them. `ArenaFeatureDressing` (appended by `MapSolidsMesh.Build`) draws:

- trucks, tankers, semi-trailers, boxcars, flatcars, tarped loads, sandbag walls and tarp fences instead of their envelope box. Each stays inside the envelope, and dark skirts close any gap under a chassis, so nothing visible opens where collision blocks;
- booths, the flag plinth and pipe racks, which keep their box and gain a roof, windows, flag or pipes.

Training Compound and Depot Yard use only roles 0–5. Collision, navigation and package identity never depend on dressing.

## Implementation boundary

- Pure files under `UnityProject/Assets/Shooter/Generation/Core/Environment/`:
  - `ArenaLayout.cs` (including `ArenaSymmetry`), `ArenaRules.cs`, `ArenaGeometry.cs` (including stair/catwalk helpers, feature helpers and `ArenaFeature`), `ArenaFeatureDressing.cs`, `ArenaVisualDetail.cs`;
  - `CompoundArenaPlanner.cs`, `DepotArenaPlanner.cs`, `OilArenaPlanner.cs`, `OilDepotDressing.cs`, `ProductMapPlanner.cs`.
  - Each new `.cs` needs a committed `.meta` with a fresh GUID.
- `CompoundPlan` carries an optional `ArenaLayout? Arena` (null keeps legacy behaviour) and `Features` (empty for legacy plans). `MapSpawnLayout` and `MapTopologyValidation` use the arena when present. `MapPackageBuilder`, `ReleaseCatalogDocument` and `CompoundCatalogFactory` dispatch through `ProductMapPlanner`.
- Collision/nav hashes are derived from the actual box list (not seed-only). Stable IDs: compound 10000+, depot 20000+, oil 30000+; visual detail 50000+/60000+/70000+.
- Code must stay C# 9 / .NET Standard 2.1 and Unity-free (it is compiled by `Tools/Generation` and referenced by the test projects).
- Presentation (`ProductMapView`) is not edited; it renders from the package as before.

## Validation and evidence

- **Pure checks:** `Tools/Test-Generation.ps1`, `Tools/Test-Maps.ps1`, `Tools/Test-Simulation.ps1`, `Tools/Test-Game.ps1`, `Tools/Test-Contracts.ps1`, source provenance, docs/fixtures checks.
- **Content:** regenerate packages with `Tools/Compile-GameContent.ps1` (native nav with the locked editor where available) and record old/new package identities in the M11 handoff.
- **Not covered:** rule validation proves geometry constraints only. Enjoyable flow, time to first contact, spawn pressure and bot behaviour on the compact layouts remain Pending a manual playtest of all three maps at 5v5 and 6v6.

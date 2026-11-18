# Feature: TDM map layouts — site-planned places (ADR-109)

Owner request 2026-11-18: Oil Depot was "more or less good" but not good enough as map design; Training Compound and Depot Yard were rejected (too many random objects, enemies visible right after leaving the respawn, a long empty space). Re-think map generation so every map is unique and meaningful and imitates a real place with no random pillars or walls in the middle of nothing. [ADR-109](../decisions.md#adr-109--accepted-site-planned-maps-old-town-harbour-rebuilt-oil-depot) replaces the [ADR-074](../decisions.md#adr-074--accepted-compact-64--80-m-arenas-with-distinct-per-map-layouts) arena planners with **site plans**: `training-compound` becomes **Old Town**, `depot-yard` becomes **Harbour**, and **Oil Depot** is rebuilt. Map ids, seeds, recipes, the 64 × 80 m frame, the package format, SGAF Collision v1, nav settings and the 256 m world/32 m chunk frame are unchanged; gameplay topology and therefore package identities change.

## Why the old maps failed

The ADR-074 planners dropped hand-placed boxes into an empty walled rectangle and mirrored them. The rules only measured numbers (cover within 8 m, sightline ≤ 48 m, no spawn-slot-to-spawn-slot line), so the cheapest way to pass was to scatter more boxes. Spawn safety was checked from the slots but never from the exits, so a player stepping out could look straight down a lane into the enemy half.

## Design principles

1. **Space is what buildings leave.** A map is a place: a boundary (backs of buildings, fence, quay edge), buildings with doors, windows and interiors, and the streets, alleys, yards, square or quay between them. Open areas are named; only a declared plaza or quay may be wide open.
2. **Every object belongs to something.** A prop stands against a facade, is parked in a street or yard, sits on a container row or quay, stands against the boundary, or runs overhead; or it is one of a few named landmarks (tank, crane, platform). There is no way to add an unowned box.
3. **Protected spawns with turning exits.** Each team spawns in an enclosed yard or building with three exits. Exits open onto a facade or a turn, never straight down a street into the enemy half.
4. **Three routes with character.** Each map keeps a left, middle and right route (interior, street/yard, elevated or long), plus cut-throughs through buildings.
5. **Symmetry for fairness.** Team 2's half is the z-mirror (Harbour, Oil Depot) or the 180° rotation (Old Town) of team 1's half. Buildings on the dividing line are their own image.
6. **Height where a real place has it.** One two-storey building per half (town hall, harbour office, control room) with a gallery or upper rooms, windows overlooking a contested area and two stairs.

## Rules (enforced at every build)

`ProductMapPlanner.ValidateOrThrow` runs `ArenaRules`, `SiteRules` and the topology validation.

| Rule | Threshold |
|---|---|
| Playable rectangle | 56–72 m along X, 72–88 m along Z |
| Perimeter | closed, ≥ 4 m |
| Anchor separation along Z | 60–76 m |
| Nav path anchor → centre / → enemy anchor (1 m grid) | 26–52 m each / 60–110 m |
| Staging exits | ≥ 3, ≥ 8 m apart, each reaches the centre |
| Distinct routes | ≥ 3 (via lane points), ≥ 1 covered |
| Spawn LOS | none between any T1 and T2 slot; none from the centre to a slot |
| Longest sightline | ≤ 48 m anywhere; ≤ 40 m outside the declared plaza/quay |
| Cover | every open sample outside staging within 8 m of cover ≥ 1.0 m |
| Symmetry | every gameplay box has its twin (1 mm) |
| Ceilings / slots | ≤ 400 gameplay boxes, ≤ 64 cover proxies; 15 slots per team, ≥ 2 m apart |
| **Site roles** | every gameplay box has a role; attached props ≤ 0.5 m from their host; ≤ 5 landmarks per half |
| **Built coverage** | building + landmark footprints and tall props cover 25–60 % of the playable area |
| **Open voids** | no point farther than 9 m from a building, landmark, tall prop or boundary (13 m inside a declared plaza/quay) |
| **Spawn-exit exposure** | no ground point within 10 m (and 15 m of walking) of a team's exits sees enemy ground beyond z = 8 m or the enemy staging (eye height) |
| **First contact** | ≥ 18 m of walking from an exit to the first point that sees an enemy exit apron |
| **Upper floors** | every upper floor reachable from the team anchor; ≥ 2 stairs per two-storey building |

Measured at ADR-109 (pure rules, approved seeds):

| Map | Path → centre | Path → enemy | Sightline | First contact | Built | Boxes / covers |
|---|---|---|---|---|---|---|
| Old Town | 50 | 96 | 32.5 | 22.3 | 58 % | 308 / 18 |
| Harbour | 50 | 100 | 35.5 | 23.9 | 54 % | 267 / 11 |
| Oil Depot | 49 | 96 | 36.75 | 27.2 | 47 % | 294 / 12 |

Windows are real openings (a sill below 1.05 m, a lintel above 2.2 m), so the exposure and sightline rules see through them; layouts avoid windows that line up with doors or streets. Walkable floors are slabs ≤ 0.40 m thick (Y-aware occupancy and layered climb, as introduced for Oil Depot by ADR-071). Upper floors sit at 2.5 m (10 treads of 0.25 m).

## Old Town (`training-compound`, Rotate180)

Small town centre, point-symmetric about the market hall. Generator `environment.town.site` 1.0.0, stable ids 10000+.

- **Spawn — inn yard** x ∈ [−10, 10], z ∈ [−40, −32], enclosed by the stable, the inn and the smithy. Exits: the yard's north-west corner into Stable Lane, through the inn (offset doors), and through the smithy into the mill yard.
- **Left — weavers' alley and the chapel:** Stable Lane, the bakery (a cut-through), the alley along the town wall, Church Walk and the chapel, whose east door opens on the square.
- **Middle — high street:** from the inn's north door past a parked delivery truck to the square. The newsagent and apothecary narrow the street mouth so it does not line up with the far side.
- **Right — town hall (2F):** mill yard, then the town hall's south or lane door. Inside, a gallery runs along the north wall with a stair at each end, and upper windows look over the square.
- **Centre — market square** with the market hall (doors offset east and west), market stalls against the facades, the tailor's shop (cut-through with a partition) and the chapel.
- Rotate180 makes a pinwheel: each half's alley-and-chapel side faces the other half's town hall.

## Harbour (`depot-yard`, MirrorZ)

Working port with water along the west boundary. Generator `environment.harbour.site` 1.0.0, stable ids 20000+.

- **Spawn — transit shed** (inside the warehouse). Exits: two roller doors onto the dock road and a side door through the net loft to the quay. Across the road stand the container terminal and the chandlery.
- **Left — quay:** cargo stacks, the quay crane and the pilot house on the dividing line; the crossing is a 3 m strip along the water.
- **Middle — container terminal and Fish Lane** (containers only on terminal rows, aisles ≥ 2.5 m) to the pier square. The portal crane's machinery stands against the fish market, so the open half of the pier is offset from Fish Lane.
- **Right — Customs Lane and the harbour office (2F):** gallery along the north wall with two stairs, upper windows over the customs house and the lane.
- **Dividing line:** fish market and customs house (each its own mirror image, partitioned with offset doors) and the bonded gate with the customs booth.

## Oil Depot (`oil-depot`, MirrorZ)

Petroleum depot. Generator `environment.oil.site` 1.0.0, stable ids 30000+.

- **Spawn — fenced loading bay** between the drum store and the lab. Exits: the west and east gate lanes and through the rack house; all three open onto facades or tanks across the tank-farm road.
- **Left — tank farm:** two large tanks per half, valve pits, a fire-water pump skid on the fence, and the manifold house on the dividing line.
- **Middle — pump house** (its own mirror image, partitioned, offset doors) with the switch house (transformer and battery cabinets) and the foam store closing the apron, and the meter house beside it.
- **Right — control rooms and pipe bridge:** each half's two-storey control room has two stairs to a gallery; an upper door opens onto a pipe bridge over the pipe alley that joins both control rooms (the elevated route, with a parked tanker and an underpass below).
- An overhead pipe rack runs along the road above head height.

## Dressing (mesh-only)

- `SiteDressing` (all site maps): pitched roofs with stepped gables on town-type buildings, parapets and roof plant on industrial ones, the church tower and spire, and a backdrop per boundary side: rows of town houses behind building-back boundaries, and harbour water with a moored freighter behind the quay.
- Building walls get the ADR-103 wall detail bands; harbour containers use the container styling (6 × 2.5 m envelopes).
- `OilDepotDressing` draws round tank shells over the square tank collision (reading `OilDepotSitePlanner.Tanks`), tank headers, fence pipe racks (skipped where a building backs onto the fence), pipes on the overhead rack, pump-house roof plant and the refinery skyline. Known limitation from ADR-071 remains: near a tank's cardinal faces the round shell sits up to ≈ 0.85 m outside the square collision.
- `ArenaFeatureDressing` still draws vehicles, cargo and booths.

Collision, navigation and package identity never depend on dressing.

## Bots

For site maps the plan's route corners form a patrol circuit, anchor → lane → anchor, through every lane point, so patrols use all three routes. Combat destinations include the lane points (on Oil Depot one is the pipe bridge).

## Implementation boundary

- Pure files under `UnityProject/Assets/Shooter/Generation/Core/Environment/Site/`:
  - `SiteLayout.cs` (model), `SiteBuilder.cs` (compiler to `ArenaBoxBuilder`), `SiteRules.cs` (+ `SiteGrid`), `SiteDressing.cs`, `SiteMapSvg.cs` (review render);
  - `OldTownSitePlanner.cs`, `HarbourSitePlanner.cs`, `OilDepotSitePlanner.cs`.
  - All are C# 9 / .NET Standard 2.1 and Unity-free, compiled by `Tools/Generation`. Each file has a committed `.meta`.
- `CompoundPlan.Site` carries the `SiteLayout`; the registry (`Content/Maps/*.map.json`) names the planners `code:town-site`, `code:harbour-site` and `code:oil-site`.
- `ArenaLayoutDocument` and `ArenaVisualDetail` stay for data-driven `map.arena` maps.

## Validation and evidence

- **Pure:** `Tools/Test-Generation.ps1` (`SiteMapTests` writes `Artifacts/maps/<id>-site.svg` and a rule report per map), `Tools/Test-Maps.ps1`, `Tools/Test-Simulation.ps1`, `Tools/Test-Game.ps1`, `Tools/Test-Contracts.ps1`, source provenance, docs and fixtures checks.
- **Content:** republish packages with `Tools/Compile-GameContent.ps1 -Native` and record old and new package identities in the M11 handoff.
- **Not covered:** rules prove geometry constraints only. Whether the maps play well (time to first contact, spawn pressure, bot routing, use of upper floors and the pipe bridge) is Pending an owner playtest of all three maps at 5v5/8v8/10v10/15v15.

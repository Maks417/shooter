# Feature: playable procedural maps

Status reviewed 2026-11-15 (ADR-071): ADR-035/038 packages include persisted catalog/gameplay metadata and actual map/soldier/weapon visual closure; runtime consumes verified bytes. Three approved maps have ≥12 slots/team and topology/spawn policy software (Compound `460072030`, Depot `460072031`, Oil Depot `460072032`). Oil Depot adds limited walkable elevation (stairs/catwalk). Native editor bridge/runtime installer exist, but normal Compile-GameContent still publishes software-grid NVB1. [T067](../next-playable-build.md) must complete native publication (now three maps × four profiles), partial/fallback path semantics and real native route/spawn/player validation. Publication/Unity open-quad tests alone do not accept MAP/FIX gates.

Superseding layout note 2026-11-15 (T076, ADR-045; Oil ADR-071): catalog arenas are compact mirror-symmetric three-lane layouts — Training Compound 88 × 104 m, Depot Yard 80 × 112 m, Oil Depot 84 × 108 m — specified in [compact TDM map layouts](tdm-map-layouts.md). The 256 m rows below describe the retired layouts; Compound/Depot ids and seeds stay unchanged.

## Catalog and layouts

| Stable MapId / display name | Proposed numeric layout | Gameplay |
|---|---|---|
| `training-compound` / Training Compound | Compact 88×104 m arena (T076); seed `460072030` | Team Deathmatch, 5v5 and 6v6 |
| `depot-yard` / Depot Yard | Compact 80×112 m cargo arena; **approved catalog seed `460072031`** (ADR-035; historical `42` remains robustness-corpus only) | Team Deathmatch, 5v5 and 6v6 |
| `oil-depot` / Oil Depot | Compact 84×108 m petroleum arena with elevated catwalk; seed `460072032` (ADR-071) | Team Deathmatch, 5v5 and 6v6 |

These names/layouts are chosen product defaults. Seeds or lighting variants of the same topology do not count as distinct maps. Release requires exactly these three approved entries. Depot uses existing primitive/material families and procedural recipe rules, without imported props or authored meshes. Stay within the existing baseline building/prop ceilings until measured: maximum 12 building shells / 120 repeated props per map initially. Density budgets do not prove frame-time acceptance.

Each map has ≥12 validated spawn slots per team, at least two separate exits per staging area, at least three distinct routes between sides including a covered route, and no initial direct spawn-to-enemy-spawn shot. All slots reach the combat routes through real navigation with standing clearance under [gameplay baseline](../gameplay-baseline.md). Spawn roles, route destinations and cover proxies have stable IDs and metadata; team/quality never changes physical collision, nav, visibility or hit rules. Mirrored blockout is acceptable initially; exploit/imbalance findings require numeric recipe fixes and regenerated identities.

## Content path — partial implementation and required exit

T037/T044/T045 retain the original package requirements. Tools/Compile-GameContent.ps1 now enumerates map/profile/recipe deps and publishes verified `shooter.map-package.v1` sets (cold/warm/forced, cancel/corrupt recovery, server gameplay-only). Tools/Test-Maps.ps1 covers catalog/package pure checks; native Unity bake/player remain Pending. Existing diagnostic compilers remain restricted to their named content sets.

Encoding boundary accepted in [ADR-035](../decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary): keep SGAF Collision v1; carry stable IDs in `shooter.map-metadata.v1`; catalog roots are `shooter.map-package.v1` byte hashes; shipping nav adapter is `unity.navmesh.bake.v1` triangulation with provenance. Use existing typed collision/navigation envelopes where valid; no arbitrary Unity object deserialization or silently renamed diagnostic artifact. Bake real Unity navigation against installed authoritative collision using the pinned toolchain in compiler/editor code, then encode the reviewed blob. The pure `NavCompiler` accepts delegates and does not by itself establish a Unity nav surface or path-following runtime.

The shipped catalog references verified packaged manifest roots only. All three maps, all selected profile visuals, both weapons, soldier rigs, shaders and allowed font must be in the release dependency closure. Server packaging includes gameplay only. Loading preflights hashes/versions and installs collision/nav before actor spawn; partial or cancelled installs release all handles. No release-player compilation, developer absolute paths or internet downloads. Corrupt or missing selected map returns to menu with a stable repair/reinstall message; never load the floor/box or another map silently.

## Validation and evidence

Validate all three approved recipes (compound `460072030`, depot `460072031`, oil `460072032`) plus the existing boundary seed corpus (1, 42, 460072030, uint64 max) where the generator supports seed variation; alternate seeds including historical `42` are generator robustness tests, not extra catalog entries. Check real collision/native nav reachability (including oil catwalk climb and underpass), doorway/step/capsule clearance, covered flanks, initial LOS, spawn pressure, cross-chunk collision continuity and complete dependency manifests. A clean checkout with empty content cache must reproduce approved identities; any generator change includes intended old/new identity evidence.

Play all three maps with both roster sizes through death/respawn, score finish and time finish. Compare Low/Balanced/High gameplay hashes and scripted authoritative outcomes. Perform 20 alternating map loads, cancellations during each install stage, and 100 traversal/stream lifecycle cycles with bounded residency. Capture readable 720p views and measured release-player workload counters for each map. Geometry/connectivity tests do not establish enjoyable combat or performance; M11 records human route/spawn review and hardware results separately.

## Adding a map (ADR-083)

Each map is one file, `Content/Maps/<id>.map.json`, with this structure:

- **Identity:** `id`, catalog `order`, `displayName`, `description` and an unused `seed`.
- **`recipe`:** `map.arena` for new maps. `map.compound`/`map.depot`/`map.oil` are frozen to the three original maps.
- **`planner`:** `layout` plus an inline `layout` document, or `code:<name>` for a planner registered in `MapRegistry.CodePlanners`.
- **`presentation`:** `sky` (`default`, `compound`, `dusty-yard` or `petroleum`) and `groundSurface` (`soil` or `asphalt`).

A layout document gives the planner identity (`id`, `version`, `idBase`), the arena (`halfExtentX/Z`, `staging`, `team1Anchor`, `team1Exits`, `lanePoints`, `symmetry`) and ordered kit operations:

- `perimeter`, `shell`
- `cover`, `vehicle`, `feature`
- `sandbags`, `fence`, `grounded`, `box`
- `wallX`, `wallZ`
- `visualDetail`

Every length is integer millimetres, and team 2 is derived by the symmetry. `training-compound.map.json` is a complete example: its 40 operations rebuild the former C# planner byte for byte.

After editing, run these, in order:

```powershell
python Tools/generate_map_registry.py
```
```powershell
./Tools/Test-Generation.ps1
```
```powershell
./Tools/Compile-GameContent.ps1 -Map <id>
```

The release catalog, menu cards, sky and yard surface follow the registry. `Tools/release-package-policy.json` and `ReleasePackagePolicy.RequiredMaps` list the shipped maps on purpose; a test fails until they match the registry. A new map must still pass `ArenaRules` and `MapTopologyValidation`, and needs native nav and a playtest before release.


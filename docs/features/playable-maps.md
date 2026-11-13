# Feature: two playable procedural maps

Status: proposed release content for M8/M10, not packaged maps. Read [generation](../generation.md), [environment](environment.md) and [Game-v1](../game-session-contracts.md). Actual baseline source is `Generation/Core/Environment/CompoundPlanner.cs`, `CompoundPlan.cs`, `World/Navigation/NavCompiler.cs` and `Content/Runtime/FileContentStore.cs` under `UnityProject/Assets/Shooter`.

## Catalog and layouts

| Stable MapId / display name | Proposed numeric layout | Gameplay |
|---|---|---|
| `training-compound` / Training Compound | Adapt the existing 256×256 m compound, seed `460072030`, opposite team staging areas, mixed indoor/outdoor routes, generated walls/building shells and cover | Team Deathmatch, 5v5 and 6v6 |
| `depot-yard` / Depot Yard | New bounded 256×256 m numeric depot recipe, seed `42`, two warehouse rows, central loading yard and perimeter flanks; separate spatial grammar/connectivity from compound | Team Deathmatch, 5v5 and 6v6 |

These names/layouts are chosen product defaults. Two seeds or lighting variants of the same topology do not count as two maps. Depot uses existing primitive/material families and procedural recipe rules, without imported props or authored meshes. Stay within the existing baseline building/prop ceilings until measured: maximum 12 building shells / 120 repeated props per map initially. Density budgets do not prove frame-time acceptance.

Each map has ≥12 validated spawn slots per team, at least two separate exits per staging area, at least three distinct routes between sides including a covered route, and no initial direct spawn-to-enemy-spawn shot. All slots reach the combat routes through real navigation with standing clearance under [gameplay baseline](../gameplay-baseline.md). Spawn roles, route destinations and cover proxies have stable IDs and metadata; team/quality never changes physical collision, nav, visibility or hit rules. Mirrored blockout is acceptable initially; exploit/imbalance findings require numeric recipe fixes and regenerated identities.

## Content path to implement

T037 builds the first real compound package; T044 adds the catalog and T045 the depot recipe. Proposed new compiler wrapper `Tools/Compile-GameContent.ps1` and map validation wrapper `Tools/Test-Maps.ps1` are **not implemented today**. They must enumerate exact approved MapIds, profiles and recipes, compile gameplay+visual dependencies with provenance, publish atomically and record cold/warm/forced-reproduction evidence. Existing Compile-Content supports only box-collision; Compile-Gallery/Soldier/M1Content are diagnostic sets, not full-map compilers.

Freeze exact recipe/schema/nav payload encoding under an ADR before worker integration if existing SGAF v1 cannot express the package. Use existing typed collision/navigation support where valid; no arbitrary Unity object deserialization or silently renamed diagnostic artifact. Bake real Unity navigation against installed authoritative collision using the pinned toolchain in compiler/editor code; version the adapter/settings/blob and prove load/query correctness. The pure `NavCompiler` accepts delegates and does not by itself establish a Unity nav surface or path-following runtime.

The shipped catalog references verified packaged manifest roots only. Both maps, all selected profile visuals, both weapons, soldier rigs, shaders and allowed font must be in the release dependency closure. Server packaging includes gameplay only. Loading preflights hashes/versions and installs collision/nav before actor spawn; partial or cancelled installs release all handles. No release-player compilation, developer absolute paths or internet downloads. Corrupt or missing selected map returns to menu with a stable repair/reinstall message; never load the floor/box or another map silently.

## Validation and evidence

Validate both approved recipes plus the existing boundary seed corpus (1, 42, 460072030, uint64 max) where the generator supports seed variation; alternate seeds are generator robustness tests, not extra catalog entries. Check real collision/native nav reachability, doorway/step/capsule clearance, covered flanks, initial LOS, spawn pressure, cross-chunk collision continuity and complete dependency manifests. A clean checkout with empty content cache must reproduce approved identities; any generator change includes intended old/new identity evidence.

Play both maps with both roster sizes through death/respawn, score finish and time finish. Compare Low/Balanced/High gameplay hashes and scripted authoritative outcomes. Perform 20 alternating map loads, cancellations during each install stage, and 100 traversal/stream lifecycle cycles with bounded residency. Capture readable 720p views and measured release-player workload counters for each map. Geometry/connectivity tests do not establish enjoyable combat or performance; M11 records human route/spawn review and hardware results separately.

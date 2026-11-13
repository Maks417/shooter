# M9 — Team Deathmatch and bot teams — bounded future agent assignments

Status: proposed roles only, no active leases. Integrator fills named owner, branch/worktree, exact base commit and expiry in coordination.json after explicit implementation assignment. All paths below are relative to UnityProject/Assets/Shooter unless beginning Tools/, docs/ or referring to a root file. Test-project includes/asmdefs/.meta/scene and schema/lock edits remain integrator-owned unless explicitly leased.

| Role | Tasks | Permitted paths | Boundary |
|---|---|---|---|
| Match worker | T040 | Simulation/TeamDeathmatch/Rules/, Tools/Simulation.Tests/TeamDeathmatch/Rules/ | Own score/timer/results reducer only; no actor, spawn or AI mutation. |
| Life/spawn worker | T041 | Simulation/TeamDeathmatch/Spawning/, Tools/Simulation.Tests/TeamDeathmatch/Spawning/ | Own respawn policy through IActorLifecycle/IMapRuntime; do not edit M8 Simulation/Session or match reducer. Test against frozen fakes until merged. |
| AI worker | T042 | Simulation/AI/ except shared contracts; Tools/Simulation.Tests/AI/ and TeamDeathmatch/AI/ | One owner for existing director/perception; no helper worker on same files. M8 client worker must have finished related bot view wiring. |
| Integrator | T043 | Bootstrap/, cross-package tests/diagnostic adapters, benchmark definitions, shared project files/locks/docs and coordination.json | Keep match facade/composition integrator-owned; review global budget counters and merge match → respawn → AI → real session integration. |

## Dispatch gate and handoff

Read [contracts/integration](contracts-integration.md). Do not fork feature workers until the upstream compiling Game-v1 contracts, matching executable fixtures and fakes are committed on main. Each dispatched task supplies actual base SHA, contract/fixture hashes, exact file list, acceptance IDs from [validation](validation-acceptance.md), evidence path, resource limits and rollback. Worker may propose a contract correction but stops dependent edits until integrator freezes it. Fakes are test-only; shipping composition rejects them.

Use separate worktrees and one configured locked Unity project/editor per worker, with independent Library/cache. Respect available agent slots; phases need not run simultaneously. No agent edits another worker's files or shared docs without reassignment. Merge dependencies first, verify all work on main, preserve evidence, then remove temporary branches/worktrees. Do not leave completed changes on a worker branch.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m8-runtime-composition/index.md) · [Next pack](../m10-maps-shell/index.md).

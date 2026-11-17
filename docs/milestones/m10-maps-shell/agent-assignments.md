# M10 — Two maps, menus and settings — bounded future agent assignments

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M10 integration validation remains open. Persisted catalog/visual packages, shell/schema-2/settings/audio and T069 editable applied bindings/accessibility are built into `7cd7e81`. Native-navigation publication and named map/UI/control/display/audio/20-cycle player review remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Status: proposed roles only, no active leases. Integrator fills named owner, branch/worktree, exact base commit and expiry in coordination.json after explicit implementation assignment. All paths below are relative to UnityProject/Assets/Shooter unless beginning Tools/, docs/ or referring to a root file. Test-project includes/asmdefs/.meta/scene and schema/lock edits remain integrator-owned unless explicitly leased.

| Role | Tasks | Permitted paths | Boundary |
|---|---|---|---|
| Catalog/map worker | T044 | Content/Maps/, Generation/Core/Environment/TeamMaps/Compound/, assigned map fixtures and tests | One owner extends M8 catalog/content loader. Core planner/compiler/registry changes go through integrator, not concurrent direct edits. |
| Depot worker | T045 | Generation/Core/Environment/TeamMaps/Depot/, own depot numeric recipes and tests | Use frozen map output seam; no edits to Compound/, shared primitive/material code, catalog, codecs or compile entry points. |
| Shell worker | T046 | Presentation/Menus/, Presentation/Scoreboard/, Presentation/Hud/TeamMatch/, own UI tests | Read catalog/results/settings adapters; no Bootstrap, settings storage, Simulation or shared HudLayout rewrite. |
| Settings worker | T047 pure/model part | Game/Settings/, Presentation/Settings/, own settings tests | Storage/model and settings screen have one owner; shell supplies settings screen navigation through frozen seam. Integrator owns actual Bootstrap device-service application. |
| Integrator | Cross-package integration | Bootstrap/, Game-v1/migration/locks, compiler wrappers/registries, Unity settings/asmdefs, cross-package tests, shared docs and coordination.json | Dispatch at most available slots, e.g. maps then shell+settings; never have two Unity editors in one project. Merge catalog→depot and settings→shell, then compose. |

## Dispatch gate and handoff

Read [contracts/integration](contracts-integration.md). Do not fork feature workers until the upstream compiling Game-v1 contracts, matching executable fixtures and fakes are committed on main. Each dispatched task supplies actual base SHA, contract/fixture hashes, exact file list, acceptance IDs from [validation](validation-acceptance.md), evidence path, resource limits and rollback. Worker may propose a contract correction but stops dependent edits until integrator freezes it. Fakes are test-only; shipping composition rejects them.

Use separate worktrees and one configured locked Unity project/editor per worker, with independent Library/cache. Respect available agent slots; phases need not run simultaneously. No agent edits another worker's files or shared docs without reassignment. Merge dependencies first, verify all work on main, preserve evidence, then remove temporary branches/worktrees. Do not leave completed changes on a worker branch.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).

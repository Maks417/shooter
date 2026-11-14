# M8 — Runtime composition and Game-v1 foundation — bounded future agent assignments

**Current disposition — 2026-11-14, reviewed at 59316d3:** M8 integration open: lifecycle/pause/input and visible/audio wiring landed in 59316d3; provenance, compiled visual closure, native navigation and authority-driven weapon/action views still need T066–T068, followed by real RUN/player evidence. See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

Status: proposed roles only, no active leases. Integrator fills named owner, branch/worktree, exact base commit and expiry in coordination.json after explicit implementation assignment. All paths below are relative to UnityProject/Assets/Shooter unless beginning Tools/, docs/ or referring to a root file. Test-project includes/asmdefs/.meta/scene and schema/lock edits remain integrator-owned unless explicitly leased.

| Role | Tasks | Permitted paths | Boundary |
|---|---|---|---|
| Integrator | T035,T036; integrates T039 | Game/Contracts/, Simulation/Session/, Simulation/Foundation/, Bootstrap/, asmdefs/project references, root locks/settings, Tools test-project files, schemas/fixtures/benchmarks, coordination.json and shared docs | No worker edits these shared files. Extract authority before M9 actor/match consumers; serialize Unity editor/build use. |
| World/content worker | T037 after Game-v1 freeze | World/Runtime/, Content/Maps/, Generation/Editor/Maps/, Tools/GameContent/ and own map tests; specific existing Environment/Navigation files only when listed in assignment | Propose schema/codec/registry changes to integrator; do not modify compiler core, frozen codecs or lock inventories independently. |
| Client worker | T038 after Game-v1 freeze | Presentation/Session/, Presentation/Input/, Presentation/Audio/Runtime/ and matching tests | Do not edit FoundationView or Bootstrap. Request integrator wiring; do not parallel-edit M10 menu/settings folders. |
| Integrator + named visual owner | T039 sequential follow-up | Assigned individual Rig/SoldierMotion files and their tests; build/evidence wrappers remain integrator-owned | Record source/recipe identity changes before regenerate; no parallel character edits or previous-failure erasure. |

## Dispatch gate and handoff

Read [contracts/integration](contracts-integration.md). Do not fork feature workers until the upstream compiling Game-v1 contracts, matching executable fixtures and fakes are committed on main. Each dispatched task supplies actual base SHA, contract/fixture hashes, exact file list, acceptance IDs from [validation](validation-acceptance.md), evidence path, resource limits and rollback. Worker may propose a contract correction but stops dependent edits until integrator freezes it. Fakes are test-only; shipping composition rejects them.

Use separate worktrees and one configured locked Unity project/editor per worker, with independent Library/cache. Respect available agent slots; phases need not run simultaneously. No agent edits another worker's files or shared docs without reassignment. Merge dependencies first, verify all work on main, preserve evidence, then remove temporary branches/worktrees. Do not leave completed changes on a worker branch.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

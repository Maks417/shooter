# M11 — Playable build and acceptance — bounded future agent assignments

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Status: proposed roles only, no active leases. Integrator fills named owner, branch/worktree, exact base commit and expiry in coordination.json after explicit implementation assignment. All paths below are relative to UnityProject/Assets/Shooter unless beginning Tools/, docs/ or referring to a root file. Test-project includes/asmdefs/.meta/scene and schema/lock edits remain integrator-owned unless explicitly leased.

| Role | Tasks | Permitted paths | Boundary |
|---|---|---|---|
| Release integrator | T048,T051 | Tools/GameRelease/, build/package wrappers, Bootstrap release wiring, CI only as authorized, manifests/benchmark definitions/shared docs/coordination.json | Own final packaging and gates; preserve hosted-CI deferral unless explicitly assigned. Local clean-run evidence is valid but not hosted-CI evidence. |
| Character/presentation owner | T049 | Explicitly leased Generation/Core/Soldiers/, SoldierMotion/, Presentation/Rig/ files and respective tests | One bounded remediation owner; source/format identity changes through integrator. No asset replacement or manually edited generated payload. |
| Validation/performance worker | T050 | Tools/Tests for new product acceptance/capture analysis and evidence reports; production changes require owning-module assignment | Runs frozen map/size workload and actual reference hardware only when available. Cannot modify workload, units, bot count, goldens or report outcomes to pass. |
| Integrator + human reviewer | Final acceptance | Release handoff/evidence and named visual/hardware review | Human subjective review is Pending until performed; automated tools cannot sign on owner's behalf. |

## Dispatch gate and handoff

Read [contracts/integration](contracts-integration.md). Do not fork feature workers until the upstream compiling Game-v1 contracts, matching executable fixtures and fakes are committed on main. Each dispatched task supplies actual base SHA, contract/fixture hashes, exact file list, acceptance IDs from [validation](validation-acceptance.md), evidence path, resource limits and rollback. Worker may propose a contract correction but stops dependent edits until integrator freezes it. Fakes are test-only; shipping composition rejects them.

Use separate worktrees and one configured locked Unity project/editor per worker, with independent Library/cache. Respect available agent slots; phases need not run simultaneously. No agent edits another worker's files or shared docs without reassignment. Merge dependencies first, verify all work on main, preserve evidence, then remove temporary branches/worktrees. Do not leave completed changes on a worker branch.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).

## Planned final Windows installer (T073)

Owner requirement, 2026-11-14: the final game is delivered by a Windows installer. Running a newer installer must update the existing installed game, preserve settings/controls and stable app identity/path/shortcuts, and maintain a matching executable/content pair. ZIPs remain preview/internal artifacts. [Installer contract and INS01–INS06](../../features/windows-installer.md) extend REL01/REL06 and FIX13/FIX14. T073 is planned documentation scope only here; a future bounded release-integrator assignment implements installer/version/upgrade/repair/uninstall/rollback tooling. No installer, online service or acceptance waiver is delivered by this update.

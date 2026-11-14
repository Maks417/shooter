# M11 — Playable build and acceptance — bounded future agent assignments

**Current disposition — 2026-11-14, reviewed at 59316d3:** M11 release acceptance open: 59316d3 commits T052–T064 remediation, not an accepted player. Provenance/regression failures and content/view/settings gaps precede a clean owner-playable archive (T066–T071); T062/T065 still own final visual/REL acceptance. P05 remains FAIL. See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

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

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).

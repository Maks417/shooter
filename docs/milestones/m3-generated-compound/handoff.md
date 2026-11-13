# M3 — Generated compound and AI — handoff and gate record

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Pure software W01–W05 / T014–T016 PASS; Unity EditMode/PlayMode/smoke and reference B02/B03 hardware remain Pending/PendingHardware and are waived for M3 only under [ADR-027](../../decisions.md#adr-027--accepted-m3-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close). See [acceptance](validation-acceptance.md).

| Field | Value |
|---|---|
| Owner / integrator / reviewer | Cursor M3 integrator (bc-08a335af); owner Max waiver via standing no-confirmation direction |
| Source commit and upstream tag | Integration HEAD at close recorded in coordination `m3Start` / `milestoneAcceptance`; worker merges `b4f8a4d`/`942a104`/`c770c92`/`7d890ed` into main |
| Editor/URP/toolchain hash | Unchanged `toolchain-lock.json` (6000.3.28f1 / URP 17.3.0); editor binary absent on closer worker |
| Contract/schema/generator/protocol versions | C0-v1 unchanged; `environment.compound` 1.0.0; nav-settings v1 |
| Seed/content/world/workload manifests | Canonical seeds 460072030/1/42/18446744073709551615; B02/B03 freeze `Benchmarks/b02-b03-m3-freeze.json` |
| Client/server build artifacts | Pending (no Unity editor on worker) |
| Test results and benchmark evidence | `Artifacts/m3-generated-compound/<commit>/m3-software-*/validation.json` — contracts 71, simulation 31 (incl. 108000-tick soak), M3 generation 16, full generation 298 |
| Hardware/power/driver details | PendingHardware — `Artifacts/m3-generated-compound/owner-closure-20261113/hardware-attempt.json` |
| Accepted ADRs and known issues | Upstream ADR-017/024/025/026; **ADR-027** M3 tooling/hardware waiver; P05 FAIL retained |
| Gate status / reviewer / date | CLOSED WITH USER WAIVER / Max (owner) via integrator record / 2026-11-13 |

## Step status

- [x] W01 World plan and gameplay geometry: `Generation/Core/Environment`; four-seed connectivity PASS.
- [x] W02 Navigation and metadata: `World/Navigation`; clearance/hash/revision PASS.
- [x] W03 Streaming and residency: `World/Streaming`; 100-traversal/budget PASS.
- [x] W04 AI decisions: `Simulation/AI`; eight-bot budgets PASS.
- [x] W05 Integration soak: pure 108000-tick soak + B02/B03 freeze PASS; Unity/hardware Pending waived under ADR-027.

## Workers and branches

| Package | Branch | Commit |
|---|---|---|
| WP-06/environment | `cursor/m3-env-bdbf` | `b4f8a4d` |
| WP-06/streaming | `cursor/m3-streaming-bdbf` | `942a104` |
| WP-06/navigation | `cursor/m3-nav-bdbf` | `c770c92` |
| WP-07/ai | `cursor/m3-ai-bdbf` | `7d890ed` |

Temporary worktrees under `.local/worktrees/m3-*` retired after merge preservation.

## Downstream handoff

Publish world seed/manifests (`Benchmarks/b02-b03-m3-freeze.json`), collision/nav hashes, B02/B03 camera/input paths from fixtures, AI counters from `CombatAiDirector.Counters`, and known Unity/hardware Pending items. M4 uses identical B02/B03 workload for optimization/soak on reference hardware when the editor is available. Native-720p 30 FPS release gate remains M4.

## Sign-off rule

Pure software required checks have archived evidence. Unity and reference hardware remain Pending/PendingHardware and are owner-waived for M3 only; they are not claimed PASS. Stretch/deferred performance work moves to M4.

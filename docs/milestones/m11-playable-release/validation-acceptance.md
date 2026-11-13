# M11 — Playable build and acceptance — validation and acceptance

Status: **NOT RUN** for this new scope. Scenario IDs below are required checks to implement and run, not claims of existing test coverage. Each result records source SHA/dirty state, toolchain/contract versions, map/seed/content hashes, profile, roster, workload, duration and exact artifact paths.

| ID | Scenario | Required result |
|---|---|---|
| REL01 | Clean independent artifact | Empty-cache reproduction, both approved maps and every visual/gameplay dependency packaged; clean-directory normal launch needs no Unity, absolute dev path, network or external server; missing/corrupt content fails recoverably. |
| REL02 | Full play matrix | Both maps × both team sizes: complete 150-kill finish and unshortened 15-minute timeout in release player, results correct, replay/menu/settings work; deterministic scripts may drive test actors but at least one manual complete match/map is reviewed. Equal-score draw also verified via boundary fixture. |
| REL03 | Visual and usability sign-off | Named reviewer/build/seed/captures identify resolved P05 defects, live soldiers/rigs/weapon poses and menu/HUD at 720p 150%; original failed report remains unchanged. No unaccepted required visual issue silently waived. |
| REL04 | Reference performance | Frozen TDM-5v5-v1 and TDM-6v6-v1 for each map at native720p Low; warm60s/capture180s/three runs under performance.md, valid counters/thermal/process proofs and original budget/hitch limits. ≥30-minute normal-AI 6v6 soak/map includes two matches/replay, memory/query/lease/cover bounds. Old eight-bot B03 is not substituted. |
| REL05 | Regression and provenance | Existing C0/content/N1/pure suites plus Game-v1/map/UI/Unity/player checks pass, server excludes client presentation/content, provenance and schema identities valid; advisory-network and hosted-CI availability reported separately. |
| REL06 | Delivery and rollback | Release archive path/hash and executable launch instructions, source/content/toolchain/workload/evidence hashes, all actual gate statuses and known issues recorded; matching previous binary+content rollback rehearsed; all completed work committed on main and temporary worktrees/branches removed after verification. |

## Commands and evidence

Existing commands from repository root: python docs/fixtures/verify_fixtures.py; python docs/verify_docs.py; ./Tools/Test-Contracts.ps1 -Offline; ./Tools/Test-Generation.ps1 -Offline; ./Tools/Test-Simulation.ps1 -Offline; ./Tools/Test-SourceProvenance.ps1. Offline advisory lookup is PendingNetwork, separate from execution results. Add the new product fixtures/projects to wrapper coverage through integrator and prove nonzero execution, rather than assuming wildcard inclusion.

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Existing Smoke is the diagnostic ready/tick check and must remain distinct from new real-game acceptance. Proposed Compile-GameContent.ps1, Test-Maps.ps1, Test-Game.ps1 and game benchmark/package commands require implementation, documented parameters, nonzero failure propagation and fresh reports before use as gates. Do not claim today's B01-only capture path supports full team workloads.

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).

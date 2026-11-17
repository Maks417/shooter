# M11 — Playable build and acceptance — validation and acceptance

**Disposition:** current status is kept only in the [pack index](index.md) and [next playable build](../../next-playable-build.md); the requirements below are unchanged.

Current coverage, 2026-11-14: software remediation Present; player/device/hardware acceptance Pending/PendingHardware; REL01–REL06 NotClaimed. Scenario IDs below remain required — do not invent PASS. See [audit evidence](../../product-state-audit.md), [FIX matrix](../../release-remediation-validation.md) and the T065 operator sequence on the handoff. Every run records exact source/build/toolchain/content/map/profile/roster/workload/duration and artifacts.

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

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Smoke remains diagnostic and separate from real-game acceptance. Compile-GameContent publishes packages, Test-Maps/Test-Game remain pure suites, and Package-Release validates clean identities and creates an archive/launcher. T066–T071 complete the current baseline/content/view/settings/measurement gaps. ADR-037 TDM collector/matrix/soak commands exist, but T070 must finish multi-match replay/counter behavior before final reference execution. Twelve captures and two soaks remain PendingHardware; B01 captures do not satisfy REL04. See the [parameterized operator commands](handoff.md).

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).

## Installer and update delivery — owner requirement 2026-11-14

Final REL01/REL06 delivery requires a Windows installer and a successful N→N+1 in-place upgrade of an existing installation, preserving settings/controls, stable application identity/path/shortcuts and a matching verified binary/content pair. A ZIP-only handoff does not close final delivery. Execute [INS01–INS06](../../features/windows-installer.md#acceptance-evidence): clean install, upgrade, repair, interrupted replacement, uninstall/data retention and installed rollback. Keep all original gates and FIX13/FIX14 identity/provenance safeguards. T073 is Planned / NotRun; the doc update claims no installer implementation or acceptance.

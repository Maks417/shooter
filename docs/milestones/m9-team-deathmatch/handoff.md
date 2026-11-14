# M9 — Team Deathmatch and bot teams — handoff and gate record

**Current disposition — 2026-11-14: M9 integration incomplete.** Reducer tests and initial roster capacity pass in their scope; real repeated respawn/team AI/spawn safety and TDM07/08 are not accepted. Repairs: T053, T057–T058, T063, T065 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software)**, 2026-11-13. Authoritative TDM reducer, 11-bot planner, and OfflineGameSession tick binding are on main. Unshortened human 15-minute/score-limit player matches and reference hardware counters remain Pending — not claimed PASS.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T040–T043 |
| Pure TDM | Score/trade/draw/deadline/duplicate/stale covered by Game-v1 tests |
| AI | `CombatAiDirector.MaxBots=11` + `TeamBotPlanner` multi-enemy sensing; global ray/path budgets retained |
| Session | `OfflineGameSession.Step` installs respawns, plans bots, steps harness, processes deaths, evaluates finish |
| TDM07 | Pending — named unshortened player matches not run this assignment |
| TDM08 | PendingHardware |

## Ledger

| Task | Status |
|---|---|
| T040 Match authority | **PASS** (pure) |
| T041 Life cycles | **PASS** (software; spawn deferral in reducer) |
| T042 Team AI | **PASS** (software; 9/11 capacity) |
| T043 Prove whole match | Partial — software binding PASS; full player matrix Pending |

[Release roadmap](../../playable-release-plan.md) · [Previous](../m8-runtime-composition/index.md) · [Next](../m10-maps-shell/index.md).

# M9 — Team Deathmatch and bot teams — handoff and gate record

Status: **CLOSED (software)**, 2026-11-13. Authoritative TDM reducer, 11-bot planner, and OfflineGameSession tick binding are on main. Unshortened human 15-minute/score-limit player matches and reference hardware counters remain Pending — not claimed PASS.

| Field | Disposition |
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

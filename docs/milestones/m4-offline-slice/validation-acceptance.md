# M4 — Offline playable slice — validation and acceptance

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Pure O01–O03 PASS; V10/V12 native-720p hardware remain PendingHardware and are Unverified/Waived for M4 under [ADR-028](../../decisions.md#adr-028--accepted-m4-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close). See [handoff](handoff.md).

| Suite | Scenario | Disposition |
|---|---|---|
| O01 restart/idempotent events | Repeated restart; death/win once | **PASS** (pure) |
| O02 HUD/accessibility | 150% UI fit; focus/rebind/contrast | **PASS** (pure) |
| O03 FX/audio | Bounded pools; no recorded samples/sprites | **PASS** (pure) |
| V10 three-run native720p | p95 budgets | **PendingHardware** (waived M4 only) |
| V10/V12 soak/load | 30-minute / cold-warm | **PendingHardware** (waived M4 only) |

## Evidence location

`Artifacts/m4-offline-slice/` and `Artifacts/m4-offline-slice/owner-closure-20261113/`.

# M7 — Release hardening and 60 FPS experiment — handoff and gate record

Status: **CLOSED WITH USER WAIVER** (ADR-032). Pure H01–H05 tooling PASS_WITH_PENDING; Unity/60 FPS hardware PendingHardware waived for M7 only. Mandatory 30 FPS baselines not relaxed.

| Field | Value |
|---|---|
| Owner / integrator | Cursor M5-M7 integrator (bc-05dea339) / Max (owner waiver) |
| Contract/protocol | C0-v1; wire N1-v1 |
| Evidence | Artifacts/m7-hardening-stretch/ + Tools/ReleaseHardening/ |
| Stretch | PendingHardware (aselinesRelaxed=false) |
| ADRs | ADR-032; deferred ledger H05 |

## Step status

- [x] H01 Clean reproducibility (locks/provenance inventory; Unity build Pending)
- [x] H02 Regression matrix (docs/fixtures/pure suites PASS; Unity Pending)
- [x] H03 Release rollback pair type + rehearsal Pending player
- [x] H04 60 FPS stretch report PendingHardware (non-blocking)
- [x] H05 Deferred scope ledger (T025/eight-player/public N4 deferred)

## Code paths

- Tools/ReleaseHardening/regression_matrix.py
- Tools/ReleaseHardening/stretch_60fps_report.py
- Tools/ReleaseHardening/deferred_scope_ledger.json
- Tools/Test-M7Release.ps1
- UnityProject/Assets/Shooter/Bootstrap/Release/ReleaseRollbackPair.cs

## Owner closeout

| Gate | Disposition |
|---|---|
| H01–H05 pure tooling | PASS_WITH_PENDING |
| Unity clean builds / player rehearsal | Pending (waived ADR-032) |
| 60 FPS stretch captures | PendingHardware (waived; not PASS) |
| T025 / eight-player / public net | Deferred (not started) |
| P05 soldier clearance | FAIL retained (ADR-024) |

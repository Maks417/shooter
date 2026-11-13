# M6 — Four-player network slice — handoff and gate record

Status: **CLOSED WITH USER WAIVER** (ADR-031). Pure S01–S05 software PASS; Unity/30-minute B04 hardware PendingHardware waived for M6 only.

| Field | Value |
|---|---|
| Owner / integrator / reviewer | Cursor M5-M7 integrator (bc-05dea339) / Max (owner waiver) |
| Source commit | Soft close on `main` after M6 merge; see coordination `m6MilestoneAcceptance` |
| Contract/protocol | C0-v1 unchanged; wire N1-v1 |
| Test evidence | Simulation **60/60** (M5 network + M6 lag/recovery/B04/abuse); `Artifacts/m6-network-slice/` |
| Hardware | PendingHardware (editor absent); waived ADR-031 |
| ADRs | ADR-031 close; depends ADR-029/030 |

## Step status

- [x] S01 Historical hit query (16-sample/200 ms buffer, eligibility, clamp window)
- [x] S02 Authoritative fire ordering (dupe/dead/ammo/cadence; same-tick trade)
- [x] S03 Session recovery (full snapshot, Ready barrier, spectator late join, fresh reconnect)
- [x] S04 Four-peer B04 software soak (150 ms/3%; 30s proxy); 30-minute hardware Pending
- [x] S05 Exposure review (LAN/private token; N4 public deferred)

## Code paths

- `Networking/LagCompensation/` — HitHistoryBuffer, AuthoritativeFireValidator
- `Networking/Recovery/` — LateJoinSession
- `Networking/Abuse/` — AbuseLimits, B04SoakCounters
- `Tools/Simulation.Tests/Network/{LagCompensation,Recovery,Abuse}/`

## Owner closeout

| Gate | Disposition |
|---|---|
| S01–S05 pure software | PASS |
| 30-minute B04 PresentMon / separate hosts | PendingHardware (waived ADR-031) |
| Unity builds/smoke | Pending (waived ADR-031) |
| P05 soldier clearance | FAIL retained (ADR-024) |

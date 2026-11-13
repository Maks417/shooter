# M5 — Networking foundation — handoff and gate record

Status: **CLOSED WITH USER WAIVER** (ADR-030). Pure N01–N05 software PASS; Unity/hardware PendingHardware waived for M5 only. See [acceptance](validation-acceptance.md) and [entry](index.md).

| Field | Value to fill at execution |
|---|---|
| Owner / integrator / reviewer | Cursor M5-M7 integrator (bc-05dea339) / Max (owner waiver) |
| Source commit and upstream tag | Soft close HEAD recorded in coordination `m5MilestoneAcceptance`; base `ff596bf` |
| Editor/URP/toolchain hash | Locked `6000.3.28f1` / URP 17.3.0 per `toolchain-lock.json`; editor binary absent on closer |
| Contract/schema/generator/protocol versions | C0-v1 unchanged; wire **N1-v1**; prediction schema 1 |
| Seed/content/world/workload manifests | Seed `460072030`; no new generated art |
| Client/server build artifacts | Not built (editor absent) — Pending |
| Test results and benchmark evidence | Pure simulation **54/54** including 15 network tests; evidence under `Artifacts/m5-network-foundation/` |
| Hardware/power/driver details | Not measured — PendingHardware |
| Accepted ADRs and known issues | ADR-029 (UTP selection); ADR-030 (M5 waiver); UTP version pin null until editor verifies |
| Gate status / reviewer / date | CLOSED_WITH_USER_WAIVER / Max via standing direction / 2026-11-13 |

## Step status

- [x] N01 Framework spike: ADR-029; loopback + impairment measured; UTP selected behind `INetworkChannel`.
- [x] N02 Session and commands: handshake/codec/validator/batcher; mismatch rejected before spawn.
- [x] N03 Local prediction: 256-tick history, restore/replay, shot dedupe; p95 ≤0.1 m static-map gate.
- [x] N04 Remote snapshots: 20 Hz codec, interpolation 50–150 ms, ≤100 ms extrapolation, resync rate limit.
- [x] N05 Two-peer impairment review: pure matrix 0/50/100/150 ms × 0/1/3% loss; offline motor unchanged.

## Downstream handoff

Publish protocol/codec vectors (`N1-v1`), headless loopback launch configuration, prediction diagnostics and packet simulator seeds. M6 adds historical hit queries and late join without replacing M5 authority model.

## Code paths

- `UnityProject/Assets/Shooter/Networking/Transport/` — loopback, impairment fake, selection constants
- `UnityProject/Assets/Shooter/Networking/Codec/` — wire primitives, session/snapshot codecs
- `UnityProject/Assets/Shooter/Networking/Session/` — handshake, dedicated host, command validator/batcher
- `UnityProject/Assets/Shooter/Networking/Prediction/` — input ring, reconciler
- `UnityProject/Assets/Shooter/Networking/Snapshots/` — interpolation buffer, delta baselines
- `Tools/Simulation.Tests/Network/` — V11/V12-oriented pure suites

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested. Owner waiver ADR-030 closes M5 without inventing Unity/hardware PASS.

## Owner closeout

| Gate | Disposition |
|---|---|
| N01–N05 pure software | PASS |
| Unity EditMode/PlayMode/smoke/builds | Pending (waived ADR-030) |
| Reference hardware / PresentMon | PendingHardware (waived ADR-030) |
| UTP package version pin | Pending (ADR-029; not invented) |
| P05 soldier clearance | FAIL retained (ADR-024); not reopened |

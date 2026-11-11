# M6 — Four-player network slice — handoff and gate record

Status: **PLANNED / NOT IMPLEMENTED**. Fill this record during implementation. Unfilled fields below intentionally request evidence; they are not missing design decisions. See [acceptance](validation-acceptance.md) and [entry](index.md).

| Field | Value to fill at execution |
|---|---|
| Owner / integrator / reviewer | Unassigned |
| Source commit and upstream tag | Not implemented |
| Editor/URP/toolchain hash | Not recorded |
| Contract/schema/generator/protocol versions | Use accepted upstream; record actual values |
| Seed/content/world/workload manifests | Not generated |
| Client/server build artifacts | Not built |
| Test results and benchmark evidence | Not run |
| Hardware/power/driver details | Not measured |
| Accepted ADRs and known issues | None recorded for implementation |
| Gate status / reviewer / date | Pending review |

## Step status

- [ ] S01 Historical hit query: implementation, tests and review attached.
- [ ] S02 Authoritative fire ordering: implementation, tests and review attached.
- [ ] S03 Session recovery: implementation, tests and review attached.
- [ ] S04 Four-peer B04: implementation, tests and review attached.
- [ ] S05 Exposure review: implementation, tests and review attached.

## Downstream handoff

Tag network release candidate with protocol/content/world hashes, private-session run instructions and B04 evidence. M7 validates clean release and regression coverage without changing workload.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.

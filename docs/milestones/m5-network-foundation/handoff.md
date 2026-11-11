# M5 — Networking foundation — handoff and gate record

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

- [ ] N01 Framework spike: implementation, tests and review attached.
- [ ] N02 Session and commands: implementation, tests and review attached.
- [ ] N03 Local prediction: implementation, tests and review attached.
- [ ] N04 Remote snapshots: implementation, tests and review attached.
- [ ] N05 Two-peer impairment review: implementation, tests and review attached.

## Downstream handoff

Publish protocol/codec vectors, headless launch configuration, prediction diagnostics and packet simulator seeds. M6 adds historical hit queries and late join without replacing M5 authority model.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.

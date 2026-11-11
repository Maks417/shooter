# M7 — Release hardening and 60 FPS experiment — handoff and gate record

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

- [ ] H01 Clean reproducibility: implementation, tests and review attached.
- [ ] H02 Regression matrix: implementation, tests and review attached.
- [ ] H03 Release rehearsal: implementation, tests and review attached.
- [ ] H04 60 FPS experiment: implementation, tests and review attached.
- [ ] H05 Deferred scope ledger: implementation, tests and review attached.

## Downstream handoff

Publish release manifest, clean install/private-session instructions, evidence index, known limits and separate future-scope backlog. Do not mark deferred T025 delivered.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.

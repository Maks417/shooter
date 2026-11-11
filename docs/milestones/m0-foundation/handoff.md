# M0 — Foundation — handoff and gate record

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

- [ ] F01 Toolchain lock: implementation, tests and review attached.
- [ ] F02 Contracts C0: implementation, tests and review attached.
- [ ] F03 Composition and adapters: implementation, tests and review attached.
- [ ] F04 Quality and evidence harness: implementation, tests and review attached.

## Downstream handoff

Publish C0 fixtures, fake ports, build entry points and toolchain hash. WP-01 starts generation; WP-05 may start motor tests against fakes. Render/geometry/character workers consume frozen DTOs.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.

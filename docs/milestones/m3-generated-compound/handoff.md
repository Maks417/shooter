# M3 — Generated compound and AI — handoff and gate record

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

- [ ] W01 World plan and gameplay geometry: implementation, tests and review attached.
- [ ] W02 Navigation and metadata: implementation, tests and review attached.
- [ ] W03 Streaming and residency: implementation, tests and review attached.
- [ ] W04 AI decisions: implementation, tests and review attached.
- [ ] W05 Integration soak: implementation, tests and review attached.

## Downstream handoff

Publish world seed/manifests, collision/nav hashes, B02/B03 camera/input paths, AI counters and known visual/performance issues. M4 uses identical workload for optimization.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.

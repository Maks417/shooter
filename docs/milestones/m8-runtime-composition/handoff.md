# M8 — Runtime composition and Game-v1 foundation — handoff and gate record

Status: **PLANNED / UNASSIGNED / NOT RUN**, 2026-11-13. This is an implementation handoff template populated with required outputs; the present planning task changes no milestone acceptance. Historical M0–M7 administrative closures and all FAIL/Pending reports remain intact.

| Field | Current disposition |
|---|---|
| Owner / reviewer / base / final main commit | Unassigned; integrator must fill at implementation start/end |
| Tasks | T035–T039, planned |
| Game-v1 / C0 / content / toolchain / workload identities | Game-v1 freeze pending; existing locks unchanged by this plan |
| Build / packaged content / executable paths and SHA256 | Not produced for this milestone |
| Pure / Unity / player / visual checks | NotRun; record per-scenario status with raw evidence |
| Reference hardware / counters / thermal review | NotRun; missing execution remains PendingHardware |
| Scope limitations / rollback pair | Fill with actual findings; historical waivers not inherited |
| Known entry blocker | Existing source audit FAIL, 51 missing metadata entries + one unapproved Assets Markdown file; T035/RUN00 owns repair. See [planning validation](../../planning-validation.md) |

## Deliver before downstream assignment

Required next artifacts: accepted Game-v1 source/fixture/lock commit; authority lifecycle adapter; one packaged compound; actual runtime player/build manifest; path/input/audio/lifecycle evidence; frozen new workload definitions; named P05 remaining defects. M9 can then implement team match consumers without modifying frozen seams.

## Task and acceptance ledger

| Task | Status |
|---|---|
| T035 — Freeze product boundary first | Planned / NotRun |
| T036 — Extract and compose real authority | Planned / NotRun |
| T037 — Compile and install the compound | Planned / NotRun |
| T038 — Connect the client path | Planned / NotRun |
| T039 — Run and review the vertical player | Planned / NotRun |

Reviewer records each ID from [validation](validation-acceptance.md), exact evidence paths, accepted residual defects and a dated conclusion. Integrator updates shared docs with real implementation paths, verifies changes committed to main, and confirms evidence preservation and temporary branch/worktree cleanup. Do not check boxes or declare closure from this template.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

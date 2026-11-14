# M8 — Runtime composition and Game-v1 foundation — implementation pack

**Current disposition — 2026-11-14, reviewed at 59316d3:** M8 integration open: lifecycle/pause/input and visible/audio wiring landed in 59316d3; provenance, compiled visual closure, native navigation and authority-driven weapon/action views still need T066–T068, followed by real RUN/player evidence. See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

## Goal

Complete a real one-map player connecting procedural content, authority, buffered input, bots, rig/HUD/audio and safe teardown. Game-v1 is already frozen; T052–T056/T061 repair the incomplete paths. Start with T066 baseline repair, then T067–T068; do not repeat the landed T052–T054 repairs or contract freeze.

## Entry gate

M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

The [historical planning audit](../../planning-validation.md) metadata failure was repaired before f3d9954, and T052 repaired its later NU1004 failure in 59316d3. Fresh source provenance now fails on 19 new missing metadata entries and Generation has a cancellation/lock failure. T066 owns these current baseline issues; preserve GUID/contract/provenance guards and historical results.

## Execute

| Document | Purpose |
|---|---|
| [Implementation](implementation.md) | Dependency order and concrete task outputs |
| [Contracts and integration](contracts-integration.md) | Frozen interfaces, failure boundaries and merge gate |
| [Agent assignments](agent-assignments.md) | Exclusive paths, shared ownership and dispatch prerequisites |
| [Validation](validation-acceptance.md) | Scenario IDs, commands and required evidence |
| [Handoff](handoff.md) | Pending gate record and downstream artifacts |

Exit requires the real artifacts and validation in this pack, reviewed by the integrator. Missing tools/hardware remain Pending; historical owner waivers are not inherited. Future implementation needs an explicit assignment and actual base commit.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

# M8 — Runtime composition and Game-v1 foundation — implementation pack

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M8 integration validation remains open. T066 provenance/cancellation and T067 packaged catalog/visual loading are repaired; T068 authority-driven weapon/action views are landed in the retained `7cd7e81` candidate. Native-navigation publication/query semantics and actual RUN/player/repeated-life evidence remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

## Goal

Complete a real one-map player connecting procedural content, authority, buffered input, bots, rig/HUD/audio and safe teardown. Game-v1 is already frozen; T052–T056/T061 repair the incomplete paths. Start with retained-candidate playtest and remaining T067 native publication/query work; do not repeat landed baseline, presentation or contract-freeze work.

## Entry gate

M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

The [historical planning audit](../../archive/planning-validation.md) metadata failure was repaired before f3d9954, and T052 repaired its later NU1004 failure in 59316d3. The later 19-metadata/cancellation failures at 59316d3 were repaired under T066; latest pure/provenance checks pass. Preserve GUID/contract/provenance guards and historical failures. T067 native publication and actual player integration validation remain open.

## Execute

| Document | Purpose |
|---|---|
| [Implementation](implementation.md) | Dependency order and concrete task outputs |
| [Contracts and integration](contracts-integration.md) | Frozen interfaces, failure boundaries and merge gate |
| [Agent assignments](agent-assignments.md) | Exclusive paths, shared ownership and dispatch prerequisites |
| [Validation](validation-acceptance.md) | Scenario IDs, commands and required evidence |
| [Handoff](handoff.md) | Pending gate record and downstream artifacts |

Exit requires the real artifacts and validation in this pack, reviewed by the integrator. Missing tools/hardware remain Pending; historical owner waivers are not inherited. Future implementation needs an explicit assignment and actual base commit.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

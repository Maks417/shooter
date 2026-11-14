# M8 — Runtime composition and Game-v1 foundation — implementation pack

**Current disposition — 2026-11-14: M8 integration incomplete.** RUN02–RUN05 remain unaccepted as complete product gates: verified full content, visible views/audio, pause and real lifecycle evidence are missing. Repairs: T052–T056, T061–T063 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

Status corrected 2026-11-14: **M8 integration incomplete**. Historical software closeout is retained in [handoff](handoff.md); its labels do not establish acceptance. Follow [remediation tasks](../../release-remediation-plan.md) and [FIX validation](../../release-remediation-validation.md).

## Goal

Complete a real one-map player connecting procedural content, authority, buffered input, bots, rig/HUD/audio and safe teardown. Game-v1 is already frozen; T052–T056/T061 repair the incomplete paths. Start with bounded T052–T054 assignments, not a new contract freeze or the entire milestone at once.

## Entry gate

M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

The [historical planning audit](../../planning-validation.md) recorded 51 missing metadata entries and one unapproved Assets Markdown file. That repair is retained and fresh source provenance passes at f3d9954. The [current audit](../../product-state-audit.md) instead records the Contracts NU1004 lock failure plus incomplete runtime integration. Preserve GUID/contract/provenance guards; do not recreate the repaired baseline.

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

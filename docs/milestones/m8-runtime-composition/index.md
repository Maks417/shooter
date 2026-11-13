# M8 — Runtime composition and Game-v1 foundation — implementation pack

Status: **IN PROGRESS**, 2026-11-13. Tasks T035–T039. T035 Game-v1 freeze (ADR-034) and RUN00 complete; T036 pure session/lifecycle landed; T037–T039 product vertical still open. See [handoff](handoff.md).

## Goal

Produce a real one-map player that connects existing procedural content, authority, local input, bots, rig/HUD/audio and safe session teardown. Freeze product seams before feature workers touch them. This is the next recommended implementation assignment; this documentation task does not assign it.

## Entry gate

M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

The current [planning audit](../../planning-validation.md) found an existing source-provenance FAIL (51 missing metadata entries and one unapproved Assets Markdown file). T035 owns baseline repair and RUN00 before feature dispatch. Its fix must preserve existing GUIDs/contract/provenance rules; documentation delivery does not waive it.

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

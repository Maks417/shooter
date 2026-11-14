# Milestone implementation packs

Each folder contains only its milestone entry, detailed sequence, contracts/integration, agent assignment cards, validation/acceptance and handoff record. Requirements and feature specifications live once in the docs root, reached through relative links from each milestone. Code and accepted artifacts from prerequisite milestones are still required to execute.

Current disposition, 2026-11-14: M0–M7 retain historical owner-waived closures and P05 FAIL under ADR-024. M8–M11 have partial implementation and historical software closeout records; [current source audit](../product-state-audit.md) leaves M8–M10 integration incomplete and M11 acceptance open. Use [T052–T065 repairs](../release-remediation-plan.md) and [FIX01–FIX14](../release-remediation-validation.md), preserving original milestone requirements and historical reports. No coding leases are created by this documentation assignment.

| Milestone | Implementation pack |
|---|---|
| M0 — Foundation | [Open pack](m0-foundation/index.md) |
| M1 — Procedural feasibility | [Open pack](m1-procedural-feasibility/index.md) |
| M2 — Combat harness | [Open pack](m2-combat-harness/index.md) |
| M3 — Generated compound and AI | [Open pack](m3-generated-compound/index.md) |
| M4 — Offline playable slice | [Open pack](m4-offline-slice/index.md) |
| M5 — Networking foundation | [Open pack](m5-network-foundation/index.md) |
| M6 — Four-player network slice | [Open pack](m6-network-slice/index.md) |
| M7 — Release hardening and 60 FPS experiment | [Open pack](m7-hardening-stretch/index.md) |
| M8 — Runtime composition and Game-v1 foundation | [Open pack](m8-runtime-composition/index.md) |
| M9 — Team Deathmatch and bot teams | [Open pack](m9-team-deathmatch/index.md) |
| M10 — Two maps, menus and settings | [Open pack](m10-maps-shell/index.md) |
| M11 — Playable build and acceptance | [Open pack](m11-playable-release/index.md) |

## Maintenance

Root documents are authoritative shared specifications. Update them in place and rerun the root verifier with intentional manifest refresh. Milestone plans link to these shared files; do not duplicate them or use local plans to relax shared requirements. Each pack index links all local implementation documents and the shared documentation index. Status starts planned, with no claim that tests have run.

[Shared documentation](../index.md) · [Milestone gates](../milestones.md) · [Global task dependencies](../backlog.md).

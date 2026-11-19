# Milestone implementation packs

[Visual quality milestones VD0–VD9](../design-milestones.md) define the owner's Battlefield V / Bodycam / Warface goal within M8–M11. Start with art direction and a representative art slice, then execute component and final-review gates. Execution follows the visual roadmap; all visual exits are Pending.

Each folder contains its milestone entry, validation/acceptance and handoff record; M8–M11 also keep their implementation, contracts/integration and agent assignment pages. The pre-work planning pages of closed M0–M7 are in the archive. Requirements and feature specifications live once in the docs root, reached through relative links from each milestone. Code and accepted artifacts from prerequisite milestones are still required to execute.

Disposition: M0–M7 retain historical closures/waivers; M5/M6 network scope is parked. M8–M10 integration validation and M11 acceptance remain open; current state and remaining work are in [next playable build](../next-playable-build.md), finding status in the [audit](../product-state-audit.md) and evidence gates in the [FIX matrix](../release-remediation-validation.md).

| Milestone | Implementation pack |
|---|---|
| M0 — Foundation | Open pack |
| M1 — Procedural feasibility | Open pack |
| M2 — Combat harness | Open pack |
| M3 — Generated compound and AI | Open pack |
| M4 — Offline playable slice | Open pack |
| M5 — Networking foundation (parked, ADR-076) | Open pack |
| M6 — Four-player network slice (parked, ADR-076) | Open pack |
| M7 — Release hardening and 60 FPS experiment | Open pack |
| M8 — Runtime composition and Game-v1 foundation | [Open pack](m8-runtime-composition/index.md) |
| M9 — Team Deathmatch and bot teams | [Open pack](m9-team-deathmatch/index.md) |
| M10 — Maps, menus and settings | [Open pack](m10-maps-shell/index.md) |
| M11 — Playable build and acceptance | [Open pack](m11-playable-release/index.md) |

## Maintenance

Root documents are authoritative shared specifications. Update them in place and rerun the root verifier with intentional manifest refresh. Milestone plans link to these shared files; do not duplicate them or use local plans to relax shared requirements. Each pack index links all local implementation documents and the shared documentation index. Test results are recorded only in handoffs, with exact evidence.

[Shared documentation](../index.md) · [Milestone gates](../milestones.md) · Global task dependencies.

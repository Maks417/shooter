# M11 — Playable build and acceptance — handoff and gate record

**Current disposition — 2026-11-14: M11 release acceptance open.** Copy/inventory tooling is not clean independent delivery; REL01–REL06 still require complete source-matched product evidence, including P05, full matches and reference performance/soak. Repairs: T052, T062–T065 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software package tooling)**, 2026-11-13. Local offline package produced via `Tools/Package-Release.ps1`. Named visual review (REL03/T049), native-720p matrix (REL04/T050), and soak remain **PendingHardware / Pending named review**. P05 historical FAIL retained (ADR-024). No invented goldens; no fake hardware PASS.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T048–T051 |
| T048 Clean package | **PASS** tooling — inventory under `Artifacts/m11-playable-release/` |
| T049 Visual review | Pending (P05 FAIL retained) |
| T050 Perf/soak matrix | PendingHardware |
| T051 Handoff | This record + package inventory; rollback = prior diagnostic build pair |

### Package evidence

- `Artifacts/m11-playable-release/<commit>/package-20261113T230405706Z-7636baef/` (`validation.json` PASS, `inventory.json`, `Shooter-Offline/CONTROLS.txt`)

[Release roadmap](../../playable-release-plan.md) · [Previous](../m10-maps-shell/index.md) · [Shared docs](../../index.md).

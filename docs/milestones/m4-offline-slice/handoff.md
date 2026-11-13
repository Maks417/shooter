# M4 — Offline playable slice — handoff and gate record

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Pure O01–O03 software PASS; Unity/reference native-720p O04/O05 remain PendingHardware and are waived for M4 only under [ADR-028](../../decisions.md#adr-028--accepted-m4-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close).

| Field | Value |
|---|---|
| Owner / integrator / reviewer | Cursor M4 integrator (bc-08a335af); owner Max waiver via standing direction |
| Source commit | Close HEAD recorded in coordination `m4MilestoneAcceptance` |
| Editor/URP/toolchain hash | Unchanged; editor binary absent on closer worker |
| Contract versions | C0-v1 unchanged |
| Test evidence | `Artifacts/m4-offline-slice/<commit>/m4-software-*/validation.json` — simulation 39/39 |
| Hardware | PendingHardware — `Artifacts/m4-offline-slice/owner-closure-20261113/hardware-attempt.json` |
| Accepted ADRs | ADR-017/024/025/026/027; **ADR-028** M4 waiver |
| Gate status | CLOSED WITH USER WAIVER / 2026-11-13 |

## Step status

- [x] O01 Objective and restart: `Simulation/Match/CompoundClearMatch` PASS (pure).
- [x] O02 Settings and HUD: `Presentation/Hud/HudLayout` PASS (pure).
- [x] O03 Generated feedback polish: `Presentation/Fx` + `Presentation/Audio` PASS (pure).
- [x] O04 Profile and optimize: PendingHardware waived under ADR-028.
- [x] O05 Offline gate release: PendingHardware/tooling waived under ADR-028 (no player binary).

## Workers

| Package | Branch | Commit |
|---|---|---|
| WP-05/match | `cursor/m4-match-bdbf` | `c24bb86` |
| WP-03/hud | `cursor/m4-hud-bdbf` | `9731efb` |
| WP-03/fx-audio | `cursor/m4-fx-bdbf` | `51d4eb8` |

## Downstream

M5 networking remains **unassigned**. Offline slice pure objective/HUD/FX/audio adapters are available; reference 30 FPS claim is not made.

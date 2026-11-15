# Feature: combat audio (sample bank)

Status: T086 / [ADR-055](../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086). Software path for generated PCM combat cues; named device hearing (FIX10 / SET02) remains Pending.

## Policy

- Combat feedback uses a **deterministic generated PCM bank** baked from numeric recipes (`CombatAudioBankBaker`), not live sine beeps.
- Optional found PCM may be merged only with manifest source note + content hash. This build is non-commercial; paid licensing workflow is not required. Extracting audio from third-party commercial games is forbidden.
- Presentation-only: authority combat events and stride footsteps drive cues; audio never mutates simulation.
- ≤24 voices, peak limiter ±1, master/effects/UI gains (volume 0 = mute). Soft mix CPU target 0.5 ms/frame.

## Cue map

| Cue | Trigger |
|---|---|
| RifleShot / PistolShot | `ShotAccepted` + weapon definition id |
| Impact | `Impact` (non-head regions) |
| DamageTaken | `DamageApplied` on body/limbs (punchier than surface impact) |
| Headshot | `Impact` / `DamageApplied` with region 1 (head) — brighter crack than body hits |
| ReloadStart / ReloadComplete | `ReloadStarted` / `ReloadCompleted` |
| Death | `Death` |
| Footstep | horizontal stride ≥ 0.75 m |
| Ui | shell pause/resume and explicit UI cues |
| Ambient | quiet loop while an audio session is attached |

## Runtime path

`OfflineGameSession` combat event lease → `ProductSessionView.PumpProductAudio` → `ProductAudioEventRouter` (dedupe by event id) → `SynthAudioMixer.TryPlay` (bank sample + distance/pan/HF) → `UnitySynthAudioOutput.OnAudioFilterRead` → device.

Missing/unavailable audio device: recoverable warning; play continues without claiming FIX10 PASS.

## Acceptance

- Pure: voice cap, peak limit, gains/mute, distance attenuation, event-id dedupe, reload/death mapping, bank hash stability.
- Device: named reviewer hears distinct rifle vs pistol, impact, footsteps, reload, UI and channel mute (FIX10 / SET02). Waveforms alone are not PASS.

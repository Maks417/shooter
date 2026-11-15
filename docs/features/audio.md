# Feature: combat audio (stereo sample bank)

Status: T089 / [ADR-062](../decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089) (extends T086 / [ADR-055](../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086)). Named device hearing (FIX10 / SET02) remains Pending.

## Policy

- Product combat feedback loads **stereo 48 kHz WAV** clips from `Content/Audio/combat/` via `manifest.json` (source note + SHA-256 per file). Not live MIDI/sine beeps.
- **Rifle vs pistol shots** are distinct CC0 outdoor recordings (OpenGameArt Tabasco/kurt packs), sliced to one-shots — not shared synthetic beeps.
- This build is non-commercial; paid licensing workflow is not required. Extracting audio from third-party commercial games is forbidden.
- Generated stereo Foley (`CombatAudioBankBaker.BakeGenerated`) is **emergency fallback** only when the bank is missing or a hash fails.
- Presentation-only: authority combat events and stride footsteps drive cues; audio never mutates simulation.
- ≤24 voices, peak limiter ±1, master/effects/UI gains (volume 0 = mute). Soft mix CPU target 0.5 ms/frame.

## Distance

| Cue | Max audible | Notes |
|---|---|---|
| Footstep | 18 m | Steep atten + 0.45 base gain |
| Reload / death / impact / damage | 35 m | Mid-range soft cues |
| Rifle / pistol / headshot | 80 m | Gunfire ceiling |
| UI / ambient | 0 (local) | Authored stereo as-is |

## Cue map

| Cue | Trigger |
|---|---|
| RifleShot / PistolShot | `ShotAccepted` + weapon definition id |
| Impact | `Impact` (non-head regions) |
| DamageTaken | `DamageApplied` on body/limbs |
| Headshot | `Impact` / `DamageApplied` with region 1 (head) |
| ReloadStart / ReloadComplete | `ReloadStarted` / `ReloadCompleted` |
| Death | `Death` |
| Footstep | horizontal stride ≥ 0.75 m |
| Ui | shell pause/resume and explicit UI cues |
| Ambient | quiet loop while an audio session is attached |

## Runtime path

`Content/Audio/combat` (editor repo root / packaged beside exe) → `CombatAudioBankBaker.Bake` → `OfflineGameSession` combat event lease → `ProductSessionView.PumpProductAudio` → `ProductAudioEventRouter` → `SynthAudioMixer.TryPlay` (stereo sample + per-kind distance + mild azimuth) → `UnitySynthAudioOutput.OnAudioFilterRead` → device.

Missing/unavailable audio device: recoverable warning; play continues without claiming FIX10 PASS.

## Acceptance

- Pure: voice cap, peak limit, gains/mute, per-kind distance cull, event-id dedupe, stereo bank load, manifest hash mismatch fallback.
- Device: named reviewer hears distinct rifle vs pistol, impact, nearby footsteps (not map-wide), reload, UI and channel mute (FIX10 / SET02). Waveforms alone are not PASS.

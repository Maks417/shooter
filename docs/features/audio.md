# Feature: combat audio (stereo sample bank)

Status: T089 / [ADR-062](../decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089) (extends T086 / [ADR-055](../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086)). Named device hearing (FIX10 / SET02) remains Pending.

## Policy

- Product combat feedback loads **stereo 48 kHz WAV** clips from `Content/Audio/combat/` via `manifest.json` (source note + SHA-256 per file). Not live MIDI/sine beeps.
- **Rifle vs pistol shots** are distinct CC0 outdoor recordings (OpenGameArt Tabasco/kurt packs), sliced to one-shots — not shared synthetic beeps.
- Every recording needs a suitable license and recorded provenance; non-commercial use alone is not permission. Extracting audio from third-party commercial games is forbidden. This pass retains the existing CC0 bank.
- Generated stereo Foley (`CombatAudioBankBaker.BakeGenerated`) is **emergency fallback** only when the bank is missing or a hash fails.
- Presentation-only: authority combat events and stride footsteps drive cues; audio never mutates simulation.
- ≤24 voices, linked stereo limiter with 0.89 peak headroom and 80 ms release, master/effects/UI gains (volume 0 = mute). Soft mix CPU target 0.5 ms/frame remains unmeasured on reference hardware.

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
| KnifeSwing | `ShotAccepted` with `weapons/knife` |
| GrenadeThrow | `ProjectileSpawned` (grenade leave-hand) |
| GrenadeExplode | grenade blast `Impact` (invalid target, upward normal, surfaceTag 1) |
| Impact | `Impact` (non-head, non-blast regions) |
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

## Owner-requested spatial and mix refinement — 2026-11-15

Follow-up to T089 / ADR-062, authorized by the owner after the source review. Bounded presentation/audio/session work; no authority, weapon rules, C0/Game-v1, settings schema, toolchain or sample-bank identities change. No third-party assets are added.

- World emitters use camera-relative equal-power panning. Active voices update their direction as the camera turns or the listener moves; local weapon/handling/damage and own footsteps stay centered. World stereo narrows with distance; local and ambient clips retain authored width. Attenuation and obstruction are sampled when a cue starts, not continuously recomputed during its tail.
- Fractional interpolated PCM playback preserves duration/pitch at 44.1, 48 and 96 kHz. Filter coefficients follow the device rate; the Unity callback uses a cached configuration and calls no Unity API. Configuration changes refresh that cache; re-enabling output rearms playback.
- Near gunfire has a restrained 180 Hz body layer derived from its existing recording. Distance attenuation/HF rolloff and narrowing separate nearby/distant reports; the final quarter of each audible radius fades to zero instead of abruptly cutting off. These are processed versions of the existing samples, not separately recorded near/far banks.
- Main-thread acoustic queries use authoritative static map collision, layer 8, ignoring triggers/actors/cosmetic geometry. Eight rays maximum per presentation update, including three roof/side probes every 0.2 s, independent of quality. Obstruction queries cache for 0.15 s in a bounded 128-entry cache, invalidated after >1 m listener movement or session detach. When the budget is exhausted an uncached cue uses clear propagation; it is not deferred to the audio thread.
- Obstructed world cues lose gain and high frequencies. Nearby roof/walls control quiet cross-fed early gunfire reflections (61/73/137 ms), contained within the original voice budget. This is a simple local-enclosure approximation, not full room/portal reverb or exact source-to-wall reflection geometry.
- At saturation, a higher-priority cue can replace a lower-priority voice: local/UI, then gunfire/explosions, then ordinary Foley, then ambience; audibility breaks ties. Equal/lower priority is rejected. Ambience retries on the presentation thread and returns once a slot becomes available. The linked stereo limiter preserves channel balance with immediate attack and 80 ms release. Stop/detach clears voices, reflections and limiter history.
- Footstep stride uses horizontal movement; a >3 m snapshot discontinuity resets the stride rather than emitting a teleport burst. Surface-specific recorded Foley, separately recorded distance banks, bullet flybys and binaural/HRTF rendering remain future work.

Validation: pure tests cover rates/pitch, direction changes during a voice, priority/cap, obstruction energy, reflection tail/stop and cull fade; a Unity integration test checks actual map-wall occlusion, cosmetic exclusion and the query budget. Exact results and build/device limits are recorded in the [M11 handoff](../milestones/m11-playable-release/handoff.md#audio-spatial-and-mix-refinement--2026-11-15). Named headset/speaker review, output-device switching and 6v6 reference CPU/quality acceptance remain Pending. No Battlefield V parity claim follows from automated tests.

## Acceptance

- Pure: voice cap, peak limit, gains/mute, per-kind distance cull, event-id dedupe, stereo bank load, manifest hash mismatch fallback.
- Device: named reviewer hears distinct rifle vs pistol, impact, nearby footsteps (not map-wide), reload, UI and channel mute (FIX10 / SET02). Waveforms alone are not PASS.

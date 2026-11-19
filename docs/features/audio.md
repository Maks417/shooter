# Feature: combat audio and music

Audio is presentation only. Authority combat events, the local lifecycle snapshot and stride footsteps drive the cues. Audio never mutates the simulation.

The decisions behind it:
- [ADR-055](../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086) and [ADR-062](../decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089): the sample bank and per-kind distances.
- [ADR-096](../decisions.md#adr-096--accepted-music-channel-combat-sample-bank-v5-and-owner-audio-source-relaxation): music and the source policy.
- [ADR-102](../decisions.md#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix), [ADR-111](../decisions.md#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models) and [ADR-118](../decisions.md#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150): named-gun, knife/grenade and new-weapon cues.

## Banks and sources

**Combat bank.** The bank is `Content/Audio/combat/manifest.json` (`combat-audio-bank-v9-stereo`). It holds stereo 48 kHz WAV clips with 1–8 variations per cue. Each clip records its source, license and SHA-256.

**Building it.** `Tools/build_audio_bank.py` renders both the combat and music banks from `Tools/audio-sources.json`. Downloads go under the ignored `.local/audio-src`. The commands are:
- `fetch`
- `analyze`
- `build`
- `review`
- `check`

Levels are normalized on the true float peak, and no clip reaches full scale.

**Sources** are CC0 libraries:
- OpenGameArt;
- Kenney Impact/Interface/RPG Audio;
- The Free Firearm Sound Library.

Every gun's voice is its definition's `audio.shot`, cut from a real firearm take (for example AK-47, AR-15, SKS, Mosin, Mossberg). Reload Foley follows the handling style of the gun that last fired from that slot. Bolt and pump actions schedule `bolt_cycle` / `pump_cycle` inside the fire interval.

**Source policy.** This is a non-commercial build, so a source's license does not gate selection, but it is always recorded. Audio extracted from commercial games is forbidden.

**Fallback.** Generated stereo Foley (`CombatAudioBankBaker.BakeGenerated`) is an emergency fallback, per cue. It is used only when the bank is missing, a hash fails or a cue is absent. A missing named-gun shot is derived by varispeed from the real rifle or pistol takes.

## Cues and distance

| Cue | Trigger | Audible to |
|---|---|---|
| Gun shots (per weapon) | `ShotAccepted` + definition id | 80 m |
| Knife swing / hit flesh / hit hard | Knife `ShotAccepted`, then `DamageApplied` or a target-less `Impact` on that tick | 35 m |
| Grenade pin / throw / bounce | `Cooking` starts / `ProjectileSpawned` / scheduled where the arc meets foot height | 18 / 35 / 35 m |
| Grenade explode + far layer | Grenade blast `Impact` | 80 m / 160 m |
| Impact, damage taken, death | `Impact`, `DamageApplied`, `Death` | 35 m |
| Headshot | Head region (1) | 80 m |
| Hit marker / kill confirm | Local instigator's damage or death | Local |
| Bullet flyby | Enemy shot ray within 2.5 m of the listener's head, >4 m from the muzzle | 3 m |
| Reload start / shell / complete | `ReloadStarted` (time-fitted ±12% to the window, faded on death or a shot) / per shell / `ReloadCompleted` | 35 m |
| Dry fire, weapon switch, footstep | Empty-gun press, slot change, horizontal stride ≥0.75 m | 18 m |
| UI hover/click/back, pause | `TacticalUi` and shell (own mixer) | Local |
| Ambient, distant battle | Session loop; far gunfire every 6–20 s | Local |

Impact, hit and hit-marker cues play once per shooter, target and tick, so a shotgun blast does not stack them.

## Mixer and spatialization

The runtime path:
1. `Content/Audio/combat` is the bank (repo root in the editor, beside the exe when packaged).
2. `CombatAudioBankBaker.Bake` loads it.
3. `OfflineGameSession` hands out the tick's event lease.
4. `ProductSessionView.PumpProductAudio` reads the events.
5. `ProductAudioEventRouter` (with `LocalWeaponCueTracker`) picks the cues.
6. `SynthAudioMixer` mixes them.
7. `UnitySynthAudioOutput.OnAudioFilterRead` sends them to the device.

The callback calls no Unity API.

**Voices and limiting:**
- At most 24 voices.
- When saturated, voices are replaced by priority: local/UI first, then gunfire and explosions, then Foley, then ambience.
- A linked stereo limiter: 0.89 peak, immediate attack, 80 ms release.
- Master, effects and UI gains; volume 0 mutes.

**Panning.** World emitters use camera-relative equal-power panning that tracks camera turns. World stereo narrows with distance, and the final quarter of each radius fades to zero. Local cues stay centred.

**Playback.** Fractional interpolated PCM keeps pitch at 44.1, 48 and 96 kHz.

**Obstruction and reflections.** Obstruction uses static map collision (layer 8), with at most 8 rays per update, cached for 0.15 s. Obstructed cues lose gain and high frequencies. Nearby roof and walls add quiet early gunfire reflections at 61, 73 and 137 ms. This is a local approximation, not room reverb.

**Footsteps.** A snapshot jump of more than 3 m resets the stride instead of playing a burst.

A missing audio device logs a recoverable warning, and play continues.

## Music

`Content/Audio/music/manifest.json` (`music-bank-v1`) lists the OGG tracks:
- menu theme;
- calm and tension beds;
- victory, defeat and draw stingers;
- results loop.

`MusicDirector` (pure) maps shell screens to tracks:

| Screen | Music |
|---|---|
| Menu screens | Menu theme |
| Loading | Silence |
| Play | Calm bed, switching to tension near the score or time limit |
| Paused | 60% volume |
| Results | Outcome stinger, then the results loop |

During play the music ducks by up to −6 dB under local fire or a blast within 15 m.

`UnityMusicOutput` crossfades two 2D sources and keeps clips Vorbis-compressed in memory. It adds the single `AudioListener` when none exists. Music level is master × `musicVolume` (default 0.6). A missing track is silent, with one warning.

## Follow-up: placeholders

This is open T095 work. Each item needs one small download batch. List the files with source and size, and get the owner's approval before fetching.

| Slot | Current placeholder | Wanted |
|---|---|---|
| `bullet_flyby` (4 variations) | Gunshot transient plus a knife whoosh | Real supersonic crack or whiz-by recordings |
| `defeat_stinger` (music) | Last 13 s of War Theme, low-passed | A 5–12 s somber orchestral defeat sting |
| `ambient` | Project-rendered wind/hiss loop | A 30–90 s real outdoor battlefield or quiet-town loop |

Steps:
1. Add each source to `Tools/audio-sources.json` (`"manual": true` for browser downloads), then run `fetch`.
2. Run `analyze <path>` to find the slice points.
3. Point the matching `clips[]` / `music[]` entries at the new source. Drop `"keep": true` for `ambient`.
4. Run `build`, `check` and `review`, then `./Tools/Test-Simulation.ps1`. The `Shipped_repo_banks…` test proves the loaders accept the banks.
5. Update this section and [next playable build](../next-playable-build.md).

## Acceptance

**Pure tests:**
- voice cap and limiter;
- gains and mute;
- distance cull;
- event dedupe;
- every bank slot resolves;
- hash-mismatch fallback;
- reload time-fit;
- flyby geometry;
- bounce scheduling;
- ducking;
- music director transitions;
- the obstruction budget (a Unity test also covers real map walls).

**Device review (FIX10 / SET02).** A named listener checks:
- distinct guns;
- impacts;
- nearby, not map-wide, footsteps;
- reloads;
- knife and grenade;
- music;
- UI;
- sliders and mute.

Waveform tests alone never pass this review. The 0.5 ms/frame mix CPU target has not been measured on the reference machine.

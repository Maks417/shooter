# Feature: combat audio and music

Status: T089 / [ADR-062](../decisions.md#adr-062--accepted-stereo-foundauthored-combat-wav-bank-and-per-kind-distance-t089) (extends T086 / [ADR-055](../decisions.md#adr-055--accepted-generated-pcm-combat-sample-banks-t086)); bank v5 and music per [ADR-096](../decisions.md#adr-096--accepted-music-channel-combat-sample-bank-v5-and-owner-audio-source-relaxation). Named device hearing (FIX10 / SET02) remains Pending.

## Policy

- Product combat feedback loads **stereo 48 kHz WAV** clips from `Content/Audio/combat/` via `manifest.json` (`combat-audio-bank-v5-stereo`, source note + license + SHA-256 per file, 1–8 variations per cue). Not live MIDI/sine beeps.
- **Rifle vs pistol shots** are distinct CC0 outdoor recordings (OpenGameArt Tabasco/kurt packs), sliced to one-shots and retained byte-for-byte in v5.
- ADR-096 owner decision: non-commercial build, so a source's license does not gate selection; it is still recorded. Extracting audio from commercial games is forbidden.
- `Tools/build_audio_bank.py` renders the bank and the music bank from `Tools/audio-sources.json` (downloads under ignored `.local/audio-src`): `fetch`, `analyze`, `build`, `review`, `check`.
- Generated stereo Foley (`CombatAudioBankBaker.BakeGenerated`) is **emergency fallback** only when the bank is missing, a hash fails, or a cue is absent from the manifest (per cue).
- Presentation-only: authority combat events, the local lifecycle snapshot and stride footsteps drive cues; audio never mutates simulation.
- ≤24 voices, linked stereo limiter with 0.89 peak headroom and 80 ms release, master/effects/UI gains (volume 0 = mute). Soft mix CPU target 0.5 ms/frame remains unmeasured on reference hardware.

## Distance

| Cue | Max audible | Notes |
|---|---|---|
| Footstep, dry fire, weapon switch, grenade pin | 18 m | Steep attenuation |
| Reload (rifle/pistol) / death / impact / damage / knife / grenade throw and bounce | 35 m | Mid-range soft cues |
| Rifle / pistol / headshot / near grenade blast | 80 m | Gunfire ceiling |
| Distant grenade layer | 160 m | Quiet up close, dominant past ~30 m |
| Bullet flyby | 3 m | Miss distance from the listener's head |
| UI, hit marker, kill confirm, distant battle, ambient | 0 (local) | Authored stereo as-is |

## Cue map

| Cue | Trigger |
|---|---|
| RifleShot / PistolShot | `ShotAccepted` + weapon definition id |
| KnifeSwing | `ShotAccepted` with `weapons/knife` |
| KnifeHitFlesh / KnifeHitHard | knife instigator's `DamageApplied` / target-less `Impact` on the swing's tick |
| GrenadePin | local `WeaponAction` enters `Cooking` |
| GrenadeThrow | `ProjectileSpawned` (grenade leave-hand) |
| GrenadeBounce | two clinks scheduled where the throw arc meets the thrower's feet height; cancelled by the blast |
| GrenadeExplode + GrenadeExplodeFar | grenade blast `Impact` (invalid target, upward normal, surfaceTag 1) |
| Impact | `Impact` (non-head, non-blast regions) |
| DamageTaken | `DamageApplied` on body/limbs |
| Headshot | `Impact` / `DamageApplied` with region 1 (head) |
| HitMarker / KillConfirm | local instigator's `DamageApplied` / `Death` (local only) |
| BulletFlyby | enemy rifle/pistol `ShotAccepted` whose ray passes within 2.5 m of the listener's head, >4 m from the muzzle |
| ReloadStart (rifle) / ReloadPistol | `ReloadStarted` by slot family; time-fitted ±12 % to the reload window; faded on the owner's death or next shot |
| ReloadComplete | `ReloadCompleted` |
| DryFire / WeaponSwitch | local fire press with an empty gun / active slot change |
| Death | `Death` |
| Footstep | horizontal stride ≥ 0.75 m |
| Ui / UiHover / UiClick / UiBack | shell pause/resume; `TacticalUi` hover, click and back controls (menu included, own mixer) |
| Ambient | quiet loop while an audio session is attached |
| DistantBattle | far-off gunfire/explosion every 6–20 s while playing (session-seeded) |

## Shipped sources (bank v5 / music v1)

- Reloads: OpenGameArt CC0 handgun and airsoft reload recordings, stitched (mag out → pouch → mag in) to span the reload window. Bolt/slide release is `reload_complete`.
- Knife, grenade handling, bounces, impacts, hits, deaths, footsteps, UI: Kenney Impact/Interface/RPG Audio (CC0).
- Distant battle, far blast layer, flyby transients: The Free Firearm Sound Library mid/near-distance recordings (CC0).
- Kept from v4: rifle/pistol shots, near grenade blast, ambience, two knife swings.
- Provisional: flyby (no real whiz recording), defeat stinger, ambience.
- The exact slices live in `Tools/audio-sources.json`.

## Music

`Content/Audio/music/manifest.json` (`music-bank-v1`) lists OGG tracks for `menu_theme`, `match_calm`, `match_tension`, `victory_stinger`, `defeat_stinger`, `draw_stinger` and `results_loop` with SHA-256 and optional loop region. `MusicDirector` (pure) maps shell screens:

- Main/Setup/Settings from menu → menu theme.
- Loading → silence.
- Playing/Paused/confirmations → calm bed, latched to tension when the leading team is within 15 kills of the limit or 60 s remain. Paused → 60 %. Ducked up to −6 dB under local fire or a blast within 15 m.
- Results → outcome stinger from the local team (victory/defeat/draw), then the results loop.

`UnityMusicOutput` crossfades two 2D `AudioSource`s and keeps clips Vorbis-compressed in memory (the loader turns streaming off before requesting compression, keeps each load request alive for its clip's lifetime and starts a cue only once its audio data is loaded, logging `SHOOTER_MUSIC_PLAY`). The code-built scene has no camera listener, so it adds the single `AudioListener` when none exists. Music level = master × music (`UserSettings.musicVolume`, default 0.6, Audio tab slider). A missing or invalid track is silent with one warning.

## Runtime path

`Content/Audio/combat` (editor repo root / packaged beside exe) → `CombatAudioBankBaker.Bake` → `OfflineGameSession` combat event lease (every tick's events since the previous frame's lease) → `ProductSessionView.PumpProductAudio` → `ProductAudioEventRouter` (+ `LocalWeaponCueTracker`, scheduled bounces, distant battle) → `SynthAudioMixer.TryPlay` (stereo sample + per-kind distance + mild azimuth) → `UnitySynthAudioOutput.OnAudioFilterRead` → device. Shell UI cues use a second mixer on the same bank; music is `ProductShellHost.UpdateMusic` → `MusicDirector` → `UnityMusicOutput`.

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

## Bank v6 — named-gun reports (ADR-102)

`combat-audio-bank-v6-stereo` appends ten cues after the v5 slots: `ak47_shot`, `ak102_shot`, `m16_shot`, `mk14_shot`, `tac338_shot`, `g17_shot`, `eagle_shot`, `bolt_cycle`, `reload_ak` and `reload_bolt`. All are cut by `Tools/build_audio_bank.py` from sources already in `Tools/audio-sources.json`: the CC0 Free Firearm Sound Library takes for AK-47 (AK-102 high-passed), AR-15 (M16), Tikka (.308, Mk 14), Mosin Nagant and Arisaka (M-TAC 338), Walther PPQ (G17) and 1911 with a low rifle layer (Golden Eagle), plus the existing reload and cocking Foley. A gun's voice is its definition's `audio.shot`. Reload Foley follows the handling style of the gun that last fired from the reloading slot, and bolt-action shots schedule `bolt_cycle` inside the fire interval. If a cue is missing from the manifest, named-gun shots are derived by varispeed from the shipped real rifle/pistol takes. Owner listening review is Pending.

## Follow-up: placeholders

Open T095 work, deferred by the owner on 2026-11-17. Each item needs one more small download batch: list the files with source and size, and get the owner's approval before fetching.

| Slot | Current placeholder | Wanted |
|---|---|---|
| `bullet_flyby` (4 variations) | Near-gunshot transient (90 ms, high-passed) plus a knife whoosh | Real supersonic crack / whiz-by recordings (search freesound/OpenGameArt for "bullet whiz", "flyby", "supersonic crack") |
| `defeat_stinger` (music) | Last 13 s of War Theme, low-passed to 2.5 kHz | A short (5–12 s) somber orchestral defeat or "mission failed" sting |
| `ambient` (1–2 loops) | v4 project-rendered wind/hiss/hum loop | Real outdoor battlefield or quiet-town ambience loop, 30–90 s, seamless or crossfade-trimmed |

Steps:
1. Add each source to `sources[]` in `Tools/audio-sources.json` and run `python Tools/build_audio_bank.py fetch`. Use `"manual": true` for sites that need a browser download.
2. Find slice points with `python Tools/build_audio_bank.py analyze <path>`.
3. Point the matching `clips[]`/`music[]` entries at the new source; drop `"keep": true` for `ambient`. The bank allows up to 8 variations per cue.
4. Run `python Tools/build_audio_bank.py build`, `check` and `review`, then `./Tools/Test-Simulation.ps1`. The `Shipped_repo_banks…` test proves the loaders accept the banks.
5. Update this section, the M11 handoff and the T095 row in [next-playable-build](../next-playable-build.md), and refresh the docs manifest.

## Acceptance

- Pure: voice cap, peak limit, gains/mute, per-kind distance cull, event-id dedupe, stereo bank load, manifest hash mismatch fallback.
- Pure (ADR-096): every v5 slot resolves, variation compaction, reload time-fit and cancel, knife hit pairing, flyby geometry, bounce scheduling, duck level, music director transitions/outcome/tension, music manifest hash check, local weapon cues, settings `musicVolume`.
- Device: named reviewer hears distinct rifle vs pistol, impact, nearby footsteps (not map-wide), rifle vs pistol reload, knife hit, grenade pin/bounce/near and far blast, menu music and UI clicks, outcome stinger, music slider and channel mute (FIX10 / SET02). Waveforms alone are not PASS.

## Bank v7: knife and grenade re-mix (ADR-111)

[ADR-111](../decisions.md#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models) moves the bank to `combat-audio-bank-v7-stereo`. Only knife and grenade clips change: the swing layers a whoosh with a blade draw "shing"; flesh hits layer a heavy thud with a wet chop; hard hits layer metal impacts with a blade scrape; the 2.2–2.8 s explosion layers the v6 take with a generated sub-bass drop, a crack transient, scattered debris and a tail; the far explosion has a longer roll. Everything comes from sources already in `.local/audio-src`; the v6 explosion takes are inputs under `.local/audio-src/kept/` (restore them from git history before a rebuild). An A/B listening page is written to `.local/knife-grenade-ab.html`. Owner listening review is Pending.

## Bank v8: true-peak levels (ADR-111 addendum)

The builder now normalises every kind on the true float peak instead of `volumedetect`, which saturated at 0 dBFS and let over-full-scale mixes ship up to 5.4 dB above their `PEAK_DB` target. 66 older clips are re-levelled (no content change, gain only); the bank is `combat-audio-bank-v8-stereo`. No clip reaches full scale. Owner listening review is Pending.

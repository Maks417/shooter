# Launch and playtest the current candidate

Reviewed 2026-11-14. A Windows offline archive is available for testing. Its build/package reports pass; manual gameplay and final release acceptance remain Pending. This guide helps reach the game and record what actually happens. See the [audit](product-state-audit.md) for technical findings and [M11 handoff](milestones/m11-playable-release/handoff.md) for retained build evidence.

## This version runs locally

One player and all allied/enemy bots run on your PC. You do **not** need to buy/rent a server, start a separate server, make an online account or use online multiplayer. The owner explicitly confirmed this first-version scope on 2026-11-14. Online play is deferred; the immediate goal is a working local game.

## Get into the game

1. Find this verified archive:

   ```text
   C:\Job\shooter\Artifacts\m11-playable-release\7cd7e8191e118929af742bef382774edb22a57c2\package-20261114T153524429Z-8edfd0c3\archive\Shooter-Offline.zip
   ```

2. Extract the entire ZIP into a new folder outside the repository, for example `C:\Games\Shooter-Playtest`. Keep `Shooter.exe`, `Shooter_Data`, DLLs, `Content` and the JSON lock files together. No Unity installation, internet connection or separate server should be needed by this package; a new independent run still needs confirmation.
3. Run `launch-offline.cmd` from the extracted folder. The expected first screen is Main menu. The launcher sets its own working directory. Direct-executable discovery exists in source but has no new independent launch evidence in this review; use the launcher first.
4. Choose **Play**, use **Prev map / Next map** for Training Compound or Depot Yard, select **5v5 / 6v6** with **Cycle size**, then **Start match**. Map/team CLI text in packaged CONTROLS does not replace this shell-owned setup; default launch does not auto-start a match.
5. Use **Esc** to pause, resume, change settings, restart or leave. A normal match ends at **150 team kills or 15 active minutes**. Check final results and **Play again** after finishing.

The archive is 40,645,312 bytes (about 38.8 MiB). SHA256:

```text
fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50
```

Verify with `Get-FileHash -Algorithm SHA256 <path-to-Shooter-Offline.zip>`. Binary source is `7cd7e8191e118929af742bef382774edb22a57c2`; newer documentation does not change that identity. The original archive and its known-issues text are retained unchanged. Local `Artifacts` are ignored and are not included in a Git clone: preserve/copy the ZIP for delivery. If absent, a release integrator must build/package an explicitly identified source, not substitute an unrelated historical binary.

## Default controls

These come from `Game/Contracts/GameData.cs:DefaultBindings` and `Presentation/Input/LocalPlayerInput.cs`; existing saved custom bindings can override them. Settings exposes binding edits and Restore defaults.

| Action | Default |
|---|---|
| Move / look | WASD / mouse |
| Fire / aim | Left / right mouse button |
| Reload | R |
| Rifle / pistol | 1 / 2 |
| Sprint / crouch / jump | Left Shift / Left Ctrl / Space |
| Slide (T072 build only) | Hold Left Shift while sprinting forward, then press F |
| Scoreboard | Hold Tab |
| Pause / back / cancel | Esc (reserved recovery control) |

**Slide availability:** T072 adds slide in newer source/builds; the historical `7cd7e81` archive documented above has **no sliding**. The T072 build/package and actual checks are recorded below when available. Default Slide is rebindable in Settings and combines with Sprint; custom legacy F bindings remain intact (Slide starts Unbound). See [slide behavior](features/combat.md#local-sprint-slide--t072--adr-039).

## What is still unproven

The retained handoff records Main menu only. This review checked archive hashes and prior reports, and did not launch or play the game. Bots, navigation, movement, combat, repeated lives, match completion, sound/display/control behavior and visual readability still need actual player evidence. P05 visual FAIL remains under ADR-024. Reference 720p Low30 performance is unmeasured for this candidate. Native-navigation publication is still incomplete: the packaged data is software-grid output despite the native adapter now existing. Do not treat this as an accepted release.

## Slide checks for a T072 build

Build momentum with W + Left Shift, press F: check camera/body lowers and forward slide slows over ≤0.75 s. Release/repress F only after the cooldown; holding it must not chain boosts. Check F alone/rest/airborne do not start slides, Space cancels, walls stop movement, a low ceiling prevents standing, pause/focus does not queue a slide, and repeated deaths/new match clear it. Repeat on both maps, sizes and profiles. Actual interactive validation remains Pending until recorded; automated InputSystem/PhysX checks are separate evidence.

## First playtest record (T071)

Use one extracted binary/content pair. Start with Training Compound 5v5, then cover the other configurations. Capture each failure before asking for a fix. A menu launch alone does not pass any row.

| Configuration | Load / movement / both weapons | Bots / repeated respawn / pause-focus | Settings / scoreboard / leave-restart | Normal complete match / results / replay |
|---|---|---|---|---|
| Training Compound 5v5 | Pending | Pending | Pending | Pending |
| Training Compound 6v6 | Pending | Pending | Pending | Pending |
| Depot Yard 5v5 | Pending | Pending | Pending | Pending |
| Depot Yard 6v6 | Pending | Pending | Pending | Pending |

For the initial T071 handoff, exercise all four configurations and finish at least one full normal match per map; record which size was completed. Do not shorten rules for evidence. T065 separately requires the complete **eight-match** score-limit/timeout matrix plus draw fixture and named reviews; this table does not replace those exits.

Check human camera/input after repeated death and ~3-second respawn, allies/enemies and scores, 10-second pause and focus loss/return, rifle/pistol visuals and reload timing, and replay without stale actors/audio. Test settings Apply/Cancel/defaults/rebinding; on the real display test Keep/Revert and its 15-second timeout. Record audio output/gains/mute by a named listener. Additional 20-cycle UI/device durability review remains required.

For every attempt retain: reviewer/date, archive hash, binary/content identities, map/seed/team size/profile/resolution, exact steps, expected/actual result, duration/finish reason, log/capture paths and PASS/FAIL/Pending. A useful defect entry is:

```text
Candidate: 7cd7e81 / archive fd54c97e...
Map / size / profile:
Reviewer / date / machine:
Steps:
Expected:
Actual / blocking or cosmetic:
Player log / screenshot or video path:
```

## If launch or a map fails

Keep the log and error text. Restore missing/corrupt files by extracting the **same ZIP into another fresh folder**, then retry the launcher. Source contains a repair/reinstall screen with Retry; whether the actual player handles both startup and map-load corruption is still Pending. Do corruption/recovery tests only on a disposable extraction and preserve the verified original.

For a dedicated diagnostic log, run from the extraction in PowerShell:

```powershell
$playtestRoot = (Get-Location).Path
& "$playtestRoot\Shooter.exe" -logFile "$playtestRoot\playtest-player.log"
```

This uses executable-relative content defaults and is a separate direct-launch check; do not call it passed until observed. Retain logs/captures in an ignored `Artifacts/project-state-audit/` or M11 run folder before deleting temporary extractions. The old handoff's `C:\Temp\shooter-t071-playtest-59bbcbb` directory was absent at this review, so it is not a current launch location.

A prior archive exists at `114815c` but rollback was recorded **SkippedNoPrevious**; do not infer a successful player rollback. It is historical and lacks T070 measurement changes. T071/T065 must rehearse a real compatible prior binary/content pair and record the result.

# Launch and playtest the current build

This guide gets a tester from the installer to a match and defines what to record. Which installer is current, and what it contains, is tracked only in [next playable build](next-playable-build.md#current-build); exact hashes and evidence are in the [M11 handoff](milestones/m11-playable-release/handoff.md#delivery-0319).

## This version runs locally

One player and all allied/enemy bots run on your PC. You do **not** need a server, an online account or a network connection. Online play is parked ([ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence)).

## Install and launch

1. Verify the installer: `Get-FileHash -Algorithm SHA256 <path-to-Shooter-Setup-x.y.z.exe>` and compare with the handoff. `Artifacts/` is ignored and not in a Git clone; if the file is missing, a release integrator builds it from an explicitly identified source (`Tools/Build-Installer.ps1`), never by relabelling an older binary.
2. Run the installer. It installs per-user to `%LOCALAPPDATA%\Programs\Shooter` by default (optional desktop shortcut). Running a newer installer upgrades the existing installation in place and keeps settings and controls; downgrades are refused unless `/ALLOWDOWNGRADE=1` is passed. See [installer behaviour](features/windows-installer.md).
3. Start **Shooter** from the Start menu or desktop shortcut. The first screen is the Main menu.
4. Choose **Play** or a map card, pick **Training Compound**, **Depot Yard** or **Oil Depot**, select **5v5**, **8v8**, **10v10** or **15v15**, then **Start match**. A 5-second countdown precedes the fight.
5. Use **Esc** to pause, resume, change settings, restart or leave. A normal match ends at **150 team kills or 15 active minutes**. Check the debrief and **Play again**.

## Default controls

From `Game/Contracts/GameData.cs` (`DefaultBindings`); saved custom bindings override them. Settings exposes rebinding and Restore defaults.

| Action | Default |
|---|---|
| Move / look | WASD / mouse |
| Fire / aim | Left / right mouse button (grenade: fire cooks and throws) |
| Reload | R |
| Rifle / pistol / knife / grenade | 1 / 2 / 3 / 4 |
| Last weapon | Q |
| Pick up dropped weapon | G |
| Sprint / crouch / jump | Left Shift / Left Ctrl / Space |
| Slide | While sprinting forward, press F |
| Scoreboard | Hold Tab |
| Pause / back / cancel | Esc (reserved) |

## What is still unproven

Builder, package and installer checks pass for each delivery, and the owner has played 0.3.x builds, but no formal T071 playtest is recorded. Complete human matches on all maps and sizes, bot routing on the compact maps, repeated lives, sound/display/control behaviour, visual readability, P05 soldier visual review and reference 720p Low30 performance all still need recorded evidence. Do not treat any installer as an accepted release.

## Playtest record (T071)

Use one installed build. Start with Training Compound 5v5, then cover every configuration. Capture each failure before asking for a fix. Reaching the menu alone does not pass any row.

| Configuration | Load / movement / all weapons | Bots / repeated respawn / pause-focus | Settings / scoreboard / leave-restart | Complete match / debrief / replay |
|---|---|---|---|---|
| Training Compound 5v5 | Pending | Pending | Pending | Pending |
| Training Compound 8v8 | Pending | Pending | Pending | Pending |
| Training Compound 10v10 | Pending | Pending | Pending | Pending |
| Training Compound 15v15 | Pending | Pending | Pending | Pending |
| Depot Yard 5v5 | Pending | Pending | Pending | Pending |
| Depot Yard 8v8 | Pending | Pending | Pending | Pending |
| Depot Yard 10v10 | Pending | Pending | Pending | Pending |
| Depot Yard 15v15 | Pending | Pending | Pending | Pending |
| Oil Depot 5v5 | Pending | Pending | Pending | Pending |
| Oil Depot 8v8 | Pending | Pending | Pending | Pending |
| Oil Depot 10v10 | Pending | Pending | Pending | Pending |
| Oil Depot 15v15 | Pending | Pending | Pending | Pending |

Finish at least one full normal match per map and record which size was completed. Do not shorten rules for evidence. T065 separately requires the full score-limit/timeout matrix, the draw fixture and named reviews; this table does not replace those exits.

Check: camera/input after repeated death and respawn; allies, enemies and scores; time to first contact and spawn pressure; bots leaving spawn and routing over stairs/catwalks (Oil Depot); 10-second pause and focus loss/return; weapon visuals, reload timing, knife and grenade; sprint/slide (F alone, at rest or airborne must not slide; holding F must not chain; walls and low ceilings stop it); replay without stale actors or audio. Test settings Apply/Cancel/defaults/rebinding; on the real display test Keep/Revert and its 15-second timeout. Record audio output, gains and mute by a named listener.

For every attempt retain reviewer/date, installer version and hash, map/team size/quality profile/resolution, exact steps, expected/actual result, duration/finish reason, log/capture paths and PASS/FAIL/Pending:

```text
Build: installer 0.3.x / source <commit>
Map / size / profile:
Reviewer / date / machine:
Steps:
Expected:
Actual / blocking or cosmetic:
Player log / screenshot or video path:
```

## If launch or a map fails

Keep the log and error text. The player log is `%USERPROFILE%\AppData\LocalLow\Procedural Shooter\Shooter\Player.log`; for a dedicated log, start the installed `Shooter.exe` with `-logFile <path>`. Bot navigation summaries appear as `SHOOTER_AI_SUMMARY` lines. To restore missing or corrupt files, rerun the same installer (repair). Corruption tests belong on a disposable installation only. Store logs and captures under ignored `Artifacts/` (M11 run folder) before removing temporary copies.

Upgrade checks: install the previous installer, customise controls, run the newer installer, confirm one uninstall entry, the same path and preserved settings. Installed rollback uses the older installer with `/ALLOWDOWNGRADE=1`. Results so far are in [installer status](features/windows-installer.md#implementation-status).

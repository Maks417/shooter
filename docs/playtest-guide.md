# Launch and playtest the current candidate

**Current installer — 2026-11-15:** **0.3.16** at `Artifacts/deliveries/Shooter-Setup-0.3.16.exe`, clean binary/content source `7e54be9` (SHA256 `03e50de130978f98e7fb524e583e3e73e7cb1b03c24ed4c7b6518bf1ae5841cb`). Compact 64 × 80 m distinct maps (ADR-074), Oil Depot dressing, combat-feel/bot-name/weapon-profile/HUD updates. Native 12 packages, clean Client, smoke, package and installer builders PASS. Installation/upgrade, EditMode/PlayMode, named compact-map playtest and acceptance remain Pending. [Delivery evidence](milestones/m11-playable-release/handoff.md#delivery-0316).

**Weapon visual preview — 2026-11-15:** installer **0.3.14** at `Artifacts/deliveries/Shooter-Setup-0.3.14.exe`, clean binary/content source `b8235a3`. Improves rifle, pistol, knife and grenade geometry and spatial PBR finishes; Contracts 76, Generation 402, Unity EditMode 57 / PlayMode 21, native publication, clean Client, smoke, package and installer builders PASS. Installation/upgrade, first-person/world visual review, reference performance and acceptance remain Pending. [Exact evidence and limitations](milestones/m11-playable-release/handoff.md#arsenal-delivery-0314).

**Latest T083 preview — 2026-11-15:** installer **0.3.11**, clean binary/content source `df64aa8`, adds packaged first-person gloved hands/sleeves for both weapons, analytic support-hand reach and a continuous reload remove/insert arc. Includes the merged T082 bot baseline. Unity EditMode 53 / PlayMode 21, native publication, clean Client/Server and package/installer builders PASS. Depot 6v6 rifle hands observed; automation stopped on live player movement and left the game open. Live pistol/reload/ADS, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact evidence and installer](milestones/m11-playable-release/handoff.md#t083-delivery-0311). Older entries below are historical.

**Latest T081 preview — 2026-11-15:** Windows installer **0.3.10**, clean binary/content source `e140dd6`, adds packaged panelled walls, bevels/footings/coping, painted cargo with rails/corrugation/locks, map/menu material separation and rougher coated weapons. Native publication, clean Client/Server, PlayMode and package/installer builders PASS. Both gameplay roots/catalog records match 0.3.9 exactly. Limited player review observed Main/cards and Compound 6v6; automation stopped on live user input, leaving the player open. Depot close-up, installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain Pending. [Exact delivery evidence](milestones/m11-playable-release/handoff.md#t081-delivery-0310). Older entries below are historical.

**Latest T080 correction preview — 2026-11-15:** installer **0.3.9**, clean binary source `beac9ae`, fixes physical surface scale, overview framing and discarded menu submeshes. Client/Server and native package/installer builders PASS; limited automated player review loaded Depot 5v5 and Compound 6v6 from a short verified copy. This remains visibly below BFV quality; soldiers, hands/actions and block architecture need further work. Deep evidence-path content loading fails; installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain open. [Actual evidence and installer](milestones/m11-playable-release/handoff.md#t080-delivery-039). Older entry pointers below are historical.

**Current T075 design preview — 2026-11-15:** Windows installer **0.3.4**, clean binary source `7407bc4`, implements procedural PBR map surfaces, textured menu staging, working actual-map cards and analytic first-person handling with LOD0 equipment on Low. Unity **43 EditMode / 17 PlayMode**, clean Client/Server, eight native packages, provenance and package/installer builders PASS. Automated player review observed both map cards and Compound 5v5 / Depot 6v6 loads; this is not full gameplay or comparative visual acceptance. P05 FAIL, reference performance and M8–M11/VD exits remain open. [Identities, evidence and limitations](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

## Launch 0.3.4

Use the upgrading Windows installer recorded in the [T075 handoff](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034). The 0.3.2/0.3.3 intermediates are retained for evidence, not the current recommendation. Choose a rendered map card or Play, select 5v5/6v6 and Start match. Controls and existing preferences are unchanged. This review loaded Compound 5v5 and Depot 6v6 only; the full matrix below remains Pending. Actual 0.3.4 installation/upgrade was not executed here.

## Prior visual preview — 0.3.1

Use the **Windows installer 0.3.1**, source b531da9, at the exact path/hash in the [current M11 handoff](milestones/m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview). It preserves the stable installer identity and preference policy; actual 0.3.1 install/upgrade acceptance has not been executed here. Main now uses a live procedural staging scene and two rendered operation cards. Choose Play or a map card, select 5v5/6v6, then Start match. Settings has Audio/Video/Controls/Accessibility groups and Apply/Cancel. Existing WASD/mouse/weapons/Slide/Escape/Tab controls remain.

Final in-player review is **Pending: the owner stopped Computer Use with Escape**. The raw log confirms Main/menu-scenery readiness, not full gameplay acceptance. Maps, soldier/weapon/hand-animation detail, reference performance and P05 remain separate open stages/gates. Use a normal installer path or a short preview extraction path: the deep internal evidence extraction failed visual-file loading in an earlier attempt. The older 5eaced5 instructions below are retained history; that archive has a known startup crash and must not be used as the current preview.

> **Known crash in the archive below (found 2026-11-14):** the `5eaced5` player crashes about 4 seconds after reaching Main (Windows APPCRASH 0x80000001; IL2CPP allocation-hook recursion when Unity attaches the audio thread). The fix and its root cause are in [ADR-042](decisions.md#adr-042--accepted-native-c-allocation-counter-for-benchmark-runs). Use the newer candidate recorded in the [M11 handoff](milestones/m11-playable-release/handoff.md#integration-session-2026-11-14) instead; the archive details below are kept as history.

Current T072 playtest build, 2026-11-14: a Windows offline archive **with sliding** is available for testing. The final game will use an [upgrading Windows installer](features/windows-installer.md); this ZIP is a preview. Its build/package reports pass; manual gameplay and final release acceptance remain Pending. This guide helps reach the game and record what actually happens. See the [audit](product-state-audit.md) for technical findings and [M11 handoff](milestones/m11-playable-release/handoff.md) for retained build evidence.

## This version runs locally

One player and all allied/enemy bots run on your PC. You do **not** need to buy/rent a server, start a separate server, make an online account or use online multiplayer. The owner explicitly confirmed this first-version scope on 2026-11-14. Online play is deferred; the immediate goal is a working local game.

## Get into the game

1. Find this verified archive:

   ```text
   C:\Job\shooter\Artifacts\m11-playable-release\5eaced5fe526ec9dbb8e5c18f1defd9235036808\package-20261114T172353598Z-47719aa8\archive\Shooter-Offline.zip
   ```

2. Extract the entire ZIP into a new folder outside the repository, for example `C:\Games\Shooter-Playtest`. Keep `Shooter.exe`, `Shooter_Data`, DLLs, `Content` and the JSON lock files together. The package is intended to run without Unity, internet or a separate server. Extracted startup was checked outside the repo; full clean-machine/manual gameplay validation is still pending.
3. Run `launch-offline.cmd` from the extracted folder. The expected first screen is Main menu. The launcher sets its own working directory. Executable-relative defaults reached Main in the independent batch-mode probe; use the launcher for normal interactive play.
4. Choose **Play**, use **Prev map / Next map** for Training Compound or Depot Yard, select **5v5 / 6v6** with **Cycle size**, then **Start match**. Map/team CLI text in packaged CONTROLS does not replace this shell-owned setup; default launch does not auto-start a match.
5. Use **Esc** to pause, resume, change settings, restart or leave. A normal match ends at **150 team kills or 15 active minutes**. Check final results and **Play again** after finishing.

The archive is 40,668,944 bytes (about 38.8 MiB). SHA256:

```text
8240e2983cc74a4e5ef665258ab12efd72fe4d05137b90c9b24fa5026b34580f
```

Verify with `Get-FileHash -Algorithm SHA256 <path-to-Shooter-Offline.zip>`. Binary source is `5eaced5fe526ec9dbb8e5c18f1defd9235036808`; newer documentation does not change that identity. The original archive and its known-issues text are retained unchanged. Local `Artifacts` are ignored and are not included in a Git clone: preserve/copy the ZIP for delivery. If absent, a release integrator must build/package an explicitly identified source, not substitute an unrelated historical binary.

## Default controls

These come from `Game/Contracts/GameData.cs:DefaultBindings` and `Presentation/Input/LocalPlayerInput.cs`; existing saved custom bindings can override them. Settings exposes binding edits and Restore defaults.

| Action | Default |
|---|---|
| Move / look | WASD / mouse |
| Fire / aim | Left / right mouse button |
| Reload | R |
| Rifle / pistol | 1 / 2 |
| Last weapon | Q (toggles previous rifle/pistol) |
| Sprint / crouch / jump | Left Shift / Left Ctrl / Space |
| Slide (подкат) | Hold Left Shift while sprinting forward, then press F |
| Scoreboard | Hold Tab |
| Pause / back / cancel | Esc (reserved recovery control) |

**Slide availability:** the `5eaced5` archive above includes T072 sliding. The retained historical `7cd7e81` archive has no slide. Clean Unity EditMode 26/26, PlayMode 13/13 (including real wall/ceiling/quality tests), Client/Server builds and package validation pass; see [M11 delivery evidence](milestones/m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery). Default Slide is rebindable in Settings and combines with Sprint; custom legacy F bindings remain intact (Slide starts Unbound). See [slide behavior](features/combat.md#local-sprint-slide--t072--adr-039).

## What is still unproven

The T072 package was independently extracted outside the repository and its executable reached Main using packaged defaults from a different working directory (batch-mode probe, no server). Archive SHA256 and 245 inventory hashes match. This is startup evidence, not manual gameplay. Local input/physics tests pass; complete human matches and device/visual/performance reviews remain pending. Bots, navigation, movement, combat, repeated lives, match completion, sound/display/control behavior and visual readability still need actual player evidence. P05 visual FAIL remains under ADR-024. Reference 720p Low30 performance is unmeasured for this candidate. Native-navigation publication is still incomplete: the packaged data is software-grid output despite the native adapter now existing. Do not treat this as an accepted release.

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
Candidate: 5eaced5 / archive 8240e298...
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

Executable-relative defaults passed the batch-mode startup probe; retain a separate log for this interactive run. Retain logs/captures in an ignored `Artifacts/project-state-audit/` or M11 run folder before deleting temporary extractions. The old handoff's `C:\Temp\shooter-t071-playtest-59bbcbb` directory was absent at this review, so it is not a current launch location.

The prior `7cd7e81` archive is retained. T072 packaging validates its distinct rollback-pair identity and presence (software rehearsal PASS); actual player rollback remains Pending. Older `114815c` is historical and lacks T070 measurement changes. T071/T065 must rehearse an actual compatible player rollback; T073 must separately prove installed-version rollback.

## Final delivery will be an installer

The owner requires the final Windows game to be installed and updated by an installer: run a newer installer to upgrade the existing installed game while keeping settings/controls and stable application identity/path/shortcuts. T073 and [INS01–INS06](features/windows-installer.md#acceptance-evidence) are planned; no installer is shipped by the T072 ZIP. You do not need a server or online account to play the installed local-bot game.

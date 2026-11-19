# Feature: Windows installation and upgrades

The game ships as a Windows installer. Running an installer for a newer version updates the existing installation in place and keeps settings and controls. ZIP files are internal artifacts only.

This is implemented with Inno Setup 6.7.3 ([ADR-041](../decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073), T073), and installers have shipped since 0.3.1.

## Implementation status

| Item | State |
|---|---|
| Tooling | `Tools/Build-Installer.ps1`, `Tools/installer/shooter.iss`, `Tools/installer/installer-policy.json` (pinned ISCC hash, frozen AppId). Synthetic regression `Tools/test_build_installer.ps1`: 11 cases PASS. |
| Build | `pwsh ./Tools/Build-Installer.ps1 -PackageDirectory <validated Package-Release dir> -ReleaseVersion <n.n.n> [-PreviousInstallerReport <installer-report.json>]`. Output: `Shooter-Setup-<version>.exe`, `.sha256` and `installer-report.json` beside the package run. |
| INS01 clean install | Pending: needs a clean Windows machine. On the owner PC, a silent install launched to Main from a foreign working directory. |
| INS02 upgrade | PASS (silent, owner PC): one uninstall entry, same path, settings hash unchanged, stale content removed, 7cd7e81→5eaced5 payload switch verified. |
| INS03 repair | PASS: rerunning the same installer restored 3 deleted/corrupted content files. |
| INS04 interrupted upgrade | Partial: running game and locked files abort before any change (exit 1 / 7); mid-copy crash or power loss not rehearsed and not detected by the game. |
| INS05 uninstall | PASS: files, shortcuts and registration removed; LocalLow settings preserved. |
| INS06 installed rollback | PASS: downgrade refused by default; prior installer with `/ALLOWDOWNGRADE=1` restored 7cd7e81 and launched to Main. |
| Not exercised | Interactive wizard, Restart Manager prompt, code signing (installer is unsigned). |

These are software exercises on the owner PC, not REL acceptance.

## Player flow

1. Run the installer on a Windows PC; install the complete local game and create the normal launch/uninstall entries.
2. Play one human with allied/enemy bots locally. No Unity editor, repository/developer cache, internet account, rented server or separate server process is required.
3. Obtain the installer for a newer version and run it. It recognizes the existing application, updates that installation in place, and keeps the player's settings and controls.
4. Launch the updated game through the existing shortcut. It uses the new matching executable/content pair; the player does not manually extract or copy files.

A background auto-updater, online matchmaking and hosting service are not required. The requested update mechanism is running the newer installer.

## Installation and update contract

| Area | Required behavior |
|---|---|
| Identity / version | Keep one stable application identity across releases. Each release has a newer installer version plus immutable source/build/content identities. An upgrade must not create a second unrelated game installation or duplicate app entries. |
| Install location | Detect and reuse the existing installation, including a user-selected path. Keep install scope consistent across upgrades; the implementation chooses and documents per-user/per-machine behavior. |
| Payload | Bundle the validated Windows client, complete verified map/visual content, required player runtime files, licenses and identity metadata. Publish installer SHA256 and its exact binary/content/toolchain/contract references. |
| Preferences | Preserve user settings, key bindings (including Slide and LastWeapon), accessibility and other user-owned data. File-schema migrations must be explicit and tested; replacing application files must not reset controls. |
| Matching files | Update executable and compiled content together as one verified release pair. Stage/validate replacements so cancellation/failure does not leave mixed versions or an unusable installation. |
| Running game | Handle locked files and a running game safely; request that the game is closed before replacement. Do not silently terminate a player's active match. |
| System integration | Maintain one installed-app/uninstall entry and stable launch shortcuts. The installed executable must locate packaged content independently of the caller's working directory. The executable icon (also used by the shortcuts and the uninstall entry) is the analytic `Generation/Icons/AppIcon` brand emblem ([ADR-123](../decisions.md#adr-123--accepted-unified-front-end-visual-system)), baked natively per Standalone icon slot by `ShooterBuild.ConfigureAppIcon` into `Assets/Shooter/Settings/AppIcon*.asset`. The setup exe uses the same emblem: `Build-Installer.ps1` runs `Tools/InstallerArt`, which writes `setup.ico`, `wizard.bmp` and `wizard-small.bmp` from `Generation/Icons/InstallerArt` into the installer extra directory, and records their hashes in `installer-report.json` (`installerArt`). |
| Repair / rollback | Rerunning the same installer can repair missing/corrupt application files. A failed/cancelled upgrade preserves or restores the previous working pair and preferences. A deliberate previous-version rollback needs its own tested compatible pair. |
| Uninstall | Remove installed application files and shortcuts/app registration. Preserve user settings by default unless the user explicitly chooses removal. |

The installer technology (Inno Setup EXE), version mapping and stable application identifier are frozen in [ADR-041](../decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073). No paid signing service, hosting or online dependency is selected. Existing generated-art provenance, clean-build identity checks, offline gameplay and acceptance rules remain mandatory.

## Acceptance evidence

| Scenario | Required result |
|---|---|
| INS01 — clean install | Install on a clean Windows environment with no Unity/repository/dev cache/server dependency; launch from the installed shortcut and exercise the existing offline map/match gates. |
| INS02 — upgrade | Install version N, customize controls/settings, run N+1 installer; the same installation and shortcuts launch N+1 with the correct new payload identities and preserved preferences. Verify one app/uninstall entry and the original selected path. |
| INS03 — repair | Remove/corrupt a disposable installed content file, rerun the same installer, verify repair and launch with the original validated pair. |
| INS04 — interrupted upgrade | Rehearse cancelled/failed replacement; previous game still launches, settings remain and no mixed binary/content pair is accepted. |
| INS05 — uninstall | Check application/shortcut/registration cleanup and explicit user-data retention behavior. |
| INS06 — installed rollback | Restore a compatible prior installed binary/content pair and prove launch; package inventory/presence alone is insufficient. |

Record installer version/hash, source/dirty/build/content/toolchain identities, install/upgrade paths, reviewer, logs and actual PASS/FAIL/Pending. Extend REL01/REL06 and FIX13/FIX14 evidence with these scenarios; do not replace existing match, visual/device, reference-performance, provenance or rollback gates. Installer tooling is implemented (see [Implementation status](#implementation-status)); INS01 and the remainder of INS04 are Pending, and REL acceptance remains NotClaimed.

## Implementation ownership

T073 is the M11 release-integrator card in task supplement. It consumes the accepted source-matched client/content pair and existing release package safeguards; shared build/package/settings/identity changes remain integrator-owned. The owner authorized T073 implementation and agent dispatch on 2026-11-14 and chose Inno Setup; the technology, identity and upgrade model are frozen in ADR-041. See [release plan](../next-playable-build.md), [remaining queue](../next-playable-build.md), [M11 acceptance](../acceptance.md#m11--release) and [playtest guide](../playtest-guide.md).

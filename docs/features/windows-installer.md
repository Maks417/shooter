# Feature: Windows installation and upgrades

Owner requirement, 2026-11-14: **the final game must be delivered as a Windows installer. Running an installer for a newer game version must update the game already installed on that PC.** This is a first-release delivery requirement, within M11. Current ZIP files are playtest/internal validation artifacts; they do not complete this requirement. Installer implementation is planned as T073, not implemented by the documentation update.

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
| Preferences | Preserve user settings, key bindings (including Slide), accessibility and other user-owned data. File-schema migrations must be explicit and tested; replacing application files must not reset controls. |
| Matching files | Update executable and compiled content together as one verified release pair. Stage/validate replacements so cancellation/failure does not leave mixed versions or an unusable installation. |
| Running game | Handle locked files and a running game safely; request that the game is closed before replacement. Do not silently terminate a player's active match. |
| System integration | Maintain one installed-app/uninstall entry and stable launch shortcuts. The installed executable must locate packaged content independently of the caller's working directory. |
| Repair / rollback | Rerunning the same installer can repair missing/corrupt application files. A failed/cancelled upgrade preserves or restores the previous working pair and preferences. A deliberate previous-version rollback needs its own tested compatible pair. |
| Uninstall | Remove installed application files and shortcuts/app registration. Preserve user settings by default unless the user explicitly chooses removal. |

The installer technology/file format (EXE or MSI), version mapping and stable application identifier are implementation decisions to freeze in a delivery ADR before T073 coding. No tool/version, paid signing service, hosting or new online dependency is selected here. Existing generated-art provenance, clean-build identity checks, offline gameplay and acceptance rules remain mandatory.

## Acceptance evidence

| Scenario | Required result |
|---|---|
| INS01 — clean install | Install on a clean Windows environment with no Unity/repository/dev cache/server dependency; launch from the installed shortcut and exercise the existing offline map/match gates. |
| INS02 — upgrade | Install version N, customize controls/settings, run N+1 installer; the same installation and shortcuts launch N+1 with the correct new payload identities and preserved preferences. Verify one app/uninstall entry and the original selected path. |
| INS03 — repair | Remove/corrupt a disposable installed content file, rerun the same installer, verify repair and launch with the original validated pair. |
| INS04 — interrupted upgrade | Rehearse cancelled/failed replacement; previous game still launches, settings remain and no mixed binary/content pair is accepted. |
| INS05 — uninstall | Check application/shortcut/registration cleanup and explicit user-data retention behavior. |
| INS06 — installed rollback | Restore a compatible prior installed binary/content pair and prove launch; package inventory/presence alone is insufficient. |

Record installer version/hash, source/dirty/build/content/toolchain identities, install/upgrade paths, reviewer, logs and actual PASS/FAIL/Pending. Extend REL01/REL06 and FIX13/FIX14 evidence with these scenarios; do not replace existing match, visual/device, reference-performance, provenance or rollback gates. Until implemented and executed, installer/update delivery is **Planned / NotRun**, and REL acceptance remains NotClaimed.

## Implementation ownership

T073 is the planned M11 release-integrator card in [task supplement](../task-supplement.md#t073--windows-installer-and-in-place-upgrade-planned). It consumes the accepted source-matched client/content pair and existing release package safeguards; shared build/package/settings/identity changes remain integrator-owned. The user's instruction here authorizes recording the requirement, not implementing installer machinery or dispatching agents. See [release plan](../playable-release-plan.md), [remaining queue](../next-playable-build.md), [M11 acceptance](../milestones/m11-playable-release/validation-acceptance.md) and [playtest guide](../playtest-guide.md).

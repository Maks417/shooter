# Playable offline release — current state and next action

**Latest T080 correction preview — 2026-11-15:** installer **0.3.9**, clean binary source `beac9ae`, fixes physical surface scale, overview framing and discarded menu submeshes. Client/Server and native package/installer builders PASS; limited automated player review loaded Depot 5v5 and Compound 6v6 from a short verified copy. This remains visibly below BFV quality; soldiers, hands/actions and block architecture need further work. Deep evidence-path content loading fails; installed upgrade, full gameplay, P05 and reference/VD/REL acceptance remain open. [Actual evidence and installer](milestones/m11-playable-release/handoff.md#t080-delivery-039). Older entry pointers below are historical.

Latest visual checkpoint: [T074 installer 0.3.1](milestones/m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview), source b531da9, has clean builds/tests and native package/installer proof. It delivers the screenshot-directed menu/HUD software. Final interactive review was stopped by the owner; full gameplay, visual/device, installed-upgrade, reference and P05 acceptance remain open. Earlier dated candidate descriptions below do not supersede this entry.

Reviewed 2026-11-14 from `84befb9` (subsequent `a54ed83` adds only CLAUDE guidance). The latest retained build is `7cd7e81`: clean Client/Server, passing retained Unity reports, eight packaged content roots and a verified Windows archive. Fresh review confirms archive SHA256/inventory and repaired pure/provenance baseline. **The remaining owner-facing gap is demonstrated gameplay, not creation of the first ZIP.** Native-navigation publication also remains technically incomplete. See the [audit](product-state-audit.md) and [launch/playtest guide](playtest-guide.md).

## Required playable product

The final deliverable is a source-matched Windows x64 installer with [in-place version upgrades](features/windows-installer.md); its installed game launches offline into Main menu. ZIPs remain interim playtest artifacts. Select Training Compound or Depot Yard, Team Deathmatch, and 5v5 or 6v6. One human joins Team 1 with nine/eleven total bots. Teams race to 150 enemy kills or 15 active minutes; equal scores draw. Movement, rifle/pistol aim/fire/reload, death/respawn, pause, live/final tables, replay and return to menu must work. Audio/video/control/accessibility settings persist and affect actual devices.

[Vision](vision.md), [team rules](features/team-deathmatch.md), [maps](features/playable-maps.md) and [shell](features/game-shell.md) retain exact requirements. Native1280×720 Low30 on the reference machine remains an acceptance gate; 60 FPS is separate stretch. No external art, extra online scope, optional T025 or public distribution is added.

## First-version scope

Owner confirmed on 2026-11-14: **local play with bots only**. Run one human plus allied/enemy bots on the same PC; no online multiplayer, external server process, server rental/hosting purchase, online account or service is required. Online/LAN multiplayer and hosted deployment are deferred. Existing server-compatible design and Server isolation tests can remain as regression guards; online delivery does not block the first playable version. See [vision](vision.md#first-version-scope-local-bots-only).

## Final installer requirement (T073, planned)

Running a newer game installer must detect and update the existing installation, preserve settings/controls and stable shortcuts/app registration, and replace matching binary/content together. Rehearse install, upgrade, repair, interrupted update, uninstall and installed rollback under [INS01–INS06](features/windows-installer.md#acceptance-evidence). This requirement extends M11 delivery; no installer implementation is claimed by the current ZIP package.

## Project estimate

| Stage | Assessment |
|---|---|
| Product software | Substantially integrated and built; T066 baseline, T068 views, T069 preferences and T070 measurement software are landed. |
| Local testable artifact | Available at `7cd7e81`; archive hash and 245 inventoried files verified. Handoff records Main menu launch. |
| Proven playable candidate | Pending: both maps/sizes, real movement/combat/bots/respawn, full matches/results/replay and recovery. |
| Accepted release | Open: remaining T067 native navigation, T062 visual disposition and T065 match/device/performance/soak/delivery evidence. |

No defensible percentage or delivery date follows from test counts alone. The first named player session determines whether remaining work is validation or additional functional repairs.

## Execute in this order

1. **T071: play the retained archive now.** Follow the guide; exercise both maps × both sizes, complete at least one normal match/map, retain actual logs/captures and classify blockers. Do not rebuild merely to pick up documentation or restart completed baseline repairs.
2. **T067: close the known native-navigation gap.** Wire pinned-Unity native publication into the content pipeline, verify both maps against authoritative collision, resolve partial/fallback query semantics and runtime install cost/clearance. Any implementation work requires its bounded assignment; new content identities require a matching rebuilt/repackaged candidate and affected reruns.
3. **T071: repair observed functional blockers under their owning cards and finish handoff.** Verify independent extraction, missing/corrupt player recovery and compatible prior-pair rollback. Publish exact archive/hash/controls/known issues; preserve original failures and binary identities.
4. **T070 + T062 + T065: validate the final stable pair.** Complete match, named visual/UI/audio/display, reference native720p Low captures, two map soaks and final provenance/delivery checks. No reference numbers are implied by the existing menu launch.

The [remaining-work cards](next-playable-build.md) keep original owners, boundaries and exits. Work remains inside M8–M11; no new milestone or waiver is required. One integrator owns Bootstrap/Contracts/settings/locks/workloads/coordination; this review dispatches no workers and authorizes no runtime edits.

## Playtest versus acceptance

The existing archive is a **test candidate**, not an assertion that T071's manual exit passed. A usable owner handoff needs all required interactions exercised and at least one complete normal match/map. Disclose P05 FAIL, incomplete native publication and unmeasured reference performance. Any observed functional blocker prevents calling it proven playable.

T065 retains eight full matches (both maps × both sizes × score/unshortened timeout), draw fixture, named reviews, 20 shell cycles, actual display/audio/control behavior, twelve reference Low captures and two ≥30-minute normal-AI soaks, server isolation/provenance/regressions and independent installer install/update/repair/uninstall/rollback (plus retained internal archive). Missing reviewers/hardware/counters remain Pending/PendingHardware. REL01–REL06 remain NotClaimed.

Use the [M11 operator runbook](milestones/m11-playable-release/handoff.md) for the final candidate. Documentation hashes and diagnostic Smoke/READY do not prove gameplay. Commit completed assigned work to main and preserve ignored evidence and old archives; never relabel them as newer source.

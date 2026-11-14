# Playable offline release — current state and next build

Reviewed 2026-11-14 at clean `main`, `59316d387e5345716392e332ca225a581a671c33`. T052–T064 remediation is committed (184 files changed). Contracts lock repair, lifecycle/pause/input, package publication, AI/spawns, views, shell/settings/audio and measurement/delivery tooling now exist. M8–M10 integration and M11 acceptance remain open for the specific gaps in the [current audit](product-state-audit.md), rather than all of those features being absent.

## Required playable product

A source-matched Windows x64 archive launches offline into Main menu. Select Training Compound or Depot Yard, Team Deathmatch, and 5v5 or 6v6. One human joins Team 1, with nine/eleven allied/enemy bots. Teams race to 150 enemy kills or 15 active minutes; equal scores draw. The player can move, aim, fire/reload rifle and pistol, die/respawn, pause, see live/final tables, replay and return to menu. Audio/video/control/accessibility settings persist and affect actual devices.

[Vision](vision.md), [team rules](features/team-deathmatch.md), [maps](features/playable-maps.md) and [shell](features/game-shell.md) retain exact requirements. Native1280×720 Low30 on the reference machine is mandatory for acceptance; 60 FPS remains separate stretch. No remote networking/server, external art, optional T025 or public distribution is added.

## What remains

The new [next playable-build plan](next-playable-build.md) provides bounded T066–T071 cards, ownership, source evidence, dependencies and exits. No new milestone is needed: these complete M8–M11. Original T035–T065 and RUN/TDM/MAP/UI/SET/REL IDs stay intact.

| Order | Work | Reviewable result |
|---|---|---|
| 1 / T066 | Repair 19 missing metadata entries; resolve failing Generation cancellation/lock case; compile/test with locked Unity | Trustworthy provenance/regression and Unity integration baseline |
| 2 / T067 | Remove runtime catalog/package generation; publish actual soldier/weapon/visual closure; implement native nav bake/install; packaged discovery/recovery | Both maps load all required verified bytes independently of repository/cache |
| 3 / T068–T069 | Correct rifle/pistol/action presentation; editable applied bindings/accessibility; finish real UI/device flows | Player sees authority-consistent combat and can configure controls/settings |
| 4 / T071 | Clean source-matched Client/Server builds, archive, extract/manual playtest and rollback | Owner-playable candidate with actual path/hash/launcher/controls and all known issues |
| 5 / T070 + T062 | Fix multi-match soak/counter semantics; validate collector; capture visual defects and resolve authorized scope | Valid product measurement and explicit visual disposition |
| 6 / T065 | Full match/visual/device/reference-performance/soak matrix and final delivery | REL01–REL06 accepted only with actual evidence |

T070 may proceed before T071, but final measurement and acceptance use one matching clean binary/content pair. One integrator owns shared Bootstrap/Contracts/settings/locks/workloads/coordination. This documentation assignment creates no coding lease or dispatch. Contract/generator/nav/schema changes require ADR and migration before consumers.

## First owner playtest versus accepted release

T071 is the earliest concrete local handoff: clean builds/tests/provenance, both maps × both sizes manually exercised, both weapons, combat/respawn/pause/settings and results/replay, at least one complete match per map, independent extraction and actual corruption/rollback recovery. The archive must be delivered with its measured state and known issues. No working current-source archive was established by this review.

P05 remains FAIL under ADR-024; reference performance and named final reviews are pending. A preview discloses these limits and does not close milestones or waive them. T062 tooling permits capture/inventory; a fourth clearance/generator search still needs an explicit owner scope decision. Retain failed artifacts and review new identities if source changes are authorized.

T065 retains the complete accepted-release boundary: eight full matches (both maps × both sizes × score/unshortened timeout), draw fixture and named manual reviews, 20 shell cycles/720p UI, actual display/audio/control behavior, twelve reference native720p Low captures and two ≥30-minute normal-AI soaks, server isolation/provenance/regression and independent archive/rollback. Missing reviewers/hardware/counters stay Pending/PendingHardware.

The [M11 handoff](milestones/m11-playable-release/handoff.md) supplies the operator record and parameterized commands; use it after the blocking code work, not as proof that only evidence collection remains. Package-Release tooling keeps `releaseAcceptance=NotClaimed`. Documentation hashes, pure suite results and diagnostic READY/Smoke never establish a playable product. Commit all completed assigned work into main under the standing repository rule; preserve ignored evidence before temporary-workspace cleanup.

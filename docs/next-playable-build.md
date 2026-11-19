# Next playable build — current state and remaining work

This is the only page that tracks current status: the build, open findings, open tasks and the order of remaining work. Other pages link here and do not repeat status. Exact identities and evidence are in the [M11 handoff](milestones/m11-playable-release/handoff.md). Gates and scenario IDs are in [acceptance](acceptance.md).

## Current build

**Latest installer: 0.3.24 (2026-11-18).**
- Installer: `Artifacts/deliveries/Shooter-Setup-0.3.24.exe`, from clean source `53cce26` ([delivery evidence](milestones/m11-playable-release/handoff.md#delivery-0324)).
- Passed: native packages, IL2CPP Client, smoke, package and installer builders, pure lanes, and Unity EditMode/PlayMode.
- Still Pending: an actual install/upgrade, a playtest of this build, the Low benchmark with the new shadows, and all acceptance.

**Committed since 0.3.24, not yet in an installer (2026-11-19):**
- [ADR-117](decisions.md#adr-117--accepted-single-point-collimator-dot-and-12x-collimator-zoom): single-dot 1.2x collimator.
- [ADR-118](decisions.md#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150): M4A1, Mk 16, M24 SWS and the M870 shotgun.
- [ADR-119](decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks): a 30-rank career and four new perks.
- Pure lanes and Unity EditMode/PlayMode pass ([record](milestones/m11-playable-release/handoff.md#adr-119-30-rank-career--2026-11-19)). Content republish, build and playtest are Pending.

## Product and scope

The deliverable is a source-matched Windows x64 installer with [in-place upgrades](features/windows-installer.md). ZIPs are internal artifacts only.

The installed game launches offline into the main menu. The player picks:
- a map: Old Town, Harbour or Oil Depot ([maps](features/tdm-map-layouts.md));
- Team Deathmatch;
- a team size: 5v5, 8v8, 10v10 or 15v15.

Match rules:
- The human joins Team 1 with 9–29 bots.
- First team to 150 kills wins, or the match ends after 15 active minutes ([rules](features/team-deathmatch.md)).
- Movement, every weapon slot, death/respawn, pause, live and final tables, replay and settings must all work.

Native 1280×720 Low at 30 FPS on the reference machine is an acceptance gate. 60 FPS is a stretch goal.

Networking is parked ([ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence)). See [vision](vision.md).

A delivered installer is a **test candidate**, not proof of playability. Any observed functional blocker means a build cannot be called proven playable.

## Open findings

| ID | Finding | Status |
|---|---|---|
| C02 | Runtime NavMesh load cost and clearance | Bakes are native and deterministic. Cold/warm load cost and clearance on all three maps still need measurement (T067/T070). |
| C04 | No proven gameplay handoff | The formal T071 matrix has not been recorded. |
| C05 | Visual, device and performance acceptance | Named visual review, device reviews, reference captures and soaks are Pending. P05: see the [defect inventory](visual-defect-inventory.md). |

C01 (native NavMesh publication) and C03 (truthful path outcomes) are resolved.

## Open tasks

| Task | Remaining |
|---|---|
| T067 content/navigation | Named route/spawn evidence on all three maps, collected with T071 |
| T068 authority presentation | FIX05 player captures and named visual review |
| T069 preferences | Named UI/control/display/audio review, real Keep/Revert, 20 load/replay cycles |
| T070 measurement | Reference captures and soaks on the final build (PendingHardware). Per-tick managed allocation still fails the zero-allocation gate. |
| T071 owner playtest | The formal matrix on the newest installer |
| T073 installer | Actual installed upgrade, INS01 on a clean machine, INS04 mid-copy interruption, the interactive wizard ([status](features/windows-installer.md#implementation-status)) |
| T062 P05 soldier | Named visual review. P05 visual acceptance stays FAIL until then. |
| T065 final acceptance | REL01–REL06 and FIX01–FIX14 on one stable final build ([sequence](acceptance.md#final-acceptance-sequence-t065)) |
| T093 runtime rig correction | Collision-backed ground for remote soldiers, per-profile support grip, bounded head look-at |
| T094 spatial audio | Not started: Steam Audio spike behind the stereo mixer, with a native-binary allowlist |
| T095 audio placeholders | Replace the bullet flyby, defeat stinger and outdoor ambience ([steps](features/audio.md#follow-up-placeholders)), then an owner listening review |

Every feature since ADR-096 is in the installer. Each still needs owner review in play:

| ADR | Feature |
|---|---|
| ADR-102 / ADR-118 | Named arsenal |
| ADR-105 | Dropped weapons |
| ADR-106 / ADR-116 | Graphics |
| ADR-107 / ADR-115 / ADR-117 | Armory and sights |
| ADR-108 / ADR-113 / ADR-119 | Career |
| ADR-109 | Site-planned maps |
| ADR-110 | Body armor |
| ADR-111 | Knife and grenade |
| ADR-112 / ADR-114 | Hits and bots |

## Order of remaining work

1. **Build and install the next installer.** It includes ADR-117–119. Do a real upgrade over 0.3.24.
2. **T071 formal playtest on that installer.** Follow the [playtest guide](playtest-guide.md):
   - every map at 5v5, 8v8, 10v10 and 15v15;
   - at least one complete normal match per map;
   - results and replay;
   - recovery from missing or corrupt content;
   - a real prior-version upgrade.

   Record the named reviewer, identities, logs and every blocker. Send each defect to its owning task.
3. **Owner review in play** of every feature in the table above:
   - every gun and sight in ADS;
   - pickup and swap;
   - promotions, unlocks and perks from a fresh career;
   - spawn exits, bot routes and upper floors on every map;
   - armor versus headshots;
   - knife and grenade;
   - map colour and shadows at Low and High.
4. **T093, T094 and the T095 placeholders**, when the owner assigns them.
5. **T070, T062 and T065 on the final stable build:**
   - reference captures and soaks on the idle reference laptop;
   - named visual, UI, audio and display reviews;
   - the full match matrix;
   - the remaining INS checks.

   Missing reviewers, hardware or counters stay Pending/PendingHardware.

One integrator owns Bootstrap, Contracts, settings, locks, workloads and `coordination.json`. Keep Unity 6000.3.28f1 / URP 17.3.0, C0-v1 and Game-v1. A boundary change needs an ADR plus migrated fixtures before any consumer uses it.

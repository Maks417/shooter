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
| T195 architecture and pipelines (owner-assigned 2026-11-19) | **Phase 1 (pipeline and disk) landed**: build debug folders removed, hard-linked staging, opt-in ZIP, shared pure/native map payloads, cached Core RP check, wrapper pruning and CI caches ([runbook](bootstrap-runbook.md#content-build-package-and-installer)). **Phase 2 landed** ([ADR-120](decisions.md#adr-120--accepted-parked-networking-behind-a-define-and-build-once-pure-lanes)): `Test-All.ps1` over `Tools/Shooter.Pure.slnx`, Networking behind `SHOOTER_NETWORKING` with its own `Test-Networking.ps1`, `Compile-GameContent -AndBuildClient` and `Deliver.ps1`. **Phase 3 in progress:** landed the read-only session port (`Game.SessionPorts`), per-actor view state, generated kill-feed/report tables, tick phases, token-hashed feature lock with refreeze commands for every lock ([ADR-121](decisions.md#adr-121--accepted-feature-lock-hashes-c-code-tokens-and-every-lock-has-a-refreeze-command)) and mode rules as data ([ADR-122](decisions.md#adr-122--accepted-mode-rules-as-data-and-team-indexed-kills-game-v1-additive)). Also landed: `CombatAiDirector`, `ProductSessionView` and `ProductShellHost` split by concern into partial files, and table-driven client modes. Not pursued: a diagnostics define, because the gallery and rig tools run from the client build. Pending: a client build, delivery and playtest of this source. |
| T196 unified front-end visual system (owner-assigned 2026-11-19, [ADR-123](decisions.md#adr-123--accepted-unified-front-end-visual-system)) | **Landed in source:**<br>• UI tokens with contrast and literal guards<br>• frame language, shared header and key-hint footer<br>• pure Main, Settings and Armory layouts<br>• real-map rooftop vista behind the menus<br>• restyled Setup, Armory, Settings, Loading, Pause, Confirm, Recovery, Results and the HUD<br>• brand emblem app icon (native per size)<br>• generated installer icon and wizard art<br><br>**Pending:**<br>• a client build and installer of this source<br>• 720p/1080p captures at 100%/150%<br>• the menu frame time on the reference laptop (PendingHardware)<br>• named owner review (VD7, REL03, UI02) |
| T232 licensed authored soldiers (owner-assigned 2026-11-27, [ADR-124](decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)) | **Landed in source:**<br>• ADR-124 art track: `Tools/art-sources.json` pins, provenance and player-build guards, code-owned import settings<br>• `AuthoredSoldierBody`: clip locomotion and death on a code-built Playables graph, procedural upper body retargeted onto the humanoid, hands solved onto the grips<br>• generated `SoldierGearKit` (helmet, goggles, plate carrier or chest rig, pouches, backpack, belt, knee pads) on the humanoid bones<br>• deterministic variant pick per actor; release credits<br><br>**Landed with art:** CC0 Quaternius Modular Men Swat body in six variants, Modular Men and Universal Animation Library clips; EditMode 102/102 including authored hit-volume alignment (`Tools/Capture-SoldierSkins.ps1` renders the review sheet).<br><br>**Pending:**<br>• client build, in-game captures and the TDM-5v5 benchmark on the reference laptop<br>• P05/REL03 named owner review |

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

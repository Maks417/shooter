# Shooter implementation rules

Repository: C:\Job\shooter. This is a Unity 6 URP procedural military FPS under UnityProject.

**Read before editing:**
1. docs/next-playable-build.md, the only page that tracks current status, open tasks and the order of remaining work.
2. docs/vision.md and docs/architecture.md.
3. docs/acceptance.md, for gates and scenario IDs.
4. The feature page for your task.

New feature workers also read docs/game-session-contracts.md (Game-v1). Continue only the assigned task. No new acceptance waiver is authorized. History lives in git, not in the docs tree.

**Scope:** offline Team Deathmatch, with one human and local allied and enemy bots in one client process. It ships as a Windows installer that upgrades an existing install in place and keeps settings and controls (ADR-041); ZIPs are internal artifacts. Under ADR-076, networking is parked:
- no new Networking code;
- no Dedicated Server build in CI or the release flow;
- no new `UNITY_SERVER` guards or transport hooks;
- server gates read Parked.

Simulation stays the single authority and Presentation stays read-only.

## Scope and ownership

**Integrator-owned.** One integrator owns Contracts, Bootstrap, project and package settings, schemas, locks, benchmark definitions and coordination.json. Nobody else edits these without an explicit assignment.

**Parallel work.** Parallel agents run only when the owner explicitly requests them. Each gets bounded paths, a worktree and its own Unity project copy. Avoid shared-file races. Use fakes in tests to unblock independent work; shipping composition must reject required fake adapters.

**Frozen contracts.** C0 and Game-v1 are frozen. Changes to units, seeds, hit poses, schemas or contract layout need an ADR plus migrated fixtures before any consumer depends on them.

**Commit hygiene.**
- Commit all completed work into `main` before finishing.
- Integrate every task branch and worktree, resolve conflicts and run the required checks.
- Remove temporary worktrees and branches only after verifying that `main` holds all their changes.
- Keep ignored build, cache and evidence files ignored, and preserve required evidence.

**Fresh checkout.** Use Tools/Configure-Unity.ps1 with the locked editor first. It prepares the hash-pinned Core RP curation (ADR-013). Never edit installed packages, Library payloads or the curated package to bypass provenance checks.

## Hard requirements

**Art:**
- Geometry, materials, rigs and motion come from code or numeric recipes, except the licensed art track (ADR-124).
- Licensed art track: CC0/CC-BY models, textures and clips only, only under `UnityProject/Assets/Shooter/Art/`, each file hash-pinned with author, source URL and license in `Tools/art-sources.json`. Nothing paid or non-redistributable (the repo is public).
- No painted, photo or AI image textures and no sprites outside that track.
- Generated caches require provenance.
- Fonts and the audio bank are named, provenance-recorded exceptions (ADR-066, ADR-062/096).
- No silent external art fallback.

**Performance:** Unity URP, C# and HLSL. Native 720p Low at 30 FPS on the reference machine is the gate, and 60 FPS is a separate stretch. The reference machine is a Ryzen 5 4600H with integrated Radeon, 16 GB DDR4-3200 and NVMe.

**Gameplay:** collision, navigation, hit volumes and weapon behavior are identical across quality tiers, on a fixed 60 Hz simulation. Make no AAA performance or realism claim without actual evidence. Use the exact editor and packages pinned in toolchain-lock.json and build-lock.json, and never upgrade the toolchain during feature work.

## Verification and completion

**Checks:**
- **Docs:** run `python docs/fixtures/verify_fixtures.py` and `python docs/verify_docs.py` for specification changes. Refresh the docs manifest only for intentional edits.
- **Pure lanes:** run Tools/Test-All.ps1, or individually Tools/Test-Contracts.ps1, Test-Generation.ps1, Test-Simulation.ps1 and Test-Game.ps1.
- **Unity, build and benchmark:** run the relevant wrappers described in docs/bootstrap-runbook.md. Each covers only its documented scope.
- **Delivery:** `Compile-GameContent -Native`, then `Package-Release -RequireNativeNav`, then `Build-Installer`.

**Evidence:**
- Builder, smoke and package PASS are not gameplay evidence.
- Manual gameplay, device, reference-hardware and P05 visual review stay Pending until recorded.
- Missing tools, hardware or counters mean Pending, never PASS.
- Do not restart landed work or relabel retained binaries as later source.
- Never edit generated payloads or goldens just to pass tests.
- Record source, toolchain, content, seed and workload with results. Measure reference-machine performance in the release player, not the editor.

**Before finishing:**
- required checks pass;
- no out-of-scope files changed;
- provenance is valid;
- contract versions are correct.

Update the affected docs with real code paths. Record evidence in the milestone handoff, and status only in docs/next-playable-build.md. State actual failures and pending checks. Documentation hashes are not game test results. Do not relax requirements to close a milestone.

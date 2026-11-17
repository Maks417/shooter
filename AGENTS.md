# Shooter implementation rules

Repository: C:\Job\shooter. Implement a Unity 6 URP procedural military FPS under UnityProject. M0–M7 retain historical owner-waived closures (ADR-017, ADR-025–028, ADR-030–032); historical pending evidence remains. M8–M10 integration validation and M11 acceptance remain open. Current build, task disposition and the order of remaining work are in docs/next-playable-build.md (the only page that tracks the current installer); finding status is in docs/product-state-audit.md and visual work follows docs/visual-roadmap.md. Read those and docs/playtest-guide.md, then docs/implementation-start.md, docs/vision.md, docs/architecture.md, docs/contract-details.md and the assigned milestone index before edits. New feature workers also read docs/game-session-contracts.md (Game-v1, ADR-034/036/037). Continue only the assigned task; parallel agents only when the owner explicitly requests them. No new acceptance waiver is authorized. Superseded plans live in docs/archive/ and are not instructions.

Owner clarification 2026-11-14: first version is one human with local allied/enemy bots, running offline in one client process. No online/LAN multiplayer, external server process, server rental/hosting purchase or online service is required; online delivery is deferred. Preserve existing server-compatible architecture and regression/isolation checks without making online deployment a playable-release prerequisite. Owner decision 2026-11-16 (ADR-076): networking is parked — no new Networking code, no Dedicated Server build in CI or the release flow, no new `UNITY_SERVER` guards or transport hooks; server gates read as Parked, not Pending. Simulation stays the single authority and Presentation read-only.

Final-delivery owner requirement 2026-11-14: ship a Windows installer; running a newer installer must update the existing installed game while preserving settings/controls and stable installation identity. Implemented as T073 (ADR-041) and shipped since 0.3.1; see docs/features/windows-installer.md for INS01–INS06 status. ZIPs are internal artifacts.

## Scope and ownership

- One integrator owns Contracts, Bootstrap, project/package settings, schemas, benchmark definitions and coordination.json. No parallel workers edit these without explicit assignment. Use worktrees and one Unity editor/project copy per worker.
- Implement only the assigned milestone/task and permitted folders from docs/backlog.md and docs/task-supplement.md. Use fakes in tests to unblock independent work; shipping composition must reject required fake adapters.
- Freeze compiling C0 contracts/fixtures before parallel feature integration. Changes to units, seeds, hit poses, packet layout or schemas need ADR plus migration/fixtures before dependent merges.
- No automatic delegation is required by this file. If the user requests parallel agents, assign bounded tasks after readiness gate; avoid shared-file races.
- Commit all completed task changes into `main` before finishing. Integrate every task branch/worktree into `main`, resolve conflicts and run the required checks, then remove temporary worktrees and their branches after verifying that all changes are preserved in `main`. Do not leave completed work only in a workspace, on a task branch or uncommitted. Keep ignored build/cache/evidence files ignored and preserve required evidence before workspace removal.
- Use Tools/Configure-Unity.ps1 with the locked editor before opening a fresh checkout. It prepares the hash-pinned Core RP source curation described in ADR-013. Never edit installed packages, Library payloads or the ignored curated package to bypass provenance checks. The M0 scaffold owner also owns Bootstrap/Core, Simulation/Foundation, World/Foundation and Presentation/Rendering/FoundationView.cs until feature owners are assigned.

## Hard requirements

No imported/manually modeled meshes, painted/photo/AI image textures, sprites, authored skeletal clips or mocap. Geometry, materials, rigs and motion come from code/numeric recipes. Generated caches require provenance; engine font is named allowlist exception. No silent external art fallback. Unity URP/C#/HLSL, native720p Low30FPS reference gate; 60FPS stretch separate. Ryzen4600H integrated Radeon,16GB DDR4-3200,NVMe; temporary historical memory utilization is not a requirement.

Gameplay collision/nav/hit volumes/weapon behavior identical across quality tiers. Fixed60Hz simulation, presentation read-only, authoritative dedicated-server-compatible design. No AAA performance/realism claim without actual evidence. Choose exact editor/packages from verified installation and pin them; do not invent versions or upgrade toolchain during feature work.

## Verification and completion

Run docs/fixtures/verify_fixtures.py and docs/verify_docs.py for specification changes. Run Tools/Test-Contracts.ps1, Tools/Test-Generation.ps1 and the relevant Unity/build/benchmark wrappers described in docs/bootstrap-runbook.md; each covers only its documented scope. Compile-GameContent -Native publishes the verified native-navigation map packages; Package-Release -RequireNativeNav and Build-Installer produce the delivery. Builder, smoke and package PASS are not gameplay evidence; manual gameplay, device, reference-hardware and P05 visual review stay Pending until recorded. Do not restart landed work or relabel retained binaries as later source. Missing tools/hardware/counters => pending, never PASS.

Never edit generated payloads/goldens just to pass tests. Record source/toolchain/content/seed/workload with results; reference-machine performance measured in release player, not editor. Update affected shared docs with real code paths and milestone handoff with evidence/limitations. Keep shared docs once at docs root; milestone folders link them. Do not recreate duplicated reference folders.

Before finishing: required checks pass, no out-of-scope files changed, provenance valid, correct contract versions, handoff updated and actual failures/pending checks stated. Documentation hashes are not game test results. Do not relax requirements to close milestone.


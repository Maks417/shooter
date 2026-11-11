# Shooter implementation rules

Repository: C:\Job\shooter. Implement a Unity 6 URP procedural military FPS under UnityProject. M0 now has a locked editor, pure contracts and a client/server foundation harness; continue only the assigned milestone, not the entire game. Read docs/implementation-start.md, docs/vision.md, docs/architecture.md, docs/contract-details.md and your assigned milestone index before edits. Current evidence and pending gates are in docs/milestones/m0-foundation/handoff.md.

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

Run docs/fixtures/verify_fixtures.py and docs/verify_docs.py for specification changes. Run Tools/Test-Contracts.ps1 and relevant Unity/build/benchmark wrappers described in docs/bootstrap-runbook.md; M0 wrappers now exist, while CompileContent explicitly reports unsupported at this milestone. Missing tools/hardware/counters => pending, never PASS.

Never edit generated payloads/goldens just to pass tests. Record source/toolchain/content/seed/workload with results; reference-machine performance measured in release player, not editor. Update affected shared docs with real code paths and milestone handoff with evidence/limitations. Keep shared docs once at docs root; milestone folders link them. Do not recreate duplicated reference folders.

Before finishing: required checks pass, no out-of-scope files changed, provenance valid, correct contract versions, handoff updated and actual failures/pending checks stated. Documentation hashes are not game test results. Do not relax requirements to close milestone.

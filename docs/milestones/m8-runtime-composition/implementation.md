# M8 — Runtime composition and Game-v1 foundation — implementation order

Status: planned. Read [entry](index.md), [Game-v1](../../game-session-contracts.md) and the authoritative feature specs before editing. Task IDs extend T001–T034 without renumbering historical tasks. Start only assigned work; integrate through main in dependency order.

## T035 — Freeze product boundary first

First repair the current source-audit baseline: 51 missing Unity metadata entries plus the unapproved Assets Markdown file identified in [planning validation](../../planning-validation.md). Integrator preserves established GUIDs/references, creates missing metadata through the approved Unity/source workflow and relocates explanatory prose outside Assets where appropriate. Audit assembly boundaries for the later pure-only additions, notably Bootstrap/Release. Regenerate any affected provenance only from its authorized generator; never edit installed packages/Library/cache payloads or broaden the allowlist to hide the failure. Required existing locks and source-provenance audit must pass before runtime acceptance or feature dispatch.

Create the separate Game-v1 contracts/fixtures/lock, session/map/settings ports and task harnesses per the shared contract plan; implement explicit schema/version validation and conformance. Freeze TDM tick/death/respawn semantics, IDs and settings before dispatch. Define new TDM-5v5-v1 and TDM-6v6-v1 workloads now, with counters/inputs; preserve historical B01–B04.

## T036 — Extract and compose real authority

Integrator extracts reusable actor lifecycle from CombatHarnessSimulation into Simulation/Session without changing C0 behavior, preserves the diagnostic adapter, and installs one real session/composition root. Feed local buffered input into ticks, wire movement/weapon/pose/damage and coherent view publications, pause/resume and epoch-safe cancellation. Separate product launch from existing diagnostic command-line routes.

## T037 — Compile and install the compound

World/content worker builds a genuine full compound dependency package and real Unity collision/nav runtime against frozen ports. Validate required content before Ready; follow actual native path corners. Keep diagnostic compilers explicit; add proposed Compile-GameContent and Test-Maps wrappers with bounded failures and provenance.

## T038 — Connect the client path

Client worker supplies actual camera/mouse controls, generated weapon and soldier views, HUD and audio-device output; use generated content and font exception only. Bot state is fed from authority and commands enter the next tick. The existing eight-bot fixture is the M8 composition workload; TDM capacity belongs to M9.

## T039 — Run and review the vertical player

Load the actual compound in an IL2CPP client, move/aim/fire/reload, receive damage and observe bots following paths/acting, hear synthesized effects and restart/unload cleanly. Triage named P05 defects with live captures and bounded source fixes under version/provenance review; acceptance remains open until T049. Retain server isolation and unchanged diagnostic smoke.

## Merge and completion

Each task returns source changes, relevant test results, source/toolchain/content/seed/workload identity and a limitation record. Integrator handles shared files once, reruns affected integration checks, updates this pack's handoff and commits completed changes into main. A future-only path or wrapper in this plan must become real and tested before it counts as a deliverable.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).

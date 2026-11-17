# Proposed repository and toolchain

The layout below is the original target, with existing M0–M7 source described in [architecture](architecture.md) and milestone handoffs. Toolchain/package locks and the Unity project already exist. M8–M11 added Game/Contracts, Game/Session, Game/Settings, Content/Maps, Simulation/Session, Simulation/TeamDeathmatch and product presentation/world adapters; the [current audit](product-state-audit.md) distinguishes these actual paths from still-missing adapters. [Remediation](archive/release-remediation-plan.md) lists permitted implementation areas; proposed new directories are not claims they already exist. Keep pure contracts, authority, Unity world adapters and client-only presentation in separate assemblies. Integrator alone adds assembly/project references and settings.

```text
shooter/
  AGENTS.md                          # concise entry rules pointing at docs
  docs/                              # this specification
  UnityProject/
    Assets/Shooter/
      Contracts/                     # Shooter.Contracts.asmdef
      Generation/Core/               # pure generators
      Generation/Editor/             # compiler, build entry points
      Content/Runtime/               # loading and cache adapters
      Simulation/                    # motor, combat, AI
      World/                         # Unity physics and navigation adapters (Shooter.World.Unity)
      World/Runtime/                 # pure navigation/streaming (Shooter.World.Runtime)
      Presentation/                  # Unity rigs, rendering, input, audio/FX adapters
      Presentation/Model/            # Unity-free view/HUD/menu/audio/FX models (Shooter.Presentation.Model)
      Networking/                    # parked while offline-only (ADR-076)
      Bootstrap/                     # client/server composition
      Shaders/                       # HLSL and URP shader wrappers
      Definitions/                   # reviewed JSON source, no binary art
      Generated/                     # generated Unity native assets + manifests
      Settings/                      # URP assets, quality profiles, input maps
      Tests/EditMode/
      Tests/PlayMode/
      Tests/Performance/
    Packages/manifest.json
    Packages/packages-lock.json
    ProjectSettings/ProjectVersion.txt
  Tools/                             # audit, docs check, cache tooling
  Tools/<Assembly>/                  # one netstandard2.1 project per pure asmdef (verify_asmdef_graph.py)
  Benchmarks/                        # seeds, paths, workload definitions
  Baselines/                         # approved compact metrics and golden hashes
  Artifacts/                         # ignored; newest run per action only, reports/ keeps JSON history (ADR-076)
  .github/workflows/                 # or equivalent CI, no vendor lock required
```

## Version pinning gate M0

Choose one supported Unity 6 LTS patch available in the installed environment, install Windows build and dedicated-server support, then commit exact editor version to ProjectVersion.txt and build-agent configuration. Select the URP major delivered for that editor; pin exact package versions in manifest and lock. Do not invent a patch number in docs or float package dependencies. Record editor revision, URP/Input System/Test Framework/AI Navigation/transport versions, scripting backend and graphics API in `toolchain-lock.json` at repository root. Compiler fails if lock and editor disagree.

Baseline uses conventional GameObjects, Input System, Unity Test Framework and AI Navigation adapters. Burst/Collections/Mathematics require a measured use case and compatible locked versions. Transport is introduced at M5; no networking dependency in Contracts. IL2CPP release client is the performance acceptance build; faster Mono development builds may be used for iteration. Prove a headless server build in M0 before gameplay depends on presentation.

Resolved F01 values are authoritative in the lock: editor 6000.3.28f1 (8f082d2fe476), URP/Core 17.3.0, Input System 1.20.1, Test Framework 1.6.0 and AI Navigation 2.0.0. Dedicated server currently uses Mono; the release client uses IL2CPP/D3D11. Burst/Collections/Mathematics enter only as locked URP dependencies, not a game optimization decision. A reproducible local Core RP source curation removes unused debug UI Resources; prepare it through `Tools/Configure-Unity.ps1`, as described in [ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation). Never replace it with a stock-package fallback.

## Source and generated data

Commit text source, definitions, Unity `.meta` files, assembly definitions and small golden fixtures. Use force-text serialization and visible meta files. Do not commit Library, Temp, Logs, obj, user settings, local caches or full benchmark captures. Large generated caches belong in versioned CI artifacts with hash manifest, not manual edits; generated assets needed by a build are restored or compiled into `Assets/Shooter/Generated` with stable GUID derivation from LogicalAssetId (recipe/artifact/variant), separate from changing ContentId. Never regenerate GUIDs simply because payload changes. Cold regeneration must work without a previous cache.

Add `.gitattributes` for LF text and binary detection. A source provenance allowlist names engine/package assets (including UI font), fixtures and generated outputs. Packages must have compatible licenses and no visual asset dependencies. Use `.asmdef` references by GUID; only module owners modify their assembly files. CI owns package lock and project settings changes via integrator review.


## Bootstrap and logical identity

Follow [bootstrap runbook](bootstrap-runbook.md) for actual initialization and [deterministic identity rules](deterministic-format.md) for GUID derivation and artifact hashing. Root AGENTS.md, the exact toolchain lock and M0 wrappers now exist. Future content-cache identity remains subject to the compiler milestone; current settings provenance describes only the foundation generator.

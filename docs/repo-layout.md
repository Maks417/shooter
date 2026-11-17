# Repository layout and toolchain

The layout below is the actual repository as of 2026-11-17. Keep pure contracts, authority, Unity world adapters and client-only presentation in separate assemblies ([architecture](architecture.md)). The integrator alone adds assembly/project references and settings.

```text
shooter/
  AGENTS.md, CLAUDE.md               # binding agent rules and orientation
  docs/                              # specification and status record; docs/archive/ holds superseded pages
  Content/                           # reviewed source data, no binary art: Maps/, Weapons/, Audio/, Fonts/
  UnityProject/
    Assets/Shooter/
      Contracts/                     # Shooter.Contracts (C0-v1, frozen)
      Game/Contracts|Session|Settings/ # Game-v1 (frozen), offline TDM session, user settings
      Generation/Core/               # pure generators, codecs; Core/Math = Shooter.VectorMath
      Generation/Editor/             # Unity asset compiler (Shooter.AssetCompiler.Editor)
      Content/Runtime|Maps/          # cache loading; map catalog/registry/packages
      Simulation/                    # motor, combat, pose, AI, match, session, TDM
      World/                         # Unity physics and navigation adapters (Shooter.World.Unity)
      World/Runtime/                 # pure navigation/streaming (Shooter.World.Runtime)
      Presentation/                  # Unity rigs, rendering, input, audio/FX adapters
      Presentation/Model/            # Unity-free view/HUD/menu/audio/FX models (Shooter.Presentation.Model)
      Networking/                    # parked while offline-only (ADR-076)
      Bootstrap/Core|Runtime|Release|Client|Editor/ # composition roots and ShooterBuild.* editor entry points
      Shaders/                       # HLSL and URP shader wrappers
      Settings/                      # URP assets, quality profiles, generated provenance
      Tests/EditMode/, Tests/PlayMode/
    Packages/manifest.json, packages-lock.json
    ProjectSettings/ProjectVersion.txt
  Tools/                             # PowerShell wrappers, Python verifiers, installer/
  Tools/<Assembly>/                  # one netstandard2.1 project per pure asmdef (verify_asmdef_graph.py)
  Tools/*.Tests/                     # xUnit projects referencing the Tools/<Assembly> projects
  Benchmarks/                        # frozen benchmark definitions
  *-lock.json, global.json           # toolchain, contract, content-format, feature-content, game-contract and build locks
  coordination.json                  # integrator-owned leases and milestone records
  Artifacts/                         # ignored; newest run per action only, reports/ keeps JSON history (ADR-076)
  .github/workflows/                 # fast pure lane; manual Unity lane
```

## Version pinning gate M0

Choose one supported Unity 6 LTS patch available in the installed environment, install Windows build support (dedicated-server support only for the parked server build), then commit exact editor version to ProjectVersion.txt and build-agent configuration. Select the URP major delivered for that editor; pin exact package versions in manifest and lock. Do not invent a patch number in docs or float package dependencies. Record editor revision, URP/Input System/Test Framework/AI Navigation/transport versions, scripting backend and graphics API in `toolchain-lock.json` at repository root. Compiler fails if lock and editor disagree.

Baseline uses conventional GameObjects, Input System, Unity Test Framework and AI Navigation adapters. Burst/Collections/Mathematics require a measured use case and compatible locked versions. No networking dependency in Contracts; transport selection is parked with networking (ADR-076). IL2CPP release client is the performance acceptance build; faster Mono development builds may be used for iteration. M0 proved a headless server build; it is now built only on request.

Resolved F01 values are authoritative in the lock: editor 6000.3.28f1 (8f082d2fe476), URP/Core 17.3.0, Input System 1.20.1, Test Framework 1.6.0 and AI Navigation 2.0.0. The parked dedicated server uses Mono; the release client uses IL2CPP/D3D11. Burst/Collections/Mathematics enter only as locked URP dependencies, not a game optimization decision. A reproducible local Core RP source curation removes unused debug UI Resources; prepare it through `Tools/Configure-Unity.ps1`, as described in [ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation). Never replace it with a stock-package fallback.

## Source and generated data

Commit text source, definitions, Unity `.meta` files, assembly definitions and small golden fixtures. Use force-text serialization and visible meta files. Do not commit Library, Temp, Logs, obj, user settings, local caches or full benchmark captures. Large generated caches belong in versioned CI artifacts with hash manifest, not manual edits; generated assets needed by a build are compiled into packaged content with stable GUID derivation from LogicalAssetId (recipe/artifact/variant), separate from changing ContentId. Never regenerate GUIDs simply because payload changes. Cold regeneration must work without a previous cache.

Add `.gitattributes` for LF text and binary detection. A source provenance allowlist names engine/package assets (including UI font), fixtures and generated outputs. Packages must have compatible licenses and no visual asset dependencies. Use `.asmdef` references by GUID; only module owners modify their assembly files. CI owns package lock and project settings changes via integrator review.


## Bootstrap and logical identity

Follow [bootstrap runbook](bootstrap-runbook.md) for actual initialization and [deterministic identity rules](deterministic-format.md) for GUID derivation and artifact hashing. Root AGENTS.md, the exact toolchain lock and M0 wrappers now exist. Future content-cache identity remains subject to the compiler milestone; current settings provenance describes only the foundation generator.

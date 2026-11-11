# Proposed repository and toolchain

These are paths to create during implementation; `docs/` and root AGENTS.md exist at specification revision 1.1; Unity implementation is not yet created. See [architecture](architecture.md) for allowed assembly dependencies.

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
      World/                         # physics and navigation adapters
      Presentation/                  # rigs, rendering, UI, audio
      Networking/                    # codec and transport adapters
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
  Benchmarks/                        # seeds, paths, workload definitions
  Baselines/                         # approved compact metrics and golden hashes
  Artifacts/                         # ignored local reports and builds
  .github/workflows/                 # or equivalent CI, no vendor lock required
```

## Version pinning gate M0

Choose one supported Unity 6 LTS patch available in the installed environment, install Windows build and dedicated-server support, then commit exact editor version to ProjectVersion.txt and build-agent configuration. Select the URP major delivered for that editor; pin exact package versions in manifest and lock. Do not invent a patch number in docs or float package dependencies. Record editor revision, URP/Input System/Test Framework/AI Navigation/transport versions, scripting backend and graphics API in `toolchain-lock.json` at repository root. Compiler fails if lock and editor disagree.

Baseline uses conventional GameObjects, Input System, Unity Test Framework and AI Navigation adapters. Burst/Collections/Mathematics require a measured use case and compatible locked versions. Transport is introduced at M5; no networking dependency in Contracts. IL2CPP release client is the performance acceptance build; faster Mono development builds may be used for iteration. Prove a headless server build in M0 before gameplay depends on presentation.

## Source and generated data

Commit text source, definitions, Unity `.meta` files, assembly definitions and small golden fixtures. Use force-text serialization and visible meta files. Do not commit Library, Temp, Logs, obj, user settings, local caches or full benchmark captures. Large generated caches belong in versioned CI artifacts with hash manifest, not manual edits; generated assets needed by a build are restored or compiled into `Assets/Shooter/Generated` with stable GUID derivation from LogicalAssetId (recipe/artifact/variant), separate from changing ContentId. Never regenerate GUIDs simply because payload changes. Cold regeneration must work without a previous cache.

Add `.gitattributes` for LF text and binary detection. A source provenance allowlist names engine/package assets (including UI font), fixtures and generated outputs. Packages must have compatible licenses and no visual asset dependencies. Use `.asmdef` references by GUID; only module owners modify their assembly files. CI owns package lock and project settings changes via integrator review.


## Bootstrap and logical identity

Follow [bootstrap runbook](bootstrap-runbook.md) for actual initialization and [deterministic identity rules](deterministic-format.md) for GUID derivation and artifact hashing. Root AGENTS.md is now supplied; wrappers and exact toolchain lock must still be implemented at M0.

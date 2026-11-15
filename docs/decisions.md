# Architecture decision records

Statuses: Accepted = architectural requirement for implementation; Provisional = build as specified until evidence gate resolves; Deferred = excluded pending separate approval/design. Changes append a dated superseding record, preserving prior rationale. Decisions date from 2026-11-11 unless a record states otherwise; acceptance of a decision does not certify runtime or hardware gates.

## ADR-001 — Accepted: Unity 6 URP rasterization

Context: realistic procedural art must run on integrated Radeon. Decision: Unity 6 URP, C#, HLSL; conventional meshes and generated PBR caches, Forward baseline D3D11. Reject HDRP baseline and full-scene ray marching for compatibility/cost. Consequence: constrain lighting/overdraw and measure from M0. Revisit renderer only on measured evidence without abandoning iGPU gate. [Architecture](architecture.md), [performance](performance.md).

## ADR-002 — Accepted: zero authored visual source assets

Code/numeric recipes are source of meshes, textures, rigs and movement. Generated caches allowed with provenance, engine font exception explicit. Imported models/photos/painted textures/sprites/mocap/hand-keyed joint clips disallowed. Consequence: soldier feasibility is a milestone gate, not an outsourcing task. [Vision](vision.md).

## ADR-003 — Accepted: compiler/runtime separation

Expensive geometry/material/LOD/nav/weight generation runs in tools; runtime loads immutable versioned artifacts and runs cheap motion. Alternative generate everything on demand rejected for CPU/GPU contention and unpredictable hitches. Consequence: cache schema, migrations and cold-build CI are first-class. [Generation](generation.md).

## ADR-004 — Accepted: gameplay independent of visual quality

Canonical collision/nav/hit profiles and weapon definitions identical across profiles. No render-mesh hit detection or AI visibility derived from absent low-tier foliage. Consequence: maintain separate gameplay artifact hash and fixed gameplay pose evaluator. [Contracts](contracts.md).

## ADR-005 — Accepted: 60 Hz authoritative simulation

30 FPS display baseline with 60 Hz simulation; command/snapshot architecture from M0, offline first. Reject variable-delta combat and physics lockstep. Consequence: interpolation, replay and bounded catch-up needed; simulation determinism limited to tested envelope. [Combat](features/combat.md).

## ADR-006 — Provisional: GameObjects and kinematic motor

Begin with managed modular simulation and engine adapters, not full ECS. Kinematic motor supports explicit replay; rigidbody movement rejected as first prediction target. Revisit after M0 motor spike/B03 CPU measurements; converting to DOTS is a scoped migration, not a parallel-agent local choice.

## ADR-007 — Provisional: generated humanoid plus analytical IK

Parametric ring meshes, distance-based weights and gait/IK motion; no imported fallback. Reject full muscle/cloth simulation and learned motion datasets for slice complexity/provenance. M1 exits with reviewed poses/perf; failure reduces visual complexity through a recorded scope change. [Soldiers](features/soldiers-animation.md).

## ADR-008 — Provisional: transport behind pure codec port

Unity Transport candidate, evaluate NGO adapter at M5; custom FPS prediction not assumed free from middleware. NFE requires explicit ECS choice. Consequence: codec/authority work stays framework independent; choose exact package only after pinned-version spike. [Networking](features/networking.md).

## ADR-009 — Accepted: reproducible source, not universal deterministic physics

PCG32 named streams, canonical encoding, semantic versions, content addressing. Reject cross-platform lockstep promise for PhysX/nav/GPU. Server supplies authoritative state and gameplay hashes. Consequence: golden topology tests plus tolerance tests and version handshake. [Generation](generation.md).

## ADR-010 — Provisional: bounded compound and offline-first scope

256 m map, eight bots, rifle/pistol, static cover before four-player network slice. Reject open world/destruction/vehicles at foundation. Entire gameplay collision/nav loads initially; visuals stream. Consequence: later capacity and gameplay streaming need new evidence, not hidden expansion.

## ADR-011 — Provisional: exact toolchain resolved at M0

Use available supported Unity 6 LTS patch with compatible URP, pin all packages before implementation contracts freeze. No invented patch/version. M0 owner records lock and proves client/headless builds; [references](references.md) are guidance to check against that lock, not package compatibility guarantees.

## Decision change template

ID/title; date; owner; status; requirement affected; measured problem; alternatives; chosen behavior; contract/schema impact; migration; affected work packages; validation and rollback; supersedes ID. Integrator approves compatibility changes; an agent cannot relax art or performance requirements by silently updating a baseline.


## ADR-012 — Accepted: review closure defaults (revision 1.1)

Separate LogicalAssetId/Unity GUID from versioned request ContentId/payload hashes; exclude build telemetry from deterministic manifests. Explicit result/lease types replace ambiguous integer query returns and direct snapshot reads. Post-movement hit poses, batched same-tick shot intents and delayed AI commands define tick order. Compound Clear/no-respawn cooperative slice and collision matrix are fixed in [gameplay baseline](gameplay-baseline.md). Analytic death collapse is required; physics ragdoll optional. [Implementation start](implementation-start.md) links exact specifications and fixtures. These close ambiguity without claiming implementation or empirical feasibility.

## ADR-013 — Accepted: initial M0 toolchain and explicit engine resource curation

Date 2026-11-11; owner single integrator/WP-00. Installed and verified editor 6000.3.28f1, revision 8f082d2fe476; URP/Core RP 17.3.0; Input System 1.20.1; Test Framework 1.6.0; AI Navigation 2.0.0. Exact portable values and dependency graph live in root `toolchain-lock.json` and `UnityProject/Packages`. This resolves ADR-011's selection; actual build/hardware gates remain separately recorded. The bundled URP template's Input System 1.12.0 failed on removed `BuildTarget.ReservedCFE`; the documented 6000.3 release package 1.20.1 compiled. This was initial dependency resolution before freeze, not an upgrade during gameplay work.

A first release build demonstrated that Core RP's `Resources/DebugUICanvas.prefab` and `DebugUIPersistentCanvas.prefab` pull authored debug UI sprites into players. This conflicts with the art policy and server payload boundary; [Unity's issue record](https://issuetracker.unity.com/issues/2697) describes the same inclusion. `Tools/prepare_unity_packages.py` packages the exact installed Core RP source into ignored `.local/engine-packages/com.unity.render-pipelines.core-17.3.0-shooter.tgz`, omitting those two prefabs and their metadata. It verifies upstream/curated file inventories and archive SHA256 against the lock. It never edits installed packages or the Library cache. The UPM manifest references this reproducible local tarball, retaining version 17.3.0; no source API or shader is modified. Runtime debugging UI is explicitly disabled. Fresh setup must prepare this source before package resolution; an unexpected existing archive fails rather than being overwritten.

Clean-checkout import of commit `e94ca64` proved that the original folder dependency was mutable: Unity upgraded texture importer metadata and the next strict inventory check correctly failed. Curation schema 2 therefore uses an immutable [local UPM tarball](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/dependencies-lp/upm-localpath), with canonical tar ordering/ownership/modes/timestamps and Gzip stored DEFLATE blocks independent of compressor versions. Upstream inventory `6dfa8cac7cd5145c23024aba1377cb17c2029edbb397bdc4d28cdc29c21daaa5` and curated inventory `78b5c9080d73ed97ee71e2dfa4d0bc2ee847834074a5aca31e18b57c3c926de2` are unchanged. Archive SHA256 is `ac919cdfbb561e93be0fc4b0a3c65ee998fd382d9933d1046785dba70ae74c8e`. The explicit `--migrate-immutable-archive` operation requires exact schema-1 input equality; ordinary verification never blesses mutated files. Independent gzip/tar decoding and host-path/timestamp invariance tests cover the archive format. The editor rejects mutable/stock Core sources; cached UPM extraction is engine-owned and is not hand-edited.

URP's image-backed dithering slots are assigned code-generated recursive Bayer textures (8 and 64 pixels) through its public Editor resource API. The unused debug-font slot receives a persistent one-pixel zero-data asset generated by code: the engine's transient black texture serialized as null and was reloaded as the stock atlas, so it was insufficient. The 64-pixel ordered lookup is explicitly not a blue-noise-quality claim; M0 uses no LOD crossfade or post effects. Both splash and Unity-logo flags are disabled: disabling splash alone left a built-in logo texture/sprite in packed reports. Tools wrappers reject these extensionless built-in entries; final packed-source inspection confirms their removal. No painted/photo/AI texture or sprite exception is introduced. Settings/scene/material/lookup outputs have source/toolchain hashes in `Assets/Shooter/Settings/provenance.json`; native URP shader-prefilter cache fields generated during a real build are recorded after that stage. Native Unity YAML trailing spaces on empty scalars are retained through Git whitespace attributes, not removed from generated payloads. No golden payload is edited to match an old hash.

Migration: no C0 DTO, unit, seed stream, gameplay pose or wire schema changes; no accepted shipping content exists. Recreate only the ignored local curation from its pinned inputs, resolve packages, regenerate numeric settings, then rerun pure/Unity tests, player art/dependency audits and client/server smoke. Rollback is the preceding source/lock pair; do not retain a player whose package/manifest differs. Future curation changes require a new inventory and decision review. [Runbook](bootstrap-runbook.md) and [M0 evidence](milestones/m0-foundation/handoff.md) contain commands and acceptance status.

## ADR-014 — Accepted: initial C0 source freeze and failure capture lifecycle

Date 2026-11-12; owner/reviewer Codex as single M0 integrator. Review covered the primitive/enum registry, immutable DTOs, input/prediction/event boundaries, typed geometry data, bounded buffers, leases, explicit errors and main-thread ports against [contract details](contract-details.md). Pure fixture/conformance tests and actual Unity adapters provide the evidence linked in the [handoff](milestones/m0-foundation/handoff.md). No independent reviewer or hardware sign-off is implied.

Freeze the existing candidate as C0-v1. Root `contract-lock.json` pins 24 contract source/assembly/metadata files, 18 JSON/binary fixture files and the unchanged toolchain. `Tools/verify_contract_lock.py` verifies the exact inventory before the pure and Unity wrappers; CI also tests rejected source/fixture drift. Test-only recipe-validator and generator doubles complete port coverage without implementing the M1 compiler. Future updates require an intentional integrator review, ADR/migration where semantics change, regenerated identities and affected tests; the verifier has no automatic refresh command.

Migration: the unused candidate-label constant becomes `ContractVersion.Current="C0-v1"`; there are no dependent feature consumers to migrate. DTO fields, units, seeds, enum values, prediction schema 1 and specification-v1.1 payloads are unchanged. Production geometry codecs, content compilation and the network wire schema remain their original milestone assignments. The contract freeze does not authorize M1 or set `readyForParallelIntegration=true` while M0 acceptance is pending.

A retained player failure showed that offline overload was correctly detected but native shutdown then stalled. The four-tick cap and debt-retaining offline pause remain unchanged. `FoundationEntry.Fail` now notifies cleanup observers once before requesting failure exit. `FoundationBenchmark` stops sampling, disposes its native recorders before native shutdown and writes a separate `failure.json` with elapsed/debt/tick telemetry. It never turns a partial capture into successful benchmark evidence. `Tools/Test-FailureShutdown.ps1` injects a 200 ms stall only in an explicitly diagnostic capture and requires actual exit code 1 within 30 seconds, retained debt after four ticks and no successful capture files. Passing this diagnostic does not prove the historical hitch/driver cause. Rollback uses the prior source/build pair and retains the failed attempt.

## ADR-015 — Accepted: calibrated release measurement and immutable evidence review

Date 2026-11-12; owner/reviewer Codex, single M0 integrator. The installed, locked IL2CPP source implements `GetAllocatedBytesForCurrentThread` as an unsupported zero-returning stub (`icalls/mscorlib/System/GC.cpp`). Historical IL2CPP zero-byte tick observations therefore cannot establish zero allocation. Preserve their raw reports, supersede that interpretation and require positive controls. Pure .NET allocation tests remain valid on their tested runtime. Heap-size differences and unavailable release profiler recorders were rejected as allocation substitutes.

`Bootstrap/Runtime/AllocationMeter.cs` registers one process-lifetime allocation callback through the pinned GameAssembly IL2CPP profiler exports, without changing engine/package files or enabling a development/deep-profile player. The callback sums reported object sizes on all allocating threads and separately in thread-local tick counters. Rooted object/string/array controls on main and worker threads establish operation before warmup; a fresh byte-array control after sampling checks continued operation. Failure yields unavailable counters, never calibrated zeros. The process-lifetime native registration has no uninstall API; a static rooted callback and one-time initialization bound its lifetime. This engine-specific adapter requires revalidation with any future editor migration. `Tools/Test-Measurement.ps1` also injects known per-frame allocation in a separate release diagnostic and requires detection.

`Bootstrap/Core/FrameTimingLedger.cs` joins delayed native timings by their QPC frame-start timestamp to bounded, monotonic Update brackets. Native/Stopwatch clock frequencies and epoch range must agree; ambiguous, zero and missing native durations remain unavailable. The client polls 32 timing-history records and drains 64 frames outside the measured interval. No timing from a different frame fills a gap. Tests exercise delayed/out-of-order observations, ambiguity and bounds. The exact supported recorder names are CPU Main Thread Frame Time and CPU Render Thread Frame Time. Raw direct recorder values/counts are retained for audit; their documented four-frame delay is checked against native timing values. They do not replace unavailable ledger samples.

The reference driver still omits some native GPU durations. The supported external alternative is standalone [Intel PresentMon](https://github.com/GameTechDev/PresentMon), pinned at 2.6.0 by executable and MIT-license hashes in `Tools/presentmon-lock.json`. It is an ignored measurement tool, not game content, an installed service or a Unity toolchain change. `Benchmark.ps1 -PresentMon` records its own uniquely named ETW session, exact Shooter PID/executable hash, tool identity, logs and raw CSV. `Tools/external_gpu.py` verifies all source hashes and QPC identity, restricts presents to the measured interval, requires the exact measured frame count, one DXGI swapchain and finite GPU duration for every present. The player validates application VSync off and uncapped presentation before the workload and at every measured frame boundary, recording the checked-frame count. Lost events, wrong processes or invalid identities fail validation; missing durations/counts remain pending. MsGPUTime is an independent per-present GPU-duration series, not row-wise imputation into missing native timings. Asynchronous render pacing can place zero or two presents in individual CPU intervals; no false one-to-one alignment is asserted. PresentMon's documented driver/HAGS accuracy limitations apply; system settings are not silently changed. PresentMon 2.6.0 [source](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/PresentData/PresentMonTraceConsumer.cpp) overwrites its CSV SyncInterval with kernel flip intervals. Analysis preserves that histogram; nonzero driver intervals require independent per-frame application configuration proof. They are not treated as evidence that application VSync was enabled. All affected frames remain in the measured distributions. A rejected earlier capture lacking that independent proof remains retained.

`Tools/hardware_evidence.py` validates explicit dated reviewer rationale, exact machine subject and hash-proven evidence under ignored Artifacts. RAM review uses the preserved CPU-Z screenshot; thermal acceptance binds a sensor CSV and timezone to the exact capture hash. Sensor bracketing and no sampled thermal flags are necessary validation, not an automatic stabilization threshold. Raw hardware/capture/result files stay immutable. `Review-Benchmark.ps1` writes a fresh derived result and application proofs. Known budget failures outrank missing conditions.

Migration: collector metadata version 2 adds calibration and clock evidence; the benchmark result schema, B01 workload, C0-v1 contracts, seeds, gameplay behavior and toolchain lock are unchanged. Legacy native allocation counters are now treated as unavailable by analysis. Rebuild both players, run pure/Unity suites, smoke, allocation/failure diagnostics and the full three-run release protocol; record actual results in the [handoff](milestones/m0-foundation/handoff.md). Rollback requires the preceding source/build pair and restores pending measurement status, never the unsupported zero-byte claim.

## ADR-016 — Accepted: conservative render-budget proof and continuous thermal conditioning

Date 2026-11-12; owner/reviewer single M0 integrator, Codex. The native active-render counter occasionally reports zero even with a counted recorder sample. Its validity remains unresolved. Replacing those values with zeros, adjacent frames or percentiles from incomplete samples is rejected. Engine/package edits, toolchain upgrades and relaxed budgets are also rejected.

Unity documents [FrameTiming timestamps](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/frametiming): frameStartTimestamp begins the CPU frame; cpuTimePresentCalled ends submission for that same frame. Render work for that frame occurs inside this enclosing interval. Therefore, by containment, renderWork(i) ≤ enclosingInterval(i) for each frame; nearest-rank p95 obeys the same inequality. This is an inference from the documented lifecycle, not an assertion that the interval equals active render work. The interval additionally includes main-thread work and queue waits, so it can reject a workload whose exact active-render cost would pass.

Collector version 3 preserves both native endpoints from the exact timestamp-keyed FrameTiming record. `Tools/render_budget.py` requires verified native/QPC clock identity, complete positive endpoints, monotonic unique frames/presents, valid bracket/submit ordering and positive native work controls. Every available positive native render duration must fit the proposed interval; a violation rejects the proof. Two clock ticks conservatively cover endpoint quantization. Missing/ambiguous endpoints remain pending. No missing active-work sample is filled. `render-budget-proof.json` records method, hashes, containment controls and unresolved native-work counts.

For version 3 captures, result metrics.renderMs reports p95 of the complete measured enclosing intervals as an explicitly conservative upper bound. Its existing ≤8 ms limit is unchanged: a larger enclosing interval must itself pass, giving a stronger budget proof rather than accepting an unavailable active-work counter. Raw frames.csv renderMs still contains the original native values/unavailability. Version 2 and historical reports retain their original interpretation and pending status. The result JSON shape/version, workload ID, seed, geometry, gameplay and C0-v1 are unchanged. Exact-work and enclosing-interval values must not be compared as identical measurements; method/source proofs accompany results.

Earlier manual launch/review gaps allowed cooling between runs, followed by renewed warming during capture. `Benchmark-Series.ps1` runs three prescribed attempts automatically without retries or manual gaps. A bounded same-player preconditioning phase precedes the first prescribed 60-second warmup; every capture still measures 180 seconds and 10800 consecutive ticks. Collector metadata records preconditioning and the actual capture-start tick. Extra conditioning is outside the measured window, without changing resolution, quality, actor load, simulation or the uncapped setting. Thermal acceptance remains a dated review of the actual precondition/capture curves and flags; elapsed time alone never passes it.

Validation: pure endpoint identity/corruption and Python containment/rejection tests; actual release diagnostic followed by attempted reference collection with concurrent sensor logging; native allocation controls, Unity suites, both builds and smoke. The diagnostic verifies 4160 enclosing intervals against 4160 positive native render durations. The full three-run proofs and thermal review did not complete; ADR-017 records the owner's M0 closure waiver without declaring those checks passed. Rollback restores collector version 2 and its explicit pending gates; historical failures remain preserved.

## ADR-017 — Accepted: M0 owner waiver of remaining hardware validation

Date 2026-11-12; accepted by Max as project owner, recorded by Codex as sole M0 integrator. After repeated pre-measurement native overload failures, Max instructed: “skip this, consider everything is correct because I did not have any perf glitches during my ongoing work on this PC”. This latest instruction explicitly supersedes waiting for further monitored hardware validation before closing M0. The owner's desktop experience is acceptance rationale, not measured evidence.

Close M0 administratively as **CLOSED WITH USER WAIVER**. One of eight milestones is closed. The exception covers the outstanding full render-budget/thermal stabilization acceptance and further monitored reruns. Native render-zero validity, thermal stabilization and the causes of 93.0863/104.0256 ms preconditioning pauses remain unresolved. Historical failed/pending results and raw data are immutable; no missing value is replaced, no automatic gate returns PASS because of this waiver, and no performance threshold changes. Hosted CI remains separately deferred by the earlier user instruction.

Clean-entry client/server builds, smoke, frozen C0-v1, 62 pure tests, 11 Unity tests, calibrated release allocation controls and bounded failure shutdown supply actual foundation evidence. The contract/build readiness checkpoint is enabled. Future milestone work still needs an explicit assignment; no M1, combat, thermal stability, 30-minute soak or fully verified reference-performance claim follows. Future performance gates retain their requirements. The [handoff](milestones/m0-foundation/handoff.md) identifies exact source/artifacts, failures and owner disposition. A local `m0-foundation-owner-accepted` marker identifies this qualified closure; it is not a passing benchmark tag. Resuming technical acceptance requires actual supported full captures and thermal review, preserving these prior reports.

## ADR-018 — Accepted: initial P02 compiler format and byte-cache boundary

Date: 2026-11-12. Owner/reviewer: Codex integrator, within the owner's assigned M1 scope. This accepts initial compiler formats and library seams, not M1 completion or reference-machine performance. Frozen C0-v1/specification-v1.1 bytes and toolchain remain unchanged.

P01 had no shipping disk geometry or cache schema. P02 introduces SGAF v1 envelopes, five bounded field-by-field typed bodies, shooter.content.manifest.v1 and shooter.recipe.box-collision.v1. The normative [format](content-format.md), separate [manifest schema](content-schemas/manifest.schema.json) and [recipe schema](content-schemas/box-collision.schema.json) have independent numeric diagnostic golden vectors and negative checks. Root `content-format-lock.json` pins the initial sources/schemas/test-vector generator and toolchain; `Tools/verify_content_format.py` rejects unreviewed drift before generation tests or content compilation. No pre-existing production cache is migrated; C0's nonshipping three-byte fixture is never treated as SGAF. Future incompatible changes require version/migration/fixtures before dependent integration.

`Shooter.Content.Runtime` has an explicit additive dependency on the BCL-only `Shooter.Generation.Core` formatting/identity helpers. This avoids maintaining a second manifest/codec implementation. Runtime does not reference the Editor compiler assembly, invoke generators or construct Unity resources. The Editor-only `Shooter.AssetCompiler.Editor` owns orchestration/publication and depends on the verified runtime reader. This refines the original architecture table's narrower runtime dependency allowance without changing simulation/presentation authority or Contracts.

Publication uses per-key OS-exclusive file handles, durable staging files, bounded rechecks and same-volume atomic rename; the successful set index switches atomically afterward. Persistent lock files are not process ownership and require no unsafe stale-file deletion. Existing corrupt/same-key different-byte caches fail and remain preserved. Cooperative local reparse/path checks do not promise adversarial race-proof filesystem isolation. Runtime leases contain complete verified SGAF files, bounded/coalesced byte residency and independent consumer cancellation; kind-only ambiguity returns InvalidInput, with an additive concrete variant API. No C0 port signature changes.

Only the real CPU collision-box diagnostic is composed by the new Editor entry. Geometry/material/rig generators and gameplay content installation remain future work. Cache eviction/crash-orphan staging reclamation, transient peak-memory/reference timing and full-slice compile budgets remain explicit later gates. No automatic art fallback, package upgrade, shader/settings policy change or M0 waiver extension is introduced.

## ADR-019 — Accepted: additive M1 feature recipes and reproducible source-byte lock

Date: 2026-11-12. Integrator ownership continues under the owner's P03/P04 assignment. C0-v1, SGAF v1, canonical units, root seeds and existing fixture payloads remain unchanged. New feature code lives outside the frozen P02 codec/recipe implementation folders. `ValidatedRecipe` validates through an explicitly composed schema validator and checks the whole canonical source against its document fields/parameters before hashing. The compiler's existing box constructor remains compatible; an additive validated-source overload permits registered weapon/material generators and explicit material layer IDs. Shipping/editor composition names real adapters directly; arbitrary runtime types or test defaults are not introduced.

The [weapon baseline](weapon-recipe-baseline.md) assigns initial numeric proportions and semantic sockets outside C0; four render LODs cannot alter collision or sockets. Weapon and material source schemas/generator versions are registered independently of the P02 disk format. The P04 frozen mask A channel remains255; the earlier feature aspiration for optional height does not reinterpret that byte. This wave implements a separate M1-content-gallery-v1 diagnostic; it cannot be substituted for B01-materials-v1 acceptance. Full workload/thermal/native-720p evidence remains a separate gate.

A clean worker checkout exposed eight initial P02 lock hashes calculated from CRLF working files before Git normalized them to repository LF. The affected entries were text metadata, two schemas, the format document, ArtifactBounds.cs and box-collision.json. The integrator verified each LF-normalized file byte-for-byte against HEAD blobs, normalized the main working copies and corrected only those source-lock hashes to committed LF bytes. No executable token, codec field, source parameter, canonical recipe, golden payload or generated artifact changed. `content-format-lock.json` records this source-byte policy. This is an initial provenance correction, not an artifact migration or silent golden acceptance. Fresh worker verification must pass before feature integration continues.

## ADR-020 — Accepted: versioned first visual corrections for weapon and metal generators

Date: 2026-11-12. The first actual P03/P04 gallery renders exposed a disconnected rifle magazine and excessive broad high-contrast painted-metal wear. The initial numeric/codec tests had passed, so those tests alone did not establish visual acceptance. Retain version1.0.0 caches, source commits and images under the first gallery run. This is a source correction within M1, not permission to hand-edit a mesh, image or cache.

Bump geometry.weapon and material.surface to1.0.1 before publishing corrected output. The rifle magazine/trigger components must connect through numerical construction; independent gameplay collision proxies follow the changed magazine dimensions for every profile. Socket transforms, units, seeds and all C0/SGAF schemas stay unchanged. Metal wear becomes sparse restrained fine scuffs, with periodic numeric fields and correct PBR channel semantics preserved. Unchanged families also receive the explicit new generator version because the implementation registry is versioned as a whole.

The [rifle before](feature-fixtures/rifle-1.0.0.json), [rifle after](feature-fixtures/rifle-1.0.1.json), [metal before](feature-fixtures/metal-1.0.0.json) and [metal after](feature-fixtures/metal-1.0.1.json) input fixtures record migration solely of generatorVersion. The new validators explicitly reject1.0.0 instead of reinterpreting it. Migrate registered source files by changing generatorVersion, then regenerate under new content IDs; old cache directories remain immutable. No shipping world or gameplay replay depends on these diagnostic versions. Re-run topology/socket/collision/profile, material-field/mip and capture checks before dependent integration. Record visual/performance gates still pending rather than infer a full gate from these fixes.

## ADR-021 — Accepted: initial material benchmark camera and central-cube clarification

Date: 2026-11-13. Owner: M1 integrator under the renewed M1 assignment. B01-materials-v1 has no accepted production captures yet. Its original description left the seven camera views unspecified and placed an added cube exactly over the original B01-minimal cube. Resolve this before the first implementation: retain the original 10×10 m floor and original gameplay cube collider, replace the original cube visual with the central plaster cube, and render seven material cubes total at X−6..6 step2, Y0.5, Z0. This prevents coplanar overlap. The workload renders96 triangles (seven cubes and floor); it adds no weapon or soldier. B01-minimal-v1 remains unchanged.

Each view holds for360 fixed60Hz ticks at camera (familyX,1.65,−6)m, yaw0/pitch0, then cuts to the next family in the documented order. The cycle is2520 ticks/42 seconds with no interpolation. Camera configuration and exact content manifests enter route/content identity. Only verified compiled material caches may be used; the gallery is still not a benchmark substitute. Existing C0 fixtures, old results, SGAF payloads and toolchain remain immutable. Full three-run release/resource/thermal acceptance remains mandatory; implementing this route does not pass it.

## ADR-022 — Accepted: versioned soldier spike and second numeric motion baseline

Date: 2026-11-13. Owner: M1 integrator. The renewed assignment explicitly includes the P05 soldier spike. Freeze the generation-local23 semantic joints as soldier.semantic23.v1 before dependent motion work. C0-v1 and SGAF v1 remain unchanged. SGAF skeletons are visual artifacts: the real compiler rejects a gameplay-profile skeleton, and the soldier generator now rejects that composition. A gameplay hit evaluator is still M2 work. Joint frames are derived from the documented child directions and reported separately, since the frozen skeleton body has no frame fields. No binary format extension or graphics-dependent gameplay pose is introduced.

Iteration1 uses the original soldier.loft1.0.0 fixed recipe and soldier.motion.spike.v1. Real pose sweeps expose foot reach/slide and aimed-reload failures despite finite transforms and held-grip success. Preserve the original source, raw tests, candidate measurements and cache/captures. EditMode render requests initially retained bind geometry; corrected image capture explicitly calls the engine's BakeMesh on real generated weights/bindposes/current bones. This capture correction does not change a generated mesh or substitute for release skinning verification.

The second bounded numeric iteration retains full ±45°yaw/±25°pitch,3.5/5.5m/s speeds, original1.4/2.2m distance cycles,98% reach, anatomical limits and±0.08m dynamic pelvis correction. Use walk/run duty0.50/0.35 and nominal moving pelvis height0.78/0.76m interpolated to the existing0.66m full-crouch height; idle standing remains0.96m. The tactical locomotion posture must be visually reviewed. Raise the pelvis-relative reload belt target Y from−0.05 to+0.075m, preserving X−0.15/Z0.1 and the mechanical trajectory phases. These are versioned procedural parameters, not authored joint keyframes or weakened thresholds.

Iteration2 also uses a neutral-down shoulder task cone axis and elbow pole (local−Y), keeping the90°cone angle and knee+Z pole. The original T-bind segment reference bases/pole defaults remain immutable; task-space overrides belong to motion.v2 and are reported explicitly. Actual skinning review found elbows raised near the head with the original T-bind-centered task cone. The corrected task frame is measured against the same held/reload grip limits. Geometry adds code-derived pelvis coverage, chest/shoulder and pelvis/thigh skin transitions, fuller uniform profiles, rounded boot/cuff forms and covered head; no authored surface data is introduced.

Publish production soldier.loft1.0.1 inputs with revised duty parameters, explicitly reject1.0.0, and record before/after migration fixtures outside frozen C0 fixtures. Motion becomes soldier.motion.spike.v2; semantic skeleton/units/seed remain unchanged. Existing1.0.0 cache directories remain immutable. Re-run expanded slopes/steps/contact phases, full aim/reload combinations, generation/profile checks and actual skinned captures before acceptance. Two failed implementation iterations require a scope review rather than unlimited expansion; no reduced scope or realism/performance pass is accepted by this ADR.

## ADR-023 — Accepted: owner-authorized third bounded body-clearance iteration

Date: 2026-11-13. After the second actual skinned review failed, the integrator stopped soldier expansion and presented the required scope decision. Max explicitly answered: “Authorize one more bounded body-clearance iteration”. This extends the original two-iteration bound by exactly one focused iteration; it does not accept the failed prototype, relax tolerances or authorize unlimited soldier expansion.

The retained iteration2 defect is persistent trigger-arm penetration. Neutral right shoulder/elbow/hand positions are approximately(0.210,1.43346,0),(−0.00213,1.26347,0.10101),(0,1.52346,0.10000)m. Interior forearm samples lie inside the actual loft ellipse, and the sleeve also intersects the chest plate. The exact hand target is reached, demonstrating why grip error alone cannot establish visual validity. Read-only geometry/pose evidence and actual images are retained in the M1 continuation artifacts.

The one additional iteration addresses coupled weapon rigid placement, stock/shoulder and eye/sight alignment, and upper/forearm body clearance. Preserve semantic joints, local weapon sockets, full supported aim/stance ranges, bone lengths/limits, gameplay independence, gait/contact tolerances and no-art restrictions. Use independent clearance checks and actual skinned captures, not masked or removed limbs. Any new production source/motion identity must be versioned with migration inputs before publication; existing1.0.0/1.0.1 outputs and failures remain immutable. The third iteration must be reviewed before further soldier work; no reduced workload or milestone closure is approved here.

The submitted candidate is soldier.loft1.0.2/motion.spike.v3, with a generated exterior chest pouch,35°cosmetic body blade, bounded clavicle/elbow task constraints and analytic reload arcs/cant. Input root and local weapon sockets remain unchanged. The [versioned baseline](soldier-baseline.md#third-bounded-clearance-candidate) records exact parameters and residuals. The conservative acceptance test remains enabled and FAILS for possible forearm/strap contact; eye/sight alignment is unresolved. Submission does not establish a pass or authorize further parameter search.

## ADR-024 — Accepted: P05 soldier visual scope stop after exhausted clearance iterations

Date: 2026-11-13. Status: Accepted (scope decision). After iteration2 FAIL and the owner-authorized third candidate FAIL under ADR-023, Max directed remaining M1 work to proceed without a fourth clearance iteration (“do all”). That continues other M1 packages only; it does not accept the failed soldier as visual PASS and does not authorize further clearance/parameter search.

Facts retained for this stop: iteration2 FAIL for arm/torso penetration despite passing numeric grip/contact checks; ADR-023 authorized exactly one third candidate, soldier.loft1.0.2/motion.spike.v3, which also FAIL (~2.18mm dense possible left forearm/strap overlap near down-aim reload phase0.45; eye/sight alignment unresolved). No fourth iteration is authorized.

Decision: stop further P05 soldier clearance/parameter search. Retain loft1.0.0/1.0.1/1.0.2 and motion.spike.v1/v2/v3, including loft1.0.2/motion.spike.v3, as non-accepted diagnostic evidence. Numeric pose/grip/contact and the eight-rig harness remain available as spike evidence only and do not override the failed visual gate. M1 soldier visual/clearance acceptance stays FAIL / not accepted. No realism claim and no tolerance relaxation are authorized. M2 combat still must not depend on accepting this skin.

Alternatives rejected: unlimited further clearance iteration; silently treating numeric or harness passes as visual PASS; deleting spike sources, caches or failure evidence.

This ADR does not close M1, does not waive hardware gates, and does not authorize imported art. It allows parallel remaining M1 polish (T031, LOD evidence, B01 three-run and related open gates) without pretending soldier PASS. Future soldier visual work requires new explicit owner authorization and a new versioned candidate. [Soldier baseline](soldier-baseline.md#scope-stop), [handoff](milestones/m1-procedural-feasibility/handoff.md).

## ADR-025 — Accepted: M1 owner waiver of remaining hardware/resource validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by Codex as M1 integrator. After ADR-024 stopped further P05 clearance search, Max instructed: “do everything left to close M1; imitate human review (he will not review manually); no further confirmations; goal is M1 closed and everything should just work.” That instruction authorizes administrative M1 closure and formal integrator imitation of the outstanding human visual/transition reviews. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M2 combat, reopening P05 clearance search, or flipping the soldier visual FAIL under ADR-024 to PASS.

Close M1 administratively as **CLOSED WITH USER WAIVER**. Two of eight milestones are closed. Actual dispositions retained:

- P01/P02 foundations and diagnostic compiler/cache evidence remain PASS as previously recorded.
- P03 formal silhouette/ADS acceptance is **PASS-with-limits** after integrator inspection of retained 1.0.1 gallery/silhouette PNGs (`Artifacts/m1-procedural-feasibility/owner-closure-20261113/p03-visual-review.json`). Limits: prototype greybox only; weak front orthographics; missing non-side lower-LOD capture angles; Low shadow aliasing; no realism claim.
- P03 LOD transition formal acceptance is **PASS** on the numeric 10% hysteresis harness (9/9 tests, zero policy violations/flicker; `.../lod-transition-acceptance.json`). Unity transition-frame captures remain Pending/unavailable and are not fabricated.
- P04 material generation/gallery/compression diagnostic work remains implemented; B01-materials three-run PresentMon series and related counters stay **PendingHardware** in their immutable reports (`Artifacts/m1-continuation/b01-materials-series-summary.json`). Missing live HWiNFO/thermal review, `channelMode`, draw/triangle/GPU-memory/variant counters and the failed 1200s precondition are **Unverified / waived for M1 closure only**.
- P05 soldier visual/clearance remains **FAIL / not accepted** under ADR-024; loft1.0.2/motion.spike.v3 is non-accepted diagnostic evidence only.

Historical failed/pending automated results and raw data are immutable; no missing value is replaced, no automatic hardware gate returns PASS because of this waiver, and no performance threshold changes. Root `feature-content-lock.json` is corrected only for the already-committed ADR-024 `docs/soldier-baseline.md` LF bytes so the registry matches HEAD; no generator source, schema, recipe or golden payload changed. ADR-017’s M0 waiver is unchanged and still does not supply M1 hardware PASS. Future milestone work still needs an explicit assignment; no M2 combat, thermal stability, 30-minute soak, full-map compile budget or fully verified reference-performance claim follows. The [handoff](milestones/m1-procedural-feasibility/handoff.md) identifies exact source/artifacts, the formal review records and owner disposition. A local `m1-procedural-feasibility-owner-accepted` marker identifies this qualified closure; it is not a passing benchmark tag. Resuming technical hardware acceptance requires actual supported full captures, counters and thermal review, preserving these prior reports.
## ADR-026 — Accepted: M2 owner waiver of remaining hardware/reference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M2 integrator. After software gates for C01–C04, Unity V05/V06/V09 scenarios, EditMode/PlayMode and client/server smoke all PASS, Max’s standing direction for this chat (“no confirmations”; “everything should just work”; implement everything for M2 with multiple agents; same pattern as ADR-025) authorizes administrative M2 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M3 world/AI, flipping prior M0/M1 PendingHardware reports to PASS, or claiming eight-bot B03 / reference-performance acceptance.

Hardware attempt retained (not acceptance):

- Pinned PresentMon 2.6.0 was configured and hash-verified via `Tools/Configure-PresentMon.ps1`.
- A short release-player diagnostic with PresentMon ran against the retained IL2CPP client build (`59bd8c6` player; evidence under HEAD `97f10ef`): `gateStatus` **PendingHardware**; diagnostic scope only (`Artifacts/m0-foundation/97f10ef88e17d25df62b8ddbb07f4ec18b8c59f8/benchmark-diagnostic-20261113T185709203Z-c04346e8/`). No counters were invented.
- No live HWiNFO/sensor log was available, so `Benchmark-Series.ps1` (mandatory `-SensorLog`) could not run. Full three-run reference capture + thermal review therefore remain incomplete.
- Full eight-bot B03 performance remains an M3/M4 gate by milestone design; M2 has no separate combat B0x workload that could substitute for that gate.

Close M2 administratively as **CLOSED WITH USER WAIVER**. Three of eight milestones are closed. Actual dispositions retained:

- C01–C04 motor/weapon/pose/view adapters and T027 defaults remain PASS as previously recorded (pure simulation 23/23; contracts 71/71).
- Unity EditMode 13/13 and PlayMode 11/11 with dedicated V05/V06/V09 scenario harnesses remain PASS (`Artifacts/m2-combat-harness/474427d77b6678ee651dfb344c5b0a26539a9b00/v05-v09-scenarios-20261113T185222419Z/`).
- Client/server release builds and empty-actor `CombatHarnessSimulation` smoke remain PASS on `59bd8c6`.
- Reference-player / resource-budget measurement stays **PendingHardware** in its automated diagnostic report and is **Unverified / waived for M2 closure only**.
- Upstream ADR-017/024/025 dispositions are unchanged; P05 soldier visual remains FAIL / not accepted.

Historical failed/pending automated results and raw data are immutable; no missing value is replaced, no automatic hardware gate returns PASS because of this waiver, and no performance threshold changes. ADR-017/025 waivers still do not supply M2 hardware PASS. Future milestone work still needs an explicit assignment; no M3 compound/AI, thermal stability, 30-minute soak, full-map compile budget, B03 eight-bot acceptance or fully verified reference-performance claim follows. The [handoff](milestones/m2-combat-harness/handoff.md) identifies exact source/timestamps, the formal review records and owner disposition. A local `m2-combat-harness-owner-accepted` marker identifies this qualified closure; it is not a passing benchmark tag. Resuming technical hardware acceptance requires actual supported full captures with live sensors, counters and thermal review, preserving these prior reports.

## ADR-027 — Accepted: M3 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M3 integrator (bc-08a335af). After pure software gates for W01–W05 / T014–T016 and T033 B02/B03 freeze PASS, Max’s standing direction for this assignment (“start M3; once done start M4”; “no confirmations”; “just work”; hardware PendingHardware may be waived with an explicit ADR like ADR-017/025/026) authorizes administrative M3 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M5 networking, reopening P05 soldier clearance, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity `6000.3.28f1` / revision `8f082d2fe476` was sought under `C:\Program Files\Unity\Hub\Editor` and PATH; editor binary was **not present** on this worker, so EditMode/PlayMode, client/server release builds and smoke could not run. Status **Pending** (missing tool), not PASS.
- Reference-player B02/B03 PresentMon series and live sensor logs were not started without a release player. Status **PendingHardware**. Evidence: `Artifacts/m3-generated-compound/owner-closure-20261113/hardware-attempt.json`.
- No counters were invented; no golden/cache edits.

Close M3 administratively as **CLOSED WITH USER WAIVER**. Four of eight milestones are closed. Actual dispositions retained:

- W01 compound planner four-seed connectivity / border / spawn-LOS / route checks PASS (pure).
- W02 nav settings/clearance/revisioned adapter and gameplay-hash profile independence PASS (pure).
- W03 streaming 3×3 prefetch, ≤2 ms upload split, cancellation, teleport fence, 100-traversal residency PASS (pure).
- W04 eight-bot AI ≤16 vision rays / ≤2 paths per tick, cover release, occluded non-acquire PASS (pure).
- W05 108000-tick (30-minute @ 60 Hz) soak without unbounded queues/orphan covers PASS (pure); B02/B03 freeze at `Benchmarks/b02-b03-m3-freeze.json`.
- Contracts 71/71; simulation 31/31; M3 generation slice 16/16; full generation wrapper 298/298.
- Unity EditMode/PlayMode/smoke and reference B02/B03 hardware remain **Pending / PendingHardware** and are **Unverified / waived for M3 closure only**. Native-720p timing acceptance remains an M4 gate.

Historical failed/pending automated results are immutable. ADR-017/024/025/026 dispositions are unchanged; P05 soldier visual remains FAIL / not accepted. The [handoff](milestones/m3-generated-compound/handoff.md) identifies exact source/timestamps and owner disposition. A local `m3-generated-compound-owner-accepted` marker identifies this qualified closure; it is not a passing benchmark tag. M4 may proceed under the same owner assignment.

## ADR-028 — Accepted: M4 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M4 integrator (bc-08a335af). After pure software gates for O01–O03 (Compound Clear restart, HUD/accessibility layout, analytic FX/synth audio) PASS, Max’s standing direction (“start M3; once done start M4”; no confirmations; hardware PendingHardware may be waived with an explicit ADR like ADR-017/025/026/027) authorizes administrative M4 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M5 networking, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity `6000.3.28f1` editor binary remained absent on this worker; EditMode/PlayMode, IL2CPP release builds, smoke and PresentMon B02/B03 series could not run. Status **Pending / PendingHardware**. Evidence: `Artifacts/m4-offline-slice/owner-closure-20261113/hardware-attempt.json`.
- No counters were invented; no golden/cache edits; B02/B03 freeze from M3 remains unchanged.

Close M4 administratively as **CLOSED WITH USER WAIVER**. Five of eight milestones are closed. Actual dispositions retained:

- O01 Compound Clear objective/restart/idempotent win-loss PASS (pure).
- O02 HUD 720p layout at 150% scale, focus/rebind/contrast/reduced-motion PASS (pure).
- O03 analytic FX pool and ≤24-voice synth audio with peak limiter PASS (pure); no sprites/recorded samples.
- O04/O05 native-720p three-run / 30-minute release soak and signed offline R01–R09 player evidence remain **PendingHardware** and are **Unverified / waived for M4 closure only**.
- Simulation suite 39/39 on close HEAD including prior M2/M3 pure tests.

Historical failed/pending results are immutable. ADR-017/024/025/026/027 unchanged; P05 soldier visual remains FAIL. The [handoff](milestones/m4-offline-slice/handoff.md) records evidence. A local `m4-offline-slice-owner-accepted` marker identifies this qualified closure; it is not a passing benchmark tag. M5 networking remains unassigned until explicit owner assignment.

## ADR-029 — Accepted: M5 Unity Transport behind INetworkChannel; custom prediction retained

Date 2026-11-13; recorded by the Cursor M5-M7 integrator (bc-05dea339). Spike compared the existing pure INetworkChannel port surface against Unity Transport (UTP) and NGO-style anticipation.

Decision: pin **Unity Transport behind INetworkChannel** for remote peers. Keep **custom** 256-tick prediction/reconciliation and remote interpolation in Shooter.Networking. **Reject** treating NGO/Netcode anticipation as FPS rollback/replay. Netcode for Entities remains a separate ECS migration ADR if ever pursued.

Measured software evidence (pure): loopback duplex, seeded impairment simulator, codec fuzz/truncation, handshake reject-before-spawn, command ownership/tick/rate validation, prediction p95 ≤0.1 m at 100 ms/1% loss on static plane, bounded snapshot interpolation/resync. Wire protocol **N1-v1**. Shipping composition rejects the impairment fake; LoopbackNetworkChannel is a real local/dedicated adapter.

Exact UTP package version remains **null** in 	oolchain-lock.json until the locked Unity 6000.3.28f1 editor verifies and pins it; no package upgrade is introduced by this ADR. C0-v1 unchanged.

## ADR-030 — Accepted: M5 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M5-M7 integrator (bc-05dea339). After pure software gates for N01–N05 / T019–T020 PASS, Max’s standing direction (“do all next milestones”; “no confirmations”; hardware PendingHardware may be waived with an explicit ADR like ADR-017/025–028) authorizes administrative M5 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M6 before this close is recorded, reopening P05 soldier clearance, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity 6000.3.28f1 editor binary remained absent on this worker; EditMode/PlayMode, IL2CPP client/dedicated-server builds, smoke and PresentMon captures could not run. Status **Pending / PendingHardware**. Evidence: Artifacts/m5-network-foundation/owner-closure-20261113/hardware-attempt.json.
- UTP package pin remains null pending editor verification (ADR-029). No counters invented.

Close M5 administratively as **CLOSED WITH USER WAIVER**. Six of eight milestones are closed. Actual dispositions retained:

- N01 transport spike ADR-029 PASS (software selection; UTP version Pending).
- N02 session/commands/codec fuzz PASS (pure).
- N03 local prediction/reconciliation PASS (pure; static-map correction p95 gate).
- N04 remote snapshots/interpolation/resync bounds PASS (pure).
- N05 two-peer impairment matrix PASS (pure); headless channel has no presentation dependency.
- Unity/process two-peer player evidence and reference hardware remain **PendingHardware** and are **Unverified / waived for M5 closure only**.

Historical failed/pending results are immutable. ADR-017/024/025/026/027/028 unchanged; P05 soldier visual remains FAIL. The [handoff](milestones/m5-network-foundation/handoff.md) records evidence. A local m5-network-foundation-owner-accepted marker identifies this qualified closure; it is not a passing benchmark tag. M6 may start after this close.

## ADR-031 — Accepted: M6 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M5-M7 integrator (bc-05dea339). After pure software gates for S01–S05 / T021–T022 PASS, Max’s standing direction (“do all next milestones”; no confirmations; hardware PendingHardware may be waived with an explicit ADR) authorizes administrative M6 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, starting M7 before this close is recorded, reopening P05, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity 6000.3.28f1 editor binary remained absent; EditMode/PlayMode, IL2CPP four-peer builds, 30-minute B04 PresentMon soak could not run. Status **Pending / PendingHardware**. Evidence: Artifacts/m6-network-slice/owner-closure-20261113/hardware-attempt.json.
- Pure B04 software soak (30s proxy at 150 ms/3% loss, four peers) PASS; full 30-minute separate-host B04 remains PendingHardware.

Close M6 administratively as **CLOSED WITH USER WAIVER**. Seven of eight milestones are closed. Actual dispositions retained:

- S01 historical hit query PASS (pure).
- S02 authoritative fire ordering/trade PASS (pure).
- S03 late join/resync/disconnect/fresh session PASS (pure).
- S04 four-peer B04 software soak PASS; 30-minute hardware soak PendingHardware.
- S05 exposure review PASS (LAN/private; N4 public deferred).
- Unity/hardware remain **Unverified / waived for M6 closure only**.

Historical failed/pending results immutable. ADR-017/024–030 unchanged; P05 FAIL retained. M7 may start after this close.

## ADR-032 — Accepted: M7 owner waiver of remaining Unity-tooling and hardwarereference validation and milestone close

Date 2026-11-13; accepted by Max as project owner, recorded by the Cursor M5-M7 integrator (bc-05dea339). After pure software gates for H01–H05 / T023–T024 PASS_WITH_PENDING (mandatory tools green; Unity/player/60 FPS captures PendingHardware), Max’s standing direction (“do all next milestones”; no confirmations; hardware PendingHardware may be waived with an explicit ADR) authorizes administrative M7 closure without waiting for interactive sign-off. It does **not** authorize inventing PASS evidence, editing goldens/caches, upgrading Unity/packages, claiming unmeasured 60 FPS acceptance, expanding T025 into mandatory release scope, reopening P05, or flipping prior PendingHardware reports to PASS.

Tooling/hardware attempt retained (not acceptance):

- Locked Unity editor absent; clean IL2CPP client/server builds, outside-editor release rehearsal and PresentMon 60 FPS stretch captures could not run. Status **Pending / PendingHardware**. Evidence: Artifacts/m7-hardening-stretch/owner-closure-20261113/hardware-attempt.json.
- Stretch report explicitly records **PendingHardware** with aselinesRelaxed=false; mandatory 30 FPS baselines unchanged.

Close M7 administratively as **CLOSED WITH USER WAIVER**. Eight of eight milestones are closed under owner tooling/hardware waivers where applicable. Actual dispositions retained:

- H01 clean reproducibility locks/provenance inventory PASS; Unity clean build Pending.
- H02 regression matrix pure docs/fixtures/contracts/simulation/generation PASS_WITH_PENDING.
- H03 release rollback pair type + ledger PASS; player rehearsal Pending.
- H04 60 FPS stretch report **PendingHardware** (does not block mandatory 30 FPS).
- H05 deferred scope ledger records T025/eight-player/public networking as deferred; forbids unmeasured claims.

Historical failed/pending results immutable. ADR-017/024–031 unchanged; P05 FAIL retained. Optional T025 remains out of scope.

## ADR-033 — Accepted: next offline playable product scope and staged delivery

Date 2026-11-13. Context: the owner requests development documentation and next-step order for an actual playable build, after the eight original milestones were closed with explicit waivers. Reviewing current Bootstrap and handoffs reveals pure feature foundations without the requested integrated team-match player. Administrative historical closure is preserved; it is not release acceptance.

Decision: plan M8 runtime composition → M9 Team Deathmatch → M10 two maps and shell/settings → M11 playable release acceptance. Product is one local human plus bots on both teams, 5v5 default with 6v6 selectable, two distinct generated maps, 150 enemy kills or 15 minutes elapsed, immutable final result tables, map/mode selection and persistent applied sound/video/control/accessibility settings. Exact chosen defaults, including full-tick score evaluation, timeout draws and respawn policy, live in [team-match rules](features/team-deathmatch.md), [maps](features/playable-maps.md) and [shell](features/game-shell.md). They are explicit design choices for the requested scope, not measured gameplay quality claims.

Introduce a separate proposed pure Game-v1 boundary for product/session/participant/results/settings semantics; keep frozen C0-v1/content-v1/N1-v1 intact. Actual Game-v1 method signatures, source/schema locks and executable fixtures must be implemented/frozen by T035 under the sole integrator, with a follow-up implementation ADR for any encoding/compatibility decisions. Workers may not invent interfaces or patch shared authority/Bootstrap concurrently. This planning ADR does not itself freeze unimplemented C# or a binary format.

Offline play needs no remote transport, LAN/public UI, extra human-player capacity, T025 weapons/penetration, package upgrade, new asset source or imported art. It retains authoritative 60Hz simulation, presentation-only quality options, procedural provenance and native720p Low30 reference requirements. Old waivers and the P05 FAIL stay intact; new milestone exits require actual player/visual/hardware evidence where specified and cannot automatically inherit waivers. This task prepares docs only; M8–M11 remain unassigned until future implementation assignment. See [ordered review](playable-release-plan.md) and [packs](milestones/index.md).

## ADR-034 — Accepted: initial Game-v1 freeze (T035)

Date 2026-11-13; accepted by the Cursor M8 integrator (bc-b005c261) under owner Max direction to implement next milestones. Freezes compiling `Shooter.Game.Contracts` (`Game-v1`) separately from C0-v1: session/map/settings ports, participant/life/results DTOs, TDM score/time constants (150 / 54000), and executable fixtures under `docs/game-fixtures/` locked by root `game-contract-lock.json`. C0-v1, SGAF/content-v1 and N1-v1 remain unchanged. New workloads `TDM-5v5-v1` / `TDM-6v6-v1` / `RUN-compound-vertical-v1` do not replace B01–B04.

Also records RUN00 repair: missing Unity `.meta` for M3–M7 sources generated with unique GUIDs; unapproved Assets Markdown relocated to `docs/notes/procedural-viewmodel-editmode.md`. Source provenance audit PASS after repair. This ADR does **not** accept Unity/player/hardware gates, close M8–M11, reopen P05, or authorize package upgrades.

## ADR-035 — Accepted: verified release map packages (T055 boundary)

Date 2026-11-14; owner sole Contracts/content-format integrator under owner Max direction to unblock T055. This accepts the encoding and publication boundary for verified release map packages. It does **not** implement Compile-GameContent publication, close FIX04, accept player/hardware gates, change C0 DTO bytes, bump SGAF envelope version, upgrade packages/editor, or authorize runtime regeneration as a shipping path.

Context: T055 is blocked because `content-format-lock.json` authorizes only the one-box collision diagnostic; SGAF Collision v1 bodies carry geometry and `surfaceTag` but not `colliderStableId`/`SurfaceId`; Navigation v1 remains an opaque adapter blob without an approved shipping format; no package-root schema exists; and `CompoundMapRuntime` regenerates plans from seeds in the release player. Catalog visual hashes are currently hashes of labels, not packaged bytes.

Decision — stable IDs: keep frozen SGAF Collision v1 and C0 `CollisionData` unchanged. Reject a Collision v2 body change for this repair wave. Carry ordered collider/surface identity in versioned canonical JSON `shooter.map-metadata.v1`, which binds each decoded Collision primitive (capsules, then boxes, then height-grids, in SGAF body order) to nonzero `colliderStableId`, `surfaceId` and matching `surfaceTag`. Query hits continue to use C0 `QueryHit` fields populated from that metadata at install time. Metadata that disagrees with Collision counts/tags, duplicates stable IDs or omits required spawns/covers rejects the package.

Decision — package root: introduce `shooter.map-package.v1` as the catalog root document. `MapCatalogEntry` gameplay/visual root hashes must become SHA256 of the canonical package-root JSON bytes (and profile-partition roots), never label strings. The package enumerates exact dependency content identities by SHA256 of complete SGAF files and of the canonical metadata document. Unknown `mapId`, incomplete dependencies or mismatched toolchain reject. Server packages include gameplay collision/nav/metadata only; client packages add the selected visual profile closure (meshes/materials/skeletons/shaders/font exception).

Decision — navigation: shipping Navigation SGAF uses adapter format `unity.navmesh.bake.v1`, adapter version 1, `settingsHash` = `NavSettings` hash (`shooter.nav-settings.v1`), and an explicit little-endian triangulation blob defined in [content-format](content-format.md) (vertices, polygons, areas, bounds). No arbitrary Unity asset/object deserialization and no opaque engine dump. The editor/compiler bakes with the pinned AI Navigation 2.0.0 package against installed authoritative collision, then encodes that bake into the blob with provenance (collision artifact hash, settings hash, toolchain hash, generator fingerprint). Pure `NavCompiler` / path-delegate checks remain compile-time reachability diagnostics and pure-test helpers; they are not a waiver of Unity-native load/query evidence required by FIX04. Release players load the packaged blob; they do not re-bake or regenerate nav from seeds.

Decision — recipes and lock migration: register `map.compound` 1.0.0 / `shooter.recipe.map-compound.v1`, `map.depot` 1.0.0 / `shooter.recipe.map-depot.v1` and publisher scope `map.package` 1.0.0 / `shooter.map-package.v1` in root `content-format-lock.json` alongside the existing diagnostic `collision.box`. Schemas and scaffolding fixtures live under `docs/content-schemas/`. No pre-existing shipping map package is migrated; unsupported package/metadata/nav-adapter versions fail closed. The one-box diagnostic remains restricted to its named set. Generator algorithm or seed-identity changes require a new generator version, new package roots and reviewed fixtures before consumers rebase.

Decision — depot seed: approved catalog seed for `depot-yard` is **460072031**, matching current `DepotPlanner` / `StaticMapCatalog` source. Historical proposed seed `42` stays only in the robustness corpus (`1`, `42`, `460072030`, `uint64` max) and must not become the catalog identity. T058 accepts spawn/route safety against the approved seed; it does not silently rewrite it to 42.

Decision — publication, reproduction and recovery: apply ADR-018 atomic staging/recheck/rename/index-switch rules to map package sets. Cold, warm and forced reproduction must recreate identical package-root and dependency bytes. Cancellation or corruption releases partial installs, retains corrupt bytes for repair, never silently substitutes another map or the floor/box diagnostic, and returns a stable repair message to the shell. Release composition validates package roots before Ready and installs collision/nav/metadata before actor spawn; developer absolute paths, on-device recipe compilation and random regeneration are forbidden on the shipping path.

Validation and rollback: structural schemas/examples and lock inventory must pass `Tools/verify_content_format.py` and documentation/fixture verifiers after this boundary lands. T055 then implements enumeration, publication and runtime load against this ADR. Rollback is the prior content-format lock/source pair; do not retain players whose catalog roots still hash labels or regenerate maps from seeds. [Playable maps](features/playable-maps.md), [content-format](content-format.md), FIX04 and T055 remain the acceptance authorities for measured publication evidence.

## ADR-036 — Accepted: UserSettings schema 2 shell coverage (T060 / FIX09)

Date 2026-11-14; owner sole Contracts/settings integrator under owner Max direction for T060 preparation. Extends the frozen Game-v1 `UserSettings` DTO and file schema so every normative [shell](features/game-shell.md) preference is versioned, migratable and fixture-locked. This does **not** close FIX09 device acceptance, named audible review, package upgrades, C0/SGAF/N1 changes, or authorize silent golden edits.

Context: ADR-034 froze settings schema=1 with master/SFX/music, quality, mouse, invert Y, subtitles and UI scale only. Required display mode/resolution/VSync/FPS cap, hip FOV, effects/UI channel naming, accessibility motion/flash/contrast and rebindable controls were absent. `IClientSettingsApplier` staged a quality byte alone. The M10 `FileUserSettingsStore` used an unbounded semicolon text, deleted-before-move replace, and lacked last-good/quarantine/newer-file preservation.

Decision — schema identity: bump `GameContractVersion.SettingsSchema` to **2**. File documents use schema id `shooter.user-settings.v2` under `docs/game-fixtures/`. Schema 1 semicolon files remain readable once; writers emit bounded UTF-8 JSON (≤64 KiB) only. C0-v1 and Game-v1 ruleVersion stay 1; settings file version is independent of `HudSettings` diagnostic serialization.

Decision — field inventory (defaults match shell):
- Audio: `masterVolume` 1.0, `effectsVolume` 1.0 (migrated from schema-1 `sfx`), `uiVolume` 0.7 (migrated from schema-1 `music` with clamp); each finite in [0,1]; 0 mutes the channel.
- Video: `qualityTier` 0=Low/1=Balanced/2=High; `resolutionWidth`/`resolutionHeight` default 1280×720 and ≥1280×720; `displayMode` Windowed=0/Borderless=1/Fullscreen=2; `vSync` false; `fpsCap` 60 with allowed {0=uncapped,30,60,120}; `hipFovDegrees` 80 in [70,100].
- Controls: `mouseSensitivity` 0.10 in [0.01,1.00] (schema-1 values clamped into range); `invertY` false; complete ASCII action→control binding map including move/look/aim/fire/reload/sprint/crouch/jump/slots/scoreboard/pause; Escape reserved for menu restore; duplicate controls reject.
- Accessibility: `uiScale` 1.0 in [1.0,1.5]; `reducedCameraMotion`/`reducedFlash` false; `contrast` 1.0 in [0.8,1.5]; `subtitles` retained from schema 1 (default false).

Decision — migration and storage safety: missing file → defaults; corrupt → quarantine sidecar + defaults + nonblocking diagnostic; schema newer than 2 → safe in-memory defaults **without overwriting** the newer file; schema 1 → migrate into schema 2 then persist only on explicit Apply/Save; interrupted atomic replace preserves `.last-good`; read-only IO leaves usable memory settings and “could not save”; gameplay/content hashes are never written into the settings document.

Decision — application seam: `IClientSettingsApplier` applies the full DTO (audio/display/quality/FOV/input/accessibility), stages display changes with Keep/Revert, and exposes pending-display state for a 15-second real-time confirmation including focus-loss/restart rollback. Pure `UserSettingsEditor` owns Apply/Cancel/defaults staging so T059 menus and T061 audio can bind without inventing a parallel settings contract. Device/player evidence remains FIX09/SET02 Pending until named review.

Validation: `docs/game-fixtures` + `game-contract-lock.json` refreeze for Contracts sources; `Tools/Test-Game.ps1 -Offline` settings cases; `docs/verify_docs.py --refresh-manifest` after doc edits. Rollback restores schema-1 DTO/store/lock pair; do not ship players that silently drop shell fields or overwrite newer settings files.

## ADR-037 — Accepted: product TDM measurement workloads (T063 / FIX12)

Date 2026-11-14; owner measurement integrator under owner Max direction for T063. Extends frozen Game-v1 `docs/game-fixtures/tdm-workloads.json` with reviewed depot measurement identities and product telemetry counters. Does **not** close REL04/TDM08 hardware acceptance, change B01–B04, alter map package format, or authorize silent fixture edits to force PASS.

Context: ADR-034 froze `TDM-5v5-v1` / `TDM-6v6-v1` against `training-compound` only. Audit A10 recorded that Benchmark.ps1 accepted B01 only and depot workload identity/route/counter coverage was not established by compound IDs. FIX12 requires both maps × both sizes with authority/AI/respawn/lease/query/content/render counters and negative collector rejection of wrong map/roster/profile/build/short windows.

Decision — fixture migration (schemaVersion **2**, additive):
- Preserve historical `TDM-5v5-v1` / `TDM-6v6-v1` as **training-compound** / seed **460072030** (explicit seed fields).
- Add reviewed depot rows `TDM-5v5-depot-v1` / `TDM-6v6-depot-v1` for **depot-yard** / seed **460072031** (ADR-035). Depot is never implied by substituting mapId on compound IDs.
- Add soak identities `TDM-soak-6v6-compound-v1` / `TDM-soak-6v6-depot-v1` (1800 s / 108000 ticks, no warmup) for ≥30-minute normal-AI evidence; remain PendingHardware until reference execution.
- Expand required counters on measurement rows: team kills, activeTicks, respawns, aiRays, pathRequests, shots, damageEvents, leaseAcquires, querySamples, contentBindings, renderFrames. `RUN-compound-vertical-v1` stays a composition note, not a performance gate.
- Runtime mirror: `ProductTdmWorkloads` in Game/Session; collector version **4** product path; analysis `Tools/analyze_tdm_benchmark.py`.

Decision — capture path: product composition (not diagnostic floor/box) autostarts the reviewed SessionRequest, binds verified map presentation, and records frame/tick/counter telemetry without changing match rules to improve scores. B01 wrappers remain separate. Twelve final captures and soaks stay PendingHardware without reference-machine proof.

Validation: refreeze `tdm-workloads.json` in `game-contract-lock.json`; `python Tools/test_tdm_benchmark_analysis.py`; `Tools/Test-Game.ps1 -Offline` product workload cases; `Tools/Benchmark.ps1` / `Benchmark-Series.ps1` / `Benchmark-Product-Matrix.ps1` / `Benchmark-Soak.ps1` documented commands. Rollback restores schema-1 compound-only fixture + B01-only wrappers; do not ship players that treat depot as a silent alias of compound workloads.

## ADR-038 — Accepted: persisted release catalog (T067 / B03)

Date 2026-11-14; owner content/world integrator under owner Max direction for T067 gaps B03+B09 items 1 and 5. Extends ADR-035 publication with a data-only release catalog sidecar. Does **not** close FIX04 native bake/visual closure, authorize C0/Game-v1 DTO changes, or claim Unity player extract proof (T071).

Context: `StaticMapCatalog.CreateReleaseSeed` rebuilt gameplay via planners and eight `MapPackageBuilder.Build` calls at release startup. Package `index.json` listed package roots but omitted catalog display metadata and `MapGameplayDefinition` sidecars, so the shipping player still compiled. Bootstrap defaults also resolved repository paths two levels above `Application.dataPath`, and missing content hard-failed before shell.

Decision — catalog document: introduce `shooter.release-catalog.v1` written atomically as `catalog.json` beside `index.json` under the map-package cache root at publish time. The document binds toolchain hash, per-map display/mode/size/recipe identity, gameplay/visual package-root SHA256 values, and the gameplay sidecar (collision/nav revisions+hashes, bounds, spawn slots, patrol/combat destinations). Schemas/fixtures live under `docs/content-schemas/release-catalog.*`. C0 and Game-v1 DTO bytes stay unchanged; this is an additive content-format sidecar.

Decision — shipping load: release Bootstrap hashes the packaged toolchain lock, loads `catalog.json` via verified bytes only, and rejects schema/toolchain/map identity mismatch. Shipping composition must not call planners, `MapPackageBuilder`, or a placeholder toolchain fallback. Compile/test helpers may still build catalog bytes through the publisher path (`ReleaseCatalogDocument.BuildBytes` / `CompileSeed`).

Decision — packaged discovery/recovery: player defaults resolve `Content/map-packages` and `toolchain-lock.json` relative to the executable install root (`Application.dataPath` parent). `launch-offline.cmd` keeps explicit `%~dp0` overrides. Editor may fall back to repository `Artifacts/map-packages` when the packaged folder is absent. Missing/corrupt content before shell enters a recovery screen using `ReleasePackagePolicy.CorruptContentRecoveryMessage` with Retry/Quit return paths; no silent map substitution.

Validation: Generation pure tests publish+load catalog without planner on the load path; Package-Release asserts/ships `catalog.json`; path-default unit tests cover install-root layout. Unity player launch/extract evidence remains Pending under T071. Rollback restores the prior index-only cache + planner `CreateReleaseSeed` pair.


## ADR-039 — Accepted: local authoritative sprint slide (T072)

Date 2026-11-14; owner Max explicitly requested implementation of slide (подкат), default Left Shift + F. Sole integrator implements this bounded M8/M10 extension; the prior documentation-only assignment does not restrict this new user-authorized feature. No agents, online rollout, art import, package upgrade or acceptance waiver.

Decision: add a local `LocalPlayerCommand` envelope (C0 InputCommand plus SlidePressed) at the existing human sampler/session boundary. Slide is consumed only alongside an accepted fresh command for the current human life, at the authority's fixed 60 Hz tick. C0-v1 DTOs/buttons/snapshots, N1-v1 packets, SGAF and existing gameplay fixture bytes remain frozen. The N1 prediction/transport lane cannot initiate this offline-only ability; remote sliding needs a future explicit versioned protocol/prediction migration. Presentation sends intent and reads authoritative crouch/velocity; it never moves the actor.

Motor state stores remaining slide/cooldown ticks. Entry needs an alive grounded actor already sprinting forward at ≥4.5 m/s, fresh Slide press and Sprint held, no aim/crouch/fire/reload/jump conflict. Entry speed 6.5 m/s, deceleration 4 m/s², maximum duration 45 ticks (0.75 s), cooldown 60 ticks from entry. Direction follows entry horizontal momentum; turning aim/movement cannot steer or add speed during slide. Use existing 1.2 m crouch collision/hit pose/eye height, normal swept collisions and ground rules. Jump cancels; leaving ground or blocking horizontal movement ends slide; standing recovery retains crouch under a ceiling. Holding F cannot retrigger. Death/reset/new life clears slide state, and pause/focus loss clears pending input without advancing authority time. No camera roll/shake is introduced.

Settings: additive optional `Slide` binding defaults to Key.F, combined with the applied Sprint binding (default Key.LeftShift). Game-v1/settings schema 2 layout is unchanged. Normalize legacy 14-action settings by adding Slide=Key.F when free, or Slide=Unbound when F was already customized; preserve existing actions and never steal a control. A legacy binding table already at the schema count limit is preserved without adding a row; Restore defaults makes Slide available in that exceptional case. Validate counts before allocating the normalization buffer. This keeps old schema-2 files valid; settings UI exposes Slide rebind/swap/defaults. Add migration tests and a new settings fixture before refreezing the Game-v1 source inventory. Older players should use their own retained binary/content/lock pair; no hash relabeling.

Validation: motor eligibility/duration/deceleration/wall/ceiling/jump/cooldown/life tests, real harness command rejection/pose coverage, render-frame edge/focus/input-device tests, settings migration/conflict/persistence tests, locked pure suites, provenance, pinned Unity EditMode/PlayMode and matching clean Client/Server/package. Keep actual interactive two-map/quality/device/performance evidence Pending unless observed. Retain the old `7cd7e81` archive unchanged; it has no slide. Rollback uses that prior complete pair and its original controls/limitations. New build identity and controls must state slide support.

## ADR-040 — Accepted: LF line-ending identity for frozen locks

Date 2026-11-14; owner approved explicitly in the session. `.gitattributes` stores text as LF, but the integrator's Windows working copy held 156 tracked files with CRLF bytes, and `content-format-lock.json` (11 `docs/content-schemas` entries) and `game-contract-lock.json` (2 fixture entries) were frozen from those CRLF bytes. Every fresh LF checkout, worktree and CI run therefore failed `Tools/verify_content_format.py` and `Tools/verify_game_contract_lock.py`, which in turn blocked `Test-Generation.ps1`, `Test-Game.ps1` and `Compile-GameContent.ps1`.

Decision: lock hashes are computed over the repository's canonical LF bytes. The integrator re-checked out the clean CRLF files as LF and replaced only those 13 values. A one-off script refused any entry whose previous hash was not exactly sha256 of the same file with LF→CRLF, so no content, format version, contract version, schema or fixture changed. C0 `contract-lock.json` and `feature-content-lock.json` already matched LF bytes and are unchanged. Retained historical binaries and evidence keep their original identities; no artifact is relabeled.

Validation: both guards PASS on a fresh LF clone and on the normalized integrator checkout. Working copies must stay LF (`git ls-files --eol` shows no `w/crlf`); a tool that writes CRLF reintroduces drift and must be fixed rather than refrozen.


## ADR-041 — Accepted: Inno Setup per-user installer and in-place upgrade (T073)

Date 2026-11-14; owner chose Inno Setup and authorized T073 implementation and agent dispatch in the session. Implements the [installer contract](features/windows-installer.md).

Decision: Inno Setup 6.7.3, pinned by version and `ISCC.exe` SHA256 `0a8757031b33777e4c9cbffee40f11a5062b36d25cbe144c1db73b6102b80ad7` in `Tools/installer/installer-policy.json`; `Tools/Build-Installer.ps1` refuses any other compiler. Stable AppId `{B01E0F69-2123-4D91-AB59-88864A944E39}` is frozen forever (one `HKCU\…\Uninstall\{AppId}_is1` entry per user). Per-user, x64-only install (`PrivilegesRequired=lowest`, default `{localappdata}\Programs\Shooter`, `UsePreviousAppDir=yes`), Start Menu shortcut plus optional remembered desktop shortcut; shortcuts start in `{app}` and the executable resolves content beside itself. The integrator supplies a monotonic `n.n.n` `-ReleaseVersion`; immutable binary/source/toolchain/content/contract identities are installed as `{app}\installer-identity.json` with the package's `identities.json`, `rollback-pair.json` and `toolchain-lock.json`. The installer refuses to install over a newer `DisplayVersion` unless `/ALLOWDOWNGRADE=1`, which is reserved for the tested prior-release rollback installer.

Payload: the wrapper consumes a validated Package-Release directory, never rebuilds the game, and requires a PASS clean-source `validation.json`, matching identities/toolchain/rollback pair, and every inventory hash with no extra file. Repair is rerunning the same installer (all payload files rewritten); after install, files under `Content`, `Shooter_Data` and `D3D12` that the release does not ship are removed.

User data: settings stay at `%USERPROFILE%\AppData\LocalLow\Procedural Shooter\Shooter\shooter-user-settings.json`, outside `{app}`; upgrade, repair and uninstall never touch it. An explicit "remove my data" uninstall option is not implemented.

Running game and interruption: the game has no named mutex, so there is no `AppMutex`. Interactive runs use Restart Manager `CloseApplications` (prompt, never silent kill); silent runs exit 1 if `Shooter.exe` is running and, before changing anything, probe every previous payload file for locks (exit 7). Inno does not stage and atomically swap a whole release: a crash or power loss mid-copy can leave a mixed pair that the game does not detect, and rerunning the installer is the recovery. An atomic side-by-side layout and a game mutex are deferred follow-ups. The installer is unsigned (SmartScreen warns).

Validation: `Tools/test_build_installer.ps1` covers 11 synthetic accept/reject cases. Silent exercises on the owner's PC against real 5eaced5/7cd7e81 packages: INS02 upgrade, INS03 repair, INS05 uninstall and INS06 installed rollback PASS; INS04 Partial (locked/running abort verified, mid-copy crash not rehearsed); INS01 clean machine Pending. Interactive wizard UI is not exercised. Installer success does not close REL01/REL06 or FIX13/FIX14.

## ADR-042 — Accepted: native C++ allocation counter for benchmark runs

Date 2026-11-14; owner chose this design in the session. Supersedes the `AllocationMeter` mechanism described under ADR-015; its positive-control rule is retained.

Context: the previous meter registered `il2cpp_profiler_install_allocation` with a managed reverse-P/Invoke callback and was installed in every session. When Unity first attaches a native thread to IL2CPP (the synthesized-audio thread at Main), `Thread::Attach` allocates a managed thread object; the profiler event re-entered the managed wrapper, which re-attached and allocated again. Crash-dump analysis (WinDbg, build symbols) showed 526 nested cycles overflowing the 1 MB stack: APPCRASH 0x80000001 about 4 s after start, present in the `5eaced5` playtest build and later. Verified alternatives fail in a release IL2CPP player: ProfilerRecorder "GC Allocated In Frame" is invalid (only heap-size gauges "GC Used/Reserved Memory" exist, rejected by ADR-015), and `GC.GetAllocatedBytesForCurrentThread` returns 0.

Decision: normal play never installs an allocation hook (`FoundationEntry` installs it only for `-shooterBenchmark` runs). Benchmarks use one C++ source plugin, `Assets/Plugins/x86_64/ShooterAllocationMeter.cpp`, compiled into GameAssembly for Standalone Win64 IL2CPP only (excluded from Editor and the Mono Dedicated Server). Its callback only adds the object size to a process-wide atomic and a thread-local counter; it makes no managed call, allocation or lock, so thread-attach allocations are counted and cannot recurse. C# reads `shooter_alloc_total_bytes`/`shooter_alloc_thread_bytes` via `[DllImport("__Internal")]`; synchronous benchmark semantics and main/worker/end positive controls are unchanged, and failure yields -1 with a diagnostic, never calibrated zeros. The plugin must live under root `Assets/Plugins/`: under `Assets/Shooter/…` Unity did not import it and linking failed.

Provenance: `Tools/provenance-allowlist.json` `namedNativeSources` lists exactly this path and its SHA256; `Test-SourceProvenance.ps1` accepts only listed paths with matching hashes and no extension wildcard. Any edit needs a deliberate hash update. This is code, not art.

Validation (worktree, b701468): Client/Server builds, smoke, EditMode/PlayMode, Test-Measurement (injected allocation detected) PASS; one full TDM-5v5-v1 Low run completed 128,165 frames / 10,800 ticks with allocation counters available (controls 4426/4426/4128) and no APPCRASH. One earlier identical run hit the 330 s wrapper timeout after VALIDATED without a capture and was not reproduced; watch for it in reference captures. Revalidate the `il2cpp_profiler_*` signatures and rerun the controls on any editor migration. Never restore the managed hook.

## ADR-043 — Accepted: two-stage native navigation publication

Date 2026-11-14. Amends the T067 C01 native publication path.

Context: the first native path published whole map packages inside the Unity Editor. Mono's floating-point rounding differs from .NET/IL2CPP, so `skeleton-soldier.bin` differed in the lowest bit of 12 floats; the IL2CPP player's skeleton identity check rejected every native package and no match could start (all twelve reference captures and both soaks aborted on `075c548`).

Decision: Mono never generates map content. `Compile-GameContent.ps1 -Native` first runs the pure .NET MapContent publisher; `ShooterBuild.CompileNativeGameContent` then only reads the .NET-published `collision-gameplay.bin` per map, runs the pinned native NavMesh bake plus a byte-identical repeat bake, and writes `<map>.nvb1` with a `<map>.nav-stage.json` descriptor (provenance, tool identity, collision SHA, input key, bake SHA). MapContent republishes with those bakes injected through `NavBakeStage` into `Artifacts/map-packages-native`. `NavBakeStage.InputKey` binds each bake to the raw float bits of the gameplay boxes and nav parameters; any mismatched, malformed, non-native or unknown-map entry fails publication, never falling back to software.

Guard: the wrapper requires each native package to equal its pure .NET counterpart byte-for-byte except `navigation-gameplay.bin` and the navigation hash/dependency entries in `package.json`, requires the native nav hash to differ from the software grid, and requires stage collision SHA and editor identity to match. Release checks start a real match on both maps against native content before packaging.

Validation: all `skeleton-soldier.bin` copies identical to the pure cache; native nav hashes unchanged (compound `07560a8d…`, depot `802dcfbb…`); diagnostic 5v5 matches started on both maps from native content; Generation 329, Maps 29, EditMode/PlayMode and provenance PASS. No lock, schema or format version changed.

## ADR-044 — Accepted: versioned procedural map surface publication

Date 2026-11-15, T075 owner-authorized design implementation. The presentation review found that map packages still published solid 1×1 albedos and the visual extractor filtered normal/mask companions. Reading only the first mask texel also discarded spatial response. The existing shader adapter could not by itself deliver detailed map surfaces.

Publish four fixed 128×128, two-metre-tile material sets through the existing `MaterialBaker`: concrete, metal cover, soil and asphalt, seed 460072030. Each includes sRGB albedo and linear XYZ-normal / metallic-occlusion-roughness planes with complete mips. Require all eight companion dependencies in visual packages and persisted extraction. Keep soldier/weapon generators and art unchanged. Bind URP Lit metallic from R, occlusion from G and smoothness from inverted B in A; opaque-alpha generated XYZ normals already match locked D3D11 normal decoding. Three code-created numeric materials retain the normal, mask and combined Lit variants in Client builds; Server never preloads them.

Version map.compound and map.depot from 1.0.0 to 1.0.1. Update strict recipe/package generator-version schemas, source examples, registry and catalog labels together. SGAF, package/catalog schema v1, material.surface 1.0.1, C0-v1 and Game-v1 remain unchanged. The map fingerprint changes, so collision/nav envelopes and catalog/package roots get new identities even though decoded collision, navigation inputs, seeds and gameplay rules are unchanged. Old 1.0.0 binaries/content remain a retained matching rollback pair; current strict readers reject old generator recipes and require rebuilt 1.0.1 packages. Do not relabel or overwrite historical archives. Native publication must rerun through ADR-043 and source-matched builds/packages must follow it.

This is a publication/version migration with channel-conversion and deterministic extraction tests, not new visual acceptance or measured performance. No manual payload/golden edits, soldier clearance search, imported art, shader-source change or waiver is authorized. The M11 handoff records actual validation and remaining Pending evidence. Rollback requires reverting source/schema/registry together and restoring the previous complete executable/content pair.

## ADR-045 — Accepted: compact TDM arena layouts for catalog maps (T076)

Date 2026-11-15. Owner assignment: redesign maps for 5v5/6v6.

Context: both catalog maps used the full 256 × 256 m frame (Compound: 12 identical shells and 120 1 m cubes on a grid; Depot: six bays and perimeter fences). Spawn-to-spawn distance exceeded 200 m with no lane structure, which is far too sparse for 10–12 players.

Decision: catalog gameplay comes from new arena planners (`CompoundArenaPlanner`, `DepotArenaPlanner`) through `ProductMapPlanner`. Each map is a mirror-symmetric (z = 0) three-lane arena of 88 × 104 m and 80 × 112 m inside a closed 5 m perimeter, with walled staging, three gates and baffles. `ArenaRules` enforces the numeric rules in [compact TDM map layouts](features/tdm-map-layouts.md); `MapPackageBuilder`, `ReleaseCatalogDocument` and `CompoundCatalogFactory` throw when rules or `MapTopologyValidation` fail. Gameplay collision/nav hashes come from the actual box list. Map ids, display names, seeds, the recipe/package/metadata schemas, `MapRecipe.GeneratorVersion` (1.0.1 after ADR-044), SGAF Collision v1, nav settings, the 256 m world/32 m chunk frame and every lock are unchanged; arena planner hash versions are 2.0.0. The legacy W01 `CompoundPlanner`, `DepotPlanner` and their goldens stay for historical evidence only.

Consequences: all map package identities (collision, navigation, metadata, package roots) change and must be republished with `Compile-GameContent.ps1 -Native`; earlier packaged installers carry the old layouts. Rule validation proves geometry constraints only; route feel, spawn pressure under `SpawnSafetyPolicy` and bot behaviour on the new layouts are Pending manual playtest. Rollback: revert the T076 merge and republish.

## ADR-046 — Accepted: offline product client recovers from frame hitches

Date 2026-11-15. Context: the installed 0.3.5 player quit after Resume with `Offline tick overload; simulation paused` because one frame of about 83 ms or more exceeded the four-tick cap and `FoundationEntry.Fail` exits the player. Resume also gave no rebase frame.

Decision: `FixedTickClock` gains an opt-in `dropHitchDebt` mode, enabled by `FoundationEntry` only for the non-server, non-diagnostic, non-benchmark product client. When a frame leaves whole-tick debt after four ticks, the clock drops that debt, keeps the fractional remainder, never sets `Overloaded`, and `FoundationEntry` logs one `SHOOTER_HITCH` warning (elapsed, dropped debt, serviced, nextTick). Tick numbers stay contiguous and ordered. Diagnostic, smoke, gallery, benchmark and failure-injection runs and the server keep the ADR-prior retained-debt fatal overload, so `Tools/Test-FailureShutdown.ps1` semantics and benchmark validity are unchanged. After a suspend-to-run transition (including `SetSuspended(false)` from Resume) the first `SampleActiveElapsed` returns zero.

Consequences: a long hitch costs simulated time (the game runs slower for that moment) rather than the session. Hitch frequency is recorded only in the player log; the cause of slow frames remains unexplained and benchmark evidence is unaffected. Rollback: construct the clock with `dropHitchDebt` false.

## ADR-047 — Accepted: relaxed spawn fallback and recoverable spawn failure

Date 2026-11-15. Status: accepted for the offline product build; Unity EditMode/PlayMode and real-player confirmation are Pending.

Context: [Team Deathmatch](features/team-deathmatch.md#death-and-respawn) requires a safe spawn that rejects enemy line of fire within 40 m. Stationary bots could keep every slot exposed, so a respawning player stayed on "Awaiting a safe spawn" indefinitely. When the 600-tick deadline expired, `OfflineGameSession` stopped the match silently while its phase remained Playing, which soft-locked the player.

Decision: `SpawnSafetyPolicy.SpawnRelaxAfterTicks` = 120. After 120 active ticks of failed safe-spawn search, the 40 m enemy line-of-fire rejection is dropped. The policy then selects a navigable, unoccupied slot with 2 m spacing, ranked by farthest nearest enemy, then least-recently-used, then lowest slot ID. The 30-tick retry cadence and the 600-tick failure deadline are unchanged. The match aborts only when no navigable, unoccupied slot exists by the deadline. On abort, `OfflineGameSession` records `GameErrorCode.SpawnFailure` in a new non-frozen `TerminalFault` property, moves to Stopped, and `Step` returns Ok as a no-op. `ProductShellHost` returns to the main menu with "No safe spawn available. Returning to the menu." `IGameSession` and Game-v1 are unchanged.

Consequences: this deliberately deviates from the strict spawn-safety rule. A relaxed spawn may be in enemy line of fire, and the design accepts that in exchange for no soft-lock. Strict rejection still applies for the first 120 ticks. The failure path is now a recoverable menu return with a visible message rather than a silent stop. Tests: `SpawnRelaxedFallbackTests` (3). Rollback: restore the strict policy and the silent stop, which reintroduces the soft-lock. Real-map spawn-pressure review and player confirmation are Pending.

## ADR-048 — Accepted: session-internal pre-match countdown

Date 2026-11-15. Status: accepted for the offline product build; Unity EditMode/PlayMode and real-player confirmation are Pending.

Context: the product shell needs a short readiness window after a match begins, so the player can orient before bots act and the clock runs. Adding a `GameMatchPhase` value would change the frozen Game-v1 surface and every consumer of the phase.

Decision: `SessionDefaults.PreMatchCountdownTicks` = 300 (5 s at 60 Hz), read through `OfflineGameSession.PreMatchTicksRemaining`. The countdown is internal to the session. The phase stays Playing, and no `GameMatchPhase` value or Game-v1 change is added. During the countdown the human's local input gate is closed, bots are not planned and issue no commands, no respawns are scheduled, and `EvaluateEndOfTick` does not run, so `ActiveTicksElapsed`, the match clock and the timeout stay at 0. The harness still steps with no commands, so gravity and settling continue. The HUD shows "MATCH STARTS IN N" with N = ceil(remaining/60), then "FIGHT!" during the first second of the live clock. The `OfflineGameSession` default is 0 ticks. Only `ProductShellHost` sets 300, so tests, benchmarks, `ProductTdmHost` and `ProductTdmBenchmark` are unchanged.

Consequences: the countdown adds 300 ticks to CompletedTick before the 54000-tick match clock starts, so tick numbers for a match are offset by 300 relative to the old start. Consumers that assume CompletedTick equals active time must use ActiveTicksElapsed. Tests: `PreMatchCountdownTests` (4). Rollback: set the product value to 0. Real-player timing and HUD readability are Pending.

## ADR-049 — Accepted: non-gameplay visual architecture boxes for map solids (T078)

Date 2026-11-15. Status: accepted; source merged to main at `ddce776`; package republish and player visual review Pending.

Context: catalog arenas already emit non-gameplay roof slabs, but `MapPackageBuilder.BuildSolidsMesh` skipped `!Gameplay` boxes, so roofs never appeared. Places still read as featureless collision boxes versus the VD5 / Battlefield V environment bar. Expanding gameplay boxes would churn collision/nav identities and ArenaRules ceilings.

Decision: planners may append **non-gameplay** `CollisionBox` architectural details (eaves, door jambs/lintels, window frames, baseboard/caps, curbs, canopy lips, thin industrial trim) with dedicated stable-id ranges that do not collide with gameplay ids. `BuildSolidsMesh` includes every box (gameplay and visual) so roofs and details render. Gameplay boxes (`Gameplay == true`) remain byte-identical in order, center, size, tag and stable id; collision SGAF, nav settings/bake inputs derived from gameplay boxes, spawn/topology rules and ArenaRules ceilings are unchanged. Presentation may strengthen packaged material contrast and cover overlays without importing art. Weapon generator `geometry.weapon` 1.0.1 and soldier loft/P05 stay frozen under ADR-024.

Consequences: packaged map-solids mesh and package/catalog visual identities change and must be rebuilt with `Compile-GameContent -Native` after merge; prior installers remain valid rollback pairs. Visual box count is bounded in tests (<800 extras/map) but is not an ArenaRules ceiling. Rollback: remove visual append + restore gameplay-only `BuildSolidsMesh` filter. Named environment acceptance (VD5) remains Pending.

## ADR-050 — Accepted: packaged architectural surface construction (T081)

Date 2026-11-15. Owner requested continued visual review and implementation after `615b6a5`. Sole integrator; no agent dispatch or P05 clearance search.

Context: ADR-049 includes visual boxes but the renderer still presents unbroken concrete slabs and unrecognisable concrete cargo. Adding more props did not correct the dominant shape/material language.

Decision: `MapSolidsMesh` builds loading-time-independent, offline packaged visual detail around the existing boxes: 15 mm edge radii on substantial gameplay solids; wall footing/coping/end strips and narrow panel joints; cargo corrugation, perimeter rails and door locking hardware. Wall bands extend at most 20 mm beyond wall faces; cargo hardware at most 51 mm beyond container end faces. Openings are not bridged and no new standalone occluder is introduced. Render mesh budget is 180,000 vertices/map. These are visual meshes only: gameplay box order/IDs/size/tag, collision/nav inputs, seed, spawns, sensing and hit poses remain byte-identical.

Extend the map-local visual material role enum with 3=DepotBlue, 4=OxidePaint, 5=Foundation; roles 0–2 retain their meaning. SGAF v1 already carries ushort material slots, so no format/schema/C0/Game-v1 change is required. Client and menu both bind the six-role palette; unknown or unbound roles fail closed. New materials use numeric tints of existing verified PBR planes, sharing texture uploads. Publication generates new visual hashes/catalog roots; existing native content may not be paired with a new claimed presentation review. Native republish and clean source-matched builds/package/installer are required. Soldier/weapon generator identities and all P05 defects remain frozen; weapon coating tint is a read-only presentation choice.

Consequences: at most six map draw submeshes rather than three, plus more offline mesh geometry. Reference hardware cost remains Pending, not inferred from a vertex budget. Validate gameplay fingerprints, both material-role closures, vertex budget, native publication and player views. Preserve prior 0.3.9 binary/content for rollback; reverting this implementation requires republishing a matching visual package. No VD/REL acceptance is closed.

## ADR-051 — Accepted: runtime cover probes share the AI ray budget (T082)

Date 2026-11-15. Status: accepted for the offline product build. Unity lanes and real-player confirmation are Pending.

Context: `ai.md` specifies cover candidates "from cover proxies/nav edges". The frozen product map packages (`MapGameplayDefinition`, Game-v1) carry only spawn slots and patrol/combat destinations. The only cover data, `NavCoverCandidate`, comes from the legacy Compound plan. As a result `SeekCover` never found cover in the product. Adding cover data to packages would change Game-v1 and every content identity.

Decision: `Simulation/AI/CoverFinder.cs` generates cover at runtime, relative to the threat.
- A bot that wants cover samples up to 8 points 4–7 m away, within ±80° of the direction away from the threat, nearest first.
- It keeps the first point where a ray from the threat's eye to a crouched chest (0.75 m) at that point is blocked.
- Probe rays go through `PerceptionSystem.TryBudgetedRay`, so they count against the **same** global 16 rays per tick. They run only after every bot's vision pass and are capped at 4 per tick, so vision fairness (a sensing opportunity within 12 ticks) is unchanged.
- Reachability is decided by the normal path request, within the 2 new paths per tick. A failed path releases the claim.
- Claims use quantized position ids with the high bit set in the existing `CoverReservationBoard`. They are released on leaving cover, on path failure, on death, on life replacement and on cancel.
- Explicit `CoverSite` lists still take precedence where provided.

Consequences: no contract, fixture, budget or package change. `aiRays` now includes probe rays, which stay within the cap. Cover quality is limited to what a single occlusion ray shows. There is no exposure scoring beyond the occlusion test and no peek-point validation; repositioning is driven by the last-seen position. Rollback: remove the `CoverFinder` calls. `SeekCover` then falls back to Search as before.

## ADR-052 — Accepted: packaged first-person glove and sleeve props (T083)

Date 2026-11-15. Owner requested continued visual implementation. Work starts after T082 merged main (`e2c26e9`, then docs-only `a9f228d`); no delegation.

Context: the camera shows a floating rifle/pistol without hands. Existing soldier/P05 anatomy and clearance search remain frozen under ADR-024. The equipment-relative reload arc also jumps about 10 cm at its 0.42 remove/insert boundary.

Decision: add four required SGAF mesh dependencies to each non-gameplay visual closure: rifle/pistol × trigger/support glove plus forearm sleeve. `FirstPersonHandGeometry` constructs them offline from numeric beveled palms, segmented curved fingers, cuffs and tapered sleeves, capped at 6,000 vertices/hand. Mesh coordinates bind the existing right/left weapon grips; no new soldier joint, hit pose, weapon socket or authoritative input is introduced. Material slots 0=sleeve, 1=glove, 2=protective trim reuse verified uniform/polymer/gear materials. Existing SGAF/package/catalog v1 codecs already support mesh dependencies and ushort slots. Missing/corrupt hand dependencies fail closed and require repair; old binary/content pairs remain valid, but new binaries require the rebuilt closure. Server/gameplay profiles contain no hand assets.

Presentation attaches the two active meshes to the existing camera-local weapon root, swaps them with rifle/pistol, disables their shadow casting, hides them with dead/session viewmodels and releases the camera-owned root with its owner. Support-hand reach is an analytic read-only reload-phase offset toward the existing magazine location, with reduced-motion scaling and exact rest endpoints. The remove/insert path now shares the same boundary value and settles smoothly to zero. No authority timing/ammo/reload rules change. This is rigid camera-prop pose animation, not a full articulated hand/finger rig or a complete magazine-replacement animation.

Consequences: four additional required meshes per visual package and two active renderers / up to six material submesh draws near the camera; actual hardware cost remains Pending. Publish fresh native content, verify unchanged collision/nav/catalog identities, freeze clean Client/Server/package identities and build an upgrading installer. Validate mesh bounds/sight clearance, corruption rejection, motion continuity, slot/life/reset/pause/teardown and real-player rifle/pistol/ADS/reload appearance. P05 FAIL and all VD/REL exits remain open. Rollback the whole closure/presentation change and use a matching retained binary/content pair.

## ADR-053 — Accepted: procedural near-field finish triplets and hip composition (T084)

Date 2026-11-15. Owner requested further visual improvement after 0.3.11. Sole integrator, base `12ff5ac`; no agents or new soldier/P05 clearance iteration.

Context: weapon, gloves and sleeves use flat color materials; a single conical sleeve and squared hip-held stock dominate the near field. Texture response must be visible in the actual player without importing art or altering weapon/soldier generators.

Decision: `FirstPersonSurfaceBaker` numerically constructs four 128px, 100 mm tiling albedo/XYZ normal/metallic-occlusion-roughness triplets: coated metal, stippled polymer, woven cloth and glove grain. Complete mip chains use the existing verified `MaterialBaker.BuildPlane` filter; the frozen material.surface 1.0.1 generator is unchanged. Weapon material paths retain their names but become spatial triplets; two additional sleeve/glove material paths and all companions become required visual dependencies. Publisher validates dimensions, channels and mip closure. Existing SGAF/package/catalog v1 supports these material bytes; all new visual roots are republished and paired with a clean binary. New binaries reject old incomplete closures; no runtime bake/fallback is added.

The product binds the verified PBR triplets through `ProductSurfaceMaterial`, with metric 10 repeats/metre and mask-driven smoothness/metallic. Body/gear soldier materials remain unchanged. Sleeve-only geometry uses one continuous ring loft with shallow folds, analytic normals and a bent taper, retaining all glove/finger/grip coordinates and the 6k vertex cap. Static hip composition uses bounded yaw/roll and fades exactly to identity at full ADS, preserving the verified sight ray and weapon dimensions. Reduced-motion keeps this resting composition while existing bob/recoil suppression remains.

Consequences: about 1 MiB of raw complete 128px RGBA plane data per four-family set (mask and occlusion share uploads), plus small sleeve geometry growth; actual GPU residency, driver overhead and reference cost remain Pending. Gameplay/collision/nav/spawn identities, weapon meshes/sockets and soldier/P05 identities remain frozen. Run pure/Unity/provenance guards, native publication, clean builds/package/installer and player review. No visual/performance/REL gate closes from software checks. Rollback the required closure and consumers together, using the retained 0.3.11 binary/content pair.

## ADR-054 — Accepted: bot footstep hearing, wider vision and a readable lethality envelope (T085)

Date 2026-11-15. Status: accepted for the offline product build. Real-player confirmation is Pending.

Context: an owner playtest of T082 found that bots trail each other along the route, miss enemies behind them, and kill too quickly to be understood. `ai.md` already requires hearing stimuli with source class, position and radius, and "not constant instant headshots"; it does not fix a vision cone or footstep radii. The rifle's damage and cadence are shared with the player and fixed by `combat.md`, so lethality has to be tuned in bot aim, not in weapons.

Decision:
- **Footstep hearing.** The planner derives each listed enemy's horizontal speed from consecutive planner positions. Audible radius: 24 m when sprinting (≥5 m/s), 13 m when running (≥2.6 m/s), 5 m when walking, and 2.5 m always. This is distance-attenuated like gunfire, with no occlusion and no rays, so the 16-ray budget is untouched.
- **Vision.** The vision cone is 120° (was 90°).
- **Unseen attackers.** Two hits within 1.5 s from an enemy the bot is not fighting (or one hit below 50 health) turn the bot onto that attacker.
- **Lethality envelope.** Accuracy recovery starts at the end of the reaction delay. Kept as a regression test on the real combat harness, against a still standing target (median from sighting): 1.25–2.3 s at 8 m, 1.8–4 s at 20 m, 2.7–7 s at 40 m, and no kill sooner than 0.67 s after the bot's first shot.
- **Team spread.** Destinations are chosen by a deterministic spread score instead of an id-ordered walk of the route. At most 2 allies investigate one noise. Allied gunfire never points at the ally.

Consequences: bots are slower to kill and harder to sneak up on at a run, but still blind to slow or crouched movement behind them beyond 5 m. Hearing through walls is possible, as it already was for gunfire. No contract, fixture, budget, content or weapon change. Rollback: revert T085. The previous constants are recorded in the [M11 handoff](milestones/m11-playable-release/handoff.md#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill).

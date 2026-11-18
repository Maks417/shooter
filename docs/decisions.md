# Architecture decision records

Statuses: Accepted = architectural requirement for implementation; Provisional = build as specified until evidence gate resolves; Deferred = excluded pending separate approval/design. Changes append a dated superseding record, preserving prior rationale. Decisions date from 2026-11-11 unless a record states otherwise; acceptance of a decision does not certify runtime or hardware gates.

## ADR-001 — Accepted: Unity 6 URP rasterization

Context: realistic procedural art must run on integrated Radeon. Decision: Unity 6 URP, C#, HLSL; conventional meshes and generated PBR caches, Forward baseline D3D11. Reject HDRP baseline and full-scene ray marching for compatibility/cost. Consequence: constrain lighting/overdraw and measure from M0. Revisit renderer only on measured evidence without abandoning iGPU gate. [Architecture](architecture.md), [performance](performance.md).

## ADR-002 — Accepted: zero authored visual source assets

Code/numeric recipes are source of meshes, textures, rigs and movement. Generated caches allowed with provenance, engine font exception explicit. Imported models/photos/painted textures/sprites/mocap/hand-keyed joint clips disallowed. Consequence: soldier feasibility is a milestone gate, not an outsourcing task. [Vision](vision.md).

## ADR-003 — Accepted: compiler/runtime separation

Expensive geometry/material/LOD/nav/weight generation runs in tools; runtime loads immutable versioned artifacts and runs cheap motion. Alternative generate everything on demand rejected for CPU/GPU contention and unpredictable hitches. Consequence: cache schema, migrations and cold-build CI are first-class. [Generation](generation.md).

## ADR-004 — Accepted: gameplay independent of visual quality

Canonical collision/nav/hit profiles and weapon definitions identical across profiles. No render-mesh hit detection or AI visibility derived from absent low-tier foliage. Consequence: maintain separate gameplay artifact hash and fixed gameplay pose evaluator. [Contracts](contract-details.md).

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

Offline play needs no remote transport, LAN/public UI, extra human-player capacity, T025 weapons/penetration, package upgrade, new asset source or imported art. It retains authoritative 60Hz simulation, presentation-only quality options, procedural provenance and native720p Low30 reference requirements. Old waivers and the P05 FAIL stay intact; new milestone exits require actual player/visual/hardware evidence where specified and cannot automatically inherit waivers. This task prepares docs only; M8–M11 remain unassigned until future implementation assignment. See [ordered review](next-playable-build.md) and [packs](milestones/index.md).

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

Addendum 2026-11-15 (identity-only refreeze, owner-requested): commit `42ae931` (ADR-056/057 binding defaults) froze the new `docs/game-fixtures/user-settings-last-weapon.valid.json` entry in `game-contract-lock.json` from a CRLF working copy (`b8976f3f…8328`), so `Tools/verify_game_contract_lock.py` and `Test-Game.ps1` failed on every clean LF checkout from then on, including after ADR-072 (`334812b`). All 12 `sources` and 7 `fixtures` entries were re-checked against their committed LF blobs; only that one differed. It was replaced with the LF value `966b8455…4064` by the same guarded rule as above (the old hash had to equal sha256 of the committed blob with LF→CRLF, otherwise refuse). No fixture, source, schema, settings layout or contract version changed. Rollback restores the prior value, which fails on LF checkouts.


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

Consequences: bots are slower to kill and harder to sneak up on at a run, but still blind to slow or crouched movement behind them beyond 5 m. Hearing through walls is possible, as it already was for gunfire. No contract, fixture, budget, content or weapon change. Rollback: revert T085. The previous constants are recorded in the [M11 handoff](milestones/m11-playable-release/handoff-archive.md#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill).

## ADR-055 — Accepted: generated PCM combat sample banks (T086)

Date 2026-11-15. Status: accepted for the offline non-commercial product build. Named device hearing (FIX10) remains Pending.

Context: owner quality direction for visuals already targets Battlefield V–class feedback. Shipping audio was a ≤24-voice sine+noise analytic synth (T030/T061) that reads as beeps, not weapon reports. `vision.md` baseline was synthesized sound with an explicit gate for recorded/external sound via provenance policy update. Owner authorized sample-bank combat audio for non-commercial use, with banks found or generated rather than purchased licensing workflow.

Decision:
- **Provenance.** Presentation may ship a deterministic **generated PCM combat audio bank** (recipe-baked mono clips + SHA-256 content hash). Drop-in found PCM is allowed only when recorded in the bank manifest with source note and hash; commercial license purchase is not required for this non-commercial build. Extracting audio from third-party commercial games (including Battlefield V) is out of scope and not used.
- **Mixer.** `SynthAudioMixer` plays bank samples (not live sine tones) with distance attenuation, stereo pan, distance high-frequency rolloff, peak limiter, and master/effects/UI gains. Voice pool remains ≤24; one authority cue maps to one voice (layers are pre-composited in the bank).
- **Cues.** Rifle and pistol shots, impact, damage-taken, footstep, reload start/complete, death, UI, and a quiet looping ambient bed. Reload/death/damage map from existing `CombatEventKind` values; weapon id selects rifle vs pistol shot.
- **Fallback.** If a bank fails to load, the mixer may synthesize a degraded analytic fallback so playability is preserved; FIX10 still requires named audible review of the sample path.

Consequences: audio CPU and memory grow with bank size; design still targets the 0.5 ms/frame mix budget as a soft host target. Docs (`vision`, `game-shell`, `combat`, [features/audio.md](features/audio.md)) and T061/FIX10 wording update to sample-bank policy. Rollback: revert T086 and restore the prior analytic-only mixer.

## ADR-056 — Accepted: last-weapon quick switch (Q) and local-only muzzle overlay

Date 2026-11-15; owner playtest feedback requested FPS-style Q last-weapon toggle and reported full-screen flicks while bots fired nearby. Sole integrator owns the bounded input/settings/presentation fix; no agents, art import, package upgrade or acceptance waiver.

Context: cosmetic `muzzleFlashRemaining` overlay and “Shot” subtitle fired on every `ShotAccepted`, including remote bots, painting a full-screen flash while the local player was idle. Weapon selection already used Slot1/Slot2 (1/2) with no previous-weapon action.

Decision — local muzzle overlay: `FirstPersonHitFeedback.IsLocalShot` gates the IMGUI overlay and shot subtitle to the local human instigator. World analytic muzzle/impact/smoke proxies and shot audio continue for all `ShotAccepted` events. ReducedFlash still suppresses the local overlay and world muzzle meshes.

Decision — LastWeapon binding: additive optional `LastWeapon` defaults to `Key.Q`. Game-v1/settings schema 2 layout is unchanged. Client-side `LocalPlayerInput` tracks the previous slot; a press edge swaps the selected slot with that previous slot and writes the result into the existing C0 `InputCommand.SelectedSlot`. Slot1/Slot2 still set an absolute slot and update the previous slot only when the active slot actually changes. On life reset, previous slot initializes to the opposite of the active spawn slot so the first Q is useful with two weapons. Holding Slot1/Slot2 takes precedence over LastWeapon in the same frame. Normalize legacy settings by adding `LastWeapon=Key.Q` when free, or `LastWeapon=Unbound` when Q was already customized; preserve existing actions and never steal a control. A legacy binding table already at the schema count limit is preserved without adding a row; Restore defaults makes LastWeapon available in that exceptional case. Settings UI exposes LastWeapon rebind/swap/defaults labeled “Last weapon”.

Validation: pure settings migration/conflict/persistence tests, EditMode input last-weapon edge/life-reset tests, local-shot overlay unit test, locked pure suites and provenance. Interactive two-map device evidence remains Pending. Rollback restores the prior Game-v1 lock/fixture pair and presentation gate; older players keep their retained binary/content/controls.

## ADR-057 — Accepted: knife and grenade loadout slots (T087)

Date 2026-11-15; owner-directed playable combat expansion so a player can still fight when rifle/pistol ammo is empty. Sole integrator owns the C0 prediction/slot migration, combat authority, settings bindings, AI, procedural visuals and verification. No acceptance waiver.

Context: C0-v1 froze `Require.Slot` to `0..1` and prediction schema 1 to exactly two `WeaponState` records. Melee and grenades were deferred. Offline TDM needs knife (Digit3) and one cook/throw grenade per life (Digit4) for humans and bots.

Decision:
- Keep `ContractVersion.Current = "C0-v1"`. Bump `PredictionSchema` **1 → 2**. `Require.Slot` accepts `0..3` (rifle/pistol/knife/grenade). Prediction `WeaponSlots` count is **4**. Add `WeaponAction.Cooking = 5` for grenade pin/cook. Migrate `docs/fixtures/prediction-state.json` and `gameplay-defaults.json`; refresh `contract-lock.json` after reviewed source/fixture identity change.
- Knife: short-range authoritative hitscan (1.8 m), base damage 55, interval 24 ticks, equip 12, no ammo consume, may fire while sprinting.
- Grenade: magazine 1 / reserve 0 per life; cook on FirePressed, throw on release, fuse 120 ticks from cook start; held cook that expires detonates at the thrower (self-damage yes, friendly damage still 0). Throw uses reserved `ProjectileSpawned`/`ProjectilePayload`, gravity arc, bounce damp, explode on fuse with 5 m linear falloff to max 100 damage. Respawn refills one grenade.
- Game-v1 settings schema 2 unchanged; additive optional `Slot3=Key.Digit3` / `Slot4=Key.Digit4` with the same legacy normalize pattern as Slide/LastWeapon. LastWeapon previous-slot spans all four loadout slots.
- Bots switch to knife when guns are dry at close range and may spend their single grenade once at medium visible range.
- Procedural knife/grenade meshes publish through the existing map visual closure; package identities change and require native republish for player review.

Consequences: offline restoration/prediction fixtures and all two-slot weapon assumptions must migrate together. Historical N1 online prediction remains deferred and must not claim schema-2 wire compatibility without a later protocol ADR. Rollback restores the prior contract-lock/fixture/prediction-schema-1 pair and two-slot combat kit.

Validation: Contracts/Simulation/Game/Generation/Maps pure suites, Unity EditMode slot/input/visual cases, provenance, docs/fixtures/manifest, native content + Client/Server when packages change. Manual gameplay and device audio remain Pending.

## ADR-058 — Accepted: minimap self marker and temporary gunfire reveal

Date 2026-11-15; owner playtest reported the tactical map did not show the local player and asked that enemies who are shooting appear on the minimap. Sole integrator owns the bounded presentation/HUD/docs fix. No contract, sensing, AI, content-format or acceptance waiver.

Context: T074/brandbook required a north-up friendly-only map. The local marker was a `TacticalUi.Line` chevron inside `GUI.BeginGroup`, which commonly clips under IMGUI matrix draws so the self pip vanished while ally `Fill` squares remained. Enemies were never plotted.

Decision:
- Draw the local player with Fill-based center pip and Fill facing tick (no Line inside the map group).
- On enemy-team `ShotAccepted` (not local, not ally), store a short-lived presentation blip from `ShotPayload.Origin` XZ in a fixed 8-slot ring (2.5 s hold, 0.5 s fade). Refresh on repeated shots. Plot coral plus/diamond Fills inside the existing 42 m plate only while the blip is live.
- Silent enemies stay hidden. Do not stream live enemy poses, do not use AI Heard/LKP, and do not change combat authority or sensing.
- Brandbook / visual brief language becomes “friendly + temporary gunfire reveal” rather than absolute friendly-only.

Validation: pure `CombatHudVisual` filter/TTL/ring tests, Simulation suite. Interactive player review remains Pending. Rollback reverts the presentation ring and restores the prior Line chevron / friendly-only wording.

## ADR-059 — Accepted: personal scoreboard Score and combat score badges

Date 2026-11-15; owner-directed personal points for scoreboard/results and Battlefield-style combat HUD feedback. Sole integrator owns the bounded Game-v1 / session / presentation change. No win-condition change, art import or acceptance waiver.

Context: Game-v1 `ParticipantState` carried only kills/deaths. Team win remains 150 enemy kills or 15:00. Owner wants personal Score (kill = 100; non-killer damage contribution = HP dealt that life) on Tab/results, plus procedural kill ribbon / center score feed / ammo +N badges. Assists were previously listed as out of scope for TDM progression; this ADR adds scoreboard Score only, not streaks or progression systems.

Decision:
- Add additive `uint Score` on frozen Game-v1 `ParticipantState` (constructor + snapshot/results rows). `Team1Kills` / `Team2Kills` / `ScoreLimit` / finish ordering unchanged.
- Kill credit: valid enemy killer still gets `Kills++` and one team point; that killer also gets **exactly** `Score += 100` (`GameContractVersion.PersonalKillPoints`). Killer does not also receive their damage sum.
- Assist: session accumulates `DamageApplied` amounts per victim life → attacker; on `Death`, every damager except the credited killer receives `Score +=` their accumulated HP. Environmental/no-killer deaths award damager HP points only (no 100). Friendly damage remains 0.
- C0-v1 combat payloads unchanged. Presentation badges consume an internal session local-award ring (not a new Game-v1 DTO). Ribbon/feed are procedural IMGUI (no sprites).
- Scoreboard/results sort: Score desc → Kills desc → Deaths asc → ParticipantId asc.

Validation: `game-contract-lock.json` refreeze; `Tools/Test-Game.ps1`; Simulation HUD/scoreboard tests; docs/manifest. Interactive badge readability remains Pending. Rollback restores the prior Game-v1 lock/fixture pair and prior K/D-only UI.

## ADR-060 — Accepted: local authoritative tightness (stamina)

Date 2026-11-15; owner reported infinite sprint on held Shift and asked for a tightness scale that gates run, slide and jump, restoring fully in 5 s while walking. Sole integrator owns bounded motor / local movement view / HUD / tests / docs. No C0/N1/Game-v1 freeze, online prediction, settings binding or acceptance waiver.

Context: `docs/features/combat.md` deferred stamina until movement was stable. ADR-039 slide already proved the offline pattern: authority state on the motor, readout on `LocalMovementView`, C0 snapshots unchanged. Sprint remained uncapped.

Decision:
- Add authoritative `MotorBodyState.Tightness` in `[0, 1]` (spawn/respawn = 1). Recipe constants on `MotorConfig` (ADR-039 style): continuous sprint empties in **8 s**; walk regen fills in **5 s**; jump costs **0.25**; slide entry costs **0.40**. Slide cooldown from ADR-039 stays independent.
- Gate in `KinematicMotor.Step`: sprint speed/`SprintActive` only while tightness &gt; 0 after this tick’s jump/slide costs; jump and slide entry require their flat costs; empty pool → walk only.
- Regen when grounded, not sliding, and Sprint is not held (walk/idle/crouch-walk). Holding exhausted Shift stays at empty until release. No regen mid-air or during an active slide.
- Expose `LocalMovementView.Tightness01` for a compact combat HUD bar. Do not extend `EntitySnapshot` / `InputCommand` / settings schema.
- Bots share the motor gate automatically; no AI blackboard change required for correctness.

Validation: pure motor drain/regen/cost/exhaust cases in `Tools/Test-Simulation.ps1`; docs/manifest. Interactive feel and installer identity remain Pending. Rollback removes the motor field/gates and HUD bar.

## ADR-061 — Accepted: geometry.weapon 1.0.2 mechanical parts and denser fire FX (T088)

Date 2026-11-15; owner-authorized VD3 weapon visual overhaul (silhouette + mechanicals + fire FX). Sole integrator owns weapon generator/recipes, map visual packaging, first-person/world presentation and analytic FX. No C0/Game-v1 combat change, no soldier/P05 work, no imported art or authored clips.

Context: `geometry.weapon` 1.0.1 baked magazine and bolt/slide into the body LODs. Reload was whole-gun + support-hand offset; fire FX were thin analytic spheres. VD3 requires detachable magazines, fire-cycle parts and denser combat feedback while preserving sockets and collision proxies.

Decision:
- Bump `geometry.weapon` **1.0.1 → 1.0.2**. Reject 1.0.0 and 1.0.1. Migrate registered recipes and add `docs/feature-fixtures/rifle-1.0.2.json`. Socket transforms, units, seeds, collision mechanical dimensions and C0/SGAF stay unchanged within existing tolerances.
- Body LODs gain richer silhouette detail; **magazine** and **cycle** (rifle bolt / pistol slide) emit as separate packaged meshes (`mesh-*-magazine.bin`, `mesh-*-cycle.bin`) and generator variants `magazine` / `cycle`.
- Presentation binds body + mag + cycle for FP and world held weapons. Mag follows `FirstPersonMagazineTravel`; cycle opens on `ShotAccepted` and closes analytically (~90 ms). Reduced-motion scales cosmetic travel. Knife/grenade slots omit these parts.
- Extend `AnalyticFxPool` with `ShellCasing` and `Tracer`. Densify muzzle (secondary flash lobe). `ReducedFlash` suppresses muzzle and tracer meshes plus the local overlay; casings/impact/smoke remain.

Validation: Generation/Simulation/Maps pure suites, Unity EditMode/PlayMode where applicable, feature-content-lock refresh, native content republish, clean Client/Server/package/installer. Named visual acceptance and reference cost remain Pending. Rollback restores 1.0.1 recipes/lock pair and prior presentation/FX.

## ADR-062 — Accepted: stereo found/authored combat WAV bank and per-kind distance (T089)

Date 2026-11-15; owner reported synthetic/beep combat audio and map-wide bot footsteps. Sole integrator owns Presentation/Audio, Content/Audio packaging, pure tests and docs. Extends [ADR-055](#adr-055--accepted-generated-pcm-combat-sample-banks-t086). No C0/Game-v1 freeze, no FMOD/Wwise, no commercial-game extraction, no acceptance waiver.

Context: T086 shipped a mono recipe bank with a soft `1/(1+0.05d)` atten and a single 80 m cull for every cue, so footsteps stayed audible across the map. Owner directed **stereo real-sample quality** (not MIDI/oscillator focus).

Decision:
- **Product path.** Ship interleaved **stereo 48 kHz** WAV clips under `Content/Audio/combat/` with `manifest.json` (kind, variation, file, sha256, source, license). Bank id `combat-audio-bank-v3-stereo`. Client/Package-Release copy the folder beside the player; WAVs stay outside `Assets` so they are not treated as imported art.
- **Mixer.** Play authored L/R with distance gain + HF rolloff and a **mild azimuth balance** (do not mono-collapse then pan). Generated stereo Foley remains emergency fallback when manifest/files/hash fail.
- **Distance.** Per-kind ceilings: footsteps **18 m** with steep atten + 0.45 base gain; reload/death/impact **35 m**; gunfire/headshot **80 m**; UI/ambient listener-local.

Validation: `Tools/Test-Simulation.ps1` audio cases; docs/manifest. FIX10 named device hearing and installer identity remain Pending. Rollback: revert T089 and restore the T086 mono/soft-distance path.

## ADR-063 — Accepted: hit-region damage coefficients (one-shot head)

Date 2026-11-15; owner requested clearer body-part lethality: headshot = death, chest needs several hits, limbs need more. Sole integrator owns `Simulation/Combat/DamageMath`, combat docs/fixtures sync and pure combat tests. No C0 region-ID change, no base rifle/pistol/knife/grenade damage or fire-rate change, no T085 bot-aim retune, no new hand/foot hit volumes.

Context: prior defaults were head×2 / torso×1 / limb×0.75 (rifle head 60 → two shots; limbs ~5). Owner feel target for rifle: **1** head / **3–4** torso / **6–7** limb shots on 100 HP. Region IDs remain Head=1, Torso=2, Arms=3–4, Legs=5–6; hands/feet share limb capsules.

Decision:
- Set `DamageMath.RegionMultiplier` to **head×4**, **torso×1**, **limbs×0.5**. Rounding rule unchanged (`floor(base×mult+0.5)`, min 1).
- Rifle outcomes: head 120 (one-shot), torso 30 (~4 shots), limb 15 (~7 shots). Pistol head also one-shots (100); pistol limbs take more shots — acceptable; primary feel is rifle.
- Sync [gameplay baseline](gameplay-baseline.md) and [combat](features/combat.md). Do not edit frozen C0/Game-v1 locks or `gameplay-defaults.json` weapon base damage.

Validation: `Tools/Test-Simulation.ps1` combat rounding cases; docs/manifest. Named player feel and matching installer remain Pending. Rollback restores ×2 / ×1 / ×0.75.

## ADR-064 — Accepted: knife/grenade FP special actions and throw presentation (T087 follow-up)

Date 2026-11-15; owner reported knife/grenade reused gun animations and grenades could not be thrown visually. Sole integrator owns bounded combat polish + presentation/FX/audio/Game.Contracts lifecycle field. No C0 event-kind change, no SoldierMotion melee choreography, no imported clips.

Context: ADR-057 shipped authority knife melee and cook/throw grenade, but FP still applied gun feedback (muzzle/rifle audio) and never rendered `ProjectileSpawned`, so the held grenade stayed in hand.

Decision:
- Authority: rising-edge cook/melee after equip (`fireHeld && !PreviousFireHeld`); clear `PreviousFireHeld` on equip→Ready so hold-through-draw still cooks/slashes once.
- Game-v1 additive `ActorLifecycleState.WeaponAction` mirrored from `EntitySnapshot.Weapon.Action` (refreeze `game-contract-lock.json`).
- Presentation: `FirstPersonSpecialActions` slash vs close-range stab (same-tick body Impact/Damage); grenade cook raise from `WeaponAction.Cooking`, throw arc + hide held prop on `ProjectileSpawned` / empty magazine; `CosmeticGrenadeFlight` gravity arc until nearby Impact.
- FX/audio: skip knife muzzle/overlay/gunfire reveal; map knife ShotAccepted to impact cue; soft UI cue on ProjectileSpawned.

Validation: Simulation knife/grenade/view/FX tests; Game lock verify; docs/manifest. Interactive player/installer Pending. Rollback restores prior lifecycle DTO/lock and gun-path FX for knife.

## ADR-065 — Accepted: screen-space PiP rifle scope (blurred periphery)

Date 2026-11-15; owner reported rifle ADS covered the whole screen with an opaque black tube. Sole integrator owns Presentation optic math, a lightweight blit presenter/shader, IMGUI housing/reticle and docs. No C0/Game-v1 change, no second optic camera, no URP Volume/DOF stack, no acceptance waiver.

Context: T088 shipped a procedural circular mask that filled outside the lens with near-black while blending the whole gameplay camera FOV tighter. That matched an analytic tube but not the desired picture-in-picture look (sharp zoomed lens, live blurred unzoomed world outside). Brandbook still forbids recursive render-to-texture optics.

Decision:
- **Single-camera screen-space PiP.** Keep hip FOV on the gameplay camera for rifle ADS. Magnification is digital UV zoom inside the lens (`ads/hip` ratio, same 42/80 cosmetic). Outside the circle: separable blur of the hip-FOV frame. Soft analytic housing rim in the composite + thin IMGUI ring/mil-dot. No opaque full-screen black fill.
- **Pistol unchanged.** Iron sights keep real camera FOV blend + edge vignette.
- **Accessibility.** `ReducedCameraMotion` keeps hip FOV and skips blur/digital zoom; housing/reticle may still fade in.
- **Shipping.** `Shooter/AdsScopePip` is preloaded beside `ProceduralSurface`; ADR-013 null `postProcessData` stays. Not a stock URP Volume feature.

Validation: Simulation pure optic tests; provenance for new shader/presenter; docs/manifest. Named player optic review remains Pending. Rollback restores black-tube `AdsScope` fills and whole-camera rifle FOV blend.

## ADR-066 — Accepted: non-commercial UI typeface bank (Barlow Condensed)

Date 2026-11-15; owner authorized custom UI fonts for this non-commercial build (“any fonts”) so shell/HUD copy can leave the default Unity IMGUI face. Sole integrator owns Presentation font loading/`TacticalUi`, provenance allowlist entries, map visual closure font token, Package-Release license prose and docs. No C0/Game-v1 freeze, no TextMeshPro, no EA/BFV typeface extraction, no acceptance waiver.

Context: ADR-002’s narrow `engine-allowlist-v1` exception covered only the engine IMGUI font. Player-facing menus still looked like default Arial while copy was partly technical. The combat audio bank (ADR-062) already shows how non-geometry media can ship with license + provenance outside the “imported art” ban.

Decision:
- **Named OFL bank.** Ship Barlow Condensed Regular / SemiBold / Bold under `UnityProject/Assets/Shooter/Presentation/Fonts/Resources/` as Unity TrueType fonts, loaded by `UiFonts` via `Resources.Load`. Display (≥22 px) uses Bold; body uses Regular; buttons use SemiBold. Engine font remains the silent fallback if a face fails to load.
- **Provenance.** `Tools/provenance-allowlist.json` gains hash-pinned `namedFontSources` (`.ttf` only under that Fonts path). `Test-SourceProvenance.ps1` validates those hashes the same way as ADR-042 native sources. OFL text lives at `Content/Fonts/OFL-BarlowCondensed.txt` (outside Assets) and is summarized in Package-Release `LICENSES.txt`.
- **Closure token.** Map visual `font-exception.json` records `fontException: ui-typeface-bank-v1` (still retained by the player; policy remains procedural UI, not authored scene art).
- **Copy.** Player-facing shell/HUD strings move toward short military FPS language via `InsertionScreenCopy` / `ShellUiCopy`; diagnostics stay out of chrome.

Validation: provenance PASS with the three TTFs; Generation font-closure assertion; Simulation shell copy tests; docs/manifest. Named 720p readability review remains Pending. Rollback restores engine-only IMGUI font + `engine-allowlist-v1`.

## ADR-067 — Accepted: soldier motion.v5 slide pose, gait stability and motor corner anti-launch

Date 2026-11-15; owner reported flicky player/bot models, bad sprint feet, missing slide animation, and sprinting near wall/floor “texture angles” launching the actor into the sky. Sole integrator owns `KinematicMotor`, `SoldierMotion`, `LocalMovementView` / presentation wiring, pure tests and docs. No C0/N1/Game-v1 wire freeze, no loft/P05 clearance search (ADR-024), no imported clips, no camera roll during slide.

Context: ADR-039 slide is capsule/eye crouch only; presentation forced crouch-idle plants while the root slid. Gait speed came from `ΔFeet / unscaledDeltaTime`, which flipped walk↔run at 3.5 m/s and reset foot contacts. PhysX/box corner normals projected airborne sprint into +Y after momentary ground loss.

Decision:
- **Motor.** After `SlideMove`, clamp `velocity.Y ≤ 0` unless an active jump ascent owns the tick. Non-walkable contact projection must not increase upward velocity. `TryStep` requires a real rise (`> ε`) and rejects zero-rise false steps. InitialContact Skin lift (0.3.5) stays.
- **Motion v5.** Bump identity to `soldier.motion.spike.v5`. Add `SoldierMotionInput.Slide` (0..1) with a cosmetic dive (low pelvis, pitched torso, lead/trail feet tracking the root). Walk/run hysteresis (`RunEnterSpeed` 3.8 / `RunLeaveSpeed` 3.2); slightly clearer run duty/clearance.
- **Presentation.** `LocalMovementView` carries authoritative horizontal speed and locomotion distance. `ProductActorPresentation` drives gait from that readout, passes `Sliding` into motion, and lightly blends displayed feet toward authority feet (hit capsules remain tick-snapped). First-person camera stays ADR-039 (no slide roll).

Validation: Simulation motor corner/step suites; Generation SoldierMotion slide/hysteresis cases; feature-content-lock `motionVersion` refresh; docs/manifest. Named player/bot pose review and installer remain Pending. Rollback restores motor clamps, motion.v4 and prior presentation gait path.

## ADR-068 — Accepted: procedural arsenal visual profiles and finishes

Date 2026-11-15; owner assigned visual improvement of every weapon with Battlefield V as a quality reference. Sole integrator owns the bounded weapon geometry, presentation materials, packaging recipes, migration, tests and evidence. No imported meshes/textures, combat changes, soldier clearance search or acceptance waiver.

Decision: geometry.weapon advances 1.0.2 → 1.0.3. Retain old fixtures and reject old recipes rather than regenerate changed bytes under old identities. Rifle/pistol use closed numeric section lofts, a slotted rifle cage, separate fasteners/recesses, muzzle collar and a faceted pistol slide. Existing detachable magazine/cycle semantics remain. Knife gains a ground diamond blade and tapered tip; grenade gains an upright ovoid shell, bent spoon and closed pull ring. Map visual payloads remain content-hashed under the existing map package format; gameplay map roots are unaffected. Weapon material slots are 0 coated metal, 1 polymer, 2 machined steel, 3 dark recess/rubber, 4 olive enamel. Three additional 128² procedural albedo/normal/mask triplets supply machined steel, recess/rubber and enamel through the existing map visual closure, alongside coated metal and polymer. All product finishes retain spatial roughness/metallic masks; no runtime asset generation. All six body/magazine/cycle renderers consume the palette. Gallery rendering must also provide all five slots.

Sockets, collision dimensions, seeds, C0/Game-v1/SGAF and four LOD ceilings stay fixed. Validate topology, winding, tangent frames, deterministic bytes, migration, detached-part totals and package round trips. Named first-person/world visual review and reference performance remain Pending; this is an incremental procedural improvement, not a claim of Battlefield V equivalence. Rollback requires restoring 1.0.2 source/recipes, old palette and matching packages together.


## ADR-069 — Accepted: articulated near-field hands and global analytic reflections

Date 2026-11-15; owner authorized the next visual pass: hands/handling and lighting, then weapon shape refinement, and subsequently requested a combined next installer with the concurrent map/AI work. Sole integrator owns this bounded camera-hand/weapon/lighting slice. Soldier anatomy, P05 stop, hit shapes, authority, settings, editor/packages and acceptance remain unchanged.

`fp.hand.v2` adds independent 17-joint camera-only glove/sleeve rigs for all four slots. Existing SGAF v1 skin/skeleton codecs carry eight hand meshes and eight skeletons in the verified map visual closure; old rigid packages fail required closure/skin validation. Fingers release/regrip through continuous reload windows and the trigger finger follows accepted-shot cycle. Wrist reach deforms the sleeve while its elbow remains anchored. Two skinned hand renderers replace two rigid ones. Magazine motion waits until the support hand reaches the well, seats before the fingers return, and no longer translates the entire weapon along the magazine path. Special slots have their own numeric grips. Slot/life reset restores bind transforms. No runtime mesh generation, imported clips, new soldier joints or gameplay hit data.

`geometry.weapon` advances 1.0.3 → 1.0.4 for selective two-segment grip/frame/magazine bevels and a rounder near-field barrel. Preserve 1.0.3 fixture bytes and reject old recipes; sockets, collision dimensions and combat rules stay fixed.

One generated 64² RGBAHalf cubemap with a full mip chain is the global reflection fallback. Analytic sky/ground and a bounded sun lobe cover positions outside the existing 48 m local probe, without per-frame capture or an additional light. This approximate mip-filtered environment is not a GGX prefilter, baked GI or screen-space reflection. Disposal restores the previous reflection owner/mode. Outdoor fill/key balance and bounded shadow bias improve material and ground-contact readability. Validate deterministic skin/package bytes, actual hand deformation and resource lifetime, reflection lifecycle, full Unity lanes and a clean combined installer. Reference performance, named visual review and all release/P05 acceptance remain Pending.

## ADR-070 — Accepted: bot tactics tranche (cover peek, wall hearing, ledge strafe, route spread) (T090)

Date 2026-11-15; owner approved the bot-AI improvement plan (Phase 1 tactics). Sole integrator owns `Simulation/AI/**`, pure AI tests and AI/docs handoff. No C0/Game-v1 freeze change, no ray/path budget raise, no difficulty selector, no weapon damage/rate change, no map cover-proxy bake (deferred).

Context: T082/T085 delivered readable aim, footsteps, spread and cover probes, but documented limits remained: cover was first-hidden without peek scoring; hearing ignored walls; strafe had no ledge sample; spread ignored ally path corridors. Owner feel review is still Pending.

Decision:
- **Cover (peek).** Runtime probes still share the ≤16 AI-ray budget (≤4 probe/peek rays per tick after vision). A candidate must hide a crouched chest. Prefer a left/right peek sample with clear LOS to the threat eye; keep the first blind-but-hidden point as fallback if no peekable site is found before candidates exhaust (`Simulation/AI/CoverFinder.cs`).
- **Hearing (walls).** Gunfire uses a direct ear→source occlusion sample (not charged to the 16 vision rays) so `ObserveGunfire` stays synchronous. Footsteps beyond presence use leftover budgeted AI rays at most once per bot per 12 ticks (fail-open when deferred; last result reused while rate-limited). Presence (≤2.5 m) always passes.
- **Strafe (ledge).** Before committing a strafe step, sample ground under the look-ahead foot with a downward collision ray (not the vision budget). Flip or cancel when the drop exceeds ~0.55 m or the probe has no footing while the bot's own feet still do. Open/diagnostic worlds with no ground fail-open.
- **Spread (routes).** `ChooseSpreadDestination` also penalizes destinations within ~9 m of an ally's remaining path corners (blackboard corners only; no new path solves).
- **Budgets / contracts.** Global ≤16 AI rays / ≤2 new paths per tick, `AiNoise` determinism, Game-v1 `ITeamBotPlanner`, and T085 lethality envelope stay unchanged.

Validation: new `BotTacticsT090Tests` plus existing AI suites; `AiRaysUsedThisTick` ≤16. Named player feel, `SHOOTER_AI_SUMMARY` on product maps, AI CPU 1.5 ms and installer remain Pending. Rollback: revert T090; prior T082/T085 behaviour.

## ADR-071 — Accepted: Oil Depot third map and limited elevated routes

Date 2026-11-15; owner requested a Warface Oil Depot–inspired third catalog map with walkable elevated routes. Sole integrator owns the bounded map recipe/planner, Y-aware connectivity/nav bake, catalog/UI/workload unlock, locks, tests and docs. No imported art; Compound and Depot Yard layouts stay unchanged.

Decision: release catalog expands from two to three approved MapIds — keep `training-compound` / `depot-yard`, add `oil-depot` (display Oil Depot), generator `map.oil` / `shooter.recipe.map-oil.v1`, seed `460072032`, planner `environment.oil.arena` 1.0.0. Game-v1 DTO shape is unchanged (`MapId` remains a string); `WellKnownMaps.OilDepot` and oil TDM workload rows are registered. Product shell shows three map cards.

Limited verticality: thin gameplay slabs (≤0.40 m) are walkable stair/catwalk surfaces; `CompoundConnectivity` occupancy is Y-aware so decks do not seal underpasses; ground-to-ground paths stay XZ A*; climb uses layered pathfinding when an endpoint is elevated. Native/software nav bake emit walkable sources for those slabs. Roofs remain non-gameplay (ADR-049). Motor StepHeight 0.25 m / MaxSlope 45° unchanged. ArenaRules still apply; oil layout is an 84×108 m three-lane petroleum arena with a mirrored catwalk flank.

Consequences: content-format and game-contract locks refresh; packages scale to three maps × four profiles; prior two-map installers remain valid rollback pairs. Named playtest, native publication of oil packages and reference soaks remain Pending. Rollback: revert ADR-071 merge and republish the two-map catalog.

Addendum 2026-11-15 (B02 route refreeze): the canonical compound (seed `460072030`) carries six 0.30 m gameplay roof slabs at 6 m. Under Y-aware occupancy they now count as walkable surfaces whose underside clears agent height, so they no longer seal the building interiors from ground XZ A*. The B02/B03 compound route therefore changes; collision hash, nav hash, nav-settings hash, shell/prop/bot counts and `environment.compound` 1.0.0 are unchanged. Owner approved refreezing only `routeHash` in `Benchmarks/b02-b03-m3-freeze.json` from `eb135d38…ab6` to `2a378cb0…bf05` (value computed by `B02B03ManifestTests.HashRoute`). Compound packaged patrol routes derived from `RouteCorners` change with it and are republished in installer 0.3.15. Rollback restores the prior hash together with the ADR-071 revert.

## ADR-072 — Accepted: DefinitionId weapon profiles (catalog + visual bind)

Date 2026-11-15; owner requested architecture so a new rifle model is a catalog/recipe row instead of forking slot-0 switches across Simulation, Generation and Presentation.

Context: combat used a closed `WeaponDefinition.FromId` whitelist; presentation/optics/handling/audio treated loadout **slot** as weapon identity; `geometry.weapon` only knew `kind ∈ {rifle,pistol}`. Contracts already carry `WeaponState.DefinitionId` / `ShotPayload.WeaponId`. C0 `PredictionSchema` 2 and the four loadout slots stay frozen.

Decision:
- **Slot ≠ identity.** Slots 0..3 remain primary / sidearm / melee / throwable loadout indices. Behavior and look resolve from `DefinitionId` via a frozen in-process **WeaponCatalog** (Simulation) and parallel **WeaponVisualProfiles** (Presentation).
- **Combat catalog.** `WeaponCatalog` owns combat defs + family + visual-profile key. `WeaponDefinition.FromId` delegates to the catalog. AI cadence/mag reads the equipped definition (no mirrored rifle constants). Geometry recipes still cannot override damage/ROF/mag.
- **Loadout policy.** `WeaponLoadoutPolicy` builds the four `WeaponState` records; default primary remains `weapons/rifle`. Alternate primaries (proof: `weapons/rifle-carbine`) occupy slot 0 when selected — not a fifth slot.
- **Game-v1 mirror.** `ActorLifecycleState` adds optional `ActiveWeaponDefinition` so presentation can bind meshes/optics without inferring from slot alone. PredictionSchema unchanged.
- **geometry.weapon 1.0.5.** Recipes gain required `parameters.silhouette` (`standard` | `carbine`; carbine only with `kind=rifle`). Sockets and collision mechanical dimensions stay on the numeric baseline. Map visual closure publishes profile asset keys (including carbine LOD/mag/cycle) and presentation binds by profile, not `IsRifleSlot`.
- **Non-goals (v1):** fifth slot, runtime JSON combat tables, world weapon pickups, per-gun simulator subclasses.

Validation: pure Simulation catalog/loadout/AI tests; Generation recipe/silhouette + package path tests; Presentation profile/optic resolution tests; Game-v1 and feature-content locks refreshed. Named player/installer review Pending. Rollback: revert ADR-072 merge and republish prior generator version / packages.

## ADR-073 — Accepted: life-scoped multi-kill and headshot HUD badges

Date 2026-11-15; owner requested different combat score badges for 2 / 3 / 4+ kills in a row until the local player dies, plus an extra badge on headshots. Sole integrator owns the bounded session award ring / HUD labels / tests / docs. No Game-v1 Score math change, C0 DeathPayload change, art import or acceptance waiver.

Context: ADR-059 added personal Score (kill = 100; assist = HP) and binary `ENEMY KILLED` / `ASSIST` badges, and explicitly carved out streaks and progression. Kill-feed already correlates fatal head region via same-tick `DamageApplied` without changing DeathPayload. Owner wants Battlefield-style multi-kill / headshot **HUD feedback** only.

Decision:
- Keep Score math and team win unchanged (kill still exactly 100; assist still HP; no streak bonus points).
- Session tracks a **local-human life kill streak**: increment on each credited enemy kill award; reset when the human dies or the match runtime is released.
- Enrich the internal `LocalScoreAward` ring (still not a Game-v1 DTO): Kill carries `KillStreak`; optional sibling `Headshot` kind (0 points) when the fatal same-batch `DamageApplied.RegionId` is head.
- Presentation maps Kill streak → `ENEMY KILLED` / `DOUBLE KILL` / `TRIPLE KILL` / `MULTI KILL` (4+); ribbon uses the streak kill label; Headshot is an extra mid-screen feed row only (no ribbon, no ammo +0). Assist unchanged.
- Procedural Fill ribbon/feed only; no imported badge art; not a progression, pickup or unlock system.

Validation: pure `ScoreBadgeVisual` / local award shaping tests; Game and Simulation wrappers as needed; docs/manifest. Named 720p badge readability remains Pending. Rollback removes streak/headshot award fields and restores binary Kill/Assist labels.

## ADR-074 — Accepted: compact 64 × 80 m arenas with distinct per-map layouts

Date 2026-11-15. The owner reported that all catalog maps were too big: every fight happened in the middle, and the spawn-side halves stayed empty. The owner also reported that the maps looked alike, always a box building in the middle with odd free-standing walls around it. The owner approved shrinking all three maps (Oil Depot keeping its most interesting parts) and giving Training Compound and Depot Yard new layouts. The sole integrator owns the bounded arena planners, rules, dressing, tests and docs.

Decision:
- **Size.** Every arena is 64 × 80 m with anchors at z = ±34. `ArenaRules` thresholds change:
  - playable X from 70–100 to 56–72 and Z from 90–120 to 72–88;
  - anchor separation from 80–110 to 60–76;
  - spawn → centre grid path from 38–55 to 26–44;
  - spawn → enemy path from 85–140 to 60–100;
  - longest sightline from 60 to 48 m.
  - Cover, exits, routes, spawn LOS, ceilings and spawn-slot rules are unchanged.
- **Symmetry.** `ArenaLayout` gains `ArenaSymmetry` (`MirrorZ` default, or `Rotate180`); `ArenaBoxBuilder`, the mirror-symmetry rule and team-2 anchors/exits use it. Training Compound becomes point-symmetric (a "garrison square" with an open parade square in the middle and a pinwheel of barracks, office, garage and truck street around it). Depot Yard becomes a "rail crossing" (a train across z = 0 with three crossings, stack yard, loading shed, no warehouse). Oil Depot keeps the tank farm, the pump house and the catwalk, now one deck spanning z = 0. Its container lane and separator walls are removed.
- **No strange walls.** Blocking geometry must be readable objects. Free-standing thin separator walls, U-pockets and spawn baffle walls are retired. Spawns are protected by parked vehicles and buildings, with gate chutes and offset staging openings. `ArenaFeature` descriptors (vehicles, rolling stock, sandbags, fences, booths, flag, tanks, pipe racks) travel on `CompoundPlan.Features`, and `ArenaFeatureDressing` draws them mesh-only inside their collision envelopes. Petroleum accent roles stay oil-only.
- Planner versions: `environment.compound.arena` 3.0.0, `environment.depot.arena` 3.0.0, `environment.oil.arena` 2.0.0. Map ids, display names, seeds, recipes, package/metadata schemas, SGAF Collision v1, nav settings and the 256 m / 32 m chunk frame are unchanged.

Consequences:
- Gameplay collision, nav hashes, route corners, spawn slots and all three maps' package identities change. Software packages must be recompiled, and native nav republished with the locked editor.
- The ArenaVisualDetail gameplay fingerprints are refrozen from the new planners.
- The legacy W01 compound (B02/B03 freeze) is untouched.
- Rule validation proves geometry only. Time to first contact, spawn pressure, bot routing on the new topologies and the named 5v5/6v6 playtest of all three maps remain Pending.

Rollback: revert the ADR-074 merge and republish the ADR-071 packages.

## ADR-075 — Provisional: third-party code libraries for noise, runtime rigging and spatial audio

Date 2026-11-16. Draft for owner review. The owner asked which existing frameworks could cover textures, movement and sound without breaking the art policy. Status is Provisional in the ADR-008 sense: each library may be adopted only after its own pinned-version spike passes the gate below. Until then nothing is vendored, and `UnityProject/Packages/manifest.json` and `toolchain-lock.json` stay unchanged. The sole integrator owns package/settings changes (root `AGENTS.md`).

Context:
- ADR-002 bans imported visual *content* (meshes, photos, painted textures, rigs, mocap, hand-keyed clips). It does not ban third-party *code* that turns numeric recipes into output. ADR-062 and ADR-066 already ship licensed non-geometry media with license, hash and provenance records.
- `MaterialBaker` uses a hand-rolled periodic value noise (`Noise`/`Cell`). That limits surface variety: there are no gradient, cellular, ridged or domain-warp bases for camo, rust, concrete, cracks or terrain blends.
- Soldier motion bakes deterministic gait/IK in `Generation/Core/SoldierMotion` (`TwoBoneIk`, ADR-007). Presentation has no runtime correction for hands on weapons, foot contact on uneven or elevated ground (ADR-071 catwalks), or aim/look-at on remote actors.
- Audio is one 2D `AudioSource` fed by `OnAudioFilterRead` (ADR-055/062): stereo pan, distance attenuation and high-frequency rolloff. There is no HRTF, occlusion or room reverb, so it is hard to place sounds above or below, behind cover, or indoors vs outdoors.
- Constraints that rule out alternatives: `Simulation`, `Generation/Core` and `Contracts` must stay C# 9 / .NET Standard 2.1, Unity-free and linked by `Tools/*` projects. That excludes .NET 6+ physics (BepuPhysics v2, Jitter2) and MonoBehaviour movement assets (Kinematic Character Controller). Those stay reference reading only. The authoritative kinematic motor (ADR-006) and PhysX world collision are unchanged.

Decision:

1. **FastNoiseLite (C#, MIT) → `Generation/Core` noise source.**
   - Vendor the single upstream C# file at an exact release tag under `Generation/Core/Noise/ThirdParty/`, unmodified except namespace wrapping. Keep its MIT header in the file and record its upstream URL, tag and SHA-256 in a new `namedThirdPartyCodeSources` list in `Tools/provenance-allowlist.json`. The file is linked by `Tools/Generation` like the rest of `Generation/Core`.
   - It is only a sampling primitive. Seeds come from existing PCG32 named streams (ADR-009). Recipes still own every parameter, and no texture, image or preset ships with it.
   - Tiling: FastNoiseLite has no built-in periodic mode. Tileable bakes must use an explicit wrap (for example torus mapping onto 3D, or seam cross-fade) implemented in our code and covered by seam tests.
   - Existing material recipes and their content hashes are **not** migrated. Only new recipe versions may select FastNoiseLite bases, so current packages and `feature-content-lock.json` stay valid until a recipe deliberately bumps.

2. **Unity Animation Rigging (`com.unity.animation.rigging`) → Presentation-only runtime correction.**
   - Allowed constraints: two-bone IK (hands to weapon sockets, feet to ground probes), multi-aim (head/spine look-at) and multi-parent (weapon hand-off). Rigs and constraint targets are created from code on the generated skeleton. No authored rig prefabs, animation clips or package samples are used.
   - The baked `SoldierMotion` pose stays the source. Rigging only applies bounded, weighted corrections after it, and is never read by Simulation, hit volumes or AI (ADR-004). Hit poses keep using the fixed gameplay pose evaluator.
   - The package and every reference stay out of `UNITY_SERVER` and out of the pure test lane.

3. **Steam Audio (Valve, Apache-2.0, Unity integration) → Presentation-only spatialization.**
   - Scope: HRTF binaural rendering, distance and air absorption, and geometry occlusion/transmission against the **gameplay collision mesh** (not render meshes). This keeps results identical across quality tiers. Baked reverb or reflections are optional and come only after a CPU measurement.
   - The current mixer must move from one mixed 2D source to per-voice spatialized sources (≤24 voices, ADR-055 limit unchanged), or feed Steam Audio's spatializer per voice. The sample bank, its manifest and its hashes are unchanged.
   - Steam Audio ships prebuilt native binaries. They are a new provenance category: record each binary's path, SHA-256, upstream release and license under a new `namedNativeBinaries` allowlist entry, Win64 client only. The default HRTF dataset ships as data with its license recorded. No SOFA files from other sources.
   - Excluded under `UNITY_SERVER`. The current stereo mixer stays as the runtime fallback when the plugin fails to load, and as a Low-profile option.

Spike gates (all required before a library moves to Accepted):
- Exact version or tag resolved against the locked editor 6000.3.28f1 / URP 17.3.0 and recorded in `toolchain-lock.json` (packages) or the allowlist (vendored code and binaries). No version is chosen in this ADR.
- `Test-SourceProvenance.ps1` PASS with the new allowlist categories, plus `.meta` completeness.
- Pure suites (`Test-Generation`, `Test-Simulation`, `Test-Game`) PASS. For FastNoiseLite, add golden-value tests across the `Tools` .NET lane and the Unity editor, so float determinism matches between lanes.
- Client IL2CPP and Server builds PASS. The Server build must not contain Animation Rigging or Steam Audio assemblies or binaries.
- CPU cost measured on the reference laptop at TDM-5v5 Low/High against the 0.5 ms audio mix target and the existing presentation budget. A missing measurement is Pending, not PASS.
- A named listen/visual review for the audio and rigging spikes, recorded in the milestone handoff.

Consequences: the allowlist schema grows (third-party code and native binaries). Client build size grows by the Steam Audio binaries. The audio mixer and remote-actor presentation take on new code paths with fallbacks. Gameplay authority, contracts, locks and current content identities are unaffected unless a later recipe bump opts in. Rejected: Substance Designer / Material Maker (GUI-authored graphs exported as bitmaps, unclear under ADR-002), FMOD/Wwise (heavier than needed; licensing outside the non-commercial scope), BepuPhysics/Jitter2 (target framework and determinism envelope). Rollback per library: remove the vendored file or package entry and its allowlist record. The prior noise, pose and stereo mixer paths remain in place throughout.

Addendum 2026-11-16 (T091/T092, FastNoiseLite → Accepted as a sampling primitive):
- **Allowlist (T091).** `Tools/provenance-allowlist.json` gains `namedThirdPartyCodeSources` (path, sha256, upstreamSha256, upstream URL, tag, license) and `namedNativeBinaries` (Win64 `.dll` under `Assets/Plugins` only). `Test-SourceProvenance.ps1` hash-checks both and flags any file under a `ThirdParty/` folder that is not listed. Eight new synthetic regression cases cover them.
- **Cross-lane drift found.** Upstream FastNoiseLite v1.1.1 computes in `float`. The locked editor's Mono runtime and the .NET tools lane disagreed by about 1 ulp on the OpenSimplex2-based bases (cellular matched), which would break content-hash agreement between lanes. The owner chose a double-precision port over dropping the library.
- **Decision.** The vendored file at `Generation/Core/Noise/ThirdParty/FastNoiseLite.cs` is generated from the pinned upstream bytes (SHA-256 `6050908…160e`) by `Tools/port_fastnoiselite_double.py`. The port is a mechanical `float` → `double` change (keywords, `f` literals become `d` literals, the `FNLfloat` alias), plus a note, `#nullable disable` and a namespace wrapper. `--check` re-derives and compares it. Hand edits are not allowed. Recipes use only `Generation/Core/Noise/NoiseField.cs`, which provides gradient, cellular, ridged FBm and domain-warped FBm bases seeded from `NamedRandomStream`, with every FastNoiseLite setting set explicitly. Tileable sampling blends four lattice-shifted copies with quintic weights and renormalizes by weight energy, after centring each basis.
- **Evidence.** The golden values match bit-for-bit in both lanes. `Test-Generation` passed 458/458, including 21 noise tests covering goldens, stream independence, seam C0 to 1e-9, seam slope no rougher than the interior, contrast and argument checks. EditMode `NoiseFieldEditModeTests` passed 4/4. Provenance, `verify_feature_registry.py` and the C0/format locks all pass. `material.surface` 1.0.1 and all package identities are unchanged.
- **Not yet done.** No material recipe uses `NoiseField` yet. The proof generator (planned as a separate `material.noise` id so `material.surface` 1.0.1 stays frozen) still needs owner sign-off and a reviewed `feature-content-lock.json` refreeze. Animation Rigging (T093) was later Rejected (see below). Steam Audio (T094) is still Provisional.

Addendum 2026-11-16 (T093 spike: Animation Rigging mechanism passes; adoption still Provisional):
- **Scope direction.** The owner said the game is local play only for now, and multiplayer and dedicated-server work is postponed. Server checks in this ADR (assembly exclusion, server build) are **deferred**, not failed. Existing `!UNITY_SERVER` constraints stay in place.
- **Setup.** On branch `spike/t093-animation-rigging`, `com.unity.animation.rigging` 1.4.1 (the version the locked editor bundles) is added through `manifest.json` / `packages-lock.json` only. It adds the built-in `com.unity.modules.animation`. Burst and Mathematics were already present. The package has no `Resources/` folders, and its images exist only under `Documentation~`.
  - The spike assembly `Shooter.Presentation.PoseCorrection` (`!UNITY_SERVER`) attaches a code-built `TwoBoneIKConstraint`: left arm to a support-grip target parented to the held weapon. The soldier has no AnimatorController or clips and is evaluated manually right after `ProductActorPresentation.Sync` writes the `SoldierMotion` pose.
- **Finding.** The plain constraint solves from the live script pose. Over three frames, including a 1 m move and a turn, hand-to-target error was 0.00000 m, the shoulder moved 0 m and every unconstrained bone moved 0 m. The Animator writes only the three constrained bones.
  - Marking bones with `RigTransform` breaks this: the solve reads stale stream values (0.716 m error). Production must not mark procedural bones, and `PoseCorrectionSpikeTests` guards this.
  - Cost for 10 actors with manual `RigBuilder.Evaluate` is about 0.28–0.31 ms/frame in the editor. That's a diagnostic number only; the reference-laptop player measurement is Pending.
- **Builds.** EditMode passes 67/67. The IL2CPP Client build passes with the rigging assembly compiled in.
  - `ShooterBuildAssemblyFilter` (an `IFilterBuildAssemblies` in `Bootstrap/Editor`) keeps the package's `DocCodeExamples` assembly out of players. Its server-exclusion branch is unverified because the server build is deferred.
  - The first spike server build did ship `Unity.Animation.Rigging.dll`.
- **To adopt.** Pinning the package in `toolchain-lock.json` changes the toolchain hash frozen in `contract-lock.json`, `content-format-lock.json` and `game-contract-lock.json`, and every retained build identity. That refreeze is an owner decision. Until then the spike stays on its branch. Next would be production hand/foot/look-at correction for remote soldiers, the reference-laptop CPU measurement and a named visual review.

Addendum 2026-11-16 (T093 decision: Animation Rigging → Rejected; corrections use the pure `TwoBoneIk`):
- **Decision.** The owner chose not to refreeze the toolchain hash in the C0, format and Game-v1 locks. `com.unity.animation.rigging` is **Rejected** for this project. `manifest.json`, `packages-lock.json` and `toolchain-lock.json` are unchanged from `d8cbeb4`. The spike assembly `Shooter.Presentation.PoseCorrection`, `PoseCorrectionSpikeTests` and `ShooterBuildAssemblyFilter` were removed rather than merged. Their findings are kept in the addendum above.
- **Why the fallback costs little.** `Generation/Core/SoldierMotion/SoldierMotion.cs` already solves both arms (`ClearArm`, with body-clearance pole search) and both legs (`Leg`) with the pure, deterministic `TwoBoneIk`. Foot planting already samples `ISoldierGround`. The package would only have duplicated these solves in a Unity-only layer.
  - What remote soldiers actually lack is input quality. `ProductActorPresentation` passes a `FlatGround` at the authoritative feet height, so feet do not follow stairs, ramps or catwalk edges. The support grip comes from the rifle recipe's sockets for every held weapon, and there is no separate head look-at.
- **Follow-up (T093 production correction).** Close these gaps inside `SoldierMotion` and its inputs, not with a post-pass:
  - a cosmetic `ISoldierGround` backed by gameplay collision for remote soldiers (read-only, within the existing per-pose query budget);
  - per-profile support-grip targets;
  - a bounded head/neck look-at toward the aim point.
  - Gates: pure Generation goldens, EditMode, the Client build, reference-laptop CPU and a named visual review.
  - Corrected poses stay cosmetic and must never feed Simulation, hit volumes or AI (ADR-004).

## ADR-076 — Accepted: offline-only scope and bounded local evidence

Date 2026-11-16. The owner accepted this as part of an architecture review. Context: the C: drive filled up (2.9 GB free). Of the repository's 53 GB, `Artifacts/` held 37 GB:
- 50 full IL2CPP client players (about 1.2 GB each);
- 31 dedicated-server builds;
- 27 release packages, each a second copy of a player, plus extract and corrupt rehearsal trees;
- 22 installers.

Every wrapper created a new `<commit>/<action>-<run id>` folder and nothing pruned it. `Clean-Artifacts.ps1` was manual, and it kept every run cited by any tracked file, which was most of them.

Decision:

1. **Offline scope.** The product is offline TDM against bots until the owner decides on multiplayer.
   - `Networking` stays in git but is parked: no new code, and no Unity assembly references it.
   - The Dedicated Server build is not run by CI (`foundation.yml` builds and smokes the Client only) or by the release flow. `Build.ps1 -Target Server` still works on request.
   - New code adds no `UNITY_SERVER` guards or transport hooks. Simulation stays the single authority and Presentation stays read-only, since that structure also suits offline play.
   - Server gates in the docs read as Parked (offline scope), not Pending.
2. **One output per pipeline.**
   - When a wrapper run finishes with any result other than FAIL, `Save-ShooterRun` (`Tools/Unity.Common.ps1`) deletes older runs of the same action across all commits. `Package-Release.ps1` and `Build-Installer.ps1` do the same for `package-*` and `installer-*`.
   - Only the newest run is kept, so there is one client player, one package and one last release installer. `SHOOTER_KEEP_RUNS` raises the count.
   - A failed run never deletes the last good one.
   - Before a pruned run is deleted, its top-level `*.json` reports under 1 MB are copied to `Artifacts/reports/<action>/<commit>/<run>/`. That small history is what handoffs cite; they do not cite stored players.
3. **Scratch is deleted.** On success, `Package-Release.ps1` removes its `extract-rehearsal/` and `corrupt-rehearsal/` trees. Their results remain in `validation.json` and `corrupt-rehearsal.json`.
4. **Free-space floor.** `New-ShooterRun`, `Package-Release.ps1` and `Build-Installer.ps1` refuse to start with less than `SHOOTER_MIN_FREE_GB` free (default 5). `Tools/Disk-Report.ps1` is a read-only report covering the repository, the Artifacts areas and the per-user caches outside the repository.
5. **One-time cleanup (2026-11-16).** These were deleted, freeing about 33 GB:
   - all `player/` folders except the newest client build;
   - all server runs, `package-*` runs and older installers;
   - the ad-hoc review folders (`weapon-visuals-evidence`, `worker-evidence`, `T08xPreview`, `unity-soldier-tests`, `tooling-tests`).

   The written results in handoffs remain the record of those runs.

Consequences:
- A milestone handoff can no longer point at a stored player from an older commit. It cites the report under `Artifacts/reports/` and the commit, and a rebuild from that commit reproduces the binary.
- Two sessions running the same action at once in one checkout may prune each other's older output. Run heavy wrappers one at a time, as the runbook already requires for SDK wrappers.

Addendum 2026-11-16 (Phase 2 of the architecture review):
- **FastNoiseLite is wired into materials.** `material.surface` **1.1.0** is added next to the frozen 1.0.1, and `MaterialRecipe.IsSupportedVersion` accepts both. In 1.1.0 the baker samples its broad, medium, grain, wear and pore fields through `NoiseField`: domain-warped FBm, gradient, gradient, ridged FBm and cellular respectively. Each field gets its own `NamedRandomStream` key (`material/<family>/noise-1.1/<field>`). The family shading formulas are unchanged.
  - Every shipped recipe stays on 1.0.1, so no content hash or material look changes.
  - Moving a recipe to 1.1.0 is a per-recipe change that needs a named visual review and the cross-lane golden check required by ADR-075.
  - `feature-content-lock.json` was refrozen with owner approval: the material sources, a schema whose `generatorVersion` is now `enum ["1.0.1", "1.1.0"]`, and a second `material.surface` generator entry.
- **`TwoBoneIk` is final.** It stays the only IK solver, and Animation Rigging won't be revisited. IK runs inside the pure, deterministic motion generator, whose output is baked into hashed content, and the product uses no Animator or Playables.
- **The JSON readers stay separate, on purpose.** Each enforces a different policy:
  - `JsonValue`: the canonical, integer-only subset used for hashing.
  - `MapJsonValue`: printable-ASCII map documents.
  - `UserSettingsJson`: tolerant user files with diagnostics.
  - `CombatAudioManifest`: Unicode provenance text.

  Merging them would weaken a validation rule, so they are not merged.
- **Tool helpers are shared.**
  - The pure SDK wrappers call `Invoke-ShooterCheckedProcess` and `Get-ShooterRestoreArguments` in `Tools/TestResults.ps1` instead of five private copies.
  - The Python analyzers import `sha`, `utc` and `percentile` from `Tools/shooterlib.py`.

Addendum 2026-11-16 (Phase 3 of the architecture review: structure):
- **The pure build mirrors the asmdefs.** `Tools/Foundation`, which compiled eight assemblies as one, is gone. Each pure asmdef now has its own `Tools/<Assembly>` project with the same assembly name and references: Bootstrap.Core, Bootstrap.Release, Simulation, Networking, Content.Maps, World.Runtime, Game.Settings, Game.Session and Presentation.Model. `Tools/verify_asmdef_graph.py` runs in the fast CI lane and fails on drift, so the .NET lane catches boundary violations that only the manual Unity lane used to find.
- **`Presentation/Model` holds the Unity-free presentation code** in the `Shooter.Presentation.Model` asmdef (`noEngineReferences`). That covers View, Hud, Menus, Scoreboard, the audio mixer, the FX pool and `ShellSessionCoordinator`. The Unity adapters (`UnitySynthAudioOutput`, `AnalyticFxPresenter`, `CosmeticGrenadePresenter`) stay in `Shooter.Presentation`. `Simulation.Tests` references the project, so the old folder links and `Compile Remove` exclusions are gone.
- **`World/Navigation` and `World/Streaming` moved under `World/Runtime`** into `Shooter.World.Runtime`. `World.Unity` keeps only the PhysX/NavMesh adapters, and the pure test projects no longer compile those folders twice.
- **Asmdef hygiene.** Unity references are not transitive, so every assembly that references `Presentation` or `World.Unity` now references `Presentation.Model` or `World.Runtime` directly. (`Bootstrap.Client`'s GUID `df380645…` is the Core RP package runtime `Unity.RenderPipelines.Core.Runtime`, which lives outside `Assets/Shooter`. It is not a dangling reference.) Moved files kept their `.meta` GUIDs.
- **Deferred to their own reviewed tasks:**
  - splitting the large classes (`CombatAiDirector`, `ProductSessionView`, `ProductShellHost`);
  - moving map packaging (`MapPackageBuilder`, `UnityNavMeshBakeCodec`, the map document reader and writer) out of `Generation/Core/Environment`. That code is not lock-hashed, but `MapRecipe` and the Editor asset compiler both depend on the nav-bake codec, so a move first needs that dependency split. Otherwise Content.Maps, Generation.Core and AssetCompiler.Editor would have to reference each other in a circle.

Addendum 2026-11-16 (superseded content packages are pruned):
- **Problem.** Every content republish (ADR-078 weapon split, ADR-079 hash change, ADR-081 AO volume) added new content-addressed package directories, and nothing ever deleted the old ones. The two map caches grew by about 700 MB per republish; 254 stale directories (about 1.5 GB) were deleted by hand.
- **Decision.** After a successful publish, `MapPackagePublisher.PruneSuperseded` deletes package directories that the new index no longer references, under these rules:
  - A map package is deleted only when its map id and profile were both part of this publish, so a partial publish (`-Map oil`, `-Profile low`) never touches other maps or profiles.
  - Older weapon packages are deleted only when a weapon package was published.
  - Staging `.tmp` directories, unreadable roots and anything still referenced are kept. A failed deletion is never fatal.
  - `MapContent` deletes its `cancel-probe` scratch folder after the cancellation check and reports `prunedSupersededPackages` in its proof.
- **Validation.** `MapPackagePruneTests` (3).

## ADR-077 — Accepted: weapon definition files generate both weapon catalogs

Date 2026-11-16. The owner asked for an easier way to add weapons and their weapon-specific settings, sounds and animation, as step 1 of the architecture review. Sole integrator owns the generator, the two catalogs, routing, tests, CI and docs. No C0, Game-v1, PredictionSchema, content-format or feature-content lock change; no acceptance waiver.

Context: after ADR-072 a new weapon still needed hand edits in two parallel C# catalogs (`WeaponCatalog`, `WeaponVisualProfiles`) whose rows were positional constructor calls, plus id switches in `KillFeedCause` and `ProductAudioEventRouter`. `WeaponVisualProfile.AudioShotKey` was never read, and shot sounds were chosen by family, so a new gun could not pick its own voice.

Decision:
- **One file per weapon.** `Content/Weapons/<name>.weapon.json` (`schemaId` `shooter.weapon.v1`) holds the id, catalog `order`, `displayLabel`, `family`, `combat` (named fields; spread and recoil in degrees), `visual` (mesh key, optional `geometryRecipe` link to `Generation/FeatureRecipes`, optic, magazine cycle, near-field handling), `audio.shot` (a `SynthVoiceKind`) and `hud.killFeedGlyph` (a `KillFeedWeapon`).
- **Build-time generation, not runtime tables.** `Tools/generate_weapon_catalog.py` validates every file and writes `Simulation/Combat/WeaponCatalog.g.cs` and `Presentation/Model/View/WeaponVisualProfiles.g.cs` (partial classes, with deterministic `.meta` GUIDs). The catalogs stay frozen compiled C#, as ADR-072 requires. Numbers are emitted with their exact JSON literal text, so the generated rows are bit-identical to the former hand-written ones. `--check` fails on stale output and runs in the fast CI lane.
- **Validation.** Unknown or missing fields, duplicate keys, ids or orders, a file name that does not match the id, exponent or out-of-range numbers, a fire mode that does not suit the family, optics or magazine cycles on melee/throwables, a missing or mismatched geometry recipe, CRLF, and a catalog missing any of the four families are all rejected. Enum values are read from the C# sources, so the file format cannot drift from the code.
- **Routing by profile.** `WeaponVisualProfile.AudioShotKey` is replaced by `ShotVoice` and `KillFeedGlyph`. `ProductAudioEventRouter.MapShotVoice` and `KillFeedCause.MapWeapon` read the profile; unknown ids keep the rifle voice and glyph.

Not in scope (later steps of the review): reload/equip voices per weapon (reload events carry only the slot), new bank sample kinds, packaged weapon meshes still listed per weapon in `MapVisualClosure`/`MapPackageBuilder`/`ProductActorPresentation`, slot-keyed first-person equip timing (closed by [ADR-099](#adr-099--accepted-per-weapon-first-person-hand-and-weapon-choreography)), and the part-based geometry grammar. A weapon with a new silhouette still needs a `geometry.weapon` change.

Validation: `WeaponDefinitionFileTests` (catalogs agree, meshes resolve, combat rows bit-identical to the pre-ADR values, voice and glyph come from the profile) and the existing catalog, profile, kill-feed and audio tests pass in `Tools/Simulation.Tests`; `Tools/test_generate_weapon_catalog.py` (16 cases) covers the generator. On 2026-11-16 Simulation 326, Game 96 and Generation 462 passed, source provenance passed with the two new `.meta` files, and Unity EditMode passed 71/71 with the generated files compiled by the locked editor. A client player build and in-game listen/kill-feed check are Pending. Rollback: revert the ADR-077 merge; the hand-written rows are restored with it.

## ADR-078 — Accepted: shared weapon package separate from map packages

Date 2026-11-16. Step 2 of the owner-approved architecture review (after ADR-077). Sole integrator owns map/weapon packaging, the verified store, actor presentation, release tooling, tests and docs. No C0, Game-v1, content-format, feature-content or toolchain lock change; no acceptance waiver.

Context: every map visual package (3 maps × low/balanced/high) embedded all weapon meshes. A new weapon meant per-weapon constants and decode methods in `MapVisualClosure`, per-weapon build code in `MapPackageBuilder`, 22 per-weapon mesh fields in `ProductActorPresentation`, and republishing every map package. Knife and grenade meshes were even encoded under each map's generator fingerprint.

Decision:
- **Weapon mesh catalog.** `Tools/generate_weapon_catalog.py` also emits `Generation/Core/Environment/WeaponMeshCatalog.g.cs`: one source per distinct `visual.meshKey`, either a `geometry.weapon` recipe (embedded compactly from `Generation/FeatureRecipes`) or a built-in prop (`knife`, `grenade`). Paths follow `mesh-<key>-lod<n>.bin`, `-magazine.bin`, `-cycle.bin`, so existing names are unchanged. A mesh key maps to exactly one recipe; several weapons may share it.
- **Shared weapon package.** `WeaponPackage` builds `shooter.weapon-package.v1` (root JSON with schema, toolchain hash, generator ids, mesh keys and hashed dependencies). Gun meshes use the `geometry.weapon` fingerprint, so their bytes equal the former map-embedded artifacts; props use a new `geometry.loadout-prop` 1.0.0 fingerprint. `MapPackagePublisher.Publish` publishes it whenever a visual profile is published, with atomic staging, reuse of identical bytes and retention of corrupt bytes, and writes `weapons.json` (`shooter.weapon-package-index.v1`). The frozen release catalog and map package index schemas are untouched.
- **Map packages drop weapon meshes.** `MapVisualClosure.RequiredPaths` and `MapPackageBuilder` no longer include them, so every map visual package root changes once. Gameplay packages, collision and navigation are unchanged.
- **Loading.** `VerifiedMapPackageStore` verifies the weapon package once per store and attaches it with `PackagedVisualPayloads.WithWeapons`. Decoders (`DecodeWeaponLod/Magazine/Cycle`) are generic by mesh key and fail when no weapon package is attached. `FromBuild` (editor/tests) attaches an in-memory build. `ProductActorPresentation` keeps one mesh set per catalog key.
- **Release tooling.** `Package-Release.ps1` verifies, ships and closure-hashes the weapon package; the installer policy requires `Content/map-packages/weapons.json`; the native `Compile-GameContent.ps1` guard requires identical weapon packages in the pure and native caches.

Consequences: adding a weapon whose mesh key exists needs only its definition file and a content republish, which rewrites the weapon package and `weapons.json` but no map package. A new silhouette still needs a recipe (frozen feature registry) and possibly a `geometry.weapon` change. Packages published before ADR-078 no longer load: republish content. Weapon materials and first-person hands stay in map packages.

Validation: new `WeaponPackageTests` (Generation) and `VerifiedWeaponPackageStoreTests` (Game), updated `MapVisualClosureTests`, `FirstPersonHandlingTests` and generator tests (20 cases); release/installer wrapper regressions. Evidence on 2026-11-16 at `5af3ae7`: Simulation 326, Game 98, Generation 469, Test-Maps 15 + 53, source provenance, asmdef/fixture/contract/feature/content-format locks, Unity EditMode 71/71 and PlayMode all PASS. `Compile-GameContent.ps1 -Native` PASS with 12 native map packages and weapon package `0e0c4b6f…a911`, whose 27 files matched the pure cache. The IL2CPP client built; Smoke PASS; a diagnostic `TDM-5v5-v1` run on the native cache logged `packaged visuals ready profile=low … weaponLod=3`; `Package-Release.ps1 -RequireNativeNav` PASS (extract and corrupt rehearsals PASS) and ships `weapons.json` with the weapon package. A named in-game visual review and installer run are Pending. Rollback: revert the ADR-078 merge and republish the ADR-077 packages.

## ADR-079 — Accepted: engine toolchain lock separate from the build lock

Date 2026-11-16. Step 3 of the owner-approved architecture review (after ADR-077/078). The owner approved this step, including its one-time refreeze of the toolchain hash. Sole integrator owns the lock files, wrappers, `ShooterBuild`, provenance regeneration, content republication and docs. No C0, Game-v1 or content-format source, schema or fixture change; no acceptance waiver.

Context: `toolchainHash` is the SHA-256 of the whole `toolchain-lock.json`. It is embedded in every SGAF artifact, map/weapon package root, release catalog and settings provenance, and frozen in `contract-lock.json`, `content-format-lock.json` and `game-contract-lock.json`. The file also listed runtime/test packages and build settings that never change generated bytes, so adding any package (the ADR-075 Animation Rigging spike) forced a refreeze of all three locks and a republish of all content. That cost is why Animation Rigging was rejected.

Decision:
- **`toolchain-lock.json` schema 2** keeps only what shapes generated content or the engine itself: editor version and revision, URP (and the curated Core RP archive), AI Navigation (native nav bakes), graphics API and the .NET SDK. Its SHA-256 keeps the same meaning: `toolchainHash`.
- **`build-lock.json` (`shooter.build-lock.v1`)** holds every other direct package as `packages` (name + version: today Input System 1.20.1 and Test Framework 1.6.0), plus client/server backends and targets, required editor modules, `provenanceAllowlistVersion` and `transportVersion`. Changing it does not change `toolchainHash`.
- **Enforcement.** `Assert-ShooterLockedPackages` (`Tools/Unity.Common.ps1`) requires every direct `manifest.json` dependency to match one of the two locks, in both `manifest.json` and `packages-lock.json`. Engine modules and the curated Core RP tarball are exempt. An engine package may not be repeated in the build lock. `ShooterBuild.CheckToolchain` merges both files, checks every locked package inside the editor, and build manifests record `buildLockHash`. `Package-Release.ps1` checks that hash when present and ships `build-lock.json`; `Benchmark.ps1` rejects a build made with a different build lock.
- **One-time refreeze.** The new `toolchainHash` is `a8331fe2bc5b4293773762d829dc293c8aa5185024bd1a771543ac4eda840500`, replacing `9ac31fe8…7a8`. The `toolchainHash` field alone changed in the three locks; their source inventories are unchanged and verify. `Assets/Shooter/Settings/provenance.json` was regenerated by `ShooterBuild.WriteSettingsProvenance`, not hand-edited. All content caches, builds and packages made with the old hash are stale and must be republished or rebuilt.

Consequences: a future runtime or editor package (for example Steam Audio, ADR-075) is a `manifest.json` + `packages-lock.json` + `build-lock.json` change with a new build, but no lock refreeze or content republish. An engine, URP, AI Navigation, graphics API or SDK change still changes `toolchainHash`, as it should. ADR-075's Animation Rigging rejection rested on the refreeze cost; ADR-075 keeps its later decision that `TwoBoneIk` stays the only solver.

Validation: `Tools/test_build_lock.ps1` (8 synthetic cases, in CI) plus the full pure, tool and Unity lanes. Evidence on 2026-11-16 at `06106e3`: Test-Contracts, Simulation, Game, Generation, Maps and SourceProvenance PASS; C0/format/Game-v1/feature/asmdef/fixture verifiers PASS; package-release, build-installer, smoke, unity-results and source-provenance wrapper regressions PASS; Unity EditMode PASS. `Compile-GameContent.ps1` and `-Native` PASS under the new hash (weapon package `fd14e0c3…6abb`). The IL2CPP client built with `buildLockHash` `10495a1c…749b`; Smoke PASS; a diagnostic `TDM-5v5-v1` run logged `packaged visuals ready`; `Package-Release.ps1 -RequireNativeNav` PASS and ships `build-lock.json`. Installer run and named in-game review Pending. Rollback: revert the ADR-079 commit (restoring the old lock file and the three lock hashes) and republish content.

## ADR-080 — Accepted: code-built post-processing, HDR and SSAO per quality tier

Date 2026-11-16. Step 4 of the owner-approved architecture review. Sole integrator owns project settings, the editor configurator, presentation post-processing, tests and docs. No C0, Game-v1, content-format or content change; gameplay is untouched; no acceptance waiver.

Context: ADR-013 set `postProcessData = null` and `supportsHDR = false` because URP's post-process data references stock images (blue noise, film grain, SMAA lookups) that the player art audit forbids. The result was an untonemapped LDR image with no ambient occlusion, which is most of the "flat" look.

Decision:
- **Code-built post data.** `ShooterPostProcessingBuild.ConfigureAssets` (Bootstrap/Editor; run by `ShooterPostProcessingBuild.Configure` and at the end of `ShooterBuild.InitializeFoundation`) creates `Settings/ProductPostProcessData.asset` with URP's stock post shaders (code) and generated lookups only: a 16-pixel ordered dither for `blueNoise16LTex` and a one-pixel zero lookup in the film-grain and SMAA slots. Film grain and SMAA are never enabled. Settings provenance records the configurator source and every generated asset.
- **Per-tier renderers.** Low keeps `FoundationRenderer` (post data, no SSAO). Balanced and High get `FoundationRendererBalanced/High` with a URP SSAO feature using InterleavedGradient (no noise texture): Balanced is half resolution, 4 samples, Gaussian blur, intensity 1.2 and radius 0.3 m; High is full resolution, 8 samples, bilateral blur, intensity 1.4 and radius 0.4 m. Because no active SSAO uses blue noise, URP's `ScreenSpaceAmbientOcclusionDynamicResourcesStripper` removes the stock noise images from players, and the existing imported-art audit fails the build if that changes. HDR is off on Low and on for Balanced and High.
- **Runtime volume.** `ProductPostProcessing` (Presentation/Rendering) builds one global `Volume` with a code-created profile (no profile assets) from the pure `PostProcessingTier` table (Presentation.Model). Low: no tonemapping, contrast +6, saturation −4, vignette 0.16. Balanced/High: Neutral tonemapping, exposure +0.25, contrast +10, saturation −6, bloom (threshold 1.0, intensity 0.25/0.35, high-quality filtering on High) and vignette 0.2. Only the product camera opts in; menu, gallery and B01 diagnostic cameras are unchanged. `ProductClientSettingsApplier` reapplies the tier after a quality change.
- **Baked GI is deferred, with a design.** True lightmaps or vertex AO are not possible today: C0 `MeshData` has no vertex-colour or second-UV channel, and map meshes are uploaded at runtime from packages, so Unity's lightmapper cannot bake them. The proposed follow-up is a deterministic pure-.NET sky-visibility/AO volume per map (ray casts against the collision boxes in double precision), shipped as a map package artifact and sampled by the map surface shader. It needs its own ADR, a visual-package change and a shader change, but no C0 change.

Consequences: Low gains a single LDR uber-post pass (colour adjustment and vignette) and nothing else. Its frame cost on the reference laptop must be measured before any Low claim. Balanced and High gain HDR, tonemapping, bloom and SSAO, at a cost the existing Low-only benchmark cannot measure. All numbers are presentation tuning that still needs a named visual review. `FoundationEditorTests` now asserts the new per-tier contract and that post data references only generated textures.

Evidence on 2026-11-16 at `c1a99d4`: Simulation 329 (including `PostProcessingTierTests`), Game, source provenance and the lock/asmdef verifiers PASS; Unity EditMode 72/72 and PlayMode PASS. The IL2CPP client built with zero packed image files, and the post data, dither lookups and per-tier renderers are in the player. URP's build preprocessor kept the SSAO variants for Balanced/High (prefilter fields recorded). Smoke PASS. A diagnostic `TDM-5v5-v1` Low run logged `SHOOTER_POST_PROCESSING tier=0 hdr=False …` and packaged visuals. Diagnostic Low frame p95 over three 5 s runs was 4.85 ms (before) and then 4.93 and 5.52 ms (after), which is within run-to-run noise. That is diagnostic only, not a reference capture. Balanced/High player runs (the benchmark supports Low only), their GPU cost and a named visual review of all tiers are Pending.

Rollback: revert the ADR-080 commit, then run `ShooterBuild.InitializeFoundation` or restore the previous Settings assets, and regenerate provenance.

## ADR-081 — Accepted: baked ambient-occlusion volume per map

Date 2026-11-16. Follow-up to ADR-080, which deferred baked GI. The owner asked for the baked AO volume. Sole integrator owns the baker, map packaging, the map surface shader, presentation binding, tests and docs. No C0, Game-v1, content-format, feature-content or toolchain lock change; no acceptance waiver.

Context: indoor and covered areas received the same sky ambient as open ground. Unity lightmapping cannot bake runtime-uploaded package meshes, and C0 `MeshData` has no vertex-colour or lightmap-UV channel, so the occlusion has to come from a separate artifact.

Decision:
- **Bake.** `MapAmbientOcclusionVolume` (Generation/Core/Environment) bakes a sky-visibility volume over a fixed 80 × 16 × 96 m box (X −40…40, Y 0…16, Z −48…48) at 0.5 m in X/Z and 1 m in Y: 160 × 16 × 192 cells. Each cell centre casts the 17 integer-lattice directions with y ≥ 0, up to 6 m, against the plan's axis-aligned collision boxes; upward rays weigh more (0.5 + 0.5·y). Cells inside solids are filled from their open neighbours over three dilation passes, so trilinear sampling does not bleed black. All arithmetic is IEEE double add/sub/mul/div/sqrt on integer-derived inputs (no trigonometry). A 4 m column binning keeps one map to about 0.6–0.9 s in the tools lane, and the result is cached per map and collision hash.
- **Packaging.** Every map visual package adds `lighting-ambient-occlusion.bin`: an SGAF material under the map fingerprint, holding an `rgba8-linear` 160 × 768 atlas with four Y layers per texel and the full mip chain the frozen codec requires (lower mips are unused). `MapVisualClosure.RequiredPaths` requires it and `ValidateVisualClosure` decodes it. Visual package roots change and content must be republished. Gameplay packages, collision and navigation are unchanged.
- **Shading.** `Shooter/MapSurface` keeps URP Lit 17.3's forward pass (same properties, pragmas and per-material CBUFFER) with one change: `surfaceData.occlusion` (indirect light only) is multiplied by the volume, sampled 0.35 m along the normal. Shadow, depth, depth-normals (SSAO) and meta passes are `UsePass` of URP Lit. The volume, its transform and strength are global shader properties. `ProductMapView` uploads the volume as an R8 `Texture3D` and draws every map surface with this shader. Actors, weapons and the menu stay on URP Lit. `ShooterPostProcessingBuild` creates the single keyword variant material that map surfaces use, and the client build preloads the shader and that material.
- **Per tier.** `PostProcessingTier.AoVolumeStrength` is 1.0 on Low (no SSAO) and 0.8 on Balanced and High, where SSAO adds contact detail. Strength is 0 whenever no map is bound.
- **Cross-lane determinism.** Golden SHA-256 values per map are asserted in both `AmbientOcclusionVolumeTests` (.NET) and `AmbientOcclusionVolumeEditModeTests` (Unity Mono).

Consequences: covered, indoor and wall-adjacent areas lose sky ambient while direct sun and shadows are unchanged. Each visual package grows by about 160 KB. One 3D texture sample is added per map pixel. Dynamic actors don't receive the volume yet; probe-based actor AO is a possible follow-up. The values (ray length, weights, strengths) are tuning that still needs a named visual review.

Evidence on 2026-11-16 at `de6f8b6`: Simulation 329, Game 98, Generation 475 (including six `AmbientOcclusionVolumeTests`), Test-Maps, source provenance and the lock/asmdef/fixture verifiers PASS. Unity EditMode passed 75/75, and the three Mono goldens matched the .NET bake byte for byte. `Compile-GameContent.ps1` and `-Native` PASS. The IL2CPP client built with `MapSurface.shader` and its variant material and zero packed image files; Smoke PASS. A diagnostic `TDM-5v5-v1` Low run bound the packaged map (`SHOOTER_PRODUCT_MAP_VIEW … packaged=1`) with frame p95 4.73 ms (diagnostic, not a reference capture). `Package-Release.ps1 -RequireNativeNav` PASS. A named visual review of the occlusion on all three maps and Balanced/High player runs are Pending.

Rollback: revert the ADR-081 commit and republish content; map surfaces return to URP Lit.

## ADR-082 — Accepted: geometry.weapon 1.1.0 parametric rifle parts

Date 2026-11-16. Step 5 of the owner-approved architecture review. The owner asked for this step, including its reviewed refreeze of `feature-content-lock.json`. Sole integrator owns the weapon generator, recipe schema, fixtures, baseline, registry, the weapon definition generator, tests and docs. No C0, Game-v1, content-format or toolchain lock change; shipped content is unchanged; no acceptance waiver.

Context: after ADR-077/078 a new weapon that reuses an existing mesh is one definition file, but a new rifle shape needed `WeaponGeometry` code. The 1.0.5 generator only knew `standard` and `carbine`, written as about ten `carbine ? a : b` constants.

Decision:
- **Parts, not silhouettes.** geometry.weapon **1.1.0** sits next to the frozen 1.0.5 (`WeaponRecipe.IsSupportedVersion`). A 1.1.0 rifle states `parameters.parts` in integer millimetres and counts:
  - `handguardFrontMm`, `barrelFrontMm`, `topRailTeeth`
  - `gasBlockZMm`, `gasTubeZMm`, `gasTubeLengthMm`, `frontSightZMm`
  - two modules, `stock` (`fixed`/`none`) and `optic` (`compact`/`none`).

  A pistol states `parts: {}`. `WeaponParts` checks every range and the front-to-back order, so a recipe cannot escape the frozen construction envelope.
- **Exact migration.** 1.0.5 `standard`/`carbine` resolve to the `WeaponParts.Standard`/`Carbine` presets. Rail, under-rail and forward-fastener counts are now derived by stopping at the handguard front, which reproduces both presets. `WeaponPartsTests` proves that a 1.1.0 recipe with the preset values builds byte-identical LODs, magazine, cycle part, collision and sockets, and that the 1.1.0 empty-parts pistol matches the 1.0.5 pistol.
- **Gameplay stays put.** Sockets, collision, magazine and cycle part keep the numeric baseline for every part combination, as ADR-072 requires.
- **Versioned fingerprints.** `WeaponAssetGenerator` and `WeaponMeshSource.Fingerprint` use the recipe's own generator version, so existing 1.0.5 artifacts keep their bytes.
- **One-file rifles.** A weapon definition may give `visual.geometryParts` instead of `visual.geometryRecipe`. `Tools/generate_weapon_catalog.py` embeds an inline 1.1.0 recipe (id = weapon id) in `WeaponMeshCatalog.g.cs`, and the C# parser enforces the ranges when the catalog loads. `optic: none` cannot be combined with the `ScopedPip` profile optic.
- **Registry.** `weapon.schema.json` accepts both versions (the parameter shape is tied to the version), `docs/feature-fixtures/rifle-1.1.0.json` is added, and `docs/weapon-recipe-baseline.md` documents the parts. `feature-content-lock.json` is refrozen with a `geometry.weapon` 1.1.0 generator row. `Tools/verify_feature_schemas.py` had been failing since 1.0.5 because it hard-coded 1.0.4 and 20 cases; it now checks the accepted version sets.

Consequences: a new rifle shape is one `Content/Weapons/*.weapon.json` file plus a content republish. All shipped recipes stay on 1.0.5, and the published catalog, map index and weapon index were byte-identical after republishing. New module types (other stocks, optics, muzzle devices, magazines that change collision) would be a later generator version. No new product weapon is added: choosing one is a gameplay and balance decision.

Validation on 2026-11-16: `WeaponPartsTests` 13/13 (byte identity, modules, ranges, canonical key order, asset generator fingerprint), generator tests 22/22, Simulation 329, Game 98, Generation 488, Test-Maps, source provenance, C0/format/feature registry and fixture verifiers, `verify_feature_schemas.py` 23/23 and Unity EditMode all PASS. A pure `Compile-GameContent.ps1` republish left `catalog.json`, `index.json` and `weapons.json` byte-identical. At `132f0a9` the IL2CPP client built, Smoke PASS, and a diagnostic `TDM-5v5-v1` Low run reached `packaged visuals ready` with no exceptions. No new product weapon was added, so in-game review of a 1.1.0 shape is Pending. Rollback: revert the ADR-082 commit; 1.0.5 content is unaffected.

## ADR-083 — Accepted: map registry, arena layout documents and the generic map.arena recipe

Date 2026-11-16. Step 6 of the owner-approved architecture review. The owner asked for this step, including its reviewed refreeze of `content-format-lock.json`. Sole integrator owns map generation, recipes, tooling, presentation hints, tests and docs. No C0, Game-v1, feature-content or toolchain change; every map's collision, navigation and package identity is unchanged; no acceptance waiver.

Context: the arena planners already composed a reusable kit (`ArenaBoxBuilder`: shells, vehicles, covers, sandbags, walls), but a map id was hard-coded in about fifteen places:

- `MapRecipe` seed, generator, schema, name and description switches
- the `ProductMapPlanner` switch
- release catalog and release policy lists
- `MapContent` and `Compile-GameContent` map selections
- sky, yard-surface and menu-card checks in presentation.

The frozen map-recipe schema also bound one generator to each of the three maps. Adding a map therefore meant code in many layers plus a content-format refreeze.

Decision:
- **Registry.** `Content/Maps/<id>.map.json` (`shooter.map-definition.v1`) holds a map's id, order, display name, description, seed, recipe generator, planner and presentation hints (`sky`, `groundSurface`). `Tools/generate_map_registry.py` validates the files and writes `MapRegistry.g.cs`. Unknown or duplicate fields, ids, orders and seeds are rejected, as are legacy recipes on the wrong map and unknown code planners. `--check` runs in the fast CI lane.
- **Everything reads it.** `MapRecipe` identity and `IsApprovedMapId`, `ProductMapPlanner.Generate`, `ReleaseCatalogDocument.RequiredMaps`, `MapContent` (`all`, the aliases, or any registered id) and `Compile-GameContent -Map` all read the registry. So do the presentation hints: `GeneratedSky` presets, `ProductMapView.UsesAsphaltYard` and menu cards for every registered map in catalog order. `MapRecipe.Parse` also requires the map to be registered with the recipe's generator. The release policy lists stay deliberate, and `MapRegistryReleasePolicyTests` fails until they match the registry.
- **Layout documents.** `ArenaLayoutDocument` interprets a planner written as data: identity, `ArenaLayout`, and ordered kit operations that map one-to-one onto `ArenaBoxBuilder` calls:
  - `perimeter`, `shell`
  - `cover`, `vehicle`, `feature`
  - `sandbags`, `fence`, `grounded`, `box`
  - `wallX`, `wallZ`
  - `visualDetail`

  All lengths are integer millimetres, converted with an exact int → float division, so documents parse identically in every runtime. Training Compound is converted: `CompoundArenaPlanner` is now a thin wrapper over its 40-operation document. Depot Yard and Oil Depot remain registered code planners (`code:depot-arena`, `code:oil-arena`).
- **Generic recipe.** `map.arena` / `shooter.recipe.map-arena.v1` is for every map registered after the three frozen ones. `docs/content-schemas/map-recipe.schema.json` and `docs/content-format.md` gained it, and `content-format-lock.json` was refrozen (source hashes only).

Consequences: a new arena is one definition file with a layout document, plus content compilation, native nav and a playtest. Layouts that need new kit primitives (stairs and catwalks are code-only today) still need code. An editor preview window that re-plans a layout live is a possible follow-up.

Evidence on 2026-11-16 at `6509f82`: `MapRegistryTests` 12/12, release-policy test, map generator tests 7/7, Simulation 329, Game 99, Generation 500, Test-Maps, source provenance, C0/format/feature/asmdef/fixture verifiers, and Unity EditMode and PlayMode all PASS. A pure `Compile-GameContent.ps1` republish left `catalog.json`, `index.json` and `weapons.json` byte-identical. The IL2CPP client built and Smoke PASS. Two diagnostic `TDM-5v5-v1` runs stalled after workload validation while the machine was in interactive use. A third run completed: `SHOOTER_BENCHMARK_COMPLETE`, 1605 frames, p95 4.80 ms. Later runs completed with the player minimised and with Notepad holding the foreground, which rules out window focus. The stall's cause is not established. `BenchmarkProgressWatchdog` now aborts a stalled capture after 10 s with a state snapshot, and `Benchmark.ps1` reports that snapshot or the last player marker, so the next occurrence records its cause. A new map authored only as a document has not been built or played yet (Pending).

Rollback: revert the ADR-083 commit; map content is unaffected.

## ADR-084 — Accepted: analytic SDF UI primitives and UI motion model

Date 2026-11-16. Wave 1, step 1 of the owner-approved visual roadmap (UI, icons, badges first; the IMGUI shell stays, with an SDF layer added). Sole integrator owns the shader, presentation UI, build preload, tests and docs. No C0, Game-v1, content-format, feature-content or toolchain change; gameplay is untouched; no acceptance waiver.

Context: `TacticalUi` drew every menu, HUD, loading screen and scoreboard as `Texture2D.whiteTexture` rectangles and 1-px rotated quads. There were no anti-aliased edges, rounded corners, gradients, shadows or glows. Icons were stacks of rectangles, and the only motion was alpha fades. ADR-002 rules out sprite or skin art, so the polish has to come from code.

Decision:
- **Shader.** `Shaders/UiSdf.shader` (`Shooter/UiSdf`) evaluates analytic signed distances per pixel:
  - shapes: rounded rect, circle/ring, arc, triangle, diamond and an edge-fade vignette;
  - styling: fill or stroke, a vertical/horizontal/radial two-colour gradient, a soft drop shadow, an outer glow, and a one-pass diagonal glint.
  - It outputs premultiplied alpha and multiplies by IMGUI's own clip mask (`_GUIClipTexture` / `unity_GUIClipTextureMatrix`), so groups and scroll views clip as before.
  - It samples no other texture, so the player art audit is unchanged.
- **Drawer.** `Presentation/Session/UiSdf.cs` draws one padded quad per shape with `Graphics.DrawTexture` during Repaint, in the caller's GUI space (UI scale, groups, rotation). Anti-aliasing stays one device pixel at any UI scale. Brand colours stay sRGB-authored and are converted for the linear colour space. If the shader is missing or unsupported, every call returns false, logs `SHOOTER_UI_SDF unavailable` once, and `TacticalUi` keeps its previous flat path. That fallback is still code-drawn, never imported art.
- **TacticalUi.** These keep their signatures and use SDF when it is available:
  - rounded shadowed panels, HUD plates, buttons, map cards and anti-aliased lines;
  - capsule reticle arms, a ring headshot mark and rounded-part weapon silhouettes;
  - pill sliders with a round thumb, switch-style toggles (replacing `[ ON ]` text), a glowing indeterminate loading segment and a single-draw vignette.
  - Hover, focus and selection ease through a per-control emphasis value.
  - The kill ribbon becomes a feathered banner with a stamped procedural medal (`TacticalUi.Medal`: gold rim, enamel, diamond emblem, laurel strokes, streak pips by tier 1–4) and one glint.
- **Motion model.** Pure `Presentation/Model/Hud/UiMotion.cs` provides:
  - easing (OutCubic, InOutCubic, OutBack) and a frame-rate-independent critically damped `Follow`;
  - the stamp scale, slide, stagger and count-up envelopes, the glint sweep and a pulse.
  - Reduced camera motion collapses movement to a snap or fade; reduced flash removes the glint, holds pulses and softens glows.
  - `TacticalUi.ReducedMotion` and `ReducedFlash` are set from applied settings by `ProductShellHost` and `ProductSessionView`.
- **Shape tokens.** Pure `UiShapeParams` holds radii, shadow, glow and hairline tokens, plus the rounded-box distance and coverage math the shader mirrors.
- **Build.** `ShooterBuild` preloads `UiSdf.shader` in client players next to `AdsScopePip`.

Consequences:
- Each SDF shape is one extra material-bound draw. The HUD issues a few dozen per frame, and the menu more; the cost is presentation only and must be measured on the reference laptop.
- Map-card media images keep square corners inside the rounded card.
- Icons are still procedural parts drawn per frame. ADR-085 (planned) moves them into a generated SDF icon atlas.

Evidence on 2026-11-16 (working tree on `fa2cbc9`, recorded in the commit that adds this ADR):
- **Pure tests.** Simulation 344 PASS (15 new `UiMotionTests` and `UiShapeParamsTests`). Source provenance PASS after `ShooterBuild.WriteSettingsProvenance` regenerated `Settings/provenance.json` for the preload change.
- **EditMode.** 79/79 PASS, including four `UiSdfEditModeTests`: the shader is supported, rounded corners are cut, rings are hollow and output is premultiplied.
- **Build.** The IL2CPP client built with `UiSdf.shader` in the packed graphics list and no imported art. Smoke PASS.
- **Player review.** The player logged `SHOOTER_UI_SDF ready`. Captures from the live 1080p window (`Artifacts/visual-design/adr-084/20261116-menu/`) show the recovery, main menu, setup and settings screens, including SDF controls inside the clipped settings scroll view.
- **Fixes found during review.** The selected card's square accent border became a rounded stroke, and the caption plate is now rounded. The left scrim's 28 stepped strips (visible banding) became two smooth gradients.
- **Pending.** The live combat HUD, kill-ribbon medal and loading screen in a running match were not driven: automated key input could not reach START MATCH reliably. Those, 720p at 100%/150% UI scale, reduced motion/flash, reference-laptop frame cost and a named visual review are Pending.

Addendum 2026-11-16 (HUD review):
- **How it was driven.** A live Oil Depot 6v6 match was started from the player and captured with the loading screen. A review-only `-shooterUiGallery 1` flag (`TacticalUi.DrawUiGallery`, which replaces Main) shows the medal tiers with a looping stamp, the kill ribbons, kill-feed icons at feed and large size, control states, the ammo plate, the reticle and the hit marker. This covers what an idle match cannot trigger. Captures are in `Artifacts/visual-design/adr-084/20261116-hud/`.
- **Fixes from the review:**
  - the ribbon banner was too transparent over bright scenes: its middle is now solid at 90% with 2-px rails;
  - the ammo-plate accent hung off the rounded edge and now sits inside the plate;
  - the loading track and slider track were too dark to read and are now lighter with a rim;
  - the pistol, knife and grenade silhouettes were redrawn (angled grip and trigger guard, pointed blade, spoon lever and pull ring);
  - hip reticle arms went from 1 px to 1.5 px and from 5 px to 6 px long.
- **Hip framing (fixed in a follow-up).** In hip fire, the rifle muzzle projected about 0.7° below and 3° left of centre, right under the reticle. The T084 composition yawed the rifle 18° across the view from a hip offset that sat slightly high.
  - `Content/Weapons/rifle.weapon.json` now uses `hipRotationDegrees [0, -12, -5]` and `nearFieldHipBias [0.08, -0.01, -0.10]`; the carbine uses `[0, -11, -4]` and `[0.08, -0.01, -0.09]`. `WeaponVisualProfiles.g.cs` was regenerated.
  - ADS is unchanged, because the hip rotation still fades to identity and the ADS position uses the sight socket. Gameplay catalogs and packages are byte-identical.
  - The new `FirstPersonHandlingTests.HipMuzzleStaysBelowAndOffTheReticle` projects the muzzle socket the way `ProductActorPresentation` does and requires it to sit at least 1.5° below, 0.5° right of and 2.5° away from the centre. It fails on the old values (0.65° below) and passes on the new ones.
  - Player capture: `hud-hip-framing-after.png`. Live ADS capture and a named feel review are Pending.
- **Pending.** The kill ribbon and kill feed during real kills, the scoreboard and results screens, 720p at 150% UI scale, reduced motion/flash, frame cost and a named review.

Rollback: revert the ADR-084 commit and regenerate settings provenance; every primitive falls back to the flat path when the shader is absent.

## ADR-085 — Accepted: generated SDF icon atlas with mesh-traced weapon silhouettes

Date 2026-11-16. Wave 1, step 2 of the owner-approved visual roadmap. Sole integrator owns the icon generator, shader, presentation UI, tests and docs. No C0, Game-v1, content-format, feature-content or toolchain change; no package or content republish; no acceptance waiver.

Context: after ADR-084 the HUD and shell drew every icon as a stack of rounded parts each frame. Kill-feed weapon shapes were guesses rather than the real weapons, and there were no insignia, emblems or category glyphs. ADR-002 forbids painted or imported icons.

Decision:
- **Pure generator (`Generation/Core/Icons`).** The generator has four parts:
  - `IconShape` and `IconRecipe` are analytic signed-distance shapes in integer icon units (1000 per cell): circle, ring, rounded box, capsule, polyline stroke and even-odd polygon, combined by union and subtraction. They use IEEE add/sub/mul/div/sqrt only, and the star points are precomputed integers.
  - `IconLibrary` holds 22 original glyphs:
    - headshot, elimination, assist, ammo, magazine, health and reload;
    - objective, ally arrow and enemy marker;
    - three chevron ranks and a star;
    - two fictional faction emblems (ally shield, enemy hexagon), which differ in shape and not only in colour;
    - a medal star and a crown;
    - four settings-category glyphs.
  - `MeshSilhouette` traces a weapon's side view (+Z right, +Y up) from its meshes. It uses 4× supersampled triangle coverage and an exact Felzenszwalb distance transform in double precision.
  - `IconAtlas` packs the glyphs into one R8 signed-distance atlas: 64-px cells, 8-px spread, 1024 px wide, value 128 at the edge. Long guns and the knife take 3 cells, the pistol 2 and the grenade 1.
- **Weapons match the 3D models.** `ProductActorPresentation` hands the decoded packaged LOD0 mesh, magazine and cycle part of every `WeaponMeshCatalog` row to `UiIconAtlas.RegisterWeapons`. Kill-feed and ammo-plate icons are therefore silhouettes of the same packaged meshes the player sees, including any future 1.1.0 parametric rifle. A repeat registration of identical sources is skipped.
- **Runtime, not a package.** `UiIconAtlas` builds the atlas in memory at first use and again once weapons arrive. It is uploaded as an R8 linear `Texture2D` and never written to disk or packed as an image, so the player art audit and the content format are unchanged. This replaces the earlier plan for a `ui-icons` package; a package adds identity surface without changing the bytes. A build failure logs `SHOOTER_UI_ICONS unavailable` once and the ADR-084 part-drawn icons stay in use.
- **Shader.** `Shooter/UiSdf` gains `UiShapeKind.Glyph`, which samples the atlas cell and converts it to logical-pixel distance. Fill, gradients, stroke, shadow, glow and glint all apply to icons. `UiSdf.Icon` fits an icon by aspect, and `TacticalUi.Icon` / `WeaponIconName` wrap it.
- **Uses:**
  - kill-feed weapon and headshot icons;
  - a weapon silhouette on the ammo plate for the weapon shown in the hands;
  - settings tab glyphs;
  - scoreboard and results tables with faction emblems on team-tinted gradient headers, rounded zebra rows, a glowing YOU row and a crown on the top scorer (presentation only; it is absent while every score is 0);
  - an icon library section in the review gallery.

Consequences: there is no new shipped file. The atlas is about 192 KB of texture memory, plus one atlas sample per icon pixel. Ranks have no data model (there is no progression), so the rank glyphs are available but unused. Medal tier art stays the ADR-084 composite.

Evidence on 2026-11-16 (working tree on `41fcdaa`, recorded in the commit that adds this ADR):
- **Pure tests.** Generation 508 PASS, including five `IconAtlasTests`:
  - exact shape distances;
  - every glyph has ink and a clear border;
  - the encoding;
  - mesh-traced proportions (rifle 2.87, carbine 2.50, knife 4.86, pistol 1.12, grenade 0.58 ink aspect);
  - determinism with library golden `22964e2d…98f0`.
- **EditMode.** 82/82 PASS, including `IconAtlasEditModeTests`: the Mono library hash equals the .NET golden byte for byte, the runtime atlas exposes icons, and glyph mode draws the health cross with clear corners.
- **Other checks.** Simulation 346, Game 99, source provenance (928 entries) and the feature registry PASS.
- **Build.** The IL2CPP client built.
- **Player.** The player logged `SHOOTER_UI_ICONS ready icons=22` (22.4 ms), then `icons=27 weapons=5` (66.9 ms) once the packaged weapons were decoded. Captures in `Artifacts/visual-design/adr-085/20261116/`: gallery, live-match scoreboard (held Tab), settings tabs, ammo-plate icon and an atlas preview.
- **Pending.** Kill-feed rows during real kills, results with a crowned top scorer, 720p at 150% UI scale, reference-laptop cost and a named review.

Rollback: revert the ADR-085 commit. Every icon call falls back to the ADR-084 shapes or the text it had before.

## ADR-086 — Accepted: animated debrief and front-end motion

Date 2026-11-16. Wave 1, step 3 of the owner-approved visual roadmap (results and menu motion). Sole integrator owns presentation models, shell, menu stage, tests and docs. No C0, Game-v1, content-format, feature-content or toolchain change; scores and statistics are read only; no acceptance waiver.

Decision:
- **Debrief model.** Pure `Presentation/Model/Scoreboard/ResultsPresentation` derives up to four award cards from the immutable final rows:
  - MVP: highest score;
  - Top Fragger: most kills;
  - Survivor: fewest deaths among operators with at least one kill;
  - Sharpest: best K/D, with at least two kills.

  Ties go to the earlier row in authority order, and a scoreless match has no awards. The same model owns the reveal timeline: banner sweep 0.55 s, team score count-up 1.1 s, awards staggered by 0.16 s from 0.65 s, tables fading in from 1.0 s. Reduced motion shows the final state at once.
- **Results screen** (`ProductShellHost.DrawDebrief`):
  - an outcome banner tinted by the winning team that sweeps in from the left;
  - faction emblems flanking counting team scores;
  - award cards that rise in with a stamped `TacticalUi.Medal` (tier per award) and a glow on the local player's own awards;
  - the ADR-085 team tables, including the top-scorer crown, fading in last.

  The previous outcome/reason lines are folded into the banner.
- **Screen transitions.** Pure `ShellMotion` handles screen entry:
  - Full-screen pages (Main, Setup, Settings, Results, Loading, Content recovery) rise 18 px out of a short dark veil over 0.26 s. The backdrop stays fixed while the content moves.
  - Under reduced motion they only fade, over 0.12 s.
  - Gameplay and the pause/confirm overlays never veil, so they never flash over the live match. `TacticalUi.Begin` takes an enter offset; input follows the GUI matrix.
- **Menu stage camera.** `ProductMenuScene.ApplyCameraDrift` adds:
  - two slow sideways sways (23 s and 37 s periods, at most about 0.09 m) and a vertical sway (29 s, 0.018 m);
  - a one-time 0.35 m push-in over 3.2 s when the menu first opens;
  - smoothed pointer parallax (0.04 m by 0.02 m).

  The look target moves 35% as far as the eye, which reads as depth. Drift uses real elapsed time, because the stage renders at 30 Hz and the per-frame `sceneTime` under-counts about 3×. That pre-existing pacing still drives the dust motes and is unchanged. Reduced motion holds the camera still.
- **Review preview.** `-shooterUiGallery results` replaces Main with the debrief over clearly sample rows, through `ScoreboardTable.BindPreview` (review only, never used by the match flow), looping every 7 s.

Consequences: no new assets or allocations per frame beyond the existing UI draws. The real debrief needs a finished match, so it was reviewed through the preview, which uses the same drawing code. There is no kill timeline in `MatchResults`, so a kills-over-time graph would need a Game-v1 change and is not attempted.

Evidence on 2026-11-16 (working tree on `170fb8d`, recorded in the commit that adds this ADR):
- **Pure tests.** Simulation 353 PASS, including seven `ResultsAndShellMotionTests`:
  - award choice and tie-break;
  - the scoreless case;
  - ratio formatting;
  - preview sorting;
  - the timeline under reduced motion;
  - the veil not touching gameplay or overlays;
  - drift bounds and the push-in settling.
- **Other checks.** Game 99, EditMode 82/82, source provenance (932 entries) and the IL2CPP client build PASS.
- **Player review.** Debrief preview captures show the mid-reveal frame (banner sweeping, scores counting, tables veiled) and the settled frame (four awards, crowned MVP). An earlier layout let the medal's laurel strokes overrun the card edge; the medal was moved inward and resized.
- **Drift diagnostic.** A temporary player log showed the drift was smooth and bounded (0.05–0.085 m sideways) with no push-in replay. The apparent framing differences between menu captures come from the existing showcase idle moving the soldiers, not from the camera. The background holds still.
- Captures are in `Artifacts/visual-design/adr-086/20261116/`.
- **Pending.** A debrief after a real finished match, the defeat and draw tones, 720p at 150% UI scale, a reduced motion/flash pass, and a named review.

Rollback: revert the ADR-086 commit; the debrief returns to the plain tables and screens appear instantly.


## ADR-087 — Accepted: tactical HUD craft, first tranche

Date 2026-11-16. Context: owner requested continuation of the visual roadmap at `1676ff8`. This bounded Wave 1b tranche covers combat HUD presentation. Wave 1 verification debt remains open; no authority, sensing, gameplay, settings, content or toolchain identities change.

Decision: use SDF arcs for damage direction and authority-tick reload progress, up to 30 round glyphs (a compact bar above 30), rotated ally/self arrows and enemy gunfire glyphs under unchanged ADR-058 rules, entering elimination rows with team plates and smooth row placement, a hit-marker pop and a gold headshot variant from DamageApplied region 1. The low-health vignette modulates the existing strength with a slow bounded pulse. Reduced motion removes pop/slide/row travel; reduced flash holds the vignette steady and softens damage arcs. Pure helpers stay in CombatHudVisual. No shader mode or generated payload is added.

Consequences: compass objective bearing, insertion blueprint/load scheduling, wordmark, cards and keycaps remain the next Wave 1b tranche. No enemy centroid is sampled for a compass marker. Full-match, 150% scale, named visual review and reference cost remain Pending until observed.

Evidence:

Checks on the implementation working tree (not a clean release candidate): Simulation **356**, Generation **508**, Game **99**, Contracts **79**, Unity EditMode **82**, source provenance **932 entries / 466 GUIDs**, specification fixtures and documentation verification PASS. No maps, generator bytes, shader modes, package settings or frozen contracts changed; Maps and native republishing are not required for this HUD-only tranche.

- Pure evidence: `Artifacts/m2-combat-harness/1676ff88…/20261116T204610705Z-dc41535e`, `Artifacts/m1-procedural-feasibility/1676ff88…/20261116T204730964Z-3668d6a7`, `Artifacts/m8-runtime-composition/1676ff88…/20261116T204912772Z-04114e0d`, `Artifacts/m0-foundation/1676ff88…/20261116T205002218Z-16af2f26`.
- Unity: `Artifacts/m0-foundation/1676ff88…/tests-editmode-20261116T204711784Z-23200cbf` PASS. Initial sandboxed SDK restore exited before tests; initial sandboxed Unity startup crashed (-536870911). Installed-environment retries pass. Wrapper pruning retains the failed editor run's small report under `Artifacts/reports/tests-editmode/`.
- Client IL2CPP build: `Artifacts/m0-foundation/1676ff88…/build-client-20261116T205153588Z-df48c5d9` PASS; manifest remains `sourceCommit=1676ff88a0a0dffeabbb7859d441f5fd66293751`, `sourceDirty=true`, source hash `392b0a5fbaf239cee9ba04f82e2144ee87d5969c9caf4d7104426b5f742d7494`. It includes the concurrent focus fixes later committed by the owner as `edbefa7`. Do not relabel it as a clean build of this delivery commit. The first build attempt was rejected because another Unity session had this project open; retry occurred after that process exited.
- Smoke PASS: `Artifacts/m0-foundation/f3a69461…/smoke-client-20261116T205435509Z-edc0faea`.
- Low TDM-5v5 short diagnostic PASS: `Artifacts/m0-foundation/f3a69461…/benchmark-diagnostic-20261116T205447453Z-70dc40c3`: 1,532 frames / 300 ticks, frame p95 **5.1296 ms**, p99 **6.5061 ms**. The recent roadmap diagnostic range was 4.7–5.5 ms, not a controlled paired reference baseline. `gateStatus=PendingHardware`; no full-product or reference-performance PASS.
- Gallery launch log: `Artifacts/visual-design/adr-087/20261116/gallery-player.log` reaches `SHOOTER_READY_CLIENT`, SDF/icon readiness and menu stage readiness. Computer-use app approval timed out before a screenshot could be inspected. The diagnostic player was closed; gallery/live HUD appearance is **Pending**, not observed. No screenshots or acceptance are claimed.
- Remaining: full-match event review (including headshot/kill-feed/reload), reduced-motion/flash player pass, native 720p 100%/150% captures, named review, reference costs, P05/VD/REL and a future clean installer. The current shipped installer remains 0.3.17; no installer was rebuilt for this partial Wave 1b step.

Rollback: revert this tranche; no save/content migration.


### ADR-087 second tranche — 2026-11-16

Owner assigned the full remaining visual roadmap. Compass chevron reads team-2 spawn coordinates from verified map metadata, never live enemy poses. Insertion draws collision footprints and both spawn markers only once verified map data is available; cold boot has no invented map. Media pans slowly, tips rotate every seven seconds, reduced motion freezes both. Wordmark has numeric stencil cuts; operation cards lift by four logical pixels and carry the objective glyph; Controls/reload hints use outlined keycaps. Verified binding is split into cancellable map mesh/material, weapon, soldier, FX and final session phases with a paint yield after each work unit. Indivisible Unity uploads exceeding 33 ms emit an overrun diagnostic; real player overruns remain Pending measurement. No fake percentages or shortened match rules. Checks and remaining slice work are recorded in the final roadmap handoff.


## ADR-088 — Accepted: seeded noise material family

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add material.noise 1.0.0 / shooter.recipe.material-noise.v1 with concrete, plaster, rust, painted-steel, asphalt, gravel, corrugated-metal and camo-fabric roles. Named NoiseField streams supply periodic cellular cracks, broad staining, warped streaks, chipped coating, aggregate/tar and cloth patterns. Bake physically scaled normals and linear RGBA albedo/mask mip chains offline at 128 pixels (256 on High). Existing material.surface 1.0.1/1.1.0 remains frozen. Extend map presentation metadata with opt-in material roles; retain collision/nav/spawn identities. Refreeze only the new registry inventory and explicitly add the generator row, schema and fixture.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-089 — Accepted: weathered map surface shading

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Retain the URP Lit forward/shadow/depth paths and the one normal/mask keyword set. Add AO-linked grime/roughness, low height-band dirt, 20 m macro variation and triplanar detail normal sampling. All additions are visual only; measure Low cost in the release player. The detail uses already packaged numeric normal maps; no new imported textures or geometry.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-090 — Accepted: packaged procedural decals and versioned visual layout operations

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Bake an eight-cell numeric decal atlas (stencil letters/numbers, hazard stripe, unit marking, oil, scorch and bullet damage) into SGAF. Arena layout documentVersion 2 adds visual-only decal operations and retains version-1 parsing; ignored visual operations never advance collision stable IDs or change navigation/spawns. Publish static decal placements in the verified map closure. Balanced/High configure the locked URP screen-space Decal renderer feature and projectors; Low uses depth-biased mesh quads. Impact marks use a bounded 64-slot pool and authority Impact events; no new collision queries. New shader passes use only the generated atlas. Refreeze the affected format registry source inventory explicitly without changing SGAF v1.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-091 — Accepted: numeric sky atmospheres, map grades and actor grounding light

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add a code-only cube sky with a once-per-map NoiseField cloud bake, morning haze / overcast / golden-hour presets from registry sky hints, matching reflected environment and height fog. Per-map integer grade parameters generate a 32-cube linear lookup for Balanced/High; Low uses only ColorAdjustments in LDR. Actor root samples of the verified AO volume drive one cosmetic visibility value per actor, plus a code-only contact shadow on Low. New sky/grade bytes have matching .NET and Mono goldens; no gameplay light/visibility changes or runtime geometry generation is introduced.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-092 — Accepted: numeric volume-noise combat flipbooks

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Add fx.flipbook 1.0.0: an 8 by 8 frame sequence for smoke puff, dust puff, fireball, muzzle star and spark streak. Integrate three-dimensional FBm and finite-difference curl advection along a volume ray with polynomial density envelopes; use IEEE operations only in hashed output. Store complete linear RGBA atlas mip chains through SGAF, cache immutable offline bakes and verify matching .NET/Mono aggregate goldens. No images or sprites are imported.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-093 — Accepted: bounded particle combat presentation

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: Replace combat mesh proxies with code-built manually advanced ParticleSystems using verified SGAF flipbooks. Keep the existing bounded AnalyticFxPool and event timing; Low emits fewer particles and skips depth softening. Surface-tag impacts choose sparks, dust or chips. Weapon definitions gain a validated muzzleFx profile; gunfire adds a smoke wisp and HDR tracers. Grenades add flash/fire/smoke/shockwave/debris/scorch plus reduced-motion-gated cosmetic camera shake. Copied movement produces bounded footstep/slide dust without new physics/sensing. ReducedFlash suppresses flash/fire/tracers while readable dust/smoke remains. No simulation, damage, shot or hit-volume behavior changes.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.

Addendum 2026-11-17 (surface impacts): the first implementation split bullet impacts only by the cover tag (sparks vs dust plus additive debris). Impacts now resolve three families through pure `ImpactSurfaceFx`: metal (cover tag, or the hit packaged collision box classifies to a metal role through the same `MapSolidsMesh.Classify` the solids mesh uses, plus oil-depot tank drums) gives sparks; concrete (other solids, or the floor on an asphalt-ground map) gives grey dust and chips; dirt (the floor on a soil-ground map) gives a brown upward kick-up and clods. Lookup is read-only against the verified packaged boxes and the registry `groundSurface`; the impact payload, simulation and package bytes are unchanged. Known limit: arena feature envelopes (vehicles, sandbags, tarp fences) are classified from their box alone, because their feature list is not in the packaged collision. Evidence: Simulation 364/364 (`Artifacts/m2-combat-harness/ff105841f92da3e717339607cbca30509a6e43ab/20261117T071440472Z-257ce52b`, 6 new `ImpactSurfaceFxTests`), EditMode 87/87 (`Artifacts/m0-foundation/ff105841f92da3e717339607cbca30509a6e43ab/tests-editmode-20261117T071546961Z-1bcef835`), source provenance PASS. Player look and owner review remain Pending.


## ADR-094 — Accepted: bounded soldier presentation revision and P05 reopening

Date 2026-11-16. Context: owner assigned the complete visual roadmap after `08eb4de`. Sole integrator; no gameplay or acceptance waiver.

Decision: The owner roadmap reopens ADR-024 only for this assignment. Bound: at most three measured candidate parameter sets and 90 minutes of P05 clearance/eye solver work after the first candidate measurement; retain all failed JSON/captures. Stop the search when either bound is reached. P05-D01 forearm/strap clearance, D02 eye/sight line and D03 trigger-arm penetration must be measured with the unchanged conservative proxy; no acceptance waiver or narrower measurement envelope. Add soldier.loft 1.0.3 with a 10-micrometre output lattice, canonical radius finite differences and explicit double frame arithmetic to make Mono/.NET publication portable, while retaining explicit 1.0.2 generation and the historical failing solver default for regression evidence. Current product uses the baseline pose solver; the failed correction is opt-in diagnostics only via `-shooterP05Candidate`. It uses lofted uniform/equipment modules, camo/fabric and numeric faction insignia. Cosmetic ground reads installed gameplay collision boxes, grip targets come from each weapon profile and head look-at is bounded. None feeds Simulation, hit volumes, AI or authoritative root motion. If the bounded correction still fails, keep P05 FAIL and deliver the implemented visual improvements with those residuals explicit.

Consequences: procedural numeric sources only, presentation read-only, quality-independent gameplay and the pinned toolchain remain binding. New visual bytes are republished under their stated identities.

Evidence: required pure/cross-lane/provenance/build checks and limitations are recorded in the visual-roadmap handoff before delivery. Missing player, reference and owner evidence remains Pending.

Rollback: revert this ADR implementation and republish matching source/content; retained old packages keep their original identities.


## ADR-095 — Accepted: structural weapon-from-eye shooting posture (P05 reopening)

Date 2026-11-17. Context: the ADR-094 bound is exhausted (three candidates, P05 FAIL). All three candidates changed only a neck look-at that pulled the head onto the sight line of a weapon whose stock stays on an unraised right shoulder joint. With the rifle sockets the sight line runs only 55 mm above the stock socket, so a head moved onto it lands on the trigger arm (worst at the +/-45 degree aim-yaw limit) and arm clearance got worse (-55.6 / -57.7 / -11.5 mm). The shipping baseline keeps the head clear but misses the sight line by up to 127 mm and still has a -1.9 mm left-forearm overlap at down-aim reload phase 0.45. The owner reviewed this root-cause analysis and authorized a structural revision instead of further head-only tuning.

Decision: the owner reopens P05 once more for one structural solver revision of `SoldierMotion` presentation posture. Allowed changes: a right-shoulder shooting posture (clavicle elevation/protraction moves the shoulder joint, and so the stock socket, toward the line of sight); a head pose that puts the right eye on the sight line by construction (cheek-weld cant/flex with the neck chain solved to that eye target); spine distribution of aim yaw; the existing clearance-aware elbow solve applied after the head is final; and an outward offset of the support-hand reload path where it passes the carrier. The skeleton, bind pose, limb lengths, weapon recipes/sockets, `ArmBodyClearance` proxy, gameplay pose/hit volumes, Simulation, AI and authoritative root motion stay unchanged. Nothing from the corrected pose feeds authority (ADR-004).

Gate, fixed before the first measurement: the unchanged `SoldierClearanceTests` envelope (stand/crouch x aim yaw -45/0/+45 x pitch -25/0/+25 x held plus reload phases 0..1 in 0.01 steps) on the baseline rifle must show minimum `ArmBodyClearance` >= 0 m, eye/sight-line residual <= 0.005 m on every sample, grip <= 0.02 m and stock socket error <= 1e-5 m. No tolerance relaxation, proxy change, narrower envelope or waiver. Bound: 180 minutes of solver work after the first full-envelope measurement; every measured parameter set's residuals are retained under `Artifacts/visual-design/adr-095/`. Stop at the bound.

If the gate passes, the revision becomes the product default under a new motion identity and the `-shooterP05Candidate` diagnostic is retired; P05-D01..D03 become numerically resolved, and P05 itself stays FAIL until a named visual review of front/side/back/three-quarter and live near/far captures accepts the candidate (the historical FAIL record is retained either way). If the gate fails, the product keeps the baseline solver, residuals are recorded and P05 stays FAIL.

Consequences: presentation-only change; quality-independent gameplay, procedural numeric sources and the pinned toolchain remain binding. A changed motion identity requires the feature-content lock refresh through its tool and republication of affected visual bytes.

Rollback: revert the solver and identity change; the baseline v5 solver and all ADR-024/094 evidence remain reproducible.

## ADR-096 — Accepted: music channel, combat sample bank v5 and owner audio-source relaxation

Date 2026-11-17. Context: owner reported that only rifle/pistol shots sound good; reload, knife and grenade cues sounded synthetic or were missing, and there was no music. The v4 bank (ADR-062, shipped as `combat-audio-bank-v4-stereo` after ADR-064's knife/grenade cues) used real recordings for gunshots and knife swing only; reload, impacts, footsteps, UI and ambience were project-rendered Foley. Nothing played in the menu or when a match finished.

Decision:
- **Source policy.** Owner decision: this is a non-commercial build, so a source's license no longer gates selection. Any freely downloadable recording or track may ship. Each file still records its source note, license (informational) and SHA-256 in its manifest. Extracting audio from commercial games stays out of scope. This amends the license sentence of ADR-055/062 and `features/audio.md`.
- **Bank v5.** `combat-audio-bank-v5-stereo` keeps the v4 slot order and adds `reload_pistol`, `knife_hit_flesh`, `knife_hit_hard`, `grenade_pin`, `grenade_bounce`, `grenade_explode_far`, `dry_fire`, `weapon_switch`, `hit_marker`, `kill_confirm`, `bullet_flyby`, `distant_battle`, `ui_hover`, `ui_click` and `ui_back`. A manifest may ship 1–8 variations per cue; each missing cue falls back to its nearest generated recipe. `Tools/build_audio_bank.py` renders the bank from `Tools/audio-sources.json`. Downloads stay under the ignored `.local/audio-src`. `keep` entries retain the v4 gunshot files byte-for-byte.
- **Routing (presentation only).**
  - Reload Foley is chosen by slot family (sidearm vs long gun). It is time-fitted within ±12 % to the `ActionPayload` reload window and faded out on the owner's death or next shot.
  - Knife `DamageApplied`/`Impact` on the swing's tick plays a flesh or hard hit.
  - The local instigator hears a hit tick and a kill confirm.
  - Enemy rounds passing within 2.5 m of the head play a flyby.
  - Grenade blasts add a distant layer. Two bounce clinks are scheduled analytically where the arc meets the thrower's feet height; a blast cancels them.
  - Dry fire, weapon switch and grenade pin come from the local lifecycle state.
  - A session-seeded distant-battle bed plays every 6–20 s.
  - No Simulation, C0 or Game-v1 event changes.
- **Music.** `MusicDirector` (pure, `Presentation.Model`) maps shell screens to: menu theme → silence while loading → calm bed → tension bed (latched when the leading team is within 15 kills of the limit or 60 s remain) → victory/defeat/draw stinger → results loop. Beds duck by up to −6 dB under local fire or a blast within 15 m. `UnityMusicOutput` crossfades two 2D `AudioSource`s and loads Vorbis-compressed OGG from `Content/Audio/music` (manifest with SHA-256 and optional loop region). It needs the built-in engine modules `com.unity.modules.unitywebrequest` and `com.unity.modules.unitywebrequestaudio`, which are exempt from the package locks (ADR-079). A missing or invalid music bank is silent with one warning. `Package-Release` ships the folder when present.
- **Menu UI sound.** A second small mixer instance sharing the bank plays hover/click/back from `TacticalUi`, so the menu has sound without an attached session.
- **Settings.** `UserSettings` gains `musicVolume` (default 0.6) as an additive, optional schema-2 key. Older files load with the default and older builds ignore the key, so `SettingsSchema` stays 2. The Game-v1 lock is refrozen for `GameData.cs`. The Audio tab gets a Music slider. Music gain = master × music.

Consequences: bank memory grows with the added cues. Music is decoded by the engine outside the 0.5 ms SFX mix target. Pure tests cover the new routing, the director, the manifest, the tracker and the settings key. Named listening review (FIX10/SET02) stays Pending until the owner records it.

Rollback: revert this change. The v4 manifest and its files are recoverable from git, and older settings files remain valid.

## ADR-097 — Accepted: headshot kill Score bonus

Date 2026-11-17. Context: owner playtest of 0.3.18 asked that a headshot kill be worth more than a body kill, for example 100 for the kill plus 20 for the head. ADR-059 fixed the killer's personal Score at exactly 100 and ADR-073 made the headshot badge worth 0 points.

Decision: an enemy kill whose fatal `DamageApplied` from the killer to the victim in the same tick batch has region 1 (head), as decided by `LocalKillBadgeLogic.IsFatalHeadshot`, adds `LocalKillBadgeLogic.HeadshotBonusPoints` = 20 personal Score on top of the ADR-059 kill points. It applies to every participant, bots included. `OfflineGameSession` adds it through `ITeamMatch.AddScore` directly after `ProcessDeath` and before `EvaluateEndOfTick`, so a match-ending headshot keeps its bonus. The local HUD Headshot award carries Points = 20: the feed shows `+20 HEADSHOT`, and the ribbon/ammo popup show the kill total (120). Team kills, the 150-kill limit, assists, environmental deaths and the C0/Game-v1 contracts are unchanged; the constant lives in `Game/Session`, so no lock refreeze is needed. This supersedes "exactly 100" in ADR-059 and "(0 points)" for the headshot badge in ADR-073.

Consequences: personal Score and results ordering can differ from earlier builds for the same play; team outcome cannot.

Rollback: set the bonus to 0 or revert this change.

## ADR-098 — Accepted: per-weapon first-person recoil presentation (camera climb + per-shot kick)

Date 2026-11-17. Context: owner playtest reported that the rifle's firing animation did not match its shot sound, and that recoil showed neither on the weapon nor on the camera while aiming. Simulation already deflected every shot by the authoritative recoil offset (`AuthoritativeRecoil`, up to 5° climb), but the camera never showed it, and a scoped ADS hides the rifle mesh, so the scope had no recoil feedback at all. The viewmodel spring (`x'' + 12x' + 40x`, about 1 Hz) used fixed slot impulses. At 600 RPM it piled up into a slow drift instead of one kick per report.

Decision:
- **Recoil stays weapon data.** `Tools/generate_weapon_catalog.py` copies each definition file's `combat.recoilPitchDegrees` / `recoilYawHalfDegrees` into `WeaponVisualProfile.RecoilPitchDegrees` / `RecoilYawHalfDegrees`, the same numbers the Simulation catalog uses. A new weapon gets its own camera and viewmodel recoil from its `.weapon.json` alone; presentation has no slot or id switches for recoil.
- **Camera recoil** (`Presentation/Model/View/FirstPersonCameraRecoil`, applied in `ProductSessionView`): the *climb* mirrors the authoritative pitch tick for tick (per-tick decay at 8/s, the extra decay inside `ApplyImpulse`, minus the weapon's pitch, clamp 5°). The screen centre and the scope reticle then stay on where rounds go. A cosmetic *punch* spring (peak ≈ 0.9 × one shot's climb, about 31 ms after the event, about 85% settled by 100 ms) gives every report a visible kick, ADS and scope included. Reduced camera motion drops the punch and keeps the climb. Authoritative yaw is seeded in Simulation, so it is not mirrored; only a small alternating cosmetic yaw punch is shown. The values are added to the camera rotation only and never reach aim input or authority. Combat events are now leased before the camera is posed, so the kick lands on the same frame as the shot audio.
- **Viewmodel kick** (`ProceduralViewModel`): each local ShotAccepted adds an impulse sized from the firing weapon's profile (`ShotKickImpulse`, peak 0.16 rad per recoil degree) to a stiff exact spring (`DampedSpring`, damping 18, frequency 36: peak about 31 ms, about 85% settled by 100 ms), so automatic fire pulses once per round. `EquipmentRecoilKick` scales up the rearward punch (about 1.7 cm at the rifle peak), which remains at full ADS. The ADS rotation and lateral offsets stay suppressed for sight alignment.

Unchanged: C0/Game-v1 contracts, Simulation recoil and hit results, weapon definition schema (no new fields), audio.

Validation: `WeaponRecoilPresentationTests` (profile/catalog recoil parity, camera climb equals `AuthoritativeRecoil` per tick for rifle, carbine and pistol, punch timing, reduced motion, remote/knife/duplicate filtering, per-weapon kick ordering, and per-shot pulsing under automatic fire). On 2026-11-17 Simulation 404, Game 102 and Generation 520 passed, and source provenance and `generate_weapon_catalog.py --check` passed. Unity EditMode, a client build and an in-game feel check are Pending.

Rollback: revert this change. The generated profile fields are optional constructor parameters.

## ADR-099 — Accepted: per-weapon first-person hand and weapon choreography

Date 2026-11-17. Context: the owner asked for better first-person hand animation per weapon, covering the regular view, shooting, reload, switching, grenade and knife. Before this change, one generic sine arc served every reload, and the same magazine went out and came back. Switches took a fixed 0.34 s regardless of the simulation's `equipTicks`. The knife played one 0.22 s arc against a 0.4 s interval, and the grenade cook snapped to a static pose. The throw was cut on the release tick, because the simulation switches back to the rifle in the same tick and the presentation dropped the throw with the slot. Sprint, jump/land, crouch and look had no viewmodel motion.

Decision:
- **Timing stays weapon data.** `Tools/generate_weapon_catalog.py` copies `combat.equipTicks`, `fireIntervalTicks`, `reloadTicks` and `fuseTicks` into `WeaponVisualProfile`. It also adds an optional `visual.adsSeconds` (default 0.15). The Simulation catalog is unchanged.
- **Choreography is numeric key tables, not clips.** `MotionCurve` is a C1 Hermite track whose extreme keys hold still. Every beat is an equipment-relative waypoint table in pure `Presentation/Model/View`. There are no authored bone poses or recordings, consistent with AGENTS.md and the motion graph in `soldiers-animation.md`.
- **Reload** (`FirstPersonReloadChoreography`): runs on the authoritative ActionPayload phase.
  - Rifle and carbine: tilt, press the release, then strip and drop the old magazine (tactical) or let it fall (empty). The hand fetches a second, fresh magazine from the belt, inserts it and slaps it home. Empty reloads add a bolt-catch press that releases the locked-back bolt.
  - Pistol: cant, thumb release (the magazine falls), fetch, insert and palm-seat. Empty reloads add an overhand slide rack.
  - The packaged magazine part is the dropped one. A second "FP fresh magazine" child that reuses the same mesh rides in the support hand until it seats, so no geometry is added.
  - The support wrist follows the hand track, and the elbow stays anchored (ADR-069).
  - Reduced motion halves the tilt and keeps the hand, magazine and weapon travel.
  - Third-person avatars keep `FirstPersonMagazineTravel`.
- **Shooting:** the bolt or slide cycle closes within `min(90 ms, 0.8 × fire interval)`, eased so it lingers at the rear and then slams home. On an empty magazine it stays locked back until the reload releases it.
- **Switching** (`FirstPersonEquipChoreography`): a presentation-only tuck of at most 0.10 s (and at most 35% of the window), then a per-weapon draw that fills the rest of the incoming weapon's equip window and ends with a small overshoot settle.
  - Rifle: swings up with a roll.
  - Pistol: rises muzzle-down.
  - Knife: wrist flip.
  - Grenade: comes up from the belt.

  This closes ADR-077's "slot-keyed first-person equip timing".
- **Knife:** each swing lasts the knife's fire interval and alternates right-to-left and left-to-right. A short wind-up is followed by a fast cut and recovery. A same-tick hit still upgrades the swing to a stab.
- **Grenade:**
  - Cook: a pin pull at the start (support hand to the ring, yank, lower away), then an eased raise into a cocked pose and a tremble that grows with the elapsed fuse share.
  - Throw: starts from the pose at release, hides the prop at 12% of the throw and keeps the hands through the follow-through.
  - `FirstPersonHandling` holds the outgoing grenade mesh while the throw plays, then swaps without a tuck and draws the rifle in the rest of its equip window (at least 0.18 s).
- **Movement** (`FirstPersonLocomotionPose`): `LocalMovementView` adds `Sprinting`, `Grounded` and `VerticalSpeedMetersPerSecond`, copied from `MotorBodyState`; `LocalMovementView` is not a frozen contract. The pose adds:
  - a sprint carry and figure-eight bob, dropped within about 50 ms by a shot, reload, special action, switch or ADS;
  - a jump lag and a landing dip scaled by fall speed (exact `DampedSpring`);
  - a crouch tuck and a slide roll;
  - look inertia: the gun trails a turn by at most 0.07 rad.

  Rotation layers fade fully at ADS, so the verified sight line is unchanged. The camera eye height now eases between stances (45 ms time constant) instead of snapping. The authoritative eye and ballistic origin are untouched.
- **Idle:** a slow breathing rise and fall is added to the existing sway.

Unchanged:
- C0/Game-v1 contracts, Simulation timing, hit results and recoil.
- Hand rig `fp.hand.v2` and the packaged meshes (no map-package republish).
- Third-person animation and audio.

Validation:
- `FirstPersonChoreographyTests` covers:
  - curve keys and overshoot;
  - reload continuity for every family and reload kind;
  - the magazine drop before the fresh one seats;
  - the empty-reload cycle lock and release;
  - reduced motion;
  - the draw finishing on the authoritative equip window for every weapon;
  - draw continuity;
  - the held throw swap;
  - knife duration and alternation;
  - pin pull and fuse tremble;
  - the throw surviving the switch back;
  - cycle timing;
  - the ADS profile time;
  - sprint blend in and out, landing dip scaling, frame-rate independence, look inertia bounds, crouch and reset.
- The Game test covers the new movement-view fields.
- Unity EditMode adds `ReloadDropsTheOldMagazineAndSeatsAFreshOneFromTheBelt`.
- `ShooterFeatureBuild.CaptureHandling` adds frames for the empty-reload fresh magazine, the bolt or slide release, the knife slash, the grenade pin pull and the grenade throw.
- On 2026-11-17: Simulation 434, Game 103, Generation 520 and Unity EditMode 88/88 passed. Source provenance, `generate_weapon_catalog.py --check` (generator tests 23) and the asmdef graph also passed. A client build, the CaptureHandling render and the in-game feel review are Pending.

Rollback: revert this change. The new profile fields and `LocalMovementView` fields are optional constructor parameters with neutral defaults.

## ADR-100 — Accepted: third-person directional gait, airborne pose and bot hops/slides

Date 2026-11-17. Context: owner playtest. When bots strafe, their legs float instead of stepping, the same happens in jumps, and jumps and slides either have no third-person animation or are never seen. Root causes:
- `SoldierMotion` placed every foot target along the aim heading. Bots strafe at 90 degrees to their aim (`CombatAiDirector.EngageMove`), so planted anchors and swing targets trailed sideways by up to a full stride cycle and the leg IK clamped at 98% reach (0.89 m foot-to-root measured on a 3 m/s strafe).
- There was no airborne pose. `ProductActorPresentation.PoseAvatar` ignored `Grounded` and vertical speed for third person, so the gait kept cycling in the air and sampled ground under elevated feet.
- The slide dive faced the aim, not the travel, and switched 0/1 without a blend.
- Bots jumped only when stuck (never in combat) and slid only in SeekCover/Reposition after 30 sprint ticks, with a 600-tick cooldown and 5 m of straight path, so players almost never saw either.

The owner approved bots jumping and sliding more often, and the `feature-content-lock.json` refreeze for the motion generator.

Decision:
- **Motion identity `soldier.motion.v7.locomotion`** (the ADR-095 shooting posture plus the items below; `ShootingPosture.Version`). The v5 baseline solver keeps `soldier.motion.spike.v5`; new `SoldierMotionInput` fields default to the previous behaviour (no heading means travel along aim; airborne 0; land -1).
- **Directional gait.** `SoldierMotionInput.MoveHeadingRadians` (world travel yaw). The stride offset of every foot target runs along the travel heading while the torso, weapon and knees keep the aim. The stance-width axis turns from the body blade toward the travel perpendicular as the actor side-steps (sign fixed to the body's right, widened up to 40%), so strafing feet shuffle staggered instead of crossing. Side-step/backpedal (travel more than about 60 degrees off aim, with hysteresis) uses a 1.0/1.6 m walk/run cycle and replans contacts on the mode change, like walk/run.
- **Reversals.** A travel turn of more than 120 degrees (a bot strafe flip) mirrors the gait phase: the planted foot's anchor becomes the leading contact for the new direction, and each swinging foot runs back along its own arc. The swing is continuous because the smoothing curve is symmetric. A turn of 60-120 degrees re-aims a swinging foot that is less than half way, with the swing start solved so the foot does not jump. Both stay inside the two-ground-query budget.
- **Airborne.** `Airborne` 0..1 selects a tuck pose with no ground samples. Pelvis at the gait's nominal height minus 4 cm; feet lift up to 0.30 m with a 0.14 m front/back split along travel while rising, and reach back down as the fall speed approaches 4 m/s. Gait contacts are released so feet re-plant on landing. Gait and airborne share one torso and upper-body solve.
- **Landing.** `Land` 0..1 dips the gait pelvis by `0.07 sin(pi land)` m; the leg IK supplies the knee bend.
- **Slide.** Hips, legs and the lead/trail feet use the travel heading; the spine twists back to the aim within the 45-degree torso budget, so the weapon stays on target.
- **Presentation.** `LocalMovementView` gains `VelocityXMetersPerSecond`/`VelocityZMetersPerSecond` (optional, not a frozen contract), copied from the motor in `OfflineGameSession.TryGetMovementView`. The pure `Presentation/Model/View/ThirdPersonLocomotionBlend` turns copied authority movement into the cosmetic inputs:
  - the heading follows velocity above 0.3 m/s, is held through slow frames (a strafe flip) and released below 0.05 m/s;
  - airborne when ungrounded and rising faster than 0.5 m/s, or ungrounded for more than 0.10 s (stairs and step edges do not flicker), blended over 0.08 s;
  - a 0.20 s landing after at least 0.15 s of airtime;
  - the slide eases in over 0.10 s and out over 0.15 s.
- **Bots** (`CombatAiDirector`, `AiTuning`, deterministic from id and tick):
  - Slides: also in Investigate and Search; 18 sprint ticks (was 30); 4 m straight path (was 5); 300-tick cooldown plus the `(id % 5) * 60` jitter (was 600). The motor's own slide gates are unchanged.
  - Traversal hop: while travelling (not engaging, firing, reloading or crouched), every 6 ticks a knee-height ray (0.35 m, above the 0.25 m motor step) looks 0.9 m along the move direction. A hit with a clear ray at 0.75 m (below the ~0.6 m jump apex plus the body) presses jump, with the existing 90-tick jump cooldown. Counter `TraversalJumps`.
  - Combat hop: on a strafe reversal whose new side is safe, at close or mid range, 25% chance (`AiNoise.SaltHop`), never while firing, in a burst, reloading, crouched or holding still; 180-tick hop cooldown and the jump cooldown. Counter `CombatHops`. The AI has no tightness readout, so the motor still refuses a jump below its tightness cost.

Unchanged: C0/Game-v1 contracts, motor, hit volumes and gameplay pose, simulation timing, skeleton and sockets, first-person presentation, map packages.

Validation:
- Generation `SoldierMotionTests`: strafe (max foot-to-root 0.33 m versus 0.89 m on the aim-heading stride; plant error and slide below 1e-6 m), backpedal, repeated strafe reversals with no contact reset, airborne tuck and reach with a ground sampler that always fails, landing dip, slide along travel with the weapon on aim, input validation. `SoldierClearanceTests` asserts the v7 identity on the unchanged ADR-095 gate.
- Simulation `ThirdPersonLocomotionBlendTests` (heading hold and release, jump and landing, stairs flicker, slide easing, reset) and `BotJumpSlideTests` (low-obstacle hop, tall walls ignored, deterministic combat hops with cooldown and no fire/crouch, none at long range, sooner and repeated slides).
- Game test: the movement view velocity matches the copied horizontal speed.
- `feature-content-lock.json` refrozen (source hashes, `motionVersion`).

Pending: client build and the owner's in-game look at bot strafes, hops and slides; P05 visual acceptance stays FAIL pending a named review.

Rollback: revert this change. That restores v6, the aim-heading stride and the previous bot tuning; the new input and view fields are optional with neutral defaults.

## ADR-101 — Accepted: selectable team sizes 5 / 8 / 10 / 15

Date 2026-11-17. Context: owner request to replace the product Setup choice of 5v5/6v6 with per-team sizes 5, 8, 10 and 15. Game-v1 was frozen at team sizes {5,6}, `MaxParticipants`/`MaxActiveActors` 12 and `CombatAiDirector.MaxBots` 11; arena maps emitted exactly 12 spawn slots per team; the bot display-name pool held 24 names. 15v15 needs 30 actors and 29 bots.

Decision:
- **Allowlist.** `SessionRequest.TeamSize` and catalog `supportedTeamSizes` accept only `{5,8,10,15}`. Product Setup shows a 2×2 grid (5v5 / 8v8 / 10v10 / 15v15). Default and unknown prefs/CLI values (including legacy saved `6`) clamp to **5**.
- **Capacity.** `GameContractVersion.MaxParticipants` / `MaxActiveActors` = **30**; `CombatAiDirector.MaxBots` = **29**. `TeamSizesMin`/`Max` = 5/15 with the explicit allowlist above. Game-v1 `ruleVersion` stays 1; C0-v1 and package encoding versions are unchanged.
- **AI budgets.** Keep the global **≤16 vision rays / ≤2 new paths per tick**. Every bot must still receive a sensing opportunity within **`MaxBots` active ticks** under a full roster; starvation remains a reject. No silent per-size budget scaling.
- **Spawns.** `MapSpawnLayout.SlotsPerTeam` = **15**; ArenaRules expects 15/team. Catalog gameplay packages are republished (hashes change).
- **Names.** `BotDisplayNames.Pool` grows to ≥29 unique short ASCII first names.
- **Workloads.** Keep `TDM-5v5-*`. Replace product `TDM-6v6-*` and soak-6v6 rows with `TDM-8v8-*`, `TDM-10v10-*`, `TDM-15v15-*` measurement identities and `TDM-soak-15v15-*` soaks. Historical 6v6 evidence stays archival; the product shell no longer starts size 6.
- **Locks.** Refreeze `game-contract-lock.json` after Contracts/fixture migration; refresh `content-format-lock.json` for the release-catalog schema/sample enum change.

Consequences: playtest matrix becomes three maps × four sizes; larger fights stress AI round-robin and presentation actor counts; reference captures/soaks must use the new workload IDs.

Rollback: revert this change, restore {5,6} allowlist and prior caps/slots/workloads, and republish catalog packages from the prior source.

Evidence: pure Game/Simulation/Generation/Maps lanes and lock verifiers after implementation; Unity EditMode for shell/setup; native content republish when the locked editor is available. Manual playtest and hardware remain Pending.

## ADR-102 — Accepted: named real-world arsenal, random spawn loadouts and muzzle flash fix

Date 2026-11-17. Context: owner request. Every gun was generic ("Rifle", "Sidearm"), shared two shapes, two shot sounds and two reload tracks, and the HUD showed slot names. The owner asked for guns modelled on real firearms (AK-47, AK-102, M16, a McMillan-style sniper, an "Mk 2" marksman rifle, Glock, a golden Desert Eagle), each with its own look, sound, animation and handling. Separately, firing showed smoke but no muzzle flash. Owner decisions: random loadout per spawn (no UI), "Mk 2" means a Mk 14 EBR-style DMR, brand names are replaced by close stand-ins, and shot sounds come from real recordings.

Decision:
- **Roster.** Five primaries and two sidearms, one `Content/Weapons/*.weapon.json` file each (ADR-077):

  | id | label | fire | dmg | falloff m (min) | mag | optic FOV |
  |---|---|---|---|---|---|---|
  | weapons/ak47 | AK-47 | auto, 6 ticks | 36 | 25-60 (0.75) | 30/90 | irons |
  | weapons/ak102 | AK-102 | auto, 5 ticks | 28 | 15-40 (0.70) | 30/90 | irons |
  | weapons/m16 | M16A4 | 3-round burst, 4 ticks inside, 18 after | 32 | 35-80 (0.80) | 30/90 | 2x scope, 40 deg |
  | weapons/mk14 | Mk 14 EBR | semi, 9 ticks | 55 | 60-150 (0.85) | 20/60 | 4x scope, 20 deg |
  | weapons/mtac338 | M-TAC 338 | bolt (semi, 72 ticks) | 120 | none | 5/25 | 8x scope, 10 deg |
  | weapons/g17 | G17 | semi, 7 ticks | 24 | 10-30 (0.60) | 17/51 | irons |
  | weapons/golden-eagle | Golden Eagle .50 | semi, 22 ticks | 55 | 15-40 (0.70) | 7/28 | irons |

  The generic `weapons/rifle`, `weapons/rifle-carbine` and `weapons/pistol` stay in the catalog with `spawnWeight 0`: they are never issued in a match, and remain the fixed default loadout for harness tests, diagnostics and the C0 fixtures, which are not migrated.
- **Simulation (not locked).** `WeaponDefinition` gains optional `burstCount`/`burstIntervalTicks` (`FireMode.Burst`), `falloffStartMeters`/`falloffEndMeters`/`falloffMinScale`, `moveSpeedScale` (clamped 0.5-1.2 in the motor) and `spawnWeight`. Burst progress is read back from `ShotIndex` (each burst starts on a multiple of the burst count), so the frozen C0 `WeaponState` is unchanged; any interruption ends the burst. Falloff scales base damage by the shot-origin-to-hit distance before the ADR-063 region multiplier.
- **Random loadout.** `WeaponLoadoutPolicy.CreateRandomLoadout(seed, actor, tick)` draws a weighted primary and sidearm (SplitMix64 of session seed, entity and tick), so a replay draws the same guns. `OfflineGameSession.RandomLoadouts` (default true) turns it on for product sessions; `CombatHarnessSimulation.RandomLoadouts` is off by default. Bots read magazine size and cadence from the guns they were issued (`IActorLoadoutSource`), and marksman/sniper guns aim down sights from medium range and wait for settled aim.
- **Geometry `geometry.weapon` 1.2.0.** Six archetype kinds (`ak`, `ar`, `dmr`, `bolt`, `pistol-striker`, `pistol-eagle`) with parts `barrelFrontMm`, `furniture` (black/gold/polymer/wood), `muzzle` (birdcage/brake/flash/none/slant), `optic` (irons/scope2/scope4/scope8) and `stock` (chassis/fixed/folding/none/wood). New `ArsenalGeometry` builds four LODs, magazine (curved AK, STANAG, box), cycle part (AK charging handle, AR charging handle, op rod, bolt, slides), sockets, collision and per-kind envelopes; 1.0.5/1.1.0 output is byte-identical. Material slot 5 (oiled wood) and 6 (polished gold) are presentation tints of the verified polymer and steel planes; no new textures. The first-person support hand follows each gun's `left-hand-grip` socket and soldier motion accepts every long-gun kind.
- **Handling.** Profile `handlingStyle` (Rifle, Ak, Bolt, Pistol): the AK rocks its magazine and, when empty, racks the right-side charging handle; the bolt gun swaps its box magazine and works the bolt on every reload and after every shot within the fire interval. `adsFovDegrees` sets magnified-optic zoom. `muzzleFlashScale` and `muzzleFlashSideJets` size each gun's flash; brakes vent two side jets.
- **Muzzle flash fix.** The flipbook bake writes frame 0 at byte row 0, but raw texture rows start at the bottom and the texture-sheet animation plays the top-left tile first, so every flipbook ran backwards and the flash peaked after it had faded. The presenter now flips rows on upload (`AnalyticFxPool.FlipTextureRows`); the `fx.flipbook` bake and map packages are unchanged. The flash is placed on the muzzle socket of the weapon the viewer sees (first-person view model or held weapon) and sorts above its smoke.
- **Audio bank v6.** `combat-audio-bank-v6-stereo` appends `ak47_shot`, `ak102_shot`, `m16_shot`, `mk14_shot`, `tac338_shot`, `g17_shot`, `eagle_shot`, `bolt_cycle`, `reload_ak` and `reload_bolt` after the unchanged v5 slots. Clips are cut from the already-sourced CC0 Free Firearm Sound Library (AK-47, AR-15, Tikka, Mosin Nagant, Arisaka, Walther PPQ, 1911) and the existing reload Foley; no new downloads. Missing cues derive from the real rifle/pistol takes by varispeed. Reload Foley follows the gun that last fired from that slot; bolt shots schedule the bolt cycle.
- **HUD.** The ammo panel shows the gun's display label; kill-feed glyphs gain one value per named gun with mesh-traced icons.

Consequences: `feature-content-lock.json` is refrozen for the 1.2.0 generator, sources, schema and the `rifle-1.2.0` fixture. The weapon package grows by seven mesh sets and must be republished with `Compile-GameContent.ps1`. No C0, Game-v1 or content-format lock change. Spawn weights favour assault rifles (3) over the DMR and sniper (2).

Rollback: set the new definitions' `spawnWeight` to 0 and the generic ones back to 1 (or revert the change), regenerate the catalogs and republish content.

Evidence: pure Simulation, Generation, Game and fixture lanes; Unity EditMode/PlayMode and a client build when the locked editor is available. Visual, audio and balance acceptance by the owner in a packaged build remain Pending.

## ADR-103 — Accepted: optical scope lens camera, Balanced/High quality uplift, flicker geometry fixes and damage feedback v2

Date 2026-11-17. Context: owner playtest at 1920x1080 High with VSync. (1) Map textures flicker sometimes. (2) Graphics look poor, worst through a sniper scope at range. (3) Taking damage, especially at low health, lacks feedback. The owner allowed project rules to bend where that gives the best graphics; this ADR records the decisions and supersedes ADR-065 (no second optic camera) and parts of ADR-080 and ADR-087.

Findings: the PiP lens was an 8x stretch of the hip-FOV 1080p frame (about 113 source pixels across the M-TAC 338 lens) with hip-FOV mips, LOD and shadows. `camera.allowMSAA` was set once before the saved tier was applied, so High rendered without anti-aliasing, and the post stack forced AA off. Generated wall trims had faces coplanar with the wall (top trim top, band/trim/end-cap ends) or 0.5-4 mm apart; metal cover slabs shared side planes with the sandbag top course; the apron sat 4 cm under the yard pad. Fog ended at 88-100 m on a 256 m map. The decal atlas had no mips.

Decision:
- **Optical lens camera.** `AdsScopePipPresenter` adds a child lens camera once the near-field rifle is hidden (`AdsOpticVisual.UsesLensCamera`). It renders at `AdsOpticVisual.LensCameraFovDegrees(hip, lensRadius, zoom)` (identical framing to the old digital zoom) into a square target sized to the lens diameter (cap 512/768/1024 per tier), with the tier MSAA and D32 depth, and the composite samples it 1:1. Blur, rim, recoil offset and reticle are unchanged. Reduced camera motion keeps the digital path. Camera targets use D32 depth instead of D24.
- **Anti-aliasing.** `ProductPostProcessing.Apply` owns per-camera MSAA and AA on every tier change: Low FXAA (texture-free), Balanced MSAA 2x, High MSAA 4x. SMAA stays off (ADR-080 lookup audit).
- **Tier budgets** (`ShooterBuild.Profile*`, regenerated by `InitializeFoundation`): shadows 40/80/150 m, cascades 2/3/4, atlas 1024/2048/4096, soft shadows on Balanced/High (High quality on High), normal bias 0.6 above Low. The sun uses pipeline bias and Soft mode. Generated surfaces and the decal atlas use anisotropic 2/8/16; the decal atlas is mipmapped/trilinear; projector draw distance 80/120/200 m. Fog start/end scale x1/x1.8/x2.6 per tier (height-fog density divided by the same factor). Low is unchanged apart from FXAA, so the 4600H Low gate budget is preserved.
- **Flicker geometry** (`MapSolidsMesh.AddWallDetails`, `MinDetailSeparation` 8 mm): the foundation band stops short of the end caps, the top trim rises 8 mm above the wall top and overhangs the ends, joints run between band and trim at 10 mm proud, end caps sit 8 mm proud of the wall end. Collision is unchanged. `ProductMapView` skips metal slabs on dressing-replaced features, lifts slabs so their top clears the cover top by 3 cm, and drops the apron top to -0.12 m. Low decal quads lift 15 mm, impact marks 20 mm.
- **Damage feedback v2.** `FirstPersonDamagePunch`: presentation-only flinch (pitch 1.2-4 deg up, yaw 0.8-2.5 deg away from the attacker, headshot x1.5, peak at 55 ms, settled by ~0.4 s), off under reduced motion. Damage-scaled edge flash (`DamageFlashStrength(..., damage)`, peak 0.38) is drawn again; translation shake doubled to 9 mm. A second code-built global volume ("Product damage volume", priority 20) applies `CombatHudVisual.LowHealth`: below 40 HP a red vignette 0.25-0.5, saturation to -55, contrast +8, exposure -0.25, pulsing with a heartbeat (1.1 Hz rising to 1.8 Hz at 25 HP), plus a 0.45 s hit grade. The IMGUI health vignette cap rises to 0.38. `LowHealthAudioFx` (post-mix, synthesised live) adds a sub-bass heartbeat below 40 HP, a body thud on each hit and a ~2.5 kHz low-pass muffle below 25 HP, scaled by master x effects volume. Reduced flash halves the grade and removes pulses and the hit grade.

Not done: the 512 px High surface bake (the frozen `material.noise` recipe only allows 128/256), and a near-plane change (viewmodel clipping risk; D32 reversed-Z already gives the precision).

Consequences: High costs more GPU (4x MSAA, 4096 four-cascade soft shadows, lens camera while scoped). Settings assets and `provenance.json` are regenerated; map packages recompiled. No C0, Game-v1, content-format or feature-content lock change.

Rollback: revert the commit and rerun `ShooterBuild.InitializeFoundation` and `Compile-GameContent.ps1 -Native`.

Evidence: pure Simulation (new `DamageFeedbackV2Tests`, lens FOV and tier tests), Generation, Maps, Game and SourceProvenance lanes; Unity EditMode/PlayMode, client build and smoke when run. Visual acceptance (scoped M-TAC 338 at 1080p High, flicker at 50-150 m, low-health feel) and a High benchmark remain Pending owner review.

## ADR-104 — Accepted: circular heading-up minimap with ring compass

Date 2026-11-17. Context: owner playtest after 0.3.20: the minimap did not show the local player or teammates and looked weak; the owner asked for a round minimap that turns with the view and for the compass to ring the minimap instead of sitting at the top of the screen, with the compass letters turning so N always points to north on the map. Supersedes ADR-058's north-up / Fill-pip clause and removes the top compass strip; gunfire reveal rules from ADR-058 are unchanged.

Finding: `DrawTacticalMap` drew inside `GUI.BeginGroup` and the self/ally markers were rotated `TacticalUi.MapGlyph` icons. IMGUI applies the group offset before `GUI.matrix`, so the rotation also rotated the group offset and every marker with a non-zero yaw landed outside the group and was clipped (the ADR-058 bug, reintroduced by the rotated glyphs). Unrotated gunfire blips still showed.

Decision:
- The minimap is drawn in absolute HUD space with no group. `UiSdf.BeginCircleClip/EndCircleClip` set a new `_ClipCircle` shader mask (view-space device px) applied to every SDF draw; flat fallbacks are culled to rects fully inside the circle.
- Heading-up disc, radius 75 px for 50 m (`CombatHudVisual.MinimapRadius/MinimapRangeMeters`), bottom-left of the safe area. Layers: shadowed bezel, radial-gradient disc, 25 m range ring, forward view cone, structures as rotated rounded rects, teammate arrows (rim pips when out of range), gunfire blips (rim-clamped and dimmed), centred self arrow pointing up.
- Compass: a 16 px bezel band around the disc with ticks every 15 degrees and N/E/S/W letters placed by `CompassRingAngle(bearing, heading)` so they turn with the map (N in amber); the objective (enemy spawn) marker sits just outside the ring at its bearing. A fixed amber notch and a three-digit heading readout sit above the ring. The HP/stamina block moves right of the ring.
- Pure math (`MinimapProject`, `MinimapClampToRim`, `CompassRingAngle`, `HeadingDegrees`, `RectInsideCircle`) lives in `CombatHudVisual` with xUnit coverage; the shader mask has an EditMode test.

Consequences: presentation only. No C0, Game-v1, content-format, sensing or authority change.

Rollback: revert the commit.

Evidence: Simulation pure lane and Unity EditMode; interactive review (rotation, markers, clipping, 150% UI scale) Pending owner playtest.

## ADR-105 — Accepted: dropped primary weapons and G pickup

Date 2026-11-18. Context: owner request: when a bot or the player dies, their main weapon should fall to the floor and stay for 15 seconds; the player walks to it and presses G to take it in place of their own main weapon; if it is the same gun, the player instead gets its ammo. Owner choices for this ADR: same-gun ammo goes to the reserve capped at the gun's reserve capacity; on a swap the old primary drops in its place; only the human picks up (bots keep their spawn loadout).

Decision:
- Authority: `Simulation/Combat/DroppedWeaponField` is a pure bounded list (32 entries; when full the entry closest to expiry is replaced) of dropped primaries with definition id, magazine, reserve, position and an expiry in **active match ticks** (`LifetimeActiveTicks = 900`, 15 s), so pause and the pre-match countdown do not run the clock. Only `WeaponFamily.PrimaryGun` with ammo drops.
- `OfflineGameSession.Step` captures the dying actor's slot 0 and motor feet inside the ordered death batch, before `RetireLife` removes the actor. Out-of-world kill-volume deaths (no instigator) drop nothing.
- Pickup: `TryFindPickup` picks the nearest entry within 1.75 m horizontally and 1.5 m vertically of the living human's feet. A different id is a **swap**: `CombatHarnessSimulation.ReplacePrimary` installs the dropped gun (magazine clamped to its size; `Equipping` for its equip time when slot 0 is in hand, which cancels a reload) and the old primary drops at the same spot with a fresh 15 s. The same id is an **ammo top-up**: `SetPrimaryReserve` to `min(ReserveCapacity, reserve + dropped magazine + dropped reserve)` and the drop is removed; it is offered only when it would add rounds. The pickup resolves after the death batch and retire step; the new gun first acts on the next tick.
- Expiry runs at end of tick after `EvaluateEndOfTick`; the field is cleared on runtime release.
- Input: like slide (ADR-039) the press travels beside the frozen C0 command: `LocalPlayerCommand.PickupPressed` → `OfflineGameSession.Step(human, slide, pickupRequested)`. C0 `InputButtons` is unchanged.
- Game-v1 additive binding `Pickup` (default Key.G, settings label "Pick up weapon"). Legacy schema-2 settings gain `Pickup=Key.G` when G is free, otherwise `Unbound` (ADR-056 pattern). `game-contract-lock.json` refrozen for `GameData.cs`.
- Presentation (read-only): `ProductActorPresentation.SyncDroppedWeapons` shows each drop with its packaged held mesh and palette lying on its side; it blinks in the last 3 s. `ProductSessionView.DrawPickupPrompt` shows "[G] TAKE AK-47" or "[G] TAKE AMMO" under the reticle using the same query as the authority (`TryGetHumanPickupPrompt`).
- A picked-up gun lasts one life: respawn re-draws the ADR-102 loadout.

Consequences: Game-v1 lock refreeze (bindings only); no C0, content-format, network or schema change. No new audio cue: a swap re-uses the equip timing; a pickup sound is a possible follow-up.

Rollback: revert the commit and restore the prior `game-contract-lock.json`. Settings saved with `Pickup` still load in the older build, whose binding validator keeps unknown additive actions.

Evidence: `Tools/Test-Simulation.ps1` (`DroppedWeaponFieldTests`), `Tools/Test-Game.ps1` (`DroppedWeaponSessionTests`, legacy Pickup migration), EditMode `InputBufferTests` pickup edge. In-game look/feel review Pending owner playtest.

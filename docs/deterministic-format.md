# Deterministic format and asset identity v1

Normative refinement of [generation](generation.md). The [fixture catalog](fixtures/index.md) supplies vectors and [verifier](fixtures/verify_fixtures.py). PRNG source reference: [PCG minimal C implementation](https://www.pcg-random.org/using-pcg-c-basic.html); selected scheme is PCG-XSH-RR 64/32 set-sequence, not another PCG family member.

## PRNG exact algorithm

State uint64 s, increment uint64 inc; arithmetic wraps modulo 2^64. Next: old=s; s=old×6364136223846793005+inc; x=uint32(((old>>18)^old)>>27); r=uint32(old>>59); return uint32((x>>r)|(x<<((-r)&31))). Cast x to32 bits before rotate. Initialization: s=0; inc=(initseq<<1)|1; Next(); s+=initstate; Next(). For initstate42/initseq54 the first six Next values are a15c02b7,7b47f409,ba1d3330,83d2f293,bfa4784b,cbed606e; compare exact bits. These are the published example outputs, also reproduced by fixture verifier.

Unit float32 = (Next()>>8)×(1/16777216), half-open[0,1), consuming exactly one value. Uniform bounded integer bound b>0 uses rejection threshold uint32(−b)%b and returns r%b once r≥threshold; b=0 rejects. Never modulo-only bounded selection. No RNG shared across jobs or procedural features.

Named stream bytes: LP(domain ASCII=`shooter.stream.v1`) + uint64LE(rootSeed) + LP(recipeId UTF8) + LP(featureKey UTF8). LP=uint32LE byte length followed by bytes, lengths≤4096. SHA256(bytes); initstate=first8 digest bytes little-endian, initseq=next8 little-endian; remaining digest retained in stream ID for diagnostics. Stable feature keys use logical slots such as `building/03/window/02`, not generation traversal index.

## Canonical JSON subset

All object keys/identifier strings ASCII. Keys sorted ordinal; no BOM/whitespace/trailing newline inside hashed bytes; separators comma/colon, JSON strings escape quote/backslash/control characters only, no slash escaping. Duplicate keys rejected before dictionary construction. Arrays preserve order. JSON bool/null allowed where schema says so. All JSON numbers must be integers within signed32bit for fixture source; uint64 seeds/IDs are canonical decimal strings with no leading zeros. Physical dimensions stored integer millimeters, angles integer millidegrees, colors integer linear0..65535, scalar coefficients integer parts-per-million, durations integer ticks. This replaces unspecified float decimal precision in v1 recipe sources. Runtime float DTOs are not canonical JSON hash sources.

Canonicalize only schema-valid documents; values outside allowed units/ranges reject, never silently round. SourceHash=SHA256(LP(schemaId)+LP(canonical JSON bytes)). For sourceHash input, schemaId is the recipe's schemaId and document includes all source fields except no embedded self hash. Canonical fixture stores literal expected UTF8 and digest; validator verifies rather than silently updating.

## Stable logical identity versus content

LogicalAssetId = recipeId + '/' + artifactKind + '/' + variant, independent of seed/version/payload where a Unity asset reference should remain stable. Stable generated Unity GUID = first16 bytes SHA256(LP(`shooter.unity-guid.v1`)+LP(LogicalAssetId)), lowercase32hex. Include world logical slot in ID for unique chunk assets; registry rejects GUID collisions before import. GUID is not ContentId. `.meta` stays stable when underlying generated content changes; content manifest maps logical asset+build to versioned artifact IDs. If side-by-side seed variants must coexist, give them distinct logical variant names explicitly.

ContentId is the deterministic build-request key. Encode tuple using LP for strings/raw hashes, uint64LE seed, uint32LE dependency count and sorted32-byte dependency hashes: domain,sourceHash,generatorId,generatorVersion,seed,dependencies,targetPlatform,visualProfile,formatVersion(uint32LE),toolchainHash. Gameplay content uses profile=`gameplay` always. See fixture verifier's `content_key` for exact executable encoding. Artifact SHA256 hashes actual payload bytes separately. Same request producing different payload hashes is a reproducibility error; the request key alone is not proof payload integrity.

Manifest deterministic fields include IDs, inputs, payload hashes/sizes, bounds/counts, static provenance and versions only. Build duration, timestamps, host/driver/worker IDs and timings live in separate `Artifacts/<run-id>/build-report.json`, outside content identity and immutable payload comparisons. Deterministic validation summary may live in manifest; validation run timestamps/log hashes may not. Compression v1=`none`; future compression changes formatVersion/toolchain fingerprint and needs stable output policy.

## Cache and codecs

Manifest≤1 MiB, ≤256 artifacts, ≤64 MiB/artifact and ≤256 MiB decompressed/content. Skeleton≤256 joints; meshes≤2million vertices/6million indices per artifact hard guard, much lower feature budgets still apply. Read bounds before allocating. Reject path traversal, rooted paths and escaping symlinks. Content directories immutable after atomic publish. Build telemetry cannot cause same-key different-byte cache failures.

Binary geometry exact field order is frozen at P02 with tiny golden mesh before dependent adapters merge. Until then no worker invents a private binary schema. Disk artifact test fixture is deliberately a three-byte codec payload, not a model or shipping asset. N01 freezes transport envelope after adapter choice; disk encoding does not silently become wire protocol.

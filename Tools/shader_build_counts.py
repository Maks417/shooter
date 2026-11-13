"""Extract retained shader stage/program counts from a real locked Unity release build log.

These are compiler stage counts, not graphics pipeline combinations or runtime measurements.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

SHADER = "Shooter/ProceduralSurface"
EXPECTED = {("ForwardLit", "vp"), ("ForwardLit", "fp"), ("ShadowCaster", "vp"), ("ShadowCaster", "fp")}
COUNT = r"([0-9]+|[0-9]{1,3}(?:[.,][0-9]{3})+)"


def integer(value):
    return int(value.replace(",", "").replace(".", ""))


def parse(raw):
    if not raw or len(raw) > 64 * 1024 * 1024:
        raise ValueError("Build log outside bounded1..64MiB range")
    if raw.count(b"\n") > 500000:
        raise ValueError("Build log exceeds500000 lines")
    lines = raw.decode("utf-8-sig", errors="strict").splitlines()
    starts = [i for i, line in enumerate(lines) if line == 'Compiling shader "' + SHADER + '"']
    if len(starts) != 1:
        raise ValueError("Required shader compiler section missing/ambiguous")
    start = starts[0]
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith('Compiling shader "')), len(lines))
    passes, active, serialized = {}, None, None
    labels = {"Full variant space": "full", "After settings filtering": "settings", "After built-in stripping": "builtIn", "After scriptable stripping": "scriptable"}
    for i in range(start + 1, end):
        line = lines[i]
        match = re.fullmatch(r'  Pass "([^"]+)" \(([a-z]+)\)', line)
        if match:
            active = tuple(match.groups())
            if active not in EXPECTED or active in passes:
                raise ValueError("Unexpected/duplicate shader pass-stage")
            passes[active] = {"pass": active[0], "stage": active[1], "line": i + 1, "countLines": {}}
            continue
        match = re.fullmatch(r"    Target graphics API: (\S+)", line)
        if match and active is not None:
            if "api" in passes[active] or match[1] != "d3d11":
                raise ValueError("Shader stage graphics API mismatch/duplicate")
            passes[active]["api"] = match[1]
        for label, field in labels.items():
            match = re.fullmatch(r"    " + re.escape(label) + r":\s+" + COUNT, line)
            if match and active is not None:
                if field in passes[active]:
                    raise ValueError("Duplicate shader stage count")
                passes[active][field] = integer(match[1]); passes[active]["countLines"][field] = i + 1
        match = re.fullmatch(r"    d3d11 \(total internal programs: " + COUNT + r", unique: " + COUNT + r"\)", line)
        if match:
            if serialized is not None:
                raise ValueError("Duplicate serialized shader program summary")
            serialized = {"totalInternalPrograms": integer(match[1]), "uniqueInternalPrograms": integer(match[2]), "line": i + 1}
    if set(passes) != EXPECTED or serialized is None:
        raise ValueError("Incomplete shader stage/serialized counts")
    for stage in passes.values():
        if not all(key in stage for key in ("api", "full", "settings", "builtIn", "scriptable")):
            raise ValueError("Incomplete shader stage counts")
        if not 0 < stage["scriptable"] <= stage["builtIn"] <= stage["settings"] <= stage["full"]:
            raise ValueError("Invalid shader stripping count order")
    total = sum(stage["scriptable"] for stage in passes.values())
    if serialized["totalInternalPrograms"] != total or not 0 < serialized["uniqueInternalPrograms"] <= total:
        raise ValueError("Serialized shader stage/program counts contradict stripping results")
    return {"shader": SHADER, "graphicsApi": "d3d11", "sectionLine": start + 1,
            "stages": list(passes.values()), "retainedStagePrograms": total, **serialized,
            "pipelineVariantCount": None,
            "pipelineVariantCountUnavailable": "Stage/compiler program counts do not identify complete graphics pipeline combinations or runtime variants",
            "scope": "Actual build compiler stripping/serialized counts, including cached programs; not new compilation count, runtime cost or global URP variants"}


def sha(path, limit=64 * 1024 * 1024):
    if not 0 < path.stat().st_size <= limit:
        raise ValueError("Proof input exceeds bounded size")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        total = 0
        while chunk := stream.read(65536):
            total += len(chunk)
            if total > limit:
                raise ValueError("Proof input grew beyond bounded size")
            digest.update(chunk)
    return digest.hexdigest()


def read_bounded(path, limit):
    with path.open("rb") as stream:
        raw = stream.read(limit + 1)
    if not 0 < len(raw) <= limit:
        raise ValueError("Build proof input outside bounded size")
    return raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-run", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError("Refusing to overwrite shader count proof")
    manifest_path, log = args.build_run / "build-manifest.json", args.build_run / "editor.log"
    if not 0 < manifest_path.stat().st_size <= 1024 * 1024 or not 0 < log.stat().st_size <= 64 * 1024 * 1024:
        raise ValueError("Build proof inputs exceed bounded size")
    manifest_raw = read_bounded(manifest_path, 1024 * 1024)
    log_raw = read_bounded(log, 64 * 1024 * 1024)
    build = json.loads(manifest_raw.decode("utf-8-sig"))
    if build.get("result") != "Succeeded" or build.get("target") != "Client" or build.get("backend") != "IL2CPP" or build.get("development") is not False:
        raise ValueError("Succeeded release IL2CPP client build required")
    root = Path(__file__).resolve().parents[1]
    if build.get("toolchainHash") != sha(root / "toolchain-lock.json"):
        raise ValueError("Build toolchain does not match locked repository")
    if re.fullmatch(r"[0-9a-f]{40}", build.get("sourceCommit", "")) is None:
        raise ValueError("Build source identity missing")
    proof = {"schemaVersion": 1, "result": "Verified", "sourceCommit": build["sourceCommit"], "sourceDirty": build.get("sourceDirty"),
             "toolchainHash": build["toolchainHash"], "buildManifestSha256": hashlib.sha256(manifest_raw).hexdigest(), "buildLogSha256": hashlib.sha256(log_raw).hexdigest(),
             "parserSha256": sha(Path(__file__)), **parse(log_raw)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(proof, indent=2))


if __name__ == "__main__":
    main()

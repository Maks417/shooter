"""Materialize the pinned Core RP source curation; never edit Unity's installed package or Library cache."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
REMOVED = ["Runtime/Debugging/Prefabs/Resources/" + name + suffix
           for name in ["DebugUICanvas.prefab", "DebugUIPersistentCanvas.prefab"] for suffix in ["", ".meta"]]


def inventory(path):
    rows = []
    for file in sorted(p for p in path.rglob("*") if p.is_file()):
        rows.append(file.relative_to(path).as_posix() + ":" + hashlib.sha256(file.read_bytes()).hexdigest())
    return hashlib.sha256("\n".join(rows).encode()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--record-initial-lock", action="store_true")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    source = args.editor.resolve().parent / "Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core"
    package = json.loads((source / "package.json").read_text())
    lock_path = ROOT / "toolchain-lock.json"
    lock = json.loads(lock_path.read_text(encoding="utf-8-sig"))
    if package["version"] != lock["urpVersion"]:
        raise ValueError("Installed Core RP version does not match the editor/URP lock")
    original_hash = inventory(source)
    destination = ROOT / ".local/engine-packages/com.unity.render-pipelines.core"
    expected_patch = lock.get("corePackageCuration")
    if expected_patch and (original_hash != expected_patch["upstreamInventorySha256"] or expected_patch["removedPaths"] != REMOVED):
        raise ValueError("Installed Core RP source differs from pinned curation inputs")
    if destination.exists():
        if not expected_patch or inventory(destination) != expected_patch["curatedInventorySha256"]:
            raise ValueError("Existing curated package differs; refusing to overwrite or bless it")
    else:
        for file in source.rglob("*"):
            if file.is_dir():
                (destination / file.relative_to(source)).mkdir(parents=True, exist_ok=True)
                continue
            if not file.is_file() or file.relative_to(source).as_posix() in REMOVED:
                continue
            target = destination / file.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, target)
    curated_hash = inventory(destination)
    proof = {"schemaVersion": 1, "package": package["name"], "version": package["version"],
             "upstreamInventorySha256": original_hash, "curatedInventorySha256": curated_hash, "removedPaths": REMOVED,
             "reason": "Exclude unused Resources debug UI prefabs and their authored sprite dependencies from client/server players; see ADR-013"}
    if args.record_initial_lock:
        if expected_patch and expected_patch != proof:
            raise ValueError("Cannot silently replace an existing package curation lock")
        lock["corePackageCuration"] = proof
        lock_path.write_bytes((json.dumps(lock, indent=2) + "\n").encode())
        manifest_path = ROOT / "UnityProject/Packages/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        manifest["dependencies"]["com.unity.render-pipelines.core"] = "file:../../.local/engine-packages/com.unity.render-pipelines.core"
        manifest_path.write_bytes((json.dumps(manifest, indent=2) + "\n").encode())
    elif expected_patch != proof:
        raise ValueError("Source curation is not pinned; initial integrator review is required")
    output = ROOT / "Artifacts/unity-setup/core-package-curation.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes((json.dumps(proof, indent=2) + "\n").encode())
    print("Verified pinned Core RP source curation" if args.quiet else json.dumps(proof, indent=2))


if __name__ == "__main__":
    main()

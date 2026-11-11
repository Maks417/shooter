"""Materialize the pinned Core RP source curation; never edit Unity's installed package or Library cache."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct
import tarfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
REMOVED = ["Runtime/Debugging/Prefabs/Resources/" + name + suffix
           for name in ["DebugUICanvas.prefab", "DebugUIPersistentCanvas.prefab"] for suffix in ["", ".meta"]]


def inventory(path, excluded=()):
    rows = []
    for file in sorted(p for p in path.rglob("*") if p.is_file()):
        relative = file.relative_to(path).as_posix()
        if relative not in excluded:
            rows.append(relative + ":" + hashlib.sha256(file.read_bytes()).hexdigest())
    return hashlib.sha256("\n".join(rows).encode()).hexdigest()


def immutable_archive(source):
    """Stable tar metadata + stored DEFLATE blocks: independent of zlib compression versions."""
    payload = io.BytesIO()
    with tarfile.open(fileobj=payload, mode="w", format=tarfile.PAX_FORMAT) as archive:
        for path in sorted(source.rglob("*")):
            relative = path.relative_to(source).as_posix()
            if relative in REMOVED:
                continue
            if path.is_symlink():
                raise ValueError("Unexpected source symlink")
            entry = tarfile.TarInfo("package/" + relative)
            entry.mtime = entry.uid = entry.gid = 0
            if path.is_dir():
                entry.type, entry.mode = tarfile.DIRTYPE, 0o755
                archive.addfile(entry)
            elif path.is_file():
                entry.size, entry.mode = path.stat().st_size, 0o644
                with path.open("rb") as stream:
                    archive.addfile(entry, stream)
    data = payload.getvalue()
    result = bytearray(bytes.fromhex("1f8b08000000000000ff"))
    for offset in range(0, len(data), 65535):
        block = data[offset:offset + 65535]
        result.extend(struct.pack("<BHH", int(offset + len(block) == len(data)), len(block), len(block) ^ 0xffff))
        result.extend(block)
    result.extend(struct.pack("<II", zlib.crc32(data), len(data) & 0xffffffff))
    return bytes(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--record-initial-lock", action="store_true")
    parser.add_argument("--migrate-immutable-archive", action="store_true")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    source = args.editor.resolve().parent / "Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core"
    package = json.loads((source / "package.json").read_text())
    lock_path = ROOT / "toolchain-lock.json"
    lock = json.loads(lock_path.read_text(encoding="utf-8-sig"))
    if package["version"] != lock["urpVersion"]:
        raise ValueError("Installed Core RP version does not match the editor/URP lock")
    original_hash = inventory(source)
    archive_relative = ".local/engine-packages/com.unity.render-pipelines.core-" + package["version"] + "-shooter.tgz"
    destination = ROOT / archive_relative
    expected_patch = lock.get("corePackageCuration")
    if expected_patch and (original_hash != expected_patch["upstreamInventorySha256"] or expected_patch["removedPaths"] != REMOVED):
        raise ValueError("Installed Core RP source differs from pinned curation inputs")
    curated_hash = inventory(source, REMOVED)
    data = immutable_archive(source)
    archive_hash = hashlib.sha256(data).hexdigest()
    if destination.exists() and hashlib.sha256(destination.read_bytes()).hexdigest() != archive_hash:
        raise ValueError("Existing package archive differs; refusing to overwrite or bless it")
    if not destination.exists():
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    proof = {"schemaVersion": 2, "package": package["name"], "version": package["version"],
             "upstreamInventorySha256": original_hash, "curatedInventorySha256": curated_hash, "removedPaths": REMOVED,
             "archiveFormat": "pax-tar-gzip-stored-v1", "archivePath": archive_relative, "archiveSha256": archive_hash,
             "reason": "Exclude unused Resources debug UI prefabs and their authored sprite dependencies from client/server players; see ADR-013"}
    if args.record_initial_lock or args.migrate_immutable_archive:
        old_proof = {key: value for key, value in proof.items() if key not in ("archiveFormat", "archivePath", "archiveSha256")}
        old_proof["schemaVersion"] = 1
        if args.migrate_immutable_archive and expected_patch != old_proof:
            raise ValueError("Archive migration requires the unchanged schema-1 curation inputs")
        if args.record_initial_lock and expected_patch and expected_patch != proof:
            raise ValueError("Cannot silently replace an existing package curation lock")
        lock["corePackageCuration"] = proof
        lock_path.write_bytes((json.dumps(lock, indent=2) + "\n").encode())
        manifest_path = ROOT / "UnityProject/Packages/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        manifest["dependencies"]["com.unity.render-pipelines.core"] = "file:../../" + archive_relative
        manifest_path.write_bytes((json.dumps(manifest, indent=2) + "\n").encode())
    elif expected_patch != proof:
        raise ValueError("Source curation is not pinned; initial integrator review is required")
    output = ROOT / "Artifacts/unity-setup/core-package-curation.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes((json.dumps(proof, indent=2) + "\n").encode())
    print("Verified pinned Core RP source curation" if args.quiet else json.dumps(proof, indent=2))


if __name__ == "__main__":
    main()

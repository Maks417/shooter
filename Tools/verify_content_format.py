"""Verify the integrator-frozen P02 format/source registry; never refresh implicitly."""
import hashlib
import json
from pathlib import Path
from shooterlib import sha

ROOT = Path(__file__).resolve().parents[1]
DIRECTORIES = (
    "UnityProject/Assets/Shooter/Generation/Core/Codecs",
    "UnityProject/Assets/Shooter/Generation/Core/Manifests",
    "UnityProject/Assets/Shooter/Generation/Core/Recipes",
    "UnityProject/Assets/Shooter/Generation/Core/Canonical",
    "UnityProject/Assets/Shooter/Generation/Core/Identity",
    "UnityProject/Assets/Shooter/Generation/Core/Random",
    "docs/content-schemas",
)
FILES = ("docs/content-format.md", "Tools/Generation.Tests/CodecTests.cs",
         "Tools/Generation.Tests/CodecFixtures/generate_vectors.py",
         "UnityProject/Assets/Shooter/Generation/Recipes/box-collision.json")



def inventory(root):
    paths = [root / name for name in FILES]
    for directory in DIRECTORIES:
        paths.extend(p for p in (root / directory).rglob("*") if p.is_file() and p.suffix in {".cs", ".meta", ".json"})
    return {p.relative_to(root).as_posix(): sha(p) for p in sorted(paths)}


def verify(root=ROOT):
    lock = json.loads((root / "content-format-lock.json").read_text(encoding="utf-8"))
    if lock.get("formatVersion") != 1 or lock.get("status") != "FrozenByIntegrator" or lock.get("contractVersion") != "C0-v1":
        raise ValueError("Unsupported or unfrozen compiler format baseline")
    if lock.get("sources") != inventory(root) or lock.get("toolchainHash") != sha(root / "toolchain-lock.json"):
        raise ValueError("Compiler format, fixtures, source registry or toolchain drift; review version/migration before refreezing")
    return {"result": "PASS", "formatVersion": 1, "contractVersion": "C0-v1", "files": len(lock["sources"]),
            "scope": "P02 format/source registry identity only; not cache, content, build or hardware acceptance"}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))

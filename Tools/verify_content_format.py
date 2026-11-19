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


def _cli(root, refreeze_fn, verify_fn):
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refreeze", metavar="ADR", help="record a reviewed change approved by this ADR (T195)")
    args = parser.parse_args()
    print(json.dumps(refreeze_fn(root, args.refreeze) if args.refreeze else verify_fn(root), indent=2))


def _record(lock, decision):
    if not decision or not decision.startswith("ADR-"):
        raise ValueError("A refreeze names the ADR that approved it")
    decisions = lock.setdefault("refreezeDecisions", [])
    if decision not in decisions:
        decisions.append(decision)
    import datetime
    lock["reviewDate"] = datetime.date.today().isoformat()


def refreeze(root, decision):
    """Reviewed refreeze: the format sources/schemas/toolchain as they are now, under the approving ADR."""
    path = root / "content-format-lock.json"
    lock = json.loads(path.read_text(encoding="utf-8"))
    _record(lock, decision)
    lock["sources"] = inventory(root)
    lock["toolchainHash"] = sha(root / "toolchain-lock.json")
    path.write_text(json.dumps(lock, indent=2) + "\n", encoding="utf-8", newline="\n")
    return verify(root)


if __name__ == "__main__":
    _cli(ROOT, refreeze, verify)

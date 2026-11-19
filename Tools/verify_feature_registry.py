"""Verify the reviewed M1 feature generator/schema/source registry without implicit refresh."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTORIES = (
    "UnityProject/Assets/Shooter/Generation/Core/Geometry",
    "UnityProject/Assets/Shooter/Generation/Core/Weapons",
    "UnityProject/Assets/Shooter/Generation/Core/Materials",
    "UnityProject/Assets/Shooter/Generation/Core/Fx",
    "UnityProject/Assets/Shooter/Generation/Core/Compilation",
    "UnityProject/Assets/Shooter/Generation/Core/Soldiers",
    "UnityProject/Assets/Shooter/Generation/Core/SoldierMotion",
    "UnityProject/Assets/Shooter/Generation/FeatureRecipes",
    "docs/feature-schemas", "docs/feature-fixtures",
)


def inventory(root=ROOT):
    paths = [root / "docs/weapon-recipe-baseline.md", root / "docs/soldier-baseline.md"]
    for directory in DIRECTORIES:
        paths.extend(p for p in (root / directory).rglob("*") if p.is_file() and p.suffix in {".cs", ".json"})
    result = {}
    for path in sorted(paths):
        data = path.read_bytes()
        if b"\r\n" in data:
            raise ValueError("Registry sources must use repository LF bytes: " + str(path))
        result[path.relative_to(root).as_posix()] = hashlib.sha256(data).hexdigest()
    return result


def verify(root=ROOT):
    registry = json.loads((root / "feature-content-lock.json").read_text(encoding="utf-8"))
    if registry.get("schemaVersion") != 1 or registry.get("formatVersion") != 1 or registry.get("contractVersion") != "C0-v1":
        raise ValueError("Unsupported feature registry")
    if registry.get("sources") != inventory(root):
        raise ValueError("Feature source/recipe/schema drift; review generator version and migration before refreezing")
    return {"result": "PASS", "generators": registry["generators"], "files": len(registry["sources"]),
            "scope": "M1 feature source registry identity only; no visual, gameplay or hardware acceptance"}


def refreeze(decision, generator=None, root=ROOT):
    """Deliberate, reviewed refreeze: record the ADR, optionally a new generator row, and the current source hashes."""
    path = root / "feature-content-lock.json"
    registry = json.loads(path.read_text(encoding="utf-8"))
    if not decision.startswith("ADR-"):
        raise ValueError("A refreeze names the ADR that approved it")
    if decision not in registry["decisions"]:
        registry["decisions"].append(decision)
    if generator is not None and generator not in registry["generators"]:
        registry["generators"].append(generator)
    registry["sources"] = inventory(root)
    path.write_text(json.dumps(registry, indent=2) + "\n", encoding="utf-8", newline="\n")
    return verify(root)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refreeze", metavar="ADR", help="record a reviewed source change approved by this ADR")
    parser.add_argument("--generator", metavar="ID@VERSION@SCHEMA", help="with --refreeze: add a generator row")
    args = parser.parse_args()
    if args.refreeze:
        row = None
        if args.generator:
            gid, version, schema = args.generator.split("@")
            row = {"id": gid, "version": version, "schema": schema}
        print(json.dumps(refreeze(args.refreeze, row), indent=2))
    else:
        print(json.dumps(verify(), indent=2))

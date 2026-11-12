"""Verify the reviewed M1 feature generator/schema/source registry without implicit refresh."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTORIES = (
    "UnityProject/Assets/Shooter/Generation/Core/Geometry",
    "UnityProject/Assets/Shooter/Generation/Core/Weapons",
    "UnityProject/Assets/Shooter/Generation/Core/Materials",
    "UnityProject/Assets/Shooter/Generation/Core/Compilation",
    "UnityProject/Assets/Shooter/Generation/FeatureRecipes",
    "docs/feature-schemas", "docs/feature-fixtures",
)


def inventory(root=ROOT):
    paths = [root / "docs/weapon-recipe-baseline.md"]
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


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))

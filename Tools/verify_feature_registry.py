"""Verify the reviewed M1 feature generator/schema/source registry without implicit refresh.

Source policy (ADR-121, sourceHashPolicy "cs-tokens-v1"): recipes, schemas, fixtures and baseline docs are hashed as raw
LF bytes. C# generator sources are hashed by their code tokens: comments are dropped, whitespace runs collapse to one
space and spaces next to ( ) { } [ ] , ; are removed. A comment or formatting edit therefore needs no refreeze; any
change to code, string literals or numbers still fails until a reviewed `--refreeze ADR-NNN`.
"""
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
POLICY = "cs-tokens-v1"
TIGHT = set("(){}[],;")


def cs_canonical(text: str) -> str:
    """Code-token form of C# source: comments removed, whitespace normalized, literals kept byte-exact."""
    out, i, n = [], 0, len(text)
    pending_space = False

    def emit(chunk):
        nonlocal pending_space
        if pending_space and out and out[-1][-1] not in TIGHT and chunk[0] not in TIGHT:
            out.append(" ")
        pending_space = False
        out.append(chunk)

    while i < n:
        c = text[i]
        if c.isspace():
            pending_space = True
            i += 1
        elif text.startswith("//", i):
            end = text.find("\n", i)
            i = n if end < 0 else end
            pending_space = True
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            if end < 0:
                raise ValueError("Unterminated block comment")
            i = end + 2
            pending_space = True
        elif c == "@" and i + 1 < n and text[i + 1] == '"':
            j = i + 2
            while True:
                j = text.find('"', j)
                if j < 0:
                    raise ValueError("Unterminated verbatim string")
                if j + 1 < n and text[j + 1] == '"':
                    j += 2
                    continue
                break
            emit(text[i:j + 1])
            i = j + 1
        elif c in "\"'":
            j = i + 1
            while j < n and text[j] != c:
                if text[j] == "\\":
                    j += 1
                if text[j] == "\n":
                    raise ValueError("Unterminated literal")
                j += 1
            if j >= n:
                raise ValueError("Unterminated literal")
            emit(text[i:j + 1])
            i = j + 1
        else:
            j = i
            while j < n and not text[j].isspace() and not text.startswith("//", j) and not text.startswith("/*", j) \
                    and text[j] not in "\"'" and not (text[j] == "@" and j + 1 < n and text[j + 1] == '"'):
                j += 1
            emit(text[i:j])
            i = j
    return "".join(out)


def file_hash(path: Path, policy=POLICY) -> str:
    data = path.read_bytes()
    if b"\r\n" in data:
        raise ValueError("Registry sources must use repository LF bytes: " + str(path))
    if policy == POLICY and path.suffix == ".cs":
        data = cs_canonical(data.decode("utf-8-sig")).encode("utf-8")
    return hashlib.sha256(data).hexdigest()


def inventory(root=ROOT, policy=POLICY):
    paths = [root / "docs/weapon-recipe-baseline.md", root / "docs/soldier-baseline.md"]
    for directory in DIRECTORIES:
        paths.extend(p for p in (root / directory).rglob("*") if p.is_file() and p.suffix in {".cs", ".json"})
    return {path.relative_to(root).as_posix(): file_hash(path, policy) for path in sorted(paths)}


def verify(root=ROOT):
    registry = json.loads((root / "feature-content-lock.json").read_text(encoding="utf-8"))
    if registry.get("schemaVersion") != 1 or registry.get("formatVersion") != 1 or registry.get("contractVersion") != "C0-v1":
        raise ValueError("Unsupported feature registry")
    policy = registry.get("sourceHashPolicy", "raw-bytes")
    if registry.get("sources") != inventory(root, policy):
        raise ValueError("Feature source/recipe/schema drift; review generator version and migration before refreezing")
    return {"result": "PASS", "generators": registry["generators"], "files": len(registry["sources"]),
            "sourceHashPolicy": policy,
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
    registry["sourceHashPolicy"] = POLICY
    registry["sources"] = inventory(root)
    path.write_text(json.dumps(registry, indent=2) + "\n", encoding="utf-8", newline="\n")
    return verify(root)


def migrate_policy(decision, root=ROOT):
    """One-time switch to cs-tokens-v1: refuses unless the tree still matches the old raw-byte lock exactly."""
    path = root / "feature-content-lock.json"
    registry = json.loads(path.read_text(encoding="utf-8"))
    if registry.get("sourceHashPolicy", "raw-bytes") == POLICY:
        raise ValueError("Already on " + POLICY)
    if registry.get("sources") != inventory(root, "raw-bytes"):
        raise ValueError("Sources drifted from the raw-byte lock; review them before migrating the hash policy")
    return refreeze(decision, None, root)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--refreeze", metavar="ADR", help="record a reviewed source change approved by this ADR")
    parser.add_argument("--generator", metavar="ID@VERSION@SCHEMA", help="with --refreeze: add a generator row")
    parser.add_argument("--migrate-policy", metavar="ADR", help="one-time switch of an unchanged raw-byte lock to " + POLICY)
    args = parser.parse_args()
    if args.migrate_policy:
        print(json.dumps(migrate_policy(args.migrate_policy), indent=2))
    elif args.refreeze:
        row = None
        if args.generator:
            gid, version, schema = args.generator.split("@")
            row = {"id": gid, "version": version, "schema": schema}
        print(json.dumps(refreeze(args.refreeze, row), indent=2))
    else:
        print(json.dumps(verify(), indent=2))

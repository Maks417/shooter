"""Validate actual P02 source/manifest with pinned full Draft 2020-12 validation."""
import argparse
import copy
import importlib.metadata
import json
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[1]


def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def read(path):
    if path.stat().st_size > 1024 * 1024:
        raise ValueError("JSON file exceeds 1 MiB")
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=pairs)


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--manifest", type=Path, required=True)
    args.add_argument("--report", type=Path, required=True)
    options = args.parse_args()
    version = importlib.metadata.version("jsonschema")
    if version != "4.26.0":
        raise RuntimeError("Expected locked jsonschema 4.26.0")
    manifest_schema = read(ROOT / "docs/content-schemas/manifest.schema.json")
    recipe_schema = read(ROOT / "docs/content-schemas/box-collision.schema.json")
    for schema in (manifest_schema, recipe_schema):
        Draft202012Validator.check_schema(schema)
    source = read(ROOT / "UnityProject/Assets/Shooter/Generation/Recipes/box-collision.json")
    manifest = read(options.manifest)
    cases = [("numeric-source", source, recipe_schema, True), ("actual-published-manifest", manifest, manifest_schema, True)]
    negative = copy.deepcopy(source); negative["parameters"]["widthMm"] = -1
    cases.append(("negative-width", negative, recipe_schema, False))
    for name, mutation in (
        ("escaping-artifact-path", lambda v: v["artifacts"][0].update(path="../escape.bin")),
        ("oversized-artifact", lambda v: v["artifacts"][0].update(uncompressedBytes=67108865)),
        ("unknown-schema", lambda v: v.update(manifestSchema="unknown")),
        ("telemetry-in-manifest", lambda v: v.update(timestamp="2026-11-12")),
    ):
        negative = copy.deepcopy(manifest); mutation(negative)
        cases.append((name, negative, manifest_schema, False))
    results = []
    for name, value, schema, valid in cases:
        errors = list(Draft202012Validator(schema).iter_errors(value))
        results.append({"case": name, "passed": (not errors) == valid, "errorPaths": [error.json_path for error in errors]})
    report = {"result": "PASS" if all(case["passed"] for case in results) else "FAIL", "validatorVersion": version,
              "manifest": str(options.manifest.resolve()), "cases": results,
              "scope": "P02 structural schemas and negative cases only; C# tests verify semantic/hash/IO invariants"}
    options.report.parent.mkdir(parents=True, exist_ok=True)
    options.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report["result"] != "PASS"


if __name__ == "__main__":
    raise SystemExit(main())

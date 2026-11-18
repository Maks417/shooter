"""Validate registered feature inputs and explicit generator-version migration fixtures."""
import argparse
import importlib.metadata
import json
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    if importlib.metadata.version("jsonschema") != "4.26.0":
        raise RuntimeError("Expected pinned jsonschema 4.26.0")
    schemas = {name: json.loads((ROOT / f"docs/feature-schemas/{name}.schema.json").read_text(encoding="utf-8")) for name in ("weapon", "material", "soldier", "material-noise", "fx-flipbook")}
    for schema in schemas.values():
        Draft202012Validator.check_schema(schema)
    outcomes = []
    sources = [(p, True) for p in sorted((ROOT / "UnityProject/Assets/Shooter/Generation/FeatureRecipes").glob("*.json"))]
    # Versions each generator still accepts (ADR-075 material.surface 1.1.0, ADR-082 geometry.weapon 1.1.0,
    # ADR-102 geometry.weapon 1.2.0, ADR-106 geometry.weapon 1.3.0, ADR-107 geometry.weapon 1.4.0, soldier.loft 1.1.0 and
    # material.noise 1.1.0, ADR-116 material.noise 1.2.0); older
    # fixtures are migration evidence and must be rejected by the current schema.
    current_versions = {"geometry.weapon": {"1.0.5", "1.1.0", "1.2.0", "1.3.0", "1.4.0"}, "material.surface": {"1.0.1", "1.1.0"}, "soldier.loft": {"1.0.2", "1.0.3", "1.1.0"}, "material.noise": {"1.0.0", "1.1.0", "1.2.0"}, "fx.flipbook": {"1.0.0"}}
    sources += [(p, json.loads(p.read_text(encoding="utf-8"))["generatorVersion"] in
                 current_versions[json.loads(p.read_text(encoding="utf-8"))["generatorId"]])
                for p in sorted((ROOT / "docs/feature-fixtures").glob("*.json"))]
    for path, expected_valid in sources:
        value = json.loads(path.read_text(encoding="utf-8"))
        schema = schemas[{"geometry.weapon": "weapon", "material.surface": "material", "soldier.loft": "soldier", "material.noise": "material-noise", "fx.flipbook": "fx-flipbook"}[value["generatorId"]]]
        errors = list(Draft202012Validator(schema).iter_errors(value))
        outcomes.append({"source": path.relative_to(ROOT).as_posix(), "expectedValid": expected_valid, "passed": (not errors) == expected_valid, "errorPaths": [e.json_path for e in errors]})
    report = {"result": "PASS" if outcomes and all(v["passed"] for v in outcomes) else "FAIL", "cases": outcomes,
              "scope": "Full Draft 2020-12 structural schemas and explicit version migration; C# tests check all semantic/hash/topology/mip constraints"}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report["result"] != "PASS"


if __name__ == "__main__":
    raise SystemExit(main())

"""Full Draft 2020-12 fixture validation; install Tools/requirements-fixtures.lock first."""
from pathlib import Path
import argparse
import importlib.metadata
import json
import sys

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "docs" / "fixtures"
EXPECTED_RESULTS = {"Valid": True, "ValidWithPayloadHashCheck": True,
                    "ValidUnmeasuredOnly": True, "InvalidInput": False}


def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def read(name):
    path = (FIXTURES / name).resolve()
    if not path.is_relative_to(FIXTURES):
        raise ValueError("Escaping fixture path")
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError(value)))


def validate_catalog(catalog):
    """Reject an unusable test plan before any fixture can count as passing."""
    if not isinstance(catalog, dict) or type(catalog.get("schemaVersion")) is not int or catalog["schemaVersion"] != 1:
        raise ValueError("Expected fixture catalog schemaVersion 1")
    cases = catalog.get("cases")
    if not isinstance(cases, list) or not cases:
        raise ValueError("Fixture catalog must contain a nonempty cases list")
    seen = set()
    for index, case in enumerate(cases):
        if not isinstance(case, dict):
            raise ValueError(f"Catalog case {index} must be an object")
        for field in ("fixture", "schema", "expected"):
            if not isinstance(case.get(field), str) or not case[field].strip():
                raise ValueError(f"Catalog case {index} requires a nonempty {field} string")
        if case["expected"] not in EXPECTED_RESULTS:
            raise ValueError(f"Catalog case {index} has an unknown expected result")
        identity = (case["fixture"], case["schema"])
        if identity in seen:
            raise ValueError(f"Duplicate fixture/schema pair at catalog case {index}")
        seen.add(identity)
        if "path" in case and "paths" in case:
            raise ValueError(f"Catalog case {index} cannot contain both path and paths")
        if "path" in case and (not isinstance(case["path"], str) or not case["path"].strip()):
            raise ValueError(f"Catalog case {index} requires a nonempty path string")
        if "paths" in case and (not isinstance(case["paths"], list) or not case["paths"] or
                                any(not isinstance(path, str) or not path.strip() for path in case["paths"])):
            raise ValueError(f"Catalog case {index} requires a nonempty list of path strings")
        if not EXPECTED_RESULTS[case["expected"]] and not ("path" in case or "paths" in case):
            raise ValueError(f"Invalid catalog case {index} requires expected error paths")
    return cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    installed = importlib.metadata.version("jsonschema")
    if installed != "4.26.0":
        raise RuntimeError("Expected locked jsonschema 4.26.0, got " + installed)
    failures, outcomes = [], []
    for case in validate_catalog(read("expected-cases.json")):
        schema, value = read(case["schema"]), read(case["fixture"])
        Draft202012Validator.check_schema(schema)
        errors = sorted(Draft202012Validator(schema).iter_errors(value), key=lambda e: str(e.json_path))
        expected_valid = EXPECTED_RESULTS[case["expected"]]
        actual_valid = not errors
        paths = {error.json_path for error in errors}
        expected_paths = set(case.get("paths", [case["path"]] if "path" in case else []))
        passed = actual_valid == expected_valid and expected_paths.issubset(paths)
        outcomes.append({"fixture": case["fixture"], "expected": case["expected"],
                         "schemaValid": actual_valid, "errorPaths": sorted(paths), "passed": passed})
        if not passed:
            failures.append(case["fixture"])
    report = {"schemaVersion": 1, "validator": "jsonschema", "validatorVersion": installed,
              "dialect": "2020-12", "result": "FAIL" if failures else "PASS", "cases": outcomes,
              "scope": "Bundled JSON Schema cases only. Semantic hashes/vectors use verify_fixtures.py and C# tests; no Unity/game/hardware acceptance."}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return bool(failures)


if __name__ == "__main__":
    sys.exit(main())

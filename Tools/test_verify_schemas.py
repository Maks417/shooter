"""Regression tests for the schema verifier; all synthetic inputs are test-only."""
import contextlib
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import verify_schemas


class CatalogTests(unittest.TestCase):
    def catalog(self, **changes):
        case = {"fixture": "value.json", "schema": "value.schema.json", "expected": "Valid"}
        case.update(changes)
        return {"schemaVersion": 1, "cases": [case]}

    def run_main(self):
        output = io.StringIO()
        with patch("sys.argv", ["verify_schemas.py"]), contextlib.redirect_stdout(output):
            result = verify_schemas.main()
        return result, json.loads(output.getvalue())

    def test_bundled_catalog_runs_all_six_cases_with_locked_validator(self):
        result, report = self.run_main()
        self.assertFalse(result)
        self.assertEqual(report["validatorVersion"], "4.26.0")
        self.assertEqual(report["result"], "PASS")
        self.assertEqual(len(report["cases"]), 6)
        self.assertTrue(all(case["passed"] for case in report["cases"]))

    def test_empty_or_unsupported_catalog_is_rejected(self):
        catalogs = [None, [], {}, {"schemaVersion": 1, "cases": []},
                    {"schemaVersion": 1, "cases": {}}, {"schemaVersion": 1, "cases": None}]
        for version in (True, "1", 2):
            catalog = self.catalog()
            catalog["schemaVersion"] = version
            catalogs.append(catalog)
        for catalog in catalogs:
            with self.subTest(catalog=catalog), self.assertRaises(ValueError):
                verify_schemas.validate_catalog(catalog)

    def test_malformed_cases_and_unknown_expectations_are_rejected(self):
        catalogs = [{"schemaVersion": 1, "cases": [None]}]
        for field in ("fixture", "schema", "expected"):
            for value in (None, [], 1, "", "   "):
                catalogs.append(self.catalog(**{field: value}))
        catalogs.extend([self.catalog(expected="ValidTypo"), self.catalog(expected="InvalidTypo")])
        for catalog in catalogs:
            with self.subTest(catalog=catalog), self.assertRaises(ValueError):
                verify_schemas.validate_catalog(catalog)

    def test_duplicate_fixture_schema_pairs_are_rejected(self):
        catalog = self.catalog()
        catalog["cases"].append(copy.deepcopy(catalog["cases"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate fixture/schema"):
            verify_schemas.validate_catalog(catalog)

    def test_invalid_cases_require_well_typed_unambiguous_error_paths(self):
        cases = [{}, {"path": ""}, {"path": None}, {"path": []}, {"paths": []},
                 {"paths": "$.x"}, {"paths": [None]}, {"paths": [""]},
                 {"path": "$.x", "paths": ["$.x"]}]
        for fields in cases:
            with self.subTest(fields=fields), self.assertRaises(ValueError):
                verify_schemas.validate_catalog(self.catalog(expected="InvalidInput", **fields))
        for fields in ({"path": "$.x"}, {"paths": ["$.x", "$.y"]}):
            self.assertEqual(len(verify_schemas.validate_catalog(self.catalog(expected="InvalidInput", **fields))), 1)

    def test_negative_case_with_wrong_expected_path_fails(self):
        catalog = verify_schemas.read("expected-cases.json")
        catalog["cases"][1]["path"] = "$.notTheActualError"
        original_read = verify_schemas.read
        with patch.object(verify_schemas, "read", side_effect=lambda name: catalog if name == "expected-cases.json" else original_read(name)):
            result, report = self.run_main()
        self.assertTrue(result)
        self.assertEqual(report["result"], "FAIL")
        self.assertFalse(report["cases"][1]["passed"])

    def test_empty_catalog_cannot_produce_a_pass_report(self):
        with patch.object(verify_schemas, "read", return_value={"schemaVersion": 1, "cases": []}):
            with self.assertRaises(ValueError):
                self.run_main()


class FixtureLoaderTests(unittest.TestCase):
    def test_paths_outside_fixture_root_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "fixtures"
            root.mkdir()
            outside = Path(directory) / "outside.json"
            outside.write_text("{}", encoding="utf-8")
            with patch.object(verify_schemas, "FIXTURES", root):
                for name in ("../outside.json", str(outside)):
                    with self.subTest(name=name), self.assertRaisesRegex(ValueError, "Escaping fixture path"):
                        verify_schemas.read(name)

    def test_duplicate_keys_and_nonfinite_json_numbers_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.object(verify_schemas, "FIXTURES", root):
                for data in ('{"a":1,"a":2}', '{"a":{"b":1,"b":2}}', 'NaN', 'Infinity', '-Infinity'):
                    (root / "value.json").write_text(data, encoding="utf-8")
                    with self.subTest(data=data), self.assertRaises(ValueError):
                        verify_schemas.read("value.json")


if __name__ == "__main__":
    unittest.main()

#!/usr/bin/env python3
"""Guard tests for T062 visual defect inventory: P05 FAIL stays immutable."""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import product_visual_defect_inventory as inv


class ProductVisualDefectInventoryTests(unittest.TestCase):
    def test_build_keeps_p05_fail_and_blocks_fourth_search(self):
        payload = inv.build_inventory("abc123", False, "/tmp/inv")
        self.assertEqual(payload["visualAcceptance"], "FAIL")
        self.assertIs(payload["fourthClearanceSearchAuthorized"], False)
        self.assertEqual(payload["disposition"], "InventoryOnly")
        self.assertEqual(payload["fix11Status"], "ToolingReady")
        self.assertEqual(payload["rel03Status"], "Open")
        p05 = [d for d in payload["defects"] if d["id"].startswith("P05-")]
        self.assertGreaterEqual(len(p05), 2)
        for defect in p05:
            self.assertNotIn(defect["status"], ("Resolved", "PASS", "Pass", "pass"))

    def test_validate_rejects_p05_resolved(self):
        payload = inv.build_inventory("abc123", False, "/tmp/inv")
        payload["defects"][0]["status"] = "Resolved"
        with self.assertRaises(ValueError):
            inv.validate_inventory(payload)

    def test_validate_rejects_pass_relabel(self):
        payload = inv.build_inventory("abc123", False, "/tmp/inv")
        # Mutate a fresh copy only; do not poison shared DEFECTS.
        payload = dict(payload)
        payload["visualAcceptance"] = "PASS"
        with self.assertRaises(ValueError):
            inv.validate_inventory(payload)

    def test_cli_writes_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            code = inv.main(
                [
                    "--output-dir",
                    tmp,
                    "--source-commit",
                    "deadbeef",
                    "--source-dirty",
                    "true",
                ]
            )
            self.assertEqual(code, 0)
            inventory = json.loads((Path(tmp) / "defect-inventory.json").read_text(encoding="utf-8"))
            status = json.loads((Path(tmp) / "p05-status.json").read_text(encoding="utf-8"))
            self.assertEqual(inventory["visualAcceptance"], "FAIL")
            self.assertFalse(inventory["fourthClearanceSearchAuthorized"])
            self.assertEqual(status["visualAcceptance"], "FAIL")
            self.assertTrue((Path(tmp) / "README.txt").is_file())


if __name__ == "__main__":
    unittest.main()

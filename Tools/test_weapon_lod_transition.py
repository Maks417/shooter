"""Synthetic happy/failure paths for weapon_lod_transition analyzer. Never hardware evidence."""
import json
import tempfile
import unittest
from pathlib import Path

import weapon_lod_transition as wlt


class SelectPolicyTests(unittest.TestCase):
    def test_hysteresis_sticky_and_crossings(self):
        for threshold, near in ((0.50, 0), (0.20, 1), (0.08, 2)):
            far = near + 1
            self.assertEqual(wlt.select(threshold * 0.98, near), near)
            self.assertEqual(wlt.select(threshold * 1.02, far), far)
            self.assertEqual(wlt.select(threshold * 0.89, near), far)
            self.assertEqual(wlt.select(threshold * 1.11, far), near)

    def test_first_person_and_camera_cut(self):
        self.assertEqual(wlt.select(0.001, 3, first_person=True), 0)
        self.assertEqual(wlt.select(0.001, 0), 3)
        self.assertEqual(wlt.select(2.0, 3), 0)


class AnalyzerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def write(self, name, payload):
        path = self.root / name
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path

    def test_sweep_report_passes_and_marks_visual_pending(self):
        report = wlt.build_sweep_report()
        summary = wlt.analyze(report)
        self.assertEqual(summary["result"], "PASS")
        self.assertEqual(summary["numericPolicyResult"], "PASS")
        self.assertEqual(summary["visualCaptureResult"], "Pending")
        self.assertGreater(summary["sampleCount"], 20)
        self.assertGreater(summary["totalSwitches"], 0)
        self.assertEqual(summary["overrideCounts"]["firstPerson"], 1)
        self.assertEqual(summary["overrideCounts"]["inspect"], 1)
        self.assertEqual(summary["flickerEvents"], [])
        self.assertEqual(summary["policyViolations"], [])

    def test_policy_mismatch_fails(self):
        report = {
            "schemaVersion": 1,
            "samples": [
                {"heightFraction": 0.5, "selectedLod": 0, "previousLod": 0, "override": None},
                {"heightFraction": 0.45, "selectedLod": 1, "previousLod": 0, "override": None},  # still inside band
            ],
        }
        summary = wlt.analyze(report)
        self.assertEqual(summary["result"], "FAIL")
        self.assertEqual(summary["numericPolicyResult"], "FAIL")
        self.assertEqual(summary["visualCaptureResult"], "Pending")
        self.assertEqual(len(summary["policyViolations"]), 1)
        self.assertEqual(summary["policyViolations"][0]["kind"], "automaticSelectionMismatch")

    def test_flicker_inside_hysteresis_fails(self):
        # Forced illegal A-B-A while height stays inside the 0.50 deadband.
        report = {
            "schemaVersion": 1,
            "samples": [
                {"heightFraction": 0.50, "selectedLod": 0, "previousLod": 0, "override": None},
                {"heightFraction": 0.50, "selectedLod": 1, "previousLod": 0, "override": None},
                {"heightFraction": 0.50, "selectedLod": 0, "previousLod": 1, "override": None},
            ],
        }
        summary = wlt.analyze(report)
        self.assertEqual(summary["result"], "FAIL")
        self.assertGreaterEqual(len(summary["policyViolations"]), 1)
        self.assertEqual(len(summary["flickerEvents"]), 1)
        self.assertEqual(summary["flickerEvents"][0]["pattern"], "0-1-0")

    def test_first_person_override_must_be_lod0(self):
        report = {
            "schemaVersion": 1,
            "samples": [
                {"heightFraction": 0.001, "selectedLod": 2, "previousLod": 3, "override": "firstPerson"},
            ],
        }
        summary = wlt.analyze(report)
        self.assertEqual(summary["result"], "FAIL")
        self.assertEqual(summary["policyViolations"][0]["kind"], "firstPersonMustBeLod0")

    def test_inspect_override_is_accepted(self):
        report = {
            "schemaVersion": 1,
            "samples": [
                {"heightFraction": 0.001, "selectedLod": 3, "previousLod": 0, "override": "inspect"},
            ],
        }
        summary = wlt.analyze(report)
        self.assertEqual(summary["result"], "PASS")
        self.assertEqual(summary["overrideCounts"]["inspect"], 1)

    def test_cli_emit_and_analyze(self):
        samples = self.root / "samples.json"
        analysis = self.root / "analysis.json"
        code = wlt.main(["--emit-sweep", str(samples), "--output", str(analysis)])
        self.assertEqual(code, 0)
        self.assertTrue(samples.is_file())
        payload = json.loads(analysis.read_text(encoding="utf-8"))
        self.assertEqual(payload["result"], "PASS")
        self.assertEqual(payload["visualCaptureResult"], "Pending")

    def test_cli_fails_on_bad_input(self):
        bad = self.write("bad.json", {
            "schemaVersion": 1,
            "samples": [
                {"heightFraction": 0.45, "selectedLod": 1, "previousLod": 0, "override": None},
            ],
        })
        analysis = self.root / "bad-analysis.json"
        code = wlt.main(["--input", str(bad), "--output", str(analysis)])
        self.assertEqual(code, 1)
        self.assertEqual(json.loads(analysis.read_text(encoding="utf-8"))["result"], "FAIL")

    def test_empty_samples_rejected(self):
        with self.assertRaisesRegex(ValueError, "samples"):
            wlt.analyze({"schemaVersion": 1, "samples": []})


if __name__ == "__main__":
    unittest.main()

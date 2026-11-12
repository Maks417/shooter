"""Synthetic proof/thermal validation only; no hardware or benchmark measurements."""
import csv
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from hardware_evidence import apply_evidence


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); self.artifacts = self.root / "Artifacts"; self.artifacts.mkdir()
        self.png = self.artifacts / "memory.png"; self.png.write_bytes(b"synthetic-proof-only")
        self.capture = self.artifacts / "capture.json"; self.capture.write_text("synthetic-capture-identity")
        self.meta = {"captureStartUtc": "2026-11-12T12:01:00Z", "captureEndUtc": "2026-11-12T12:04:00Z"}
        self.hardware = {"cpu": "AMD Ryzen 5 4600H", "ramBytes": 16 * 1024**3, "ramSpeed": 3200, "channelMode": None, "thermalStabilized": False}
        self.review = {"reviewer": "Synthetic test", "reviewedUtc": "2026-11-12T13:00:00Z", "rationale": "Test validation only"}
        self.evidence = {"schemaVersion": 1, "kind": "shooter.hardware-review.v1", "review": self.review,
            "subject": {key: self.hardware[key] for key in ["cpu", "ramBytes", "ramSpeed"]},
            "memory": {"review": self.review, "channelMode": "Dual-channel", "source": self.proof(self.png)}}
        self.path = self.root / "review.json"

    def proof(self, path):
        return {"path": path.relative_to(self.root).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

    def apply(self):
        self.path.write_text(json.dumps(self.evidence))
        return apply_evidence(self.root, self.path, self.hardware, self.meta, self.capture)

    def thermal(self, flag="No", short=False):
        self.sensor = self.artifacts / "sensor.csv"
        names = ["Date", "Time", "CPU (Tctl/Tdie) [°C]", "GPU Temperature [°C]", "Thermal Throttling (HTC) [Yes/No]",
                 "Thermal Throttling (PROCHOT CPU) [Yes/No]", "Thermal Throttling (PROCHOT EXT) [Yes/No]", "Throttle Reason - Thermal [Yes/No]"]
        with self.sensor.open("w", newline="", encoding="cp1252") as stream:
            writer = csv.writer(stream); writer.writerow(names)
            for minute in range(0, 4 if short else 6):
                writer.writerow(["12.11.2026", f"13:{minute}:00.000", 75, 65, flag, "No", "No", "No"])
        self.evidence["thermal"] = {"review": self.review, "acceptedStabilization": True,
            "captureSha256": hashlib.sha256(self.capture.read_bytes()).hexdigest(), "source": self.proof(self.sensor), "utcOffsetMinutes": 60}

    def test_memory_proof_is_explicit_and_does_not_mutate_raw_hardware(self):
        effective, applied = self.apply()
        self.assertEqual(effective["channelMode"], "Dual-channel")
        self.assertIsNone(self.hardware["channelMode"])
        self.assertFalse(effective["thermalStabilized"])
        self.assertIn("sourceSha256", applied["applied"]["channelMode"])

    def test_tampered_proof_rejected(self):
        self.png.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "hash mismatch"): self.apply()

    def test_subject_mismatch_rejected(self):
        self.evidence["subject"]["ramSpeed"] = 1600
        with self.assertRaisesRegex(ValueError, "subject mismatch"): self.apply()

    def test_proof_cannot_escape_artifacts(self):
        self.evidence["memory"]["source"]["path"] = "../outside.png"
        with self.assertRaisesRegex(ValueError, "inside repository Artifacts"): self.apply()

    def test_unreviewed_and_conflicting_memory_rejected(self):
        self.evidence["memory"]["review"] = {}
        with self.assertRaisesRegex(ValueError, "explicit reviewer"): self.apply()
        self.evidence["memory"]["review"] = self.review; self.hardware["channelMode"] = "Single-channel"
        with self.assertRaisesRegex(ValueError, "conflicts"): self.apply()

    def test_thermal_review_must_match_capture_and_concurrent_sensor_data(self):
        self.thermal(); effective, application = self.apply()
        self.assertTrue(effective["thermalStabilized"]); self.assertFalse(self.hardware["thermalStabilized"])
        self.assertEqual(application["applied"]["thermalStabilized"]["observed"]["sampleCount"], 4)
        self.evidence["thermal"]["captureSha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "exact capture"): self.apply()

    def test_short_or_thermal_flagged_log_is_not_accepted(self):
        self.thermal(short=True)
        with self.assertRaisesRegex(ValueError, "bracket"): self.apply()
        self.thermal(flag="Yes")
        with self.assertRaisesRegex(ValueError, "positive flags"): self.apply()

    def test_thermal_acceptance_cannot_be_inferred_from_a_timer(self):
        self.thermal(); self.evidence["thermal"].pop("acceptedStabilization")
        with self.assertRaisesRegex(ValueError, "explicitly accept"): self.apply()


if __name__ == "__main__": unittest.main()

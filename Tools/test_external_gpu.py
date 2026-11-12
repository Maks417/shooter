"""Synthetic rejection tests; these are not GPU performance evidence."""
import csv
import json
from pathlib import Path
import tempfile
import unittest
from external_gpu import gpu_samples, sha


class ExternalGpuTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.meta = dict(collectorVersion=2, timestampFrequencyMatches=True, stopwatchFrequency=1000, nativeCpuFrequency=1000)
        self.frames = [dict(lowerTimestamp=90, upperTimestamp=100, endTimestamp=110, frameMs=10),
                       dict(lowerTimestamp=100, upperTimestamp=110, endTimestamp=120, frameMs=10)]
        self.rows = [dict(Application="Shooter.exe", ProcessID=42, TimeInQPC=t, SwapChainAddress="one", PresentRuntime="DXGI", SyncInterval=0, MsGPUTime=2) for t in (99, 105, 115, 125)]
        self.launch = dict(processId=42, exitCode=0, executableSha256="test-player")
        lock = json.loads((Path(__file__).parent / "presentmon-lock.json").read_text())
        self.manifest = dict(schemaVersion=1, toolVersion=lock["version"], toolSha256=lock["sha256"], processId=42, playerSha256="test-player", traceExitCode=0)
        self.log = "Started recording\nStopped recording\n"

    def run_trace(self, alter=None):
        for name, obj in (("capture.json", self.meta), ("player-launch.json", self.launch)):
            (self.root / name).write_text(json.dumps(obj))
        (self.root / "presentmon.log").write_text(self.log)
        with (self.root / "presentmon.csv").open("w", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(self.rows[0]))
            writer.writeheader(); writer.writerows(self.rows)
        for key, file in (("captureSha256", "capture.json"), ("csvSha256", "presentmon.csv"), ("logSha256", "presentmon.log")):
            self.manifest[key] = sha(self.root / file)
        (self.root / "external-gpu.json").write_text(json.dumps(self.manifest))
        if alter: alter()
        return gpu_samples(self.root / "external-gpu.json", self.root, self.frames)

    def test_exact_aggregate_interval_includes_all_presents(self):
        values, proof = self.run_trace()
        self.assertEqual(values, [2, 2])
        self.assertEqual(proof["presentedFrames"], 2)

    def test_missing_present_is_pending_not_imputed(self):
        self.rows[2]["TimeInQPC"] = 121
        self.assertIsNone(self.run_trace()[0])

    def test_missing_and_zero_gpu_are_pending_without_dropping(self):
        for value in ("NA", 0):
            self.rows[1]["MsGPUTime"] = self.rows[2]["MsGPUTime"] = value
            self.assertIsNone(self.run_trace()[0])

    def test_bad_identity_is_rejected(self):
        for key, bad in (("processId", 43), ("playerSha256", "other"), ("toolSha256", "other"), ("traceExitCode", 1)):
            original = self.manifest[key]; self.manifest[key] = bad
            with self.assertRaises(ValueError): self.run_trace()
            self.manifest[key] = original

    def test_hash_mutation_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "hash"):
            self.run_trace(lambda: (self.root / "presentmon.csv").write_text("changed"))

    def test_event_loss_wrong_process_and_duplicate_are_rejected(self):
        for warning in ("Events lost: 1", "1 ETW events were lost", "1 ETW buffers were lost", "8919 overflowed present events detected"):
            self.log = warning
            with self.assertRaises(ValueError): self.run_trace()
        self.log = "Stopped recording"
        self.rows[0]["ProcessID"] = 43
        with self.assertRaises(ValueError): self.run_trace()
        self.rows[0]["ProcessID"] = 42
        self.rows[2]["TimeInQPC"] = 105
        with self.assertRaises(ValueError): self.run_trace()

    def test_nonfinite_negative_and_changed_swapchain_are_rejected(self):
        for value in ("NaN", "inf", -1):
            self.rows[1]["MsGPUTime"] = value
            with self.assertRaises(ValueError): self.run_trace()
        self.rows[1]["MsGPUTime"] = 2
        self.rows[1]["SwapChainAddress"] = "other"
        with self.assertRaises(ValueError): self.run_trace()

    def test_clock_and_frame_interval_identity_are_required(self):
        self.meta["timestampFrequencyMatches"] = False
        with self.assertRaises(ValueError): self.run_trace()
        self.meta["timestampFrequencyMatches"] = True
        self.frames[1]["upperTimestamp"] = 111
        with self.assertRaises(ValueError): self.run_trace()


if __name__ == "__main__": unittest.main()

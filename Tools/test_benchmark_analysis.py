import csv
import json
import math
from pathlib import Path
import tempfile
import unittest
from analyze_benchmark import analyze, percentile


class PercentileTests(unittest.TestCase):
    def test_uses_nearest_rank_without_interpolation(self):
        self.assertEqual(percentile([50, 10, 40, 20, 30], .95), 50)
        self.assertEqual(percentile([50, 10, 40, 20, 30], .5), 30)
        self.assertEqual(percentile([7], .99), 7)

    def test_invalid_or_missing_counters_do_not_become_measurements(self):
        for samples in [[], [math.nan], [math.inf], [-1]]:
            with self.assertRaises(ValueError):
                percentile(samples, .95)


class CaptureAnalysisTests(unittest.TestCase):
    """Synthetic captures exercise validation only; they are never measurement evidence."""

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.meta = {"workloadId": "B01-minimal-v1", "seed": "460072030", "profile": "Low",
                     "collectorVersion": 2, "tickAllocationAvailable": True, "frameAllocationAvailable": True,
                     "allocationMainControlBytes": 4424, "allocationWorkerControlBytes": 4424,
                     "allocationEndControlBytes": 4128, "tickAllocationEndControlBytes": 4128,
                     "width": 1280, "height": 720, "graphicsApi": "Direct3D11", "frames": 2,
                     "ticks": 10800, "warmupSeconds": 60, "captureSeconds": 180, "diagnostic": False,
                     "gpuAdapter": "AMD Radeon(TM) Graphics", "cameraMeters": [0, 1.65, -6],
                     "captureStartUtc": "2026-11-11T12:01:00Z", "captureEndUtc": "2026-11-11T12:04:00Z"}
        self.build = {"result": "Succeeded", "target": "Client", "backend": "IL2CPP", "development": False,
                      "sourceCommit": "synthetic-test-only", "toolchainHash": "test-only", "contentManifestHash": "test-only"}
        self.hardware = {"cpu": "AMD Ryzen 5 4600H", "gpuAdapter": None, "ramBytes": 16 * 1024 ** 3,
                         "ramSpeed": 3200, "channelMode": "test-only", "os": "test-only", "driver": "test-only",
                         "powerPlan": "test-only", "acPower": True, "thermalStabilized": True, "nvmeVerified": True}
        self.frames = [{"tick": 3600 + index, "frameMs": 10, "mainMs": 2, "renderMs": 1,
                        "gpuMs": 3, "managedFrameBytes": 0} for index in range(2)]
        self.ticks = [{"tick": tick, "simulationMs": .1, "managedTickBytes": 0} for tick in range(3600, 14400)]
        self.memory = [{"utc": "2026-11-11T12:02:00Z", "privateBytes": 1000, "rssBytes": 500},
                       {"utc": "2026-11-11T12:03:00Z", "privateBytes": 2000, "rssBytes": 600}]

    def report(self):
        for name, value in [("capture.json", self.meta), ("build.json", self.build), ("hardware.json", self.hardware)]:
            (self.root / name).write_text(json.dumps(value), encoding="utf-8")
        for name, rows, fields in [("frames.csv", self.frames, list(self.frames[0])),
                                   ("ticks.csv", self.ticks, list(self.ticks[0])),
                                   ("memory.csv", self.memory, ["utc", "privateBytes", "rssBytes"])]:
            with (self.root / name).open("w", newline="", encoding="utf-8") as stream:
                writer = csv.DictWriter(stream, fieldnames=fields)
                writer.writeheader()
                writer.writerows(rows)
        return analyze(self.root, self.root / "build.json", self.root / "hardware.json", 1)

    def test_complete_samples_keep_nearest_rank_peaks_and_source_identity(self):
        report = self.report()
        self.assertEqual(report["gateStatus"], "ComputedPass")
        self.assertEqual(report["metrics"]["privateCommitBytesPeak"], 2000)
        self.assertEqual(report["metrics"]["rssBytesPeak"], 600)
        self.assertEqual(report["metrics"]["managedBytesPerTick"], 0)
        self.assertEqual(report["metrics"]["frameMs"], {"p50": 10, "p95": 10, "p99": 10, "max": 10})
        self.assertEqual(report["sourceCommit"], "synthetic-test-only")

    def test_negative_tick_allocation_cannot_hide_behind_valid_sample(self):
        self.ticks[0]["managedTickBytes"] = -1
        with self.assertRaisesRegex(ValueError, "allocation"):
            self.report()

    def test_invalid_memory_sample_cannot_hide_behind_positive_peak(self):
        for column in ("privateBytes", "rssBytes"):
            with self.subTest(column=column):
                self.memory[0][column] = -1
                with self.assertRaisesRegex(ValueError, "memory"):
                    self.report()
                self.memory[0][column] = 100

    def test_nonfinite_required_frame_and_simulation_timings_are_rejected(self):
        for sample, column in [(self.frames[0], "frameMs"), (self.ticks[0], "simulationMs")]:
            for value in (math.nan, math.inf, -math.inf, -1):
                with self.subTest(column=column, value=value):
                    sample[column] = value
                    with self.assertRaises(ValueError):
                        self.report()
            sample[column] = .1

    def test_optional_recorder_sentinel_or_nonfinite_value_stays_unavailable(self):
        for column in ("mainMs", "renderMs", "gpuMs", "managedFrameBytes"):
            for value in (-1, math.nan, math.inf):
                with self.subTest(column=column, value=value):
                    self.frames[0][column] = value
                    report = self.report()
                    metric = "managedBytesPerFrame" if column == "managedFrameBytes" else column
                    self.assertIsNone(report["metrics"][metric])
                    self.assertEqual(report["gateStatus"], "PendingHardware")
                    self.assertTrue(any(reason.startswith(column + ":") for reason in report["unavailableMetrics"]))
                    json.dumps(report, allow_nan=False)
            self.frames[0][column] = 0

    def test_missing_cpu_and_rendered_gpu_are_pending_without_substitution(self):
        self.hardware.pop("cpu")
        self.meta["gpuAdapter"] = None
        report = self.report()
        self.assertEqual(report["gateStatus"], "PendingHardware")
        self.assertIsNone(report["machine"]["cpu"])
        self.assertIsNone(report["machine"]["gpuAdapter"])
        self.assertTrue(any(reason.startswith("machine.cpu:") for reason in report["unavailableMetrics"]))
        self.assertTrue(any(reason.startswith("machine.gpuAdapter:") for reason in report["unavailableMetrics"]))

    def test_missing_memory_samples_are_pending_not_zero(self):
        self.memory.clear()
        report = self.report()
        self.assertEqual(report["gateStatus"], "PendingHardware")
        self.assertIsNone(report["metrics"]["privateCommitBytesPeak"])
        self.assertIsNone(report["metrics"]["rssBytesPeak"])

    def test_measured_failure_takes_priority_over_pending_hardware(self):
        self.hardware["cpu"] = None
        for sample, column, value in [(self.frames[0], "frameMs", 101),
                                      (self.ticks[0], "managedTickBytes", 1),
                                      (self.memory[0], "privateBytes", 3 * 1024 ** 3 + 1)]:
            with self.subTest(column=column):
                sample[column] = value
                self.assertEqual(self.report()["gateStatus"], "ComputedFail")
                sample[column] = 0

    def test_short_diagnostic_never_produces_acceptance(self):
        self.meta.update(diagnostic=True, warmupSeconds=2, captureSeconds=5, ticks=300)
        self.ticks = [{"tick": tick, "simulationMs": .1, "managedTickBytes": 0} for tick in range(120, 420)]
        report = self.report()
        self.assertTrue(report["diagnostic"])
        self.assertEqual(report["gateStatus"], "PendingHardware")
        self.assertNotIn("metrics", report)
        self.ticks[0]["managedTickBytes"] = -1
        with self.assertRaisesRegex(ValueError, "allocation"):
            self.report()

    def test_legacy_zero_allocations_are_not_accepted_without_positive_controls(self):
        self.meta.pop("collectorVersion")
        report = self.report()
        self.assertEqual(report["gateStatus"], "PendingHardware")
        self.assertIsNone(report["metrics"]["managedBytesPerTick"])
        self.assertIsNone(report["metrics"]["managedBytesPerFrame"])

    def test_unsupported_tick_meter_sentinel_remains_pending(self):
        self.meta["tickAllocationAvailable"] = False
        for tick in self.ticks:
            tick["managedTickBytes"] = -1
        self.assertEqual(self.report()["gateStatus"], "PendingHardware")

    def test_known_frame_allocation_fails_even_when_hardware_is_pending(self):
        self.hardware["thermalStabilized"] = False
        self.frames[0]["managedFrameBytes"] = 4096
        self.assertEqual(self.report()["gateStatus"], "ComputedFail")


if __name__ == '__main__':
    unittest.main()

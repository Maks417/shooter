"""Negative/positive collector tests for product TDM analysis (FIX12). Synthetic only — never measurement evidence."""
import csv
import json
import tempfile
import unittest
from pathlib import Path
from analyze_tdm_benchmark import analyze
import tdm_workloads


class TdmWorkloadFixtureTests(unittest.TestCase):
    def test_reviewed_depot_and_compound_identities_are_distinct(self):
        document, by_id = tdm_workloads.load_workloads()
        self.assertEqual(document["schemaVersion"], 2)
        self.assertEqual(by_id["TDM-5v5-v1"]["mapId"], "training-compound")
        self.assertEqual(by_id["TDM-5v5-depot-v1"]["mapId"], "depot-yard")
        self.assertEqual(by_id["TDM-5v5-depot-v1"]["seed"], "460072031")
        self.assertNotEqual(by_id["TDM-15v15-v1"]["mapId"], by_id["TDM-15v15-depot-v1"]["mapId"])


class TdmCaptureAnalysisTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.meta = {
            "workloadId": "TDM-5v5-v1", "productTdm": True, "collectorVersion": 5,
            "matchesCompleted": 0, "matchIndex": 1, "cumulativeActiveTicks": 4000, "sessionEpoch": "1001",
            "counterSemantics": "respawns=life-replacement;querySamples=IWorldQueries;contentBindings=verified-package-roots",
            "seed": "460072030", "mapId": "training-compound", "profile": "Low",
            "teamSize": 5, "actors": 10, "width": 1280, "height": 720, "graphicsApi": "Direct3D11",
            "contentManifestHash": "a" * 64, "packageRootHash": "b" * 64,
            "frames": 2, "ticks": 10800, "warmupSeconds": 60, "captureSeconds": 180, "diagnostic": False,
            "preconditionSeconds": 0, "captureStartTick": 3600,
            "gpuAdapter": "AMD Radeon(TM) Graphics",
            "captureStartUtc": "2026-11-14T12:01:00Z", "captureEndUtc": "2026-11-14T12:04:00Z",
            "tickAllocationAvailable": True, "frameAllocationAvailable": True,
            "allocationMainControlBytes": 4424, "allocationWorkerControlBytes": 4424,
            "allocationEndControlBytes": 4128, "tickAllocationEndControlBytes": 4128,
            "renderBudgetMethod": "nativeCpuFrameStartToPresentUpperBound-v1",
            "timestampFrequencyMatches": True, "nativeCpuFrequency": 1000000, "stopwatchFrequency": 1000000,
            "team1Kills": 1, "team2Kills": 2, "activeTicks": 4000, "respawns": 3, "aiRays": 100,
            "pathRequests": 20, "shots": 50, "damageEvents": 40, "leaseAcquires": 8000,
            "querySamples": 120, "contentBindings": 1, "renderFrames": 2,
        }
        self.build = {"result": "Succeeded", "target": "Client", "backend": "IL2CPP", "development": False,
                      "sourceCommit": "synthetic-test-only", "toolchainHash": "test-only", "contentManifestHash": "ignored"}
        self.hardware = {"cpu": "AMD Ryzen 5 4600H", "gpuAdapter": None, "ramBytes": 16 * 1024 ** 3,
                         "ramSpeed": 3200, "channelMode": "test-only", "os": "test-only", "driver": "test-only",
                         "powerPlan": "test-only", "acPower": True, "thermalStabilized": True, "nvmeVerified": True}
        self.frames = []
        for index in range(2):
            start = 10000 + index * 10000
            self.frames.append({
                "tick": 3600 + index, "frameMs": 10, "mainMs": 2, "renderMs": 1, "gpuMs": 3, "managedFrameBytes": 0,
                "nativeFrameStartTimestamp": start, "nativePresentCalledTimestamp": start + 1000,
                "nativeFirstSubmitTimestamp": start + 500, "lowerTimestamp": start - 100,
                "upperTimestamp": start + 100, "endTimestamp": start + 10100, "ambiguous": "False",
            })
        self.ticks = [{
            "tick": tick, "simulationMs": .1, "managedTickBytes": 0,
            "team1Kills": 1, "team2Kills": 2, "respawns": 3, "aiRays": 100, "pathRequests": 20,
            "shots": 50, "damageEvents": 40, "leaseAcquires": 8000, "querySamples": 120,
        } for tick in range(3600, 14400)]
        self.memory = [{"utc": "2026-11-14T12:02:00Z", "privateBytes": 1000, "rssBytes": 500},
                       {"utc": "2026-11-14T12:03:00Z", "privateBytes": 2000, "rssBytes": 600}]

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

    def test_complete_product_capture_stays_pending_without_claiming_hardware_pass(self):
        report = self.report()
        self.assertEqual(report["workloadId"], "TDM-5v5-v1")
        self.assertEqual(report["mapId"], "training-compound")
        self.assertEqual(report["counters"]["leaseAcquires"], 8000)
        self.assertEqual(report["gateStatus"], "ComputedPass")

    def test_wrong_map_is_rejected(self):
        self.meta["mapId"] = "depot-yard"
        with self.assertRaisesRegex(ValueError, "identity"):
            self.report()

    def test_wrong_roster_is_rejected(self):
        self.meta["actors"] = 12
        with self.assertRaisesRegex(ValueError, "identity"):
            self.report()

    def test_wrong_profile_is_rejected(self):
        self.meta["profile"] = "High"
        with self.assertRaisesRegex(ValueError, "configuration"):
            self.report()

    def test_wrong_build_is_rejected(self):
        self.build["development"] = True
        with self.assertRaisesRegex(ValueError, "IL2CPP"):
            self.report()

    def test_missing_counter_is_rejected(self):
        del self.meta["leaseAcquires"]
        with self.assertRaisesRegex(ValueError, "Incomplete counters"):
            self.report()

    def test_absent_content_bindings_are_rejected(self):
        self.meta["contentBindings"] = 0
        with self.assertRaisesRegex(ValueError, "contentBindings"):
            self.report()

    def test_soak_without_replay_match_is_rejected(self):
        self.meta.update(
            workloadId="TDM-soak-15v15-compound-v1",
            mapId="training-compound",
            seed="460072030",
            teamSize=15,
            actors=30,
            warmupSeconds=0,
            captureSeconds=1800,
            ticks=108000,
            captureStartTick=0,
            matchIndex=1,
            matchesCompleted=0,
        )
        self.ticks = [{
            "tick": tick, "simulationMs": .1, "managedTickBytes": 0,
            "team1Kills": 1, "team2Kills": 2, "respawns": 3, "aiRays": 100, "pathRequests": 20,
            "shots": 50, "damageEvents": 40, "leaseAcquires": 8000, "querySamples": 120,
        } for tick in range(0, 108000)]
        with self.assertRaisesRegex(ValueError, "second match|replay"):
            self.report()

    def test_shortened_window_is_rejected(self):
        self.meta["captureSeconds"] = 60
        self.meta["ticks"] = 3600
        self.ticks = self.ticks[:3600]
        with self.assertRaisesRegex(ValueError, "durations|shortened|incomplete"):
            self.report()

    def test_depot_identity_requires_approved_seed(self):
        self.meta.update(workloadId="TDM-5v5-depot-v1", mapId="depot-yard", seed="460072031", teamSize=5, actors=10)
        report = self.report()
        self.assertEqual(report["mapId"], "depot-yard")
        self.meta["seed"] = "460072030"
        with self.assertRaisesRegex(ValueError, "identity"):
            self.report()

    def test_compound_fixture_cannot_silently_become_depot(self):
        workload = tdm_workloads.require_measurement("TDM-15v15-v1")
        self.assertEqual(workload["mapId"], "training-compound")
        with self.assertRaises(ValueError):
            # Pretend analysis accepted a substituted map without reviewed depot ID.
            self.meta["mapId"] = "depot-yard"
            self.report()


if __name__ == "__main__":
    unittest.main()

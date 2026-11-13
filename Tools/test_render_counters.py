import copy
import unittest
import render_counters


class RenderCounterTests(unittest.TestCase):
    def setUp(self):
        self.rows = [{}, {}]
        self.meta = {"frames": 2, "renderCounterMethod": render_counters.METHOD, "renderCounters": []}
        for column, (count, name, category, unit) in render_counters.COUNTERS.items():
            self.meta["renderCounters"].append({"column": column, "requestedCategory": category, "name": name,
                "category": render_counters.NATIVE_CATEGORIES[category], "expectedNativeCategoryName": render_counters.NATIVE_CATEGORIES[category],
                "unit": unit, "dataType": "Int64", "flags": "Counter", "identityValidated": True, "available": True,
                "capturedFrames": 2, "invalidFrames": 0, "unavailable": None})
            for i, row in enumerate(self.rows):
                row[column], row[count] = str(i + 10), "1"

    def test_complete_fresh_samples_produce_nearest_rank_summary(self):
        result, unavailable = render_counters.analyze(self.meta, self.rows)
        self.assertEqual(unavailable, [])
        self.assertEqual(result["drawCalls"], {"p50": 10, "p95": 11, "max": 11, "samples": 2})

    def test_missing_legacy_collector_is_null_with_reasons(self):
        result, unavailable = render_counters.analyze({"frames": 2}, self.rows)
        self.assertTrue(all(value is None for value in result.values()))
        self.assertEqual(len(unavailable), 3)

    def test_partial_zero_or_stale_sample_is_unavailable_not_filtered(self):
        for value, count in [("0", "1"), ("-1", "0"), ("20", "0"), ("20", "2")]:
            with self.subTest(value=value, count=count):
                rows, meta = copy.deepcopy(self.rows), copy.deepcopy(self.meta)
                rows[0]["drawCalls"], rows[0]["drawCallsCount"] = value, count
                meta["renderCounters"][0].update(available=False, invalidFrames=1, unavailable="Missing fresh sample")
                result, unavailable = render_counters.analyze(meta, rows)
                self.assertIsNone(result["drawCalls"])
                self.assertIsNotNone(result["renderedTriangles"])
                self.assertEqual(len(unavailable), 1)

    def test_sample_availability_and_metadata_mutations_reject(self):
        for field, value in [("name", "Scene triangles inventory"), ("unit", "Bytes"), ("flags", "Default"),
                             ("category", "Memory"), ("expectedNativeCategoryName", "OtherCategory"),
                             ("dataType", "Float"), ("dataType", "Double"), ("dataType", "String"),
                             ("capturedFrames", 1), ("invalidFrames", 1), ("available", False), ("identityValidated", False)]:
            with self.subTest(field=field):
                meta = copy.deepcopy(self.meta); meta["renderCounters"][0][field] = value
                with self.assertRaises(ValueError): render_counters.analyze(meta, self.rows)

    def test_duplicate_counter_unknown_method_and_missing_raw_reject(self):
        meta = copy.deepcopy(self.meta); meta["renderCounters"][1] = meta["renderCounters"][0]
        with self.assertRaises(ValueError): render_counters.analyze(meta, self.rows)
        meta = copy.deepcopy(self.meta); meta["renderCounterMethod"] = "Capacity1StaleLastValue"
        with self.assertRaises(ValueError): render_counters.analyze(meta, self.rows)
        del self.rows[0]["drawCalls"]
        with self.assertRaises(ValueError): render_counters.analyze(self.meta, self.rows)

    def test_unavailable_counter_requires_explicit_reason(self):
        proof = self.meta["renderCounters"][0]
        proof.update(identityValidated=False, available=False)
        with self.assertRaises(ValueError): render_counters.analyze(self.meta, self.rows)
        proof["unavailable"] = "Counter unavailable in selected player"
        result, unavailable = render_counters.analyze(self.meta, self.rows)
        self.assertIsNone(result["drawCalls"]); self.assertEqual(len(unavailable), 1)

    def test_noninteger_and_out_of_range_raw_samples_reject(self):
        for value in ["NaN", "12.5", True, "-2", str(2**63)]:
            with self.subTest(value=value):
                rows = copy.deepcopy(self.rows); rows[0]["drawCalls"] = value
                with self.assertRaises(ValueError): render_counters.analyze(self.meta, rows)

    def test_unsigned64_counter_is_accepted_only_with_signed_sample_range(self):
        for proof in self.meta["renderCounters"]:
            proof["dataType"] = "UInt64"
        result, unavailable = render_counters.analyze(self.meta, self.rows)
        self.assertEqual(result["drawCalls"]["max"], 11); self.assertEqual(unavailable, [])

    def test_declared_integer_datatypes_enforce_normalized_sample_bounds(self):
        for data_type, maximum in render_counters.TYPE_MAXIMUM.items():
            with self.subTest(data_type=data_type):
                meta, rows = copy.deepcopy(self.meta), copy.deepcopy(self.rows)
                meta["renderCounters"][0]["dataType"] = data_type
                rows[0]["drawCalls"] = str(maximum)
                result, unavailable = render_counters.analyze(meta, rows)
                self.assertEqual(result["drawCalls"]["max"], maximum); self.assertEqual(unavailable, [])
                rows[0]["drawCalls"] = str(maximum + 1)
                with self.assertRaises(ValueError): render_counters.analyze(meta, rows)


if __name__ == "__main__":
    unittest.main()

import math
import unittest
from analyze_benchmark import percentile


class PercentileTests(unittest.TestCase):
    def test_uses_nearest_rank_without_interpolation(self):
        self.assertEqual(percentile([50, 10, 40, 20, 30], .95), 50)
        self.assertEqual(percentile([50, 10, 40, 20, 30], .5), 30)
        self.assertEqual(percentile([7], .99), 7)

    def test_invalid_or_missing_counters_do_not_become_measurements(self):
        for samples in [[], [math.nan], [math.inf], [-1]]:
            with self.assertRaises(ValueError):
                percentile(samples, .95)


if __name__ == '__main__':
    unittest.main()

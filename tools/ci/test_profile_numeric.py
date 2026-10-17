"""The numeric profiles (tools/data/profile_numeric.py, p1-dataset-analysis-s02): the statistics are exact, and the committed profiles agree with the earlier aggregates."""
import math
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "data"))
import profile_numeric as pn  # noqa: E402


class Statistics(unittest.TestCase):
    def test_skewness_of_a_symmetric_set_is_zero_and_of_a_long_right_tail_is_positive(self):
        self.assertAlmostEqual(pn.skewness([1, 2, 3, 4, 5]), 0.0)
        self.assertGreater(pn.skewness([1, 1, 1, 2, 2, 3, 20]), 1.0)

    def test_ranks_give_tied_values_their_mean_rank(self):
        self.assertEqual(pn.ranks([10, 20, 20, 30]), [1.0, 2.5, 2.5, 4.0])

    def test_pearson_and_spearman_of_a_monotone_curve(self):
        a = [1, 2, 3, 4, 5, 6]
        b = [x ** 3 for x in a]
        c = pn.correlation(a, b)
        self.assertAlmostEqual(c["spearman"], 1.0)
        self.assertLess(c["pearson"], 1.0)

    def test_fewer_than_50_pairs_are_marked_weak_and_50_are_not(self):
        a = list(range(49))
        self.assertTrue(pn.correlation(a, a)["weak"])
        b = list(range(50))
        self.assertFalse(pn.correlation(b, [x + (x % 3) for x in b])["weak"])

    def test_an_interval_surrounds_the_estimate(self):
        a = list(range(60))
        b = [x + (7 if x % 2 else -7) for x in a]
        c = pn.correlation(a, b)
        low, high = c["spearman_ci95"]
        self.assertLess(low, c["spearman"])
        self.assertGreater(high, c["spearman"])

    def test_band_shares_count_outside_and_borderline_values(self):
        xs = [9.0, 10.0, 10.5, 15.0, 19.5, 20.0, 21.0]
        s = pn.band_shares(xs, 10.0, 20.0)
        self.assertAlmostEqual(s["outside_normal_share"], 2 / 7, places=3)       # 9 and 21
        self.assertAlmostEqual(s["borderline_share"], 4 / 7, places=3)           # 10, 10.5, 19.5 and 20 are within 1.0 of an edge; 15 is not
        self.assertEqual(pn.band_shares([], 1.0, 2.0), {})

    def test_percentiles_match_the_extractors_definition(self):
        self.assertEqual(pn.percentile([1, 2, 3, 4], 50), 2.5)

    def test_a_constant_set_has_no_correlation(self):
        self.assertIsNone(pn.pearson([1, 1, 1, 1], [1, 2, 3, 4]))


class CommittedProfiles(unittest.TestCase):
    def test_the_profiles_reproduce_the_earlier_aggregates_and_cover_the_bands(self):
        self.assertEqual(pn.check(), 0)


if __name__ == "__main__":
    unittest.main()

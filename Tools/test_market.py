import json
import os
import unittest

import market

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")


class MarketTests(unittest.TestCase):
    def test_rules_hold(self):
        market.validate(market.catalog())

    def test_shipped_json_matches_generator(self):
        path = os.path.join(ROOT, "Assets", "Bidwarss", "Data", "ItemCatalog.json")
        with open(path, encoding="utf-8") as f:
            shipped = json.load(f)
        self.assertEqual(shipped, market.game_json(market.catalog()),
                         "ItemCatalog.json eski: python Tools/market.py ile yeniden uret")

    def test_collectibles_scale_up(self):
        plain = market.bands(100, 0)
        antique = market.bands(100, 8)
        self.assertGreater(antique[6][0], plain[6][1] * 2)
        self.assertGreater(antique[0][0], plain[0][0])  # even a wrecked antique keeps value

    def test_antique_pricing_sanity(self):
        by = {r["key"]: r for r in market.catalog()}
        self.assertLess(by["antika-biber-degirmeni"]["baseDollars"], by["boy-saati"]["baseDollars"])
        self.assertGreaterEqual(by["vintage-masa"]["minDollars"][6], 1500)
        self.assertLessEqual(by["cop-kovasi"]["maxDollars"][6], 8 * by["cop-kovasi"]["baseDollars"])


if __name__ == "__main__":
    unittest.main()

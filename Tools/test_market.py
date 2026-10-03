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

    def test_every_item_has_a_possessed_form(self):
        keys = {r["key"] for r in market.catalog()}
        self.assertEqual(keys, {i["key"] for i in market.load_items() if i["owned"]}, "OWNED ile katalog anahtarlari ayni olmali")

    def test_motorcycles_are_not_toys(self):
        by = {r["key"]: r for r in market.catalog()}
        # Gercek motosikletler (model/oyuncak degil): bir bisikletten ve tum ev esyalarindan pahali olmali.
        for key in ("model-motosiklet", "cafe-racer"):
            self.assertGreaterEqual(by[key]["baseDollars"], 3000, key)
            self.assertGreater(by[key]["minDollars"][0], by["retro-bisiklet"]["baseDollars"], key)
        self.assertGreater(by["model-motosiklet"]["minDollars"][6], 30000)

    def test_new_machines_are_priced_as_collectibles(self):
        by = {r["key"]: r for r in market.catalog()}
        self.assertGreater(by["yaris-arcade"]["baseDollars"], by["arcade-makinesi"]["baseDollars"])
        self.assertGreater(by["thompson"]["minDollars"][6], 20000)
        self.assertGreater(by["sigara-otomati"]["baseDollars"], by["modern-otomat"]["baseDollars"])


if __name__ == "__main__":
    unittest.main()

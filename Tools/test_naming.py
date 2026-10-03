"""Otomatik isimlendirme sisteminin testleri:  python Tools/test_naming.py"""
import copy
import json
import re
import unittest

import market
import naming
import site_import


class Poss3(unittest.TestCase):
    def test_cases(self):
        cases = {
            "motosiklet": "motosikleti", "çakmak": "çakmağı", "tabure": "taburesi", "kılıç": "kılıcı",
            "tüfek": "tüfeği", "dolap": "dolabı", "kitap": "kitabı", "saat": "saati", "plak": "plağı",
            "sandalye": "sandalyesi", "masa": "masası", "vazo": "vazosu", "ayna": "aynası", "pipo": "piposu",
            "radyo": "radyosu", "telefon": "telefonu", "gramofon": "gramofonu", "ocak": "ocağı", "mumluk": "mumluğu",
            "çöp kovası": "çöp kovası".replace("kovası", "kovası"), "duvar saat": "duvar saati",
            "pikap": "pikapı", "tekerlek": "tekerleği", "bisiklet": "bisikleti", "kutu": "kutusu",
        }
        for word, want in cases.items():
            if word == "çöp kovası":  # zaten iyelikli bilesik
                continue
            self.assertEqual(naming.poss3(word), want, word)

    def test_title_case(self):
        self.assertEqual(naming.title_case_first("ışık"), "Işık")
        self.assertEqual(naming.title_case_first("içki"), "İçki")


class Names(unittest.TestCase):
    def check(self, name, title, owned=None, cat=None, analysis=""):
        r = naming.name_item(name, analysis)
        self.assertEqual(r["title"], title, name)
        if owned is not None:
            self.assertEqual(r["owned"], owned, name)
        if cat is not None:
            self.assertEqual(r["cat"], cat, name)
        return r

    def test_known_site_names(self):
        self.check("Zippo Lighter 1", "Metal çakmak", "metal çakmağı")
        self.check("Type-64 SMG", "Hafif makineli tüfek", "hafif makineli tüfeği", "weapon")
        self.check("Dance Dance Revolution", "Dans arcade makinesi", "dans arcade makinesi", "toy")
        self.check("cigarattes machine", "Sigara otomatı", "sigara otomatı")          # yazim hatasi
        self.check("Document сabinet", "Evrak dolabı", "evrak dolabı")                # kiril harfli c
        self.check("STYLIZED VENDING MASHINE HIGH-OPTIMIZED MODEL", "Otomat", "otomatı")
        self.check("ice cold machine", "Soğuk içecek otomatı", "soğuk içecek otomatı", "drink")
        self.check("candy machine", "Şeker otomatı", "şeker otomatı")
        self.check("GASSTOVE", "Gaz ocağı", "gaz ocağı")                              # bitisik yazim
        self.check("Japanese Sword", "Japon kılıcı", "Japon kılıcı", "weapon")        # uyruk isim tamlamasi
        self.check("Old Soviet Chair", "Eski Sovyet sandalyesi", "Sovyet sandalyesi")
        self.check("Wooden Pipe", "Ahşap pipo", "ahşap piposu")                       # sifat + isim: ek basta degil
        self.check("Vintage Wooden Wall Clock", "Vintage ahşap duvar saati", "ahşap duvar saati", "clock")
        self.check("Grandfather Clock", "Boy saati", "boy saati")
        self.check("Katana With Dragon", "Ejderhalı katana", "ejderhalı katanası")
        self.check("GAME READY ARCADE MACHINE ASSET", "Arcade makinesi", "arcade makinesi", "toy")
        self.check("Leather Footstool", "Deri tabure", "deri taburesi")

    def test_motorcycles_are_vehicles(self):
        for n in ("Motorcycle", "Harley Davidson Chopper", "Ducati Monster"):
            r = naming.name_item(n)
            self.assertEqual(r["cat"], "vehicle", n)
            self.assertIn("motosiklet", r["title"].lower())

    def test_turkish_input(self):
        self.check("Sovyet Buzdolabı", "Sovyet buzdolabı", "Sovyet buzdolabı", "kitchen")
        self.check("Lamba", "Lamba", "lambası")

    def test_analysis_fallback(self):
        r = naming.name_item("SPY-HYPERSPORT", "Hızlı bir spor motosiklet modeli.")
        self.assertEqual(r["title"], "Spor motosiklet")
        self.assertEqual(r["cat"], "vehicle")
        self.assertTrue(r["review"])

    def test_unknown_goes_to_review_without_leaking(self):
        r = naming.name_item("Zorblax 9000")
        self.assertEqual(r["title"], "Eşya")
        self.assertTrue(r["review"])
        self.assertNotIn("zorblax", (r["title"] + r["owned"]).lower())

    def test_brands_never_reach_titles(self):
        for brand in naming.BRANDS:
            for suffix in ("", " Lighter", " Machine", " Pistol"):
                r = naming.name_item(brand.title() + suffix, "")
                text = (r["title"] + " " + r["owned"]).lower()
                self.assertNotRegex(text, r"\b" + re.escape(brand) + r"\b", brand + suffix)

    def test_all_outputs_in_vocabulary(self):
        # Basliga yalnizca sozlukteki Turkce kelimeler girebilir (ham site kelimesi sizamaz)
        r = naming.name_item("Quantum Frobnicator Deluxe Lamp")
        self.assertEqual(r["title"], "Lamba")


class Guard(unittest.TestCase):
    def test_motorcycle_floor(self):
        base, coll, notes = naming.guard(300, 1, "vehicle", "Spor motosiklet", 180, 100, 200, 70)
        self.assertEqual((base, coll), (3000, 5))
        self.assertTrue(notes)

    def test_expensive_motorcycle_untouched(self):
        base, coll, notes = naming.guard(9000, 8, "vehicle", "Motosiklet")
        self.assertEqual((base, coll, notes), (9000, 8, []))

    def test_heavy_vehicle_floor(self):
        base, _, _ = naming.guard(100, 0, "vehicle", "Scooter", 120)
        self.assertEqual(base, 1500)

    def test_keys(self):
        self.assertEqual(naming.slug("Şeker otomatı"), "seker-otomati")
        taken = {"vazo"}
        self.assertEqual(naming.unique_key("Vazo", taken), "vazo-2")


class Import(unittest.TestCase):
    DOCS = [
        {"id": "a", "name": "Rolex Submariner Watch", "base": 900, "coll": 6, "kg": 0.2},
        {"id": "b", "name": "Harley Davidson Chopper", "base": 300, "coll": 2, "kg": 250, "h": 110, "w": 220, "d": 80},
        {"id": "c", "name": "Zippo Lighter 3", "base": 40, "coll": 4},
        {"id": "d", "name": "Zippo Lighter 4", "base": 45, "coll": 4},
        {"id": "e", "name": "OutRun Arcade Cabinet", "base": 950, "coll": 6},  # takma ad -> kanonik, telifsiz ad
    ]

    def run_once(self, entries):
        docs = copy.deepcopy(self.DOCS)
        return site_import.process(docs, entries)

    def test_new_items_are_created_and_valid(self):
        entries = copy.deepcopy(market.load_items())
        created, updates, _ = self.run_once(entries)
        self.assertEqual(len(created), 4)  # takma adli olan yeni sayilmaz
        keys = [e["key"] for e in entries]
        titles = [e["title"].lower() for e in entries]
        self.assertEqual(len(keys), len(set(keys)))
        self.assertEqual(len(titles), len(set(titles)), "basliklar tekrar etmemeli")
        moto = next(e for e in created if "motosiklet" in e["title"].lower())
        self.assertGreaterEqual(moto["base"], 3000)
        self.assertEqual(moto["cat"], "vehicle")
        self.assertTrue(all(e.get("auto") and e["owned"] for e in created))
        for e in created:
            self.assertNotRegex((e["title"] + e["owned"]).lower(), r"zippo|rolex|harley|davidson")
        # katalog gecerli ve siteyle ayni bantlari uretir
        old = market.load_items
        market.load_items = lambda path=None: entries
        try:
            market.validate(market.catalog())
        finally:
            market.load_items = old

    def test_alias_is_renamed_on_site(self):
        entries = copy.deepcopy(market.load_items())
        _, updates, _ = self.run_once(entries)
        renames = {i: u["name"] for i, u in updates if "name" in u}
        self.assertEqual(renames, {"e": "Coast Rush Arcade Cabinet"})

    def test_second_pass_is_idempotent(self):
        entries = copy.deepcopy(market.load_items())
        docs = copy.deepcopy(self.DOCS)
        _, updates, _ = site_import.process(docs, entries)
        for d in docs:  # guncellemeleri siteye uygulanmis say
            for doc_id, upd in updates:
                if doc_id == d["id"]:
                    d.update(upd)
        n = len(entries)
        created, updates2, _ = site_import.process(docs, entries)
        self.assertEqual((created, updates2, len(entries)), ([], [], n))

    def test_site_classes_match_game_bands(self):
        classes = site_import.classes_for(150, 4)
        bands = market.bands(150, 4)
        self.assertEqual([(c["pmin"], c["pmax"]) for c in classes], bands)
        self.assertEqual([c["chance"] for c in classes], market.WEIGHTS)


class Catalog(unittest.TestCase):
    def test_every_item_has_owned_and_known_category(self):
        for i in market.load_items():
            self.assertTrue(i["owned"].strip(), i["key"])
            self.assertIn(i["cat"], market.COLORS, i["key"])
            self.assertIn(i["shape"], ("Box", "Table", "Chair", "Radio", "Mirror", "Lamp"), i["key"])


if __name__ == "__main__":
    unittest.main()

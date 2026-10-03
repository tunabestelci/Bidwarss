"""Bidwarss piyasa modeli: her item icin 7 durumun dolar fiyat araligini uretir.

Tek dogru kaynak bu dosyadir. Cikti iki yere gider:
  * Assets/Bidwarss/Data/ItemCatalog.json  -> Unity editoru (Bidwarss > Sync Market Catalog)
  * Kasa Defteri sitesi (Tools/market.py --site <klasor> ile ayni degerler siteye yazilir)

Calistir:  python Tools/market.py            (JSON'u yeniden uretir, ozet basar)
           python Tools/market.py --check    (kurallari dogrular, dosya yazmaz)
"""
import json
import math
import os
import sys

CONDITIONS = ["Rezalet", "Çok kötü", "Kötü", "Orta", "İyi", "Çok iyi", "Efsane"]
TIER_IDS = ["rezalet", "cok-kotu", "kotu", "orta", "iyi", "cok-iyi", "efsane"]
TIER_COLORS = ["#70584c", "#7d8590", "#a0805a", "#4f8fb8", "#3fa17a", "#8a63d2", "#e0a526"]
# Oyunun genel durum agirliklari (yuzde). Toplam 100.
WEIGHTS = [8, 14, 20, 27, 18, 10, 3]
ORTA = 3  # "Orta" durumun fiyati itemin piyasa degeridir (base).

# Kategori -> oyun rengi
COLORS = {
    "furniture": "#8a5a2b", "decor": "#c9a24a", "weapon": "#7a8591", "electronics": "#3d6f8f",
    "kitchen": "#b0b7bf", "tools": "#6a7a52", "music": "#8a4fb0", "toy": "#c5463c",
    "vehicle": "#4a5b6e", "clock": "#a3763a", "drink": "#7b4a1e",
}


def step_round(v):
    """Fiyati okunur adimlara yuvarlar: 100 alti 1, 1000 alti 5, ustu 10."""
    st = 1 if v < 100 else (5 if v < 1000 else 10)
    return max(1, int(math.floor(v / st + 0.5)) * st)


def log_multipliers(coll):
    """7 durum icin ln(carpan). Orta = 0 (piyasa degeri).
    Alt durumlar koleksiyon puaniyla biraz yukselir (antika bozuk olsa da deger tasir).
    Ust durumlar (Cok iyi, Efsane) koleksiyon potansiyeline gore hizla acilir."""
    c = max(0, min(10, coll))
    l5 = 0.85 + 0.04 * c
    return [-1.55 + 0.03 * c, -0.95 + 0.02 * c, -0.40 + 0.01 * c, 0.0, 0.40, l5, l5 + 0.85 + 0.15 * c]


def bands(base, coll):
    """[(min, max)] x 7. Ardisik bantlar kesisemez, her bant bir oncekinden yukarida baslar."""
    out = []
    prev_max = 0
    for k, lm in enumerate(log_multipliers(coll)):
        center = max(1, base) * math.exp(lm)
        pos = k / 6.0
        sp = 0.09 + 0.16 * pos * pos
        lo = step_round(center * (1 - sp))
        hi = step_round(center * (1 + sp))
        if k == ORTA:
            lo, hi = min(lo, base), max(hi, base)
        lo = max(lo, prev_max + 1)
        hi = max(hi, lo)
        out.append((lo, hi))
        prev_max = hi
    return out


def selection(base, coll):
    w = 10 if coll <= 2 else 6 if coll <= 4 else 3 if coll <= 6 else 1
    g = 4 if coll <= 2 else 3 if coll <= 4 else 2 if coll <= 6 else 1
    if base >= 400:
        w, g = min(w, 3), min(g, 2)
    return w, g


# name = sitedeki (Kasa Defteri) item adi; yeni itemler icin site=None
# (name, key, title, base, coll, cat, shape)
ITEMS = [
    ("Vintage Wooden Wall Clock", "ahsap-duvar-saati", "Eski ahşap duvar saati", 150, 4, "clock", "Box"),
    ("SPY-HYPERSPORT", "model-motosiklet", "Spor motosiklet", 4800, 5, "vehicle", "Box"),
    ("Basic Clock Rigged", "basit-saat", "Basit masa saati", 18, 0, "clock", "Box"),
    ("VİNTAGE TABLE", "vintage-masa", "Vintage ahşap masa", 350, 4, "furniture", "Table"),
    ("ZIL Buzdolabı (Sovyet)", "zil-buzdolabi", "ZIL buzdolabı (Sovyet)", 250, 4, "kitchen", "Box"),
    ("Antique Pepper Mill", "antika-biber-degirmeni", "Antika biber değirmeni", 90, 4, "kitchen", "Box"),
    ("Wheel", "tekerlek", "Tekerlek", 20, 0, "tools", "Box"),
    ("Retro bike", "retro-bisiklet", "Retro bisiklet", 220, 3, "vehicle", "Box"),
    ("Brazilian Rum", "brezilya-romu", "Brezilya romu (şişe)", 35, 1, "drink", "Box"),
    ("Wooden Pipe", "ahsap-pipo", "Ahşap pipo", 40, 3, "decor", "Box"),
    ("Flashlight", "el-feneri", "El feneri", 8, 0, "tools", "Box"),
    ("COFFEE MAKER 1", "kahve-makinesi", "Filtre kahve makinesi", 15, 0, "kitchen", "Box"),
    ("Old Military Radio", "askeri-telsiz", "Eski askeri telsiz", 200, 5, "electronics", "Radio"),
    ("walkie- talkie", "telsiz", "Telsiz (walkie-talkie)", 15, 1, "electronics", "Box"),
    ("Marquetry table", "marketri-masa", "Marketri (kakma) masa", 600, 7, "furniture", "Table"),
    ("Japanese Sword", "japon-kilici", "Japon kılıcı", 330, 7, "weapon", "Box"),
    ("GASSTOVE", "gazli-ocak", "Gazlı ocak", 90, 1, "kitchen", "Box"),
    ("DJ set from ep6", "dj-seti", "DJ seti", 160, 1, "music", "Box"),
    ("Electronic Drum Set", "elektronik-davul", "Elektronik davul seti", 300, 1, "music", "Box"),
    ("Document сabinet", "evrak-dolabi", "Evrak dolabı", 90, 1, "furniture", "Box"),
    ("Espresso Coffee Machine", "espresso-makinesi", "Espresso makinesi", 60, 1, "kitchen", "Box"),
    ("M9A3 Pistol", "m9a3-tabanca", "M9A3 tabanca (replika)", 120, 3, "weapon", "Box"),
    ("Katana", "katana", "Katana", 350, 7, "weapon", "Box"),
    ("DISCO BALL", "disko-topu", "Disko topu", 30, 2, "decor", "Box"),
    ("Katana With Dragon", "ejderhali-katana", "Ejderhalı katana", 300, 7, "weapon", "Box"),
    ("Leather Footstool", "deri-tabure", "Deri tabure", 55, 1, "furniture", "Box"),
    ("GAME READY ARCADE MACHINE ASSET", "arcade-makinesi", "Arcade oyun makinesi", 700, 5, "toy", "Box"),
    ("Old Soviet Chair", "sovyet-sandalye", "Eski Sovyet sandalyesi", 45, 3, "furniture", "Chair"),
    ("mumluk", "mumluk", "Pirinç mumluk", 50, 4, "decor", "Box"),
    ("VİNTAGE PİSTOL", "vintage-tabanca", "Vintage tabanca", 400, 6, "weapon", "Box"),
    ("Floral Vase", "cicekli-vazo", "Çiçek desenli vazo", 60, 4, "decor", "Box"),
    ("Echo Wall Mirror", "duvar-aynasi", "Oymalı duvar aynası", 150, 4, "decor", "Mirror"),
    ("Radio V", "masaustu-radyo", "Masaüstü radyo", 90, 4, "electronics", "Radio"),
    ("Grandfather Clock", "boy-saati", "Antika boy saati", 600, 7, "clock", "Box"),
    ("Vintage Gramophone", "gramofon", "Antika gramofon", 420, 6, "music", "Box"),
    ("Weathered antique table mirror", "antika-masa-aynasi", "Antika masa aynası", 200, 6, "decor", "Mirror"),
    ("Trash Can", "cop-kovasi", "Çöp kovası", 10, 0, "tools", "Box"),
    ("STYLIZED VENDING MASHINE HIGH-OPTIMIZED MODEL", "otomat", "Otomat (vending)", 500, 2, "electronics", "Box"),
    ("Vase Remake (OldArt)", "vazo-remake", "Antika görünümlü vazo (remake)", 50, 2, "decor", "Box"),
    ("Vase", "vazo", "Vazo", 35, 2, "decor", "Box"),
    ("Vintage Pocket Watch", "cep-saati", "Antika cep saati", 250, 7, "clock", "Box"),
    ("OLD CHINA CABINET", "cini-vitrin", "Antika çini vitrin", 500, 6, "furniture", "Box"),
    # Oyunda olup sitede olmayan:
    ("Lamba", "masa-lambasi", "Masa lambası", 80, 3, "decor", "Lamp"),
    # Sonradan sitede eklenenler (Ekim 2026):
    ("Zippo Lighter 1", "benzinli-cakmak", "Metal benzinli çakmak", 40, 4, "decor", "Box"),
    ("Zippo Lighter 2", "benzinli-cakmak-2", "Metal benzinli çakmak (2)", 45, 4, "decor", "Box"),
    ("candy machine", "seker-otomati", "Retro şeker otomatı", 650, 6, "electronics", "Box"),
    ("Type-64 SMG", "hafif-makineli", "Sessiz hafif makineli (replika)", 900, 6, "weapon", "Box"),
    ("Dance Dance Revolution", "dans-arcade", "Dans arcade makinesi", 900, 5, "toy", "Box"),
    ("Coast Rush Arcade Cabinet", "yaris-arcade", "Yarış arcade makinesi", 950, 6, "toy", "Box"),
    ("bubblegum machine", "sakiz-makinesi", "Retro sakız makinesi", 200, 5, "toy", "Box"),
    ("Vintage Rotary Telephone", "doner-telefon", "Döner kadranlı telefon", 80, 4, "electronics", "Box"),
    ("1911A1 Engraved", "gravurlu-1911", "Gravürlü 1911 tabanca", 650, 7, "weapon", "Box"),
    ("Motorcycle", "cafe-racer", "Café racer motosiklet", 3500, 6, "vehicle", "Box"),
    ("Vending Machine X", "modern-otomat", "Modern otomat", 450, 2, "electronics", "Box"),
    ("Tommy gun", "thompson", "Thompson tipi makineli tüfek", 1200, 8, "weapon", "Box"),
    ("cigarattes machine", "sigara-otomati", "Eski sigara otomatı", 800, 7, "electronics", "Box"),
    ("Snack Master", "atistirmalik-otomati", "Atıştırmalık otomatı", 350, 2, "electronics", "Box"),
    ("Arcade Retro", "retro-arcade", "Retro arcade makinesi", 800, 5, "toy", "Box"),
    ("ice cold machine", "soda-otomati", "Eski soda otomatı", 750, 7, "drink", "Box"),
]

# "<Yildiz>'in <owned>" biciminde gosterilir (yalniz Cok iyi / Efsane esyalar). Kucuk harf, iyelik ekli.
OWNED = {
    "ahsap-duvar-saati": "ahşap duvar saati", "model-motosiklet": "spor motosikleti", "basit-saat": "masa saati",
    "vintage-masa": "vintage masası", "zil-buzdolabi": "buzdolabı", "antika-biber-degirmeni": "biber değirmeni",
    "tekerlek": "tekerleği", "retro-bisiklet": "bisikleti", "brezilya-romu": "rom şişesi", "ahsap-pipo": "piposu",
    "el-feneri": "el feneri", "kahve-makinesi": "kahve makinesi", "askeri-telsiz": "askeri telsizi", "telsiz": "telsizi",
    "marketri-masa": "marketri masası", "japon-kilici": "Japon kılıcı", "gazli-ocak": "gazlı ocağı", "dj-seti": "DJ seti",
    "elektronik-davul": "elektronik davul seti", "evrak-dolabi": "evrak dolabı", "espresso-makinesi": "espresso makinesi",
    "m9a3-tabanca": "tabancası", "katana": "katanası", "disko-topu": "disko topu", "ejderhali-katana": "ejderhalı katanası",
    "deri-tabure": "deri taburesi", "arcade-makinesi": "arcade makinesi", "sovyet-sandalye": "Sovyet sandalyesi",
    "mumluk": "pirinç mumluğu", "vintage-tabanca": "vintage tabancası", "cicekli-vazo": "çiçekli vazosu",
    "duvar-aynasi": "duvar aynası", "masaustu-radyo": "masaüstü radyosu", "boy-saati": "boy saati", "gramofon": "gramofonu",
    "antika-masa-aynasi": "masa aynası", "cop-kovasi": "çöp kovası", "otomat": "otomatı", "vazo-remake": "vazosu",
    "vazo": "vazosu", "cep-saati": "cep saati", "cini-vitrin": "çini vitrini", "masa-lambasi": "masa lambası",
    "benzinli-cakmak": "çakmağı", "benzinli-cakmak-2": "çakmağı", "seker-otomati": "şeker otomatı",
    "hafif-makineli": "hafif makinelisi", "dans-arcade": "dans makinesi", "yaris-arcade": "yarış arcade makinesi",
    "sakiz-makinesi": "sakız makinesi", "doner-telefon": "telefonu", "gravurlu-1911": "gravürlü tabancası",
    "cafe-racer": "motosikleti", "modern-otomat": "otomatı", "thompson": "makineli tüfeği", "sigara-otomati": "sigara otomatı",
    "atistirmalik-otomati": "atıştırmalık otomatı", "retro-arcade": "retro arcade makinesi", "soda-otomati": "soda otomatı",
}

# Sitede olmayan itemlerin sabit olculeri (kg, boy, en, derinlik cm)
NEW_ITEM_DIMS = {"Lamba": (2.5, 45, 30, 30)}


def catalog():
    rows = []
    for name, key, title, base, coll, cat, shape in ITEMS:
        b = bands(base, coll)
        w, g = selection(base, coll)
        rows.append({
            "name": name, "key": key, "title": title, "baseDollars": base, "collector": coll,
            "owned": OWNED[key], "selectionWeight": w, "maxGroups": g, "color": COLORS[cat], "shape": shape,
            "minDollars": [x[0] for x in b], "maxDollars": [x[1] for x in b],
        })
    return rows


def validate(rows):
    keys = set()
    for r in rows:
        assert r["key"] not in keys and 0 < len(r["key"]) <= 48, r["key"]
        keys.add(r["key"])
        prev = 0
        for lo, hi in zip(r["minDollars"], r["maxDollars"]):
            assert 1 <= lo <= hi <= 1_000_000, r["key"]
            assert lo > prev, (r["key"], "bantlar kesisiyor")
            prev = hi
        assert r["owned"].strip(), (r["key"], "owned bos")
        o = CONDITIONS.index("Orta")
        assert r["minDollars"][o] <= r["baseDollars"] <= r["maxDollars"][o], r["key"]
    assert sum(WEIGHTS) == 100


def game_json(rows):
    return {
        "version": 3,
        "conditions": [{"name": n, "weight": w} for n, w in zip(CONDITIONS, WEIGHTS)],
        "items": [{k: v for k, v in r.items() if k != "name"} for r in rows],
    }


def main():
    rows = catalog()
    validate(rows)
    if "--check" in sys.argv:
        print("OK", len(rows), "item")
        return
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Bidwarss", "Data", "ItemCatalog.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(game_json(rows), f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("yazildi:", os.path.normpath(out), len(rows), "item")


if __name__ == "__main__":
    main()

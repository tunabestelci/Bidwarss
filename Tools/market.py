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


HERE = os.path.dirname(os.path.abspath(__file__))
ITEMS_FILE = os.path.join(HERE, "items.json")


def load_items(path=ITEMS_FILE):
    """Tools/items.json: her kayit {name, key, title, owned, base, coll, cat, shape[, aliases][, auto]}.
    Yeni siteler icin Tools/site_import.py bu dosyaya otomatik kayit ekler."""
    with open(path, encoding="utf-8") as f:
        return json.load(f)["items"]


def item_tuples():
    return [(i["name"], i["key"], i["title"], i["base"], i["coll"], i["cat"], i["shape"]) for i in load_items()]


# Sitede olmayan itemlerin sabit olculeri (kg, boy, en, derinlik cm)
NEW_ITEM_DIMS = {"Lamba": (2.5, 45, 30, 30)}


def catalog():
    rows = []
    owned = {i["key"]: i["owned"] for i in load_items()}
    for name, key, title, base, coll, cat, shape in item_tuples():
        b = bands(base, coll)
        w, g = selection(base, coll)
        rows.append({
            "name": name, "key": key, "title": title, "baseDollars": base, "collector": coll,
            "owned": owned[key], "selectionWeight": w, "maxGroups": g, "color": COLORS[cat], "shape": shape,
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

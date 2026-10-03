"""Kasa Defteri sitesindeki itemleri oyun katalogu ve piyasa fiyatlariyla eslestirir.

Yeni bir item sitede gorununce bu betik
  1. adini naming.py ile telifsiz Turkce ada, iyelikli (ünlü sahip) bicime, kategoriye ve sekle cevirir,
  2. fiyati naming.guard ile bariz hatalara karsi kontrol eder (oyuncak sanilan motosiklet gibi),
  3. Tools/items.json'a `auto: true` kayit ekler (elle duzeltmek icin `auto`yu silip alanlari degistirin),
  4. site belgeleri icin piyasa v3 guncellemelerini (base, coll, classes, priceReview) uretir.

Kullanim:
  python Tools/site_import.py <site_dokumu>            # items.json'u gunceller + <site_dokumu>/../site_updates yazar
  python Tools/site_import.py <site_dokumu> --dry-run  # sadece rapor, hicbir dosyaya yazmaz
  python Tools/site_import.py <site_dokumu> --out DIR  # guncelleme dosyalari icin klasor

<site_dokumu>: her belge icin bir .json iceren klasor (dosya adi belge kimligi) ya da belge listesi iceren tek .json.
Belge alanlari: name, analysis, base, coll, kg, h, w, d, classes, priceReview (Kasa Defteri `items` koleksiyonu).
Sonra `python Tools/market.py` ile oyun katalogunu yeniden uretin.
"""
import argparse
import glob
import json
import os
import re
import sys

import market
import naming

REVIEW_TAG = "market-v3"


def norm(name):
    t = (name or "").translate(naming.CYRILLIC).lower().replace("i̇", "i")
    return re.sub(r"[^a-z0-9çğıöşü]+", " ", t).strip()


def load_docs(path):
    docs = []
    if os.path.isdir(path):
        files = sorted(glob.glob(os.path.join(path, "**", "*.json"), recursive=True))
        for f in files:
            with open(f, encoding="utf-8") as fh:
                d = json.load(fh)
            if isinstance(d, dict) and "name" in d:
                d.setdefault("id", os.path.splitext(os.path.basename(f))[0])
                docs.append(d)
    else:
        with open(path, encoding="utf-8") as fh:
            data = json.load(fh)
        for d in data if isinstance(data, list) else data.get("docs", data.get("items", [])):
            d.setdefault("id", d.get("doc_id") or d.get("_id") or "")
            docs.append(d)
    return docs


def unique_title(title, titles):
    if title.lower() not in titles:
        return title
    n = 2
    while "%s (%d)" % (title, n) in titles:
        n += 1
    return "%s (%d)" % (title, n)


def classes_for(base, coll):
    bands = market.bands(base, coll)
    return [{"tierId": market.TIER_IDS[k], "chance": market.WEIGHTS[k], "pmin": lo, "pmax": hi}
            for k, (lo, hi) in enumerate(bands)]


def process(docs, entries):
    """entries: items.json 'items' listesi (yerinde guncellenir). (yeni kayitlar, guncellemeler, rapor) dondurur."""
    index = {}
    for e in entries:
        for n in [e["name"]] + e.get("aliases", []):
            index[norm(n)] = e
    keys = {e["key"] for e in entries}
    titles = {e["title"].lower() for e in entries}
    created, updates, report = [], [], []
    for d in docs:
        e = index.get(norm(d["name"]))
        if e is None:
            r = naming.name_item(d["name"], d.get("analysis", ""))
            base, coll, notes = naming.guard(int(d.get("base", 1) or 1), int(d.get("coll", 0) or 0), r["cat"], r["title"],
                                             d.get("kg"), d.get("h"), d.get("w"), d.get("d"))
            title = unique_title(r["title"], titles)
            key = naming.unique_key(title, keys)
            e = {"name": d["name"], "key": key, "title": title, "owned": r["owned"], "base": base, "coll": coll,
                 "cat": r["cat"], "shape": r["shape"], "auto": True}
            entries.append(e)
            index[norm(d["name"])] = e
            keys.add(key)
            titles.add(title.lower())
            created.append(e)
            report.append("YENİ  %-34s -> %s / %s  [%s, %s$, koleksiyon %d]" % (d["name"], title, r["owned"], r["cat"], base, coll))
            for x in r["review"] + notes:
                report.append("      ! " + x)
        elif e.get("auto"):
            # otomatik kayitlarda site degerleri esas alinir (kullanici siteden duzeltmis olabilir)
            base, coll, notes = naming.guard(int(d.get("base", e["base"]) or 1), int(d.get("coll", e["coll"]) or 0),
                                             e["cat"], e["title"], d.get("kg"), d.get("h"), d.get("w"), d.get("d"))
            if (base, coll) != (e["base"], e["coll"]):
                report.append("GÜNCEL %-34s %s$/%d -> %s$/%d" % (d["name"], e["base"], e["coll"], base, coll))
                e["base"], e["coll"] = base, coll
            for x in notes:
                report.append("      ! " + d["name"] + ": " + x)
        upd = {}
        if e["base"] != d.get("base"):
            upd["base"] = e["base"]
        if e["coll"] != d.get("coll"):
            upd["coll"] = e["coll"]
        want = classes_for(e["base"], e["coll"])
        if d.get("classes") != want:
            upd["classes"] = want
        if d.get("priceReview") != REVIEW_TAG:
            upd["priceReview"] = REVIEW_TAG
        if d["name"] != e["name"] and norm(d["name"]) != norm(e["name"]):
            upd["name"] = e["name"]  # takma adli belge (telifli ad) kanonik, telifsiz ada alinir
        if upd:
            updates.append((d["id"], upd))
    return created, updates, report


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("dump")
    ap.add_argument("--out")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args(argv)
    docs = load_docs(args.dump)
    with open(market.ITEMS_FILE, encoding="utf-8") as f:
        data = json.load(f)
    created, updates, report = process(docs, data["items"])
    print("\n".join(report) or "yeni item yok")
    print("%d belge, %d yeni item, %d site guncellemesi" % (len(docs), len(created), len(updates)))
    if args.dry_run:
        return 0
    if created:
        with open(market.ITEMS_FILE, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
            f.write("\n")
        print("items.json guncellendi; simdi: python Tools/market.py")
    out = args.out or os.path.join(os.path.dirname(os.path.abspath(args.dump.rstrip("/"))), "site_updates")
    if updates:
        os.makedirs(out, exist_ok=True)
        ops = []
        for doc_id, upd in updates:
            with open(os.path.join(out, doc_id + ".json"), "w", encoding="utf-8") as f:
                json.dump(upd, f, ensure_ascii=False)
            ops.append({"op": "update", "collection": "items", "doc_id": doc_id, "data": upd})
        with open(os.path.join(out, "batch.json"), "w", encoding="utf-8") as f:
            json.dump(ops, f, ensure_ascii=False)
        print("site guncellemeleri:", out, "(batch.json; ArtifactData batch ile uygulayin, belge basina if_version ekleyin)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

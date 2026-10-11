// Tools > Bidwars > Loot > 0) Kasa Defteri'nden aktar
// Kasa Defteri (web sayfasi) her esya icin 7 durum sinifi tutar: cikma sansi (%), fiyat araligi ($), kilo ve gercek olculer.
// Bu arac defterin JSON'unu (Assets/_Game/Loot/KasaDefteri.json ya da sayfadan "JSON indir" ile alinan kasa-defteri.json)
// ItemCatalog'a islenir: her esya oyunda kendi sinif sansiyla ve o sinifin fiyat araligindan bir fiyatla cikar.
// Defterde olmayan esyalar icin ayni formul (orta deger + koleksiyon puani) ile tablo uretilir.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bidwarss;
using Bidwarss.Domain;
using UnityEditor;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public static class LootLedger
    {
        public const string JsonPath = "Assets/_Game/Loot/KasaDefteri.json";
        public const string Url = "https://claude.ai/artifact/V2oXCKkU7jt64FpDkaszgx";

        [Serializable] public class LTier { public string id, name, color; }
        [Serializable] public class LClass { public string tier; public float chancePercent, priceMin, priceMax; }
        [Serializable] public class LItem
        {
            public string id, name, note, analysis, assetId;
            public bool classified;
            public float kg, heightCm, widthCm, depthCm, baseValue, collectorScore;
            // Rastgele depo: bir turda bu eşyadan en fazla kaç tane (0 = boyuna ve kiloya göre otomatik). Sitede "Turda en fazla".
            public int maxCount, maxc;
            public LClass[] classes;
        }
        [Serializable] public class LFile { public LTier[] tiers; public LItem[] items; }

        [MenuItem("Tools/Bidwars/Loot/0) Kasa Defteri'nden aktar (fiyat, durum, kilo, ölçü)")]
        public static void ImportMenu()
        {
            var catalog = LootCatalogBuilder.FindCatalog(false);
            if (catalog == null) { LootCatalogBuilder.Build(true); return; }   // Build zaten defteri de isler
            Apply(catalog, true, true);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Bidwars/Loot/Kasa Defteri sayfasını aç")]
        public static void OpenPage() { Application.OpenURL(Url); }

        // En yeni defter dosyasi: KasaDefteri.json ya da Loot klasorune birakilan kasa-defteri*.json
        static string FindFile()
        {
            string best = null; DateTime bestTime = DateTime.MinValue;
            if (!Directory.Exists("Assets/_Game/Loot")) return null;
            foreach (string f in Directory.GetFiles("Assets/_Game/Loot", "*.json", SearchOption.AllDirectories))
            {
                string n = Path.GetFileName(f).ToLowerInvariant().Replace("-", "").Replace("_", "");
                if (!n.StartsWith("kasadefteri")) continue;
                var t = File.GetLastWriteTimeUtc(f);
                if (best == null || t > bestTime) { best = f.Replace('\\', '/'); bestTime = t; }
            }
            return best;
        }

        /// <summary>Defteri katalogdaki esyalara isler. force: degismemis olsa da yeniden uygular.</summary>
        public static string Apply(ItemCatalog catalog, bool verbose, bool force)
        {
            if (catalog == null || catalog.entries == null) return "";
            Undo.RecordObject(catalog, "Kasa Defteri");
            string path = FindFile();
            LFile file = null;
            if (path != null)
            {
                try { file = JsonUtility.FromJson<LFile>(File.ReadAllText(path, Encoding.UTF8)); }
                catch (Exception ex) { Debug.LogError("[Loot] Kasa Defteri okunamadı (" + path + "): " + ex.Message); }
            }
            var items = file != null && file.items != null ? file.items : new LItem[0];
            var log = new StringBuilder();
            int updated = 0, generated = 0;
            var matched = new System.Collections.Generic.HashSet<string>();

            foreach (var e in catalog.entries)
            {
                var def = LootItemTable.FindKey(e.key);
                string lid = !string.IsNullOrEmpty(e.ledgerId) ? e.ledgerId : def != null ? def.ledger : null;
                LItem li = lid != null ? items.FirstOrDefault(x => x != null && x.id == lid) : null;
                if (li == null && def != null && def.ledger != null) li = items.FirstOrDefault(x => x != null && x.id == def.ledger);
                if (li != null)
                {
                    matched.Add(li.id);
                    string stamp = Stamp(li);
                    if (force || e.ledgerStamp != stamp || !e.HasConditionTable)
                    {
                        ApplyItem(e, li, file.tiers, def, log);
                        e.ledgerId = li.id; e.ledgerStamp = stamp;
                        updated++;
                    }
                }
                else if (!e.HasConditionTable)
                {
                    Generate(e, def);
                    generated++;
                    log.AppendLine("  ~ " + e.title + ": defterde yok, tahmini tablo (orta $" + e.baseDollars + ", koleksiyon " + e.collector.ToString("0.#") + ")");
                }
            }
            foreach (var li in items)
                if (li != null && !matched.Contains(li.id))
                    log.AppendLine("  ? defterde var ama oyunda karşılığı yok: " + li.name + " (prefab'ını Assets/ThirdParty'ye at, sonra LootItemTable'a satır ekle)");

            EditorUtility.SetDirty(catalog);
            string msg = "[Loot] Kasa Defteri: " + (path ?? "dosya yok (" + JsonPath + ")") + " -> " + updated + " eşya güncellendi, " + generated + " eşyaya tahmini tablo üretildi.\n" + log;
            if (verbose) Debug.Log(msg);
            return msg;
        }

        static void ApplyItem(ItemCatalog.Entry e, LItem li, LTier[] tiers, LootItemTable.Def def, StringBuilder log)
        {
            if (li.baseValue > 0) e.baseDollars = Mathf.Max(1, Mathf.RoundToInt(li.baseValue));
            if (li.kg > 0) e.kg = li.kg;
            int hand = li.maxCount > 0 ? li.maxCount : li.maxc;
            if (hand > 0 && hand < 20) e.maxCount = Mathf.Clamp(hand, 1, GameRules.HardMaxCount);   // 20 = defterin varsayilani: otomatik birakilir
            e.collector = Mathf.Clamp(li.collectorScore, 0f, 10f);

            // siniflar: defterin sinif sirasina gore 7 yuvaya
            var table = new ItemCatalog.ConditionPrice[7];
            int n = tiers != null && tiers.Length > 0 ? tiers.Length : 7;
            bool any = false;
            if (li.classes != null)
                foreach (var c in li.classes)
                {
                    if (c == null) continue;
                    int idx = tiers != null ? Array.FindIndex(tiers, t => t != null && t.name == c.tier) : -1;
                    if (idx < 0) idx = Array.FindIndex(GameRules.ConditionNames, t => string.Equals(t, c.tier, StringComparison.CurrentCultureIgnoreCase));
                    if (idx < 0) continue;
                    int slot = n == 7 ? idx : Mathf.Clamp(Mathf.RoundToInt(idx * 6f / Mathf.Max(1, n - 1)), 0, 6);
                    int lo = Mathf.Max(1, Mathf.RoundToInt(c.priceMin)), hi = Mathf.Max(lo, Mathf.RoundToInt(c.priceMax));
                    table[slot] = new ItemCatalog.ConditionPrice { chance = Mathf.Max(0f, c.chancePercent), min = lo, max = hi };
                    any = true;
                }
            if (any)
            {
                for (int i = 0; i < 7; i++) if (table[i] == null) table[i] = new ItemCatalog.ConditionPrice { chance = 0, min = 1, max = 1 };
                e.conditionPrices = table;
            }
            else Generate(e, def);   // defterde siniflandirilmamis: formulle

            // olculer: modelin oranlari korunur, en uzun kenar defterdeki en uzun kenar olur
            float w = li.widthCm / 100f, h = li.heightCm / 100f, d = li.depthCm / 100f;
            float longest = Mathf.Max(w, Mathf.Max(h, d));
            if (longest > 0.005f)
            {
                Vector3 size = LootCatalogBuilder.FitSize(e.visualPrefab, new Vector3(w > 0 ? w : longest, h > 0 ? h : longest, d > 0 ? d : longest), def != null && !def.byLength);
                if ((size - e.realSize).sqrMagnitude > 1e-6f)
                    log.AppendLine("  " + e.title + ": " + Fmt(e.realSize) + " -> " + Fmt(size) + " cm, " + li.kg.ToString("0.##") + " kg, orta $" + e.baseDollars);
                e.realSize = size;
            }
            e.crateMask = def != null ? def.mask : LootPools.Mask(e);
        }

        static string Fmt(Vector3 v) { return Mathf.RoundToInt(v.x * 100f) + "x" + Mathf.RoundToInt(v.y * 100f) + "x" + Mathf.RoundToInt(v.z * 100f); }

        static string Stamp(LItem li)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(li)))).Replace("-", "").Substring(0, 16);
        }

        // ---------------------------------------------------------------- formul (Kasa Defteri sayfasiyla ayni)
        // Orta sinif = piyasa degeri. Yukari dogru carpan ivmelenir; ust siniflardaki sicramayi koleksiyon puani belirler.
        static readonly float[] DefaultChances = { 30f, 26f, 20f, 13f, 7f, 3.5f, 0.5f };

        public static float CollectorFor(ItemRarity r)
        {
            switch (r) { case ItemRarity.Common: return 1f; case ItemRarity.Uncommon: return 3f; case ItemRarity.Rare: return 5f; case ItemRarity.Epic: return 7f; default: return 9f; }
        }

        public static void Generate(ItemCatalog.Entry e, LootItemTable.Def def)
        {
            if (def != null)
            {
                if (e.baseDollars <= 0 || e.ledgerStamp == null) e.baseDollars = Mathf.Max(1, def.dollars);
                if (e.kg <= 0f) e.kg = def.kg;
                e.collector = def.collector;
            }
            else if (e.collector <= 0f) e.collector = CollectorFor(e.rarity);
            e.conditionPrices = Table(e.baseDollars, e.collector);
        }

        public static ItemCatalog.ConditionPrice[] Table(int orta, float collector)
        {
            float c = Mathf.Clamp(collector, 0f, 10f);
            var L = new System.Collections.Generic.List<double> { -0.65, -0.35, 0 };
            double acc = 0;
            foreach (double st in new[] { 0.40, 0.45, 0.48 + 0.05 * c, 0.50 + 0.10 * c }) { acc += st; L.Add(acc); }
            var t = new ItemCatalog.ConditionPrice[7];
            for (int k = 0; k < 7; k++)
            {
                double center = Math.Max(1, orta) * Math.Exp(L[k]), pos = k / 6.0, sp = 0.09 + 0.16 * pos * pos;
                int lo = Math.Max(1, RoundPrice(center * (1 - sp))), hi = Math.Max(lo, RoundPrice(center * (1 + sp)));
                t[k] = new ItemCatalog.ConditionPrice { chance = DefaultChances[k], min = lo, max = hi };
            }
            return t;
        }

        static int RoundPrice(double v) { double st = v < 100 ? 1 : v < 1000 ? 5 : 10; return (int)Math.Max(0, Math.Round(v / st, MidpointRounding.AwayFromZero) * st); }
    }
}

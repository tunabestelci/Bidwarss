// Depo yerlesim plani (Tools > Bidwars > Loot > 2 kullanir):
// - Her esya turune kendi kasalari: ceil(turdaki adet / kasa basina) tane, her kasada tek tur esya.
// - Her kasanin boyu kendi icindekine gore (buzdolabi kasasi buyuk, tabanca kolisi kucuk). Sabit hucre yok.
// - Kasalar 10 konteynerin 20 sirasina toplam boy esit olacak sekilde dagitilir (buyukten kucuge), siranin icinde
//   turler karisik dizilir, aralarina esit bosluk birakilir. Koliler ikiser yan yana durur.
using System.Collections.Generic;
using System.Linq;
using Bidwarss;
using Bidwarss.Domain;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public static class LootLayout
    {
        public const float RowStart = 3.3f, RowEnd = 32.1f;   // konteyner yerel x: siranin kullanilabilir boyu (kapi -X tarafinda)
        public const float MinGap = 0.3f, NiceGap = 1.1f, PairGap = 0.1f;
        public static readonly Vector3 MinInterior = new Vector3(0.26f, 0.2f, 0.24f);   // CrateStage.ChooseShape ile ayni
        public const float Jitter = 12f;                       // kucuk kasa ve koliler hafif egik durur (derece)

        public class Box
        {
            public CrateKind kind;
            public string key;          // bu kasaya ayrilan esya (null = herhangi)
            public int item = -1;       // katalog indeksi
            public int count = 1;       // kasadaki en fazla esya
            public Vector3 cell;        // bir esyanin kasadaki yeri (CratePacking)
            // pivot'a gore siranin boyunca (konteyner yerel x) kapladigi aralik, sira 0 ve sira 1 icin (yon farkli olabilir)
            public float[] lo = { -0.6f, -0.6f }, hi = { 0.6f, 0.6f };
            public float Length(int row) { return hi[row] - lo[row]; }
            public int container = -1, row;
            public float x;             // sonuc: pivot'un konteyner yerel x'i
        }

        class Unit
        {
            public List<Box> boxes = new List<Box>();
            public CrateKind kind;
            public float Length(int row) { float l = 0f; foreach (var b in boxes) l += b.Length(row); return l + PairGap * (boxes.Count - 1); }
            public float Max { get { return Mathf.Max(Length(0), Length(1)); } }
            public int hash;
        }

        public static Vector3 SizeOf(ItemCatalog.Entry e)
        {
            return new Vector3(Mathf.Max(.05f, e.realSize.x), Mathf.Max(.05f, e.realSize.y), Mathf.Max(.05f, e.realSize.z));
        }

        /// <summary>Turdaki adetlere gore kasalar. null = katalogda turda adeti olan esya yok (eski grup modu).</summary>
        public static List<Box> Boxes(ItemCatalog cat) { return Boxes(cat, 1f); }

        /// <summary>Rastgele depo: her tur icin, turda cikabilecek EN COK adede (boy/kilo siniri) yetecek kadar kasa ayrilir;
        /// bir turda bunlarin yalniz bir kismi dolar. scale &lt; 1 ayrilan kasalari azaltir (konteynerlere sigsin diye).</summary>
        public static List<Box> Boxes(ItemCatalog cat, float scale)
        {
            if (cat == null || cat.entries == null || cat.entries.Length == 0 || !cat.entries.Any(e => e != null && (e.roundCount > 0 || (cat.randomRound && e.maxCount > 0)))) return null;
            bool random = cat.randomRound;
            var probe = cat.CreateRules(4, 1, new[] { (int)CrateKind.Small, (int)CrateKind.TallSide, (int)CrateKind.Lying, (int)CrateKind.Cardboard }, (int)CrateKind.Cardboard);
            var list = new List<Box>();
            for (int i = 0; i < cat.entries.Length; i++)
            {
                var r = probe.items[i];
                if (random ? !r.InPool : r.roundCount <= 0) continue;
                int k = probe.ManifestKind(r);
                if (k < 0) continue;
                int pool = random ? Mathf.Max(1, Mathf.RoundToInt(r.EffectiveMaxCount() * scale)) : r.roundCount;
                int n = (pool + r.packSize - 1) / r.packSize;                   // RoundEngine: tur esyalari kasalarina esit bolunur
                // rastgele turda bir kasaya en cok packSize esya girebilir; kasa buna gore olculur
                int per = random ? Mathf.Min(pool, r.packSize) : (pool + n - 1) / n;
                var e = cat.entries[i];
                Vector3 size = SizeOf(e);
                bool lie = k == (int)CrateKind.Cardboard ? CratePacking.LiesInParcel(size) : k == (int)CrateKind.Lying && e.lyingOK;
                Vector3 cell = CratePacking.Cell(size, lie);
                for (int b = 0; b < n; b++)
                {
                    var box = new Box { kind = (CrateKind)k, key = e.key, item = i, count = per, cell = cell };
                    Estimate(box);
                    list.Add(box);
                }
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>Rastgele depo: istenen palet sayisi = DepotMaxItems / 10 (4500 esya = 450 palet), turdaki tum esyalarin (en cok adet)
        /// palet sayisini gecmeyecek sekilde. Sahnede o kadar raf yoksa EnsureSlots olabildigince ekler, kalani raporlanir.</summary>
        public static int RandomPalletBudget(ItemCatalog cat, int[] crateKinds)
        {
            var probe = cat.CreateRules(crateKinds.Length, 1, crateKinds, (int)CrateKind.Cardboard);
            int pallets = 0;
            foreach (var r in probe.items)
                if (r.InPool && probe.ManifestKind(r) >= 0) pallets += (r.EffectiveMaxCount() + GameRules.StackSize - 1) / GameRules.StackSize;
            int wished = (GameRules.DepotMaxItems + GameRules.StackSize - 1) / GameRules.StackSize;
            return Mathf.Clamp(Mathf.Min(wished, pallets), 24, GameRules.MaxStacks);
        }

        /// <summary>Kasalarin toplam isteyecegi palet sayisi (her esya turu icin kasalarin toplam kapasitesi / 10, yukari yuvarlanir).</summary>
        public static int PalletsOf(List<Box> boxes)
        {
            var perKey = new Dictionary<string, int>();
            foreach (var b in boxes) { string k = b.key ?? "?"; int n; perKey.TryGetValue(k, out n); perKey[k] = n + Mathf.Max(1, b.count); }
            int pallets = 0;
            foreach (var kv in perKey) pallets += (kv.Value + GameRules.StackSize - 1) / GameRules.StackSize;
            return pallets;
        }

        /// <summary>Olcmeden tahmini kaplama (koli icin kesin: ParcelSize metre cinsinden dis olcu).</summary>
        public static void Estimate(Box b)
        {
            float half;
            if (b.kind == CrateKind.Cardboard)
            {
                Vector3 p = CratePacking.ParcelSize(b.cell, b.count);
                half = Turned(p.x, p.z) * 0.5f + 0.02f;
            }
            else
            {
                Vector3 inner = CratePacking.CrateInterior(b.cell, b.count);
                float a = Mathf.Max(inner.x, MinInterior.x) * 1.12f + 0.1f, w = Mathf.Max(inner.z, MinInterior.z) * 1.12f + 0.1f;
                half = Mathf.Max(Mathf.Max(a, w), Turned(a, w)) * 0.5f;
            }
            for (int r = 0; r < 2; r++) { b.lo[r] = -half; b.hi[r] = half; }
        }

        // a boyunca (x), w capraz kutunun Jitter derece donunce x boyunca kapladigi boy
        public static float Turned(float a, float w)
        {
            float rad = Jitter * Mathf.Deg2Rad;
            return a * Mathf.Cos(rad) + w * Mathf.Sin(rad);
        }

        /// <summary>Kasalari konteyner sıralarına dagitir ve x konumlarini verir. Donen deger: en dar bosluk (m).</summary>
        public static float Arrange(List<Box> boxes, int containers)
        {
            int rows = containers * 2;
            // birimler: ahsap kasa tek, koliler ikiser (benzer boydakiler yan yana)
            var units = new List<Unit>();
            foreach (var b in boxes.Where(b => b.kind != CrateKind.Cardboard)) units.Add(new Unit { kind = b.kind, boxes = { b } });
            var parcels = boxes.Where(b => b.kind == CrateKind.Cardboard).OrderByDescending(b => b.Length(0)).ThenBy(b => b.key, System.StringComparer.Ordinal).ToList();
            for (int i = 0; i < parcels.Count; i += 2)
            {
                var u = new Unit { kind = CrateKind.Cardboard };
                u.boxes.Add(parcels[i]);
                if (i + 1 < parcels.Count) u.boxes.Add(parcels[i + 1]);
                units.Add(u);
            }
            for (int i = 0; i < units.Count; i++) units[i].hash = Hash(units[i].boxes[0].key, i);

            // siralara: en buyukten baslayarak, toplam boyu (bosluklar dahil) en az olan siraya
            var len = new float[rows]; var cnt = new int[rows];
            var inRow = new List<Unit>[rows];
            for (int r = 0; r < rows; r++) inRow[r] = new List<Unit>();
            foreach (var u in units.OrderByDescending(u => u.Max).ThenBy(u => u.hash))
            {
                int best = 0; float bestLen = float.MaxValue;
                for (int r = 0; r < rows; r++)
                {
                    // ayni konteynerde ayni turden cok kasa birikmesin: hafif ceza
                    float l = len[r] + u.Length(r % 2) + MinGap * (cnt[r] + 1) + 0.05f * inRow[r].Count(o => o.boxes[0].key == u.boxes[0].key);
                    if (l < bestLen - 1e-4f) { bestLen = l; best = r; }
                }
                inRow[best].Add(u); len[best] += u.Length(best % 2); cnt[best]++;
            }

            float worst = float.MaxValue;
            for (int r = 0; r < rows; r++)
            {
                var list = inRow[r];
                if (list.Count == 0) continue;
                int row = r % 2, container = r / 2;
                // sira icinde turler karisik: her turun i. kasasi (i+0.5)/n konumuna
                var order = new List<KeyValuePair<double, Unit>>();
                foreach (var g in list.GroupBy(u => u.kind))
                {
                    var members = g.OrderBy(u => u.hash).ToList();
                    for (int i = 0; i < members.Count; i++) order.Add(new KeyValuePair<double, Unit>((i + 0.5) / members.Count + 0.001 * (int)g.Key, members[i]));
                }
                order.Sort((a, b) => a.Key.CompareTo(b.Key));
                float total = 0f; foreach (var u in list) total += u.Length(row);
                float space = RowEnd - RowStart, free = space - total;
                float gap = Mathf.Min(NiceGap, free / (list.Count + 1));
                worst = Mathf.Min(worst, gap);
                gap = Mathf.Max(gap, MinGap);
                float used = total + gap * (list.Count - 1);
                float cursor = RowStart + Mathf.Max(0f, (space - used) * 0.5f);
                foreach (var kv in order)
                {
                    var u = kv.Value;
                    for (int i = 0; i < u.boxes.Count; i++)
                    {
                        var b = u.boxes[i];
                        b.container = container; b.row = row;
                        b.x = cursor - b.lo[row];
                        cursor += b.Length(row) + (i + 1 < u.boxes.Count ? PairGap : 0f);
                    }
                    cursor += gap;
                }
            }
            boxes.Sort((a, b) => a.container != b.container ? a.container.CompareTo(b.container) : a.row != b.row ? a.row.CompareTo(b.row) : a.x.CompareTo(b.x));
            return worst == float.MaxValue ? NiceGap : worst;
        }

        /// <summary>Tum siralarin toplam boyu ve kasalarin toplam kapladigi boy (m).</summary>
        public static void Totals(List<Box> boxes, int containers, out float need, out float have)
        {
            need = 0f; foreach (var b in boxes) need += b.Length(0) + MinGap;
            have = containers * 2 * (RowEnd - RowStart);
        }

        static int Hash(string s, int i)
        {
            unchecked
            {
                int h = (int)2166136261;
                if (s != null) foreach (char c in s) h = (h ^ c) * 16777619;
                h = (h ^ i) * 16777619;
                return h & 0x7fffffff;
            }
        }
    }
}

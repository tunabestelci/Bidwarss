using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Bidwarss.Domain
{
    public enum ItemCondition : byte { Terrible, VeryBad, Bad, Average, Good, VeryGood, Legendary }
    public enum ItemLocation : byte { Sealed, Loose, Held, Stacked }

    // Wooden crate models that stand inside the containers. Each item type lists (crateMask) which of them it may
    // come out of: small things come out of the small crate, tall appliances out of the tall crates, long things
    // out of the lying crate, small special/valuable things out of the taped cardboard box. Bit n of crateMask = CrateKind n.
    public enum CrateKind : byte { Small = 0, Tall = 1, TallSide = 2, Lying = 3, Cardboard = 4 }

    public static class CrateKinds
    {
        public const int Count = 5;
        public const int All = (1 << Count) - 1;
        public static readonly string[] Names = { "Küçük kasa (üstten)", "Uzun kasa (üstten) - kullanılmıyor", "Uzun kasa (konteyner kapısına doğru yandan)", "Yatık uzun kasa (yandan)", "Karton koli (maket bıçağıyla)" };
        public static readonly string[] ShortNames = { "KÜÇÜK", "UZUN", "UZUN-YAN", "YATIK", "KOLİ" };
        public static int Bit(int kind) => 1 << kind;
        public static bool Allows(int mask, int kind) => kind >= 0 && kind < Count && (mask & (1 << kind)) != 0;
    }

    [Serializable]
    public sealed class ItemRule
    {
        public string key;
        public int baseDollars = 100;
        public int selectionWeight = 1;
        public int maxGroups = 5;
        public int crateMask = CrateKinds.All;
        // In a parcel crate kind (GameRules.packedKind) a whole group is packed into a few boxes: small things ~5 per box,
        // bigger ones ~3 per box. Only orders which groups get more boxes.
        public int packSize = 5;
        // Kasa Defteri: this item's own condition table (7 entries, worst -> best). condWeights = chance (any scale,
        // 0 = never), condMin/condMax = dollar price range of that condition. null = the depot-wide percent bands are used.
        public int[] condWeights, condMin, condMax;
        // Manifest mode: how many of this item every round has (0 = not in the manifest). packSize is then the most
        // items one crate of its kind holds (wooden crates: as many as fit side by side, parcels 3-10).
        public int roundCount;
        // ---- Random depot (GameRules.randomRound) ----
        // Real size (longest edge, cm) and weight (grams). 0 = unknown. They give the automatic per-round limit: a 2 m
        // grandfather clock must never come 20 times, a bottle cap may.
        public int sizeCm, grams;
        // Hand-set limit per round (0 = automatic from size and weight). Never above GameRules.HardMaxCount.
        public int maxCount;

        // Biggest count of this item one round may hold, by size and weight. Always 1..HardMaxCount.
        public int SizeCap()
        {
            // Same table as DepotSim.maxCount on the Kasa Defteri site.
            int cap = GameRules.HardMaxCount;
            if (sizeCm >= 150) cap = Math.Min(cap, 5);
            else if (sizeCm >= 100) cap = Math.Min(cap, 10);
            else if (sizeCm >= 70) cap = Math.Min(cap, 15);
            if (grams >= 60000) cap = Math.Min(cap, 5);
            else if (grams >= 30000) cap = Math.Min(cap, 10);
            else if (grams >= 15000) cap = Math.Min(cap, 15);
            return Math.Max(1, cap);
        }
        public int EffectiveMaxCount() => maxCount > 0 ? Math.Min(maxCount, GameRules.HardMaxCount) : SizeCap();
        // Does this type take part in the random depot at all? (roundCount > 0 or an explicit maxCount switches it on.)
        public bool InPool => roundCount > 0 || maxCount > 0;
        public bool HasConditionTable => condWeights != null && condMin != null && condMax != null;
    }

    [Serializable]
    public sealed class ConditionRule
    {
        public int minimumPercent, maximumPercent, weight;
        public ConditionRule(int min, int max, int chance)
        { minimumPercent = min; maximumPercent = max; weight = chance; }
    }

    [Serializable]
    public sealed class GameRules
    {
        public const int StackSize = 10;
        public const int Version = 9;
        public const int HardMaxCount = 20;
        public const int MaxItemKinds = 512;
        public const int MaxCrates = 400;
        public const int MaxStacks = 200;
        public const int MaxRoundCount = 50;
        public const int MaxSelectionWeight = 1000000;
        public int crateCount = 10;
        public int totalGroups = 12;
        public ItemRule[] items;
        // Kind of every crate (CrateKind values). null = legacy mode: every item may come out of every crate.
        public int[] crateKinds;
        // Crate kind whose crates hold packs (3-5 items of one type) instead of one item: the cardboard boxes. -1 = none.
        public int packedKind = -1;
        // Manifest mode: which item type each crate holds (item index, -1 = any). The depot builder lays the crates out
        // for exactly this, so every crate is sized and spaced for its own contents. null = types are spread at random.
        public int[] crateItems;
        // Random depot: every round draws its own item types, counts (mostly 5/10/15/20, never above the item's size limit)
        // and which of the reserved crates are used; the rest of the crates stay empty and inactive. Needs crateKinds and
        // crateItems (the crates reserved per item type: Loot > 2 builds them for the maximum count). totalGroups is then the
        // pallet budget of a round. Without crateItems the fixed manifest (roundCount) or the legacy groups are used.
        public bool randomRound;
        public ConditionRule[] conditions = DefaultConditions();
        // Kasa Defteri sınıfları (yıpranmıştan en iyiye)
        public static readonly string[] ConditionNames = { "Çok kötü", "Kötü", "Orta", "İyi", "Çok iyi", "Destansı", "Efsanevi" };

        public static ConditionRule[] DefaultConditions() => new[] {
            new ConditionRule(15,30,8), new ConditionRule(35,50,14), new ConditionRule(60,85,20),
            new ConditionRule(100,150,27), new ConditionRule(170,220,18),
            new ConditionRule(250,300,10), new ConditionRule(500,600,3) };

        // Manifest mode: every item type with a roundCount appears in every round, exactly that many times.
        // Random depot in effect (needs the per-type reserved crates).
        public bool RandomActive => randomRound && crateKinds != null && crateItems != null;
        public bool Manifest { get { if (RandomActive) return true; if (items != null) foreach (var i in items) if (i != null && i.roundCount > 0) return true; return false; } }

        // Order in which a manifest item picks its crate kind: parcels first, then small, lying, tall crates.
        static readonly int[] KindPriority = { (int)CrateKind.Cardboard, (int)CrateKind.Small, (int)CrateKind.Lying, (int)CrateKind.TallSide, (int)CrateKind.Tall };

        // Crate kind a manifest item goes into: the first allowed kind (in KindPriority order) that this depot has. -1 = none.
        public int ManifestKind(ItemRule item)
        {
            if (crateKinds == null) return -1;
            foreach (int k in KindPriority)
            {
                if (!CrateKinds.Allows(item.crateMask, k)) continue;
                foreach (int c in crateKinds) if (c == k) return k;
            }
            return -1;
        }

        // Pallets of a manifest round: per item type full pallets of ten, then one smaller pallet for the rest.
        public int ManifestStacks
        {
            get
            {
                if (RandomActive) return totalGroups;
                int n = 0;
                foreach (var i in items) if (i != null && i.roundCount > 0 && ManifestKind(i) >= 0) n += (i.roundCount + StackSize - 1) / StackSize;
                return n;
            }
        }

        public void Validate()
        {
            if (Manifest) { ValidateManifest(); return; }
            if (crateCount < 1 || crateCount > MaxCrates || totalGroups < 1 || totalGroups > 30 || totalGroups * StackSize < crateCount - PackedCrates + PackedGroups * StackSize)
                throw new ArgumentException("Kutu sayısı 1–"+MaxCrates+", istif sayısı 1–30 olmalı; her kutuya en az bir eşya düşmeli.");
            if (items == null || items.Length == 0 || items.Length > MaxItemKinds) throw new ArgumentException("Katalogda 1–" + MaxItemKinds + " eşya türü olmalı.");
            if (crateKinds != null)
            {
                if (crateKinds.Length != crateCount) throw new ArgumentException("Kasa türü listesi kasa sayısıyla aynı uzunlukta olmalı.");
                foreach (int k in crateKinds) if (k < 0 || k >= CrateKinds.Count) throw new ArgumentException("Geçersiz kasa türü.");
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int capacity = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key) || item.key.Length > 48 || item.key.Contains("|") || item.key.Contains("\n"))
                    throw new ArgumentException("Eşya anahtarları boş olmamalı ve benzersiz olmalı.");
                if (item.baseDollars < 1 || item.baseDollars > 1000000 || item.selectionWeight < 1 || item.selectionWeight > MaxSelectionWeight || item.maxGroups < 1 || item.maxGroups > 30)
                    throw new ArgumentException("Eşya fiyatı, ağırlığı veya grup sınırı geçersiz.");
                if (item.crateMask < 0 || item.crateMask > CrateKinds.All) throw new ArgumentException("Geçersiz kasa maskesi: " + item.key);
                if (item.packSize < 1 || item.packSize > 10) throw new ArgumentException("Paket boyu 1-10 olmalı: " + item.key);
                ValidateTable(item);
                if (IsEligible(item)) capacity += item.maxGroups;
            }
            if (capacity < totalGroups) throw new ArgumentException("Tür başına grup sınırları toplam istif sayısını karşılamıyor.");
            if (conditions == null || conditions.Length != 7) throw new ArgumentException("Tam yedi durum gerekli.");
            int previous = 0;
            foreach (var band in conditions)
            {
                if (band == null || band.minimumPercent < previous || band.minimumPercent < 1 || band.maximumPercent < band.minimumPercent || band.maximumPercent > 1000 || band.weight < 1 || band.weight > 1000)
                    throw new ArgumentException("Durum fiyat aralıkları sıralı, pozitif ve çakışmasız olmalı.");
                previous = band.maximumPercent + 1;
            }
        }

        void ValidateManifest()
        {
            if (crateCount < 1 || crateCount > MaxCrates) throw new ArgumentException("Kasa sayısı 1–" + MaxCrates + " olmalı.");
            if (crateKinds == null || crateKinds.Length != crateCount) throw new ArgumentException("Kasa türleri eksik: Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir.");
            foreach (int k in crateKinds) if (k < 0 || k >= CrateKinds.Count) throw new ArgumentException("Geçersiz kasa türü.");
            if (items == null || items.Length == 0 || items.Length > MaxItemKinds) throw new ArgumentException("Katalogda 1–" + MaxItemKinds + " eşya türü olmalı.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var types = new int[CrateKinds.Count]; var crates = new int[CrateKinds.Count];
            foreach (int k in crateKinds) crates[k]++;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key) || item.key.Length > 48 || item.key.Contains("|") || item.key.Contains("\n"))
                    throw new ArgumentException("Eşya anahtarları boş olmamalı ve benzersiz olmalı.");
                if (item.baseDollars < 1 || item.baseDollars > 1000000 || item.selectionWeight < 1 || item.selectionWeight > MaxSelectionWeight || item.maxGroups < 1 || item.maxGroups > 30)
                    throw new ArgumentException("Eşya fiyatı, ağırlığı veya grup sınırı geçersiz: " + item.key);
                if (item.crateMask < 0 || item.crateMask > CrateKinds.All) throw new ArgumentException("Geçersiz kasa maskesi: " + item.key);
                if (item.packSize < 1 || item.packSize > 10) throw new ArgumentException("Paket boyu 1-10 olmalı: " + item.key);
                if (item.roundCount < 0 || item.roundCount > MaxRoundCount) throw new ArgumentException("Turdaki adet 0-" + MaxRoundCount + " olmalı: " + item.key);
                ValidateTable(item);
                if (item.maxCount < 0 || item.maxCount > HardMaxCount || item.sizeCm < 0 || item.grams < 0) throw new ArgumentException("Tur başına en çok adet 0-" + HardMaxCount + " olmalı: " + item.key);
                if (item.roundCount > 0) { int k = ManifestKind(item); if (k >= 0) types[k]++; }
            }
            for (int k = 0; k < CrateKinds.Count && !RandomActive; k++)
                if (types[k] > crates[k])
                    throw new ArgumentException(CrateKinds.Names[k] + ": " + types[k] + " eşya türü var ama " + crates[k] + " kasa. Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir (kasaları adetlere göre yeniden dizer).");
            if (crateItems != null)
            {
                if (crateItems.Length != crateCount) throw new ArgumentException("Kasa eşya listesi kasa sayısıyla aynı uzunlukta olmalı.");
                foreach (int t in crateItems) if (t < -1 || t >= items.Length) throw new ArgumentException("Geçersiz kasa eşyası.");
            }
            int stacks = ManifestStacks;
            if (RandomActive && (totalGroups < 1 || totalGroups > MaxStacks)) throw new ArgumentException("Rastgele tur: palet bütçesi 1-" + MaxStacks + " olmalı.");
            if (stacks < 1 || stacks > MaxStacks) throw new ArgumentException("Palet sayısı 1-" + MaxStacks + " olmalı (şu an " + stacks + ").");
            ValidateConditions();
        }

        void ValidateTable(ItemRule item)
        {
            if (item.condWeights == null && item.condMin == null && item.condMax == null) return;
            if (!item.HasConditionTable || item.condWeights.Length != 7 || item.condMin.Length != 7 || item.condMax.Length != 7)
                throw new ArgumentException("Durum tablosu 7 satır olmalı: " + item.key);
            long sum = 0;
            for (int c = 0; c < 7; c++)
            {
                if (item.condWeights[c] < 0 || item.condWeights[c] > MaxSelectionWeight || item.condMin[c] < 1 || item.condMax[c] < item.condMin[c] || item.condMax[c] > 10000000)
                    throw new ArgumentException("Durum tablosu geçersiz (şans 0+, fiyat 1+, en az <= en çok): " + item.key);
                sum += item.condWeights[c];
            }
            if (sum <= 0) throw new ArgumentException("Durum tablosunda en az bir sınıfın şansı olmalı: " + item.key);
        }

        void ValidateConditions()
        {
            if (conditions == null || conditions.Length != 7) throw new ArgumentException("Tam yedi durum gerekli.");
            int previous = 0;
            foreach (var band in conditions)
            {
                if (band == null || band.minimumPercent < previous || band.minimumPercent < 1 || band.maximumPercent < band.minimumPercent || band.maximumPercent > 1000 || band.weight < 1 || band.weight > 1000)
                    throw new ArgumentException("Durum fiyat aralıkları sıralı, pozitif ve çakışmasız olmalı.");
                previous = band.maximumPercent + 1;
            }
        }

        // Number of parcel crates (cardboard boxes) and how many groups of ten they hold: each group is split into 2-3 boxes.
        public int PackedCrates
        {
            get
            {
                if (packedKind < 0 || crateKinds == null) return 0;
                int n = 0; foreach (int k in crateKinds) if (k == packedKind) n++;
                return n;
            }
        }
        public int PackedGroups
        {
            get
            {
                int b = PackedCrates;
                if (b <= 0) return 0;
                if (b < 2) return 1;
                int g = (int)Math.Round(b / 2.5, MidpointRounding.AwayFromZero);
                int min = (b + 2) / 3, max = b / 2;
                return Math.Max(min, Math.Min(max, g));
            }
        }

        // May this item type come out of crate #crate?
        public bool Accepts(ItemRule item, int crate)
        {
            if (crateKinds == null) return true;
            return crate >= 0 && crate < crateKinds.Length && CrateKinds.Allows(item.crateMask, crateKinds[crate]);
        }

        // An item type that no crate of this depot accepts can never appear.
        public bool IsEligible(ItemRule item)
        {
            if (crateKinds == null) return true;
            for (int c = 0; c < crateKinds.Length; c++) if (CrateKinds.Allows(item.crateMask, crateKinds[c])) return true;
            return false;
        }

        public string Fingerprint()
        {
            Validate();
            var s = new StringBuilder().Append(Version).Append('|').Append(crateCount).Append('|').Append(totalGroups);
            if (Manifest) s.Append("|M");
            if (RandomActive) s.Append("|R");
            if (crateItems != null) { s.Append("|I"); foreach (int t in crateItems) s.Append(',').Append(t); }
            foreach (var i in items)
            {
                s.Append('|').Append(i.key).Append("|n").Append(i.roundCount).Append("|x").Append(i.maxCount).Append("|z").Append(i.sizeCm).Append("|w").Append(i.grams).Append('|').Append(i.baseDollars).Append('|').Append(i.selectionWeight).Append('|').Append(i.maxGroups).Append('|').Append(i.crateMask).Append('|').Append(i.packSize);
                if (i.HasConditionTable) for (int c = 0; c < 7; c++) s.Append("|C").Append(i.condWeights[c]).Append(',').Append(i.condMin[c]).Append(',').Append(i.condMax[c]);
            }
            s.Append("|P").Append(packedKind);
            if (crateKinds != null) { s.Append("|K"); foreach (int k in crateKinds) s.Append(',').Append(k); }
            foreach (var c in conditions) s.Append('|').Append(c.minimumPercent).Append('|').Append(c.maximumPercent).Append('|').Append(c.weight);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        public int Price(int kind, ItemCondition condition, int percent)
        {
            var band = conditions[(int)condition];
            if (percent < band.minimumPercent || percent > band.maximumPercent) throw new ArgumentOutOfRangeException(nameof(percent));
            return Math.Max(1, checked(items[kind].baseDollars * percent) / 100);
        }
    }

    public struct StableRandom
    {
        uint state;
        public StableRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x9E3779B9; }
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public int Range(int exclusiveMax)
        {
            if (exclusiveMax < 1) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            // Rejection sampling avoids modulo bias.
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)exclusiveMax);
            uint n; do { n = Next(); } while (n >= limit);
            return (int)(n % exclusiveMax);
        }
        public long RangeLong(long exclusiveMax)
        {
            if (exclusiveMax < 1) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            if (exclusiveMax <= int.MaxValue) return Range((int)exclusiveMax);
            ulong span = (ulong)exclusiveMax, limit = ulong.MaxValue - (ulong.MaxValue % span), n;
            do { n = ((ulong)Next() << 32) | Next(); } while (n >= limit);
            return (long)(n % span);
        }
        public void Shuffle<T>(IList<T> values)
        { for (int i = values.Count - 1; i > 0; i--) { int j = Range(i + 1); T t = values[i]; values[i] = values[j]; values[j] = t; } }
    }
}

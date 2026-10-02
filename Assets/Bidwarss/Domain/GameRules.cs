using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Bidwarss.Domain
{
    public enum ItemCondition : byte { Terrible, VeryBad, Bad, Average, Good, VeryGood, Legendary }
    public enum ItemLocation : byte { Sealed, Loose, Held, Stacked }

    [Serializable]
    public sealed class ItemRule
    {
        public string key;
        public int baseDollars = 100;
        public int selectionWeight = 1;
        public int maxGroups = 5;
        // Optional per-item market: dollar range for each of the seven conditions, ascending and
        // non-overlapping. Null/empty falls back to baseDollars x the global percent bands, which
        // cannot express antiques (a collectible's top tiers are worth far more than a plain item's).
        public int[] minDollars;
        public int[] maxDollars;
        public bool HasMarket => minDollars != null && maxDollars != null && minDollars.Length > 0;
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
        public const int Version = 3;
        public const int MaxItemKinds = 64;
        public const int MaxItemDollars = 1000000;
        public int crateCount = 10;
        public int totalGroups = 12;
        public ItemRule[] items;
        public ConditionRule[] conditions = DefaultConditions();
        public static readonly string[] ConditionNames = { "Rezalet", "Çok kötü", "Kötü", "Orta", "İyi", "Çok iyi", "Efsane" };

        public static ConditionRule[] DefaultConditions() => new[] {
            new ConditionRule(15,30,8), new ConditionRule(35,50,14), new ConditionRule(60,85,20),
            new ConditionRule(100,150,27), new ConditionRule(170,220,18),
            new ConditionRule(250,300,10), new ConditionRule(500,600,3) };

        public void Validate()
        {
            if (crateCount < 1 || crateCount > 20 || totalGroups < 1 || totalGroups > 30 || totalGroups * StackSize < crateCount)
                throw new ArgumentException("Kutu sayısı 1–20, istif sayısı 1–30 olmalı; her kutuya en az bir eşya düşmeli.");
            if (items == null || items.Length == 0 || items.Length > MaxItemKinds) throw new ArgumentException("Katalogda 1–" + MaxItemKinds + " eşya türü olmalı.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int capacity = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key) || item.key.Length > 48 || item.key.Contains("|") || item.key.Contains("\n"))
                    throw new ArgumentException("Eşya anahtarları boş olmamalı ve benzersiz olmalı.");
                if (item.baseDollars < 1 || item.baseDollars > 100000 || item.selectionWeight < 1 || item.selectionWeight > 1000 || item.maxGroups < 1 || item.maxGroups > 30)
                    throw new ArgumentException("Eşya fiyatı, ağırlığı veya grup sınırı geçersiz.");
                ValidateMarket(item);
                capacity += item.maxGroups;
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

        static void ValidateMarket(ItemRule item)
        {
            if (!item.HasMarket) return;
            if (item.minDollars.Length != 7 || item.maxDollars == null || item.maxDollars.Length != 7)
                throw new ArgumentException("Eşya piyasası tam yedi durum fiyat aralığı içermeli: " + item.key);
            int previous = 0;
            for (int i = 0; i < 7; i++)
            {
                int low = item.minDollars[i], high = item.maxDollars[i];
                // Every condition must sit strictly above the previous one: a worse item can never out-price a better one.
                if (low < 1 || high < low || high > MaxItemDollars || low <= previous)
                    throw new ArgumentException("Durum fiyat aralıkları artan ve çakışmasız olmalı: " + item.key);
                previous = high;
            }
        }

        public string Fingerprint()
        {
            Validate();
            var s = new StringBuilder().Append(Version).Append('|').Append(crateCount).Append('|').Append(totalGroups);
            foreach (var i in items)
            {
                s.Append('|').Append(i.key).Append('|').Append(i.baseDollars).Append('|').Append(i.selectionWeight).Append('|').Append(i.maxGroups);
                if (i.HasMarket) for (int k = 0; k < 7; k++) s.Append(':').Append(i.minDollars[k]).Append('-').Append(i.maxDollars[k]);
            }
            foreach (var c in conditions) s.Append('|').Append(c.minimumPercent).Append('|').Append(c.maximumPercent).Append('|').Append(c.weight);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        // Inclusive dollar range of one item in one condition.
        public void PriceBand(int kind, ItemCondition condition, out int min, out int max)
        {
            var item = items[kind];
            int c = (int)condition;
            if (item.HasMarket) { min = item.minDollars[c]; max = item.maxDollars[c]; return; }
            var band = conditions[c];
            min = Math.Max(1, checked(item.baseDollars * band.minimumPercent) / 100);
            max = Math.Max(min, checked(item.baseDollars * band.maximumPercent) / 100);
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
        public void Shuffle<T>(IList<T> values)
        { for (int i = values.Count - 1; i > 0; i--) { int j = Range(i + 1); T t = values[i]; values[i] = values[j]; values[j] = t; } }
    }
}

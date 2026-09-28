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
        public const int Version = 2;
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
            if (items == null || items.Length == 0 || items.Length > 16) throw new ArgumentException("Katalogda 1–16 eşya türü olmalı.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int capacity = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key) || item.key.Length > 48 || item.key.Contains("|") || item.key.Contains("\n"))
                    throw new ArgumentException("Eşya anahtarları boş olmamalı ve benzersiz olmalı.");
                if (item.baseDollars < 1 || item.baseDollars > 100000 || item.selectionWeight < 1 || item.selectionWeight > 1000 || item.maxGroups < 1 || item.maxGroups > 30)
                    throw new ArgumentException("Eşya fiyatı, ağırlığı veya grup sınırı geçersiz.");
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

        public string Fingerprint()
        {
            Validate();
            var s = new StringBuilder().Append(Version).Append('|').Append(crateCount).Append('|').Append(totalGroups);
            foreach (var i in items) s.Append('|').Append(i.key).Append('|').Append(i.baseDollars).Append('|').Append(i.selectionWeight).Append('|').Append(i.maxGroups);
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
        public void Shuffle<T>(IList<T> values)
        { for (int i = values.Count - 1; i > 0; i--) { int j = Range(i + 1); T t = values[i]; values[i] = values[j]; values[j] = t; } }
    }
}

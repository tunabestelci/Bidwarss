using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bidwarss.Domain
{
    // Seven wear classes, worst to best. The names and order match the "Kasa Defteri" item wiki.
    public enum ItemCondition : byte { VeryBad, Bad, Average, Good, VeryGood, Epic, Legendary }
    public enum ItemLocation : byte { Sealed, Loose, Held, Stacked }

    // Chance weights (thousandths of a percent) and price bounds for the seven classes of one item.
    public sealed class ClassTable
    {
        public readonly int[] weight = new int[GameRules.ConditionCount];
        public readonly int[] min = new int[GameRules.ConditionCount];
        public readonly int[] max = new int[GameRules.ConditionCount];
        public int totalWeight;
    }

    [Serializable]
    public sealed class ItemRule
    {
        public string key;
        public int baseDollars = 100;          // typical price of an average-class piece
        public int selectionWeight = 1;        // how likely the type is to be drawn into a depot
        public int maxCount;                   // most pieces of this type in one depot, 0 = derive from size and weight
        public float kg;
        public int heightCm, widthCm, depthCm; // real-world size; big pieces appear in smaller numbers
        public float collector;                // 0..10, how steeply the price climbs in the top classes
        // Optional explicit class table (the wiki exports it). Empty arrays mean "derive from baseDollars/collector".
        public float[] classChance;
        public int[] classMin, classMax;

        static readonly double[] DefaultChance = { 30, 26, 20, 13, 7, 3.5, 0.5 };

        public bool HasExplicitTable =>
            classChance != null && classChance.Length > 0 || classMin != null && classMin.Length > 0 || classMax != null && classMax.Length > 0;

        // A big, heavy piece must never come by the dozen: a two metre clock appears at most five times.
        public int EffectiveMaxCount()
        {
            if (maxCount > 0) return Math.Min(GameRules.MaxPerType, maxCount);
            int size = Math.Max(heightCm, Math.Max(widthCm, depthCm));
            int cap = GameRules.MaxPerType;
            if (size >= 150) cap = 5; else if (size >= 100) cap = 10; else if (size >= 70) cap = 15;
            if (kg >= 60) cap = Math.Min(cap, 5); else if (kg >= 30) cap = Math.Min(cap, 10); else if (kg >= 15) cap = Math.Min(cap, 15);
            return cap;
        }

        public static double Quantize(double value) => Math.Floor(value * 1000000.0 + 0.5) / 1000000.0;

        // Price multipliers of the seven classes; "Orta" (index 2) is the market value.
        public static double[] Multipliers(double collector)
        {
            double c = Math.Min(10, Math.Max(0, collector));
            var log = new double[GameRules.ConditionCount];
            log[0] = -0.65; log[1] = -0.35; log[2] = 0;
            double[] steps = { 0.40, 0.45, 0.48 + 0.05 * c, 0.50 + 0.10 * c };
            double total = 0;
            for (int k = 0; k < steps.Length; k++) { total += steps[k]; log[3 + k] = total; }
            var result = new double[GameRules.ConditionCount];
            for (int k = 0; k < result.Length; k++) result[k] = Quantize(Math.Exp(log[k]));
            return result;
        }

        static int RoundPrice(double value)
        {
            double step = value < 100 ? 1 : (value < 1000 ? 5 : 10);
            return (int)Math.Max(0, Math.Floor(value / step + 0.5) * step);
        }

        public ClassTable Table()
        {
            var table = new ClassTable();
            int n = GameRules.ConditionCount;
            if (HasExplicitTable)
            {
                if (classChance == null || classMin == null || classMax == null || classChance.Length != n || classMin.Length != n || classMax.Length != n)
                    throw new ArgumentException("Sınıf tablosu tam yedi satır içermeli: " + key);
                for (int k = 0; k < n; k++)
                {
                    if (classChance[k] < 0 || classChance[k] > 100 || classMin[k] < 1 || classMax[k] < classMin[k] || classMax[k] > GameRules.MaxPrice)
                        throw new ArgumentException("Sınıf şansı veya fiyat aralığı geçersiz: " + key);
                    table.weight[k] = (int)Math.Floor(classChance[k] * 1000.0 + 0.5);
                    table.min[k] = classMin[k]; table.max[k] = classMax[k]; table.totalWeight += table.weight[k];
                }
            }
            else
            {
                var multiplier = Multipliers(collector);
                for (int k = 0; k < n; k++)
                {
                    double center = Math.Max(1, baseDollars) * multiplier[k], position = k / (double)(n - 1);
                    double spread = 0.09 + 0.16 * position * position;
                    int low = Math.Max(1, RoundPrice(center * (1 - spread)));
                    int high = Math.Max(low, RoundPrice(center * (1 + spread)));
                    table.weight[k] = (int)Math.Floor(DefaultChance[k] * 1000.0 + 0.5);
                    table.min[k] = low; table.max[k] = Math.Min(GameRules.MaxPrice, Math.Max(low, high)); table.totalWeight += table.weight[k];
                }
            }
            if (table.totalWeight < 1) throw new ArgumentException("Hiçbir sınıfın çıkma şansı yok: " + key);
            return table;
        }
    }

    [Serializable]
    public sealed class GameRules
    {
        public const int StackSize = 10;          // carry limit and capacity of a full rack
        public const int Version = 3;
        public const int ConditionCount = 7;
        public const int MaxPerType = 20;         // never more than twenty of one type in a depot
        public const int CrateLimit = 24;         // pieces one container can spill out
        public const int MaxPrice = 10000000;
        public static readonly int[] CountOptions = { 5, 10, 15, 20 };
        public static readonly int[] CountWeights = { 3, 4, 3, 2 };

        public int crateCount = 10;               // containers that exist in the scene
        public int minCrates = 6;                 // each depot uses between minCrates and crateCount of them
        public int totalGroups = 12;              // racks that exist in the scene
        public int minGroups = 8;                 // each depot fills between minGroups and totalGroups racks
        public ItemRule[] items;
        public static readonly string[] ConditionNames = { "Çok kötü", "Kötü", "Orta", "İyi", "Çok iyi", "Destansı", "Efsanevi" };

        public static int RacksFor(int count) => (count + StackSize - 1) / StackSize;

        public void Validate()
        {
            if (crateCount < 1 || crateCount > 20 || minCrates < 1 || minCrates > crateCount)
                throw new ArgumentException("Konteyner sayısı 1–20 olmalı; en az konteyner sayısı 1 ile toplam arasında olmalı.");
            if (totalGroups < 1 || totalGroups > 30 || minGroups < 1 || minGroups > totalGroups)
                throw new ArgumentException("Raf sayısı 1–30 olmalı; en az raf sayısı 1 ile toplam arasında olmalı.");
            if (minCrates * CrateLimit < totalGroups * StackSize)
                throw new ArgumentException("En az konteyner, en kalabalık depodaki bütün eşyayı taşıyamaz.");
            if (items == null || items.Length == 0 || items.Length > 40) throw new ArgumentException("Katalogda 1–40 eşya türü olmalı.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key) || item.key.Length > 48 || item.key.Contains("|") || item.key.Contains("\n"))
                    throw new ArgumentException("Eşya anahtarları boş olmamalı ve benzersiz olmalı.");
                if (item.baseDollars < 1 || item.baseDollars > 100000 || item.selectionWeight < 1 || item.selectionWeight > 1000 || item.maxCount < 0 || item.maxCount > MaxPerType)
                    throw new ArgumentException("Eşya fiyatı, ağırlığı veya adet sınırı geçersiz: " + item.key);
                if (item.kg < 0 || item.kg > 5000 || item.heightCm < 0 || item.heightCm > 2000 || item.widthCm < 0 || item.widthCm > 2000 || item.depthCm < 0 || item.depthCm > 2000 ||
                    item.collector < 0 || item.collector > 10)
                    throw new ArgumentException("Eşya ölçüleri veya koleksiyon puanı geçersiz: " + item.key);
                item.Table();
            }
        }

        public string Fingerprint()
        {
            Validate();
            var inv = CultureInfo.InvariantCulture;
            var s = new StringBuilder().Append(Version).Append('|').Append(crateCount).Append('|').Append(minCrates).Append('|').Append(totalGroups).Append('|').Append(minGroups);
            foreach (var i in items)
            {
                s.Append('|').Append(i.key).Append('|').Append(i.selectionWeight).Append('|').Append(i.EffectiveMaxCount());
                var table = i.Table();
                for (int k = 0; k < ConditionCount; k++) s.Append('|').Append(table.weight[k].ToString(inv)).Append(',').Append(table.min[k].ToString(inv)).Append(',').Append(table.max[k].ToString(inv));
            }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-", "").ToLowerInvariant();
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

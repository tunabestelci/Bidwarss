using System;
using System.Collections.Generic;

namespace Bidwarss.Domain
{
    // The exchange format between the "Kasa Defteri" item wiki and the game ("bidwarss-catalog" version 1).
    // Plain serialisable classes so both Unity's JsonUtility and test code can fill them.
    [Serializable]
    public sealed class CatalogClassData
    {
        public float chance;            // percent
        public int priceMin, priceMax;  // dollars
    }

    [Serializable]
    public sealed class CatalogItemData
    {
        public string key, title, shape;
        public float kg;
        public int heightCm, widthCm, depthCm;
        public int baseValue;
        public float collector;
        public int maxCount;
        public int selectionWeight = 1;
        public CatalogClassData[] classes;   // empty = derive from baseValue and collector

        public ItemRule ToRule()
        {
            var rule = new ItemRule
            {
                key = key, baseDollars = Math.Max(1, baseValue), selectionWeight = Math.Max(1, selectionWeight), maxCount = maxCount,
                kg = kg, heightCm = heightCm, widthCm = widthCm, depthCm = depthCm, collector = collector
            };
            if (classes != null && classes.Length > 0)
            {
                rule.classChance = new float[classes.Length]; rule.classMin = new int[classes.Length]; rule.classMax = new int[classes.Length];
                for (int i = 0; i < classes.Length; i++) { rule.classChance[i] = classes[i].chance; rule.classMin[i] = classes[i].priceMin; rule.classMax[i] = classes[i].priceMax; }
            }
            return rule;
        }
    }

    [Serializable]
    public sealed class CatalogFileData
    {
        public const string Format = "bidwarss-catalog";
        public string format;
        public int version;
        public string[] tiers;
        public CatalogItemData[] items;

        // Returns null when the file can be used, otherwise a short Turkish reason.
        public string Problem()
        {
            if (format != Format || version != 1) return "Bu dosya bir Bidwarss kataloğu değil (format \"" + Format + "\", sürüm 1 bekleniyordu).";
            if (tiers == null || tiers.Length != GameRules.ConditionCount) return "Katalogda tam " + GameRules.ConditionCount + " durum sınıfı olmalı.";
            if (items == null || items.Length == 0) return "Katalogda eşya yok.";
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Add(item.key)) return "Eşya anahtarları boş olmamalı ve benzersiz olmalı.";
                if (item.classes != null && item.classes.Length != 0 && item.classes.Length != GameRules.ConditionCount) return item.key + ": " + GameRules.ConditionCount + " sınıf satırı gerekli.";
            }
            try
            {
                var rules = new GameRules { items = Array.ConvertAll(items, i => i.ToRule()) };
                rules.Validate();
            }
            catch (ArgumentException ex) { return ex.Message; }
            return null;
        }

        public ItemRule[] ToRules() => Array.ConvertAll(items, i => i.ToRule());
    }
}

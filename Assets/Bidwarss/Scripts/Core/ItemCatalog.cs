using System;
using UnityEngine;
using Bidwarss.Domain;

namespace Bidwarss
{
    [CreateAssetMenu(menuName = "Bidwarss/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string title;
            [Min(1)] public int baseDollars = 100;
            [Range(1,1000)] public int selectionWeight = 1;
            [Tooltip("Most pieces of this type in one depot. 0 = derive from size and weight (big pieces come in small numbers).")]
            [Range(0,20)] public int maxCount;
            [Min(0)] public float kg;
            [Min(0)] public int heightCm, widthCm, depthCm;
            [Range(0,10)] public float collector;
            [Tooltip("Optional seven-row class table imported from the item wiki. Leave empty to derive it from base price and collector score.")]
            public float[] classChance;
            public int[] classMin, classMax;
            public Color color = Color.white;
            public GameObject visualPrefab;
            public SampleShape sampleShape;
        }
        public enum SampleShape { Box, Mirror, Table, Chair, Radio, Lamp, Clock, Vase, Sword }
        public Entry[] entries;
        public GameRules CreateRules(int crates, int groups) => new GameRules {
            crateCount = crates, totalGroups = groups,
            minCrates = Math.Max(1, Math.Min(crates, (crates * 3 + 4) / 5)),
            minGroups = Math.Max(1, Math.Min(groups, (groups * 2 + 2) / 3)),
            items = Array.ConvertAll(entries, e => new ItemRule {
                key = e.key, baseDollars = e.baseDollars, selectionWeight = e.selectionWeight, maxCount = e.maxCount,
                kg = e.kg, heightCm = e.heightCm, widthCm = e.widthCm, depthCm = e.depthCm, collector = e.collector,
                classChance = e.classChance, classMin = e.classMin, classMax = e.classMax }) };
    }
}

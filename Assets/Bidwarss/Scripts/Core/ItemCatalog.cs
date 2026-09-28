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
            [Range(1,30)] public int maxGroups = 5;
            public Color color = Color.white;
            public GameObject visualPrefab;
            public SampleShape sampleShape;
        }
        public enum SampleShape { Box, Mirror, Table, Chair, Radio, Lamp }
        public Entry[] entries;
        public ConditionRule[] conditions = GameRules.DefaultConditions();
        public GameRules CreateRules(int crates, int groups) => new GameRules {
            crateCount = crates, totalGroups = groups, conditions = conditions,
            items = Array.ConvertAll(entries, e => new ItemRule {
                key = e.key, baseDollars = e.baseDollars, selectionWeight = e.selectionWeight, maxGroups = e.maxGroups }) };
    }
}

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
            [Tooltip("Possessed form for epic/legendary finds: 'Mira Starling'in Motosikleti' needs 'motosikleti'.")]
            public string owned;
            [Min(1)] public int baseDollars = 100;
            [Range(1,1000)] public int selectionWeight = 1;
            [Range(1,30)] public int maxGroups = 5;
            [Tooltip("Collector potential 0-10. Informational: prices come from the dollar ranges below.")]
            [Range(0,10)] public int collector;
            [Tooltip("Dollar range per condition, Rezalet to Efsane. Leave empty/zero to use base x global percent bands. Fill with Bidwarss > Sync Market Catalog.")]
            public int[] minDollars = new int[0];
            public int[] maxDollars = new int[0];
            public Color color = Color.white;
            public GameObject visualPrefab;
            public SampleShape sampleShape;
        }
        public enum SampleShape { Box, Mirror, Table, Chair, Radio, Lamp }
        public Entry[] entries;
        // Name shown for a concrete item: the famous previous owner replaces the plain title when there is one.
        public string DisplayTitle(int kind, int star)
        {
            var entry = entries[kind];
            return star >= 0 && !string.IsNullOrWhiteSpace(entry.owned) ? Provenance.Title(entry.owned, star) : entry.title;
        }
        public ConditionRule[] conditions = GameRules.DefaultConditions();
        public GameRules CreateRules(int crates, int groups) => new GameRules {
            crateCount = crates, totalGroups = groups, conditions = conditions,
            items = Array.ConvertAll(entries, e => new ItemRule {
                key = e.key, baseDollars = e.baseDollars, selectionWeight = e.selectionWeight, maxGroups = e.maxGroups,
                // A market with any zero is incomplete: fall back to the legacy percent bands instead of rejecting the catalog.
                minDollars = HasMarket(e) ? (int[])e.minDollars.Clone() : null, maxDollars = HasMarket(e) ? (int[])e.maxDollars.Clone() : null }) };
        static bool HasMarket(Entry e) =>
            e.minDollars != null && e.maxDollars != null && e.minDollars.Length == 7 && e.maxDollars.Length == 7 &&
            Array.TrueForAll(e.minDollars, v => v > 0) && Array.TrueForAll(e.maxDollars, v => v > 0);
    }
}

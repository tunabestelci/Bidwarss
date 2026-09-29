// 20 ft kasa/konteyner. Grup kokune eklenir; icindeki dogma noktalarini ve hacmi toplar.
using System.Collections.Generic;
using UnityEngine;

namespace DepoLevel
{
    public class DepoContainer : MonoBehaviour
    {
        [Tooltip("1-10. 01-05 bati (sol), 06-10 dogu (sag)")]
        public int containerNo;
        public string side;
        public bool unlocked = true;
        public List<DepoLootSpawn> lootSpawns = new List<DepoLootSpawn>();
        [Tooltip("Konteyner ic hacmi (DepoZone trigger)")]
        public DepoZone volume;
        [Tooltip("Ornek (yer tutucu) esyalarin grubu. Oyunda gercek loot dogunca silinebilir.")]
        public GameObject placeholderRoot;

        public string DisplayNumber => containerNo.ToString("00");

        public void CollectChildren()
        {
            lootSpawns.Clear();
            GetComponentsInChildren(true, lootSpawns);
            lootSpawns.Sort((a, b) => a.index.CompareTo(b.index));
            if (volume == null) volume = GetComponentInChildren<DepoZone>(true);
        }

        public void ClearPlaceholders()
        {
            if (placeholderRoot == null) return;
            if (Application.isPlaying) Destroy(placeholderRoot); else DestroyImmediate(placeholderRoot);
            placeholderRoot = null;
        }

        /// Verilen prefab listesinden her dogma noktasina sirayla/rastgele bir esya koyar.
        public List<GameObject> SpawnLoot(IList<GameObject> prefabs, int count, int seed)
        {
            var result = new List<GameObject>();
            if (prefabs == null || prefabs.Count == 0) return result;
            var rng = new System.Random(seed);
            int n = Mathf.Min(count, lootSpawns.Count);
            for (int i = 0; i < n; i++)
            {
                var p = prefabs[rng.Next(prefabs.Count)];
                var go = lootSpawns[i].Spawn(p);
                if (go != null) result.Add(go);
            }
            return result;
        }
    }
}

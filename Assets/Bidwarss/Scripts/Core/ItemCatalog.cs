using System;
using UnityEngine;

namespace Bidwarss
{
    [CreateAssetMenu(menuName = "Bidwarss/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string title;
            public Color color = Color.white;
            public Vector3 size = Vector3.one * .4f;
        }

        public Entry[] entries;

        // Item art and constrained scenario distribution are intentionally deferred.
    }
}

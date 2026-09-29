// Anahtar -> prefab eslemesi. Builder, JSON'daki "prefab" anahtari eslesen gruplara
// ilkel kutular yerine senin modelini koyar. Slot/tetik/dogma noktalari ve yazilar korunur.
// Pivot kurali:
//   - Grup anahtarlari (Raf_Standart, Askilik, Konteyner_Buyuk ...): prefab pivotu = grubun orijini
//     (zemin seviyesi), grubun yerel eksenlerinde. Boyutlar icin README'deki tabloya bak.
//   - Kutu anahtarlari (RampaKapisi, GirisKapisi, Kasa_Kapisi): pivot = kutunun ALT ORTASI.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DepoLevel
{
    [CreateAssetMenu(fileName = "DepoPrefabMap", menuName = "Depo/Prefab Map", order = 0)]
    public class DepoPrefabMap : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public GameObject prefab;
            [Tooltip("Prefab kendi isigini iceriyorsa kapat; JSON'daki isik yine de olusturulur.")]
            public bool keepJsonLights = true;
            public Vector3 positionOffset;
            public Vector3 rotationOffset;
            public Vector3 scale = Vector3.one;
        }

        public List<Entry> entries = new List<Entry>();

        public static readonly string[] KnownKeys =
        {
            "Raf_Standart", "Raf_Kitap", "Raf_Duvar", "Raf_Palet", "Lastik_Rafi", "Askilik",
            "Gitar_Duvari", "Gitar_Standi", "Bateri_Podyumu", "Teshir_Masasi", "Kaide_Kup", "Podyum", "Delikli_Pano", "Motor_Standi",
            "Bisiklet_Rafi", "Dik_Raf", "Tel_Sepet", "Manken", "Aksesuar_Standi", "Dik_Stand",
            "Takim_Dolabi", "Transpalet", "Tasima_Arabasi", "Konteyner_Buyuk", "GirisKapisi",
            "Tavan_Lambasi", "Envanter_Panosu"
        };

        public Entry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in entries)
                if (e != null && e.prefab != null && e.key == key) return e;
            return null;
        }

        [ContextMenu("Bilinen anahtarlari ekle")]
        public void AddKnownKeys()
        {
            foreach (var k in KnownKeys)
                if (!entries.Exists(e => e.key == k)) entries.Add(new Entry { key = k });
        }
    }
}

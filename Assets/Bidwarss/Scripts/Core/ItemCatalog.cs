using System;
using UnityEngine;
using Bidwarss.Domain;

namespace Bidwarss
{
    // Rarity is only a label + a shortcut: "Loot Editörü" (Tools > Bidwars > Loot) turns the weight of a rarity tier
    // into the selectionWeight of every item of that tier. The weight that really counts is Entry.selectionWeight.
    public enum ItemRarity : byte { Common, Uncommon, Rare, Epic, Legendary, Mythic }

    [Serializable]
    public sealed class RarityTier
    {
        public string name;
        public Color color = Color.white;
        [Tooltip("Bu nadirlikteki eşyaların varsayılan şans ağırlığı (büyük = sık çıkar).")] public int weight = 100;
        [Tooltip("Bir tur içinde en fazla kaç istif (grup) çıkabilir.")] [Range(1, 30)] public int maxGroups = 2;
        [Tooltip("Varsayılan fiyat çarpanı (yeni eşya eklerken kullanılır).")] public float priceFactor = 1f;
    }

    [CreateAssetMenu(menuName = "Bidwarss/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string title;
            [Min(1)] public int baseDollars = 100;
            [Tooltip("Şans ağırlığı. Büyük sayı = sık çıkar. Gerçek yüzde = bu / (havuzdaki tüm ağırlıkların toplamı).")]
            [Min(1)] public int selectionWeight = 1;
            [Range(1,30)] public int maxGroups = 5;
            public Color color = Color.white;
            public GameObject visualPrefab;
            public SampleShape sampleShape;
            // ---- loot (kasa havuzu) ----
            public ItemRarity rarity = ItemRarity.Common;
            [Tooltip("Hangi kasalardan çıkabilir? Bit 0 Küçük, 1 Uzun (kullanılmıyor), 2 Uzun-yan, 3 Yatık, 4 Karton koli (bkz. CrateKinds).")]
            public int crateMask = CrateKinds.All;
            [Tooltip("Gerçek dünya boyutu, metre: x genişlik, y yükseklik, z derinlik. Hangi kasaya sığacağı bununla hesaplanır.")]
            public Vector3 realSize = new Vector3(.4f, .4f, .4f);
            [Tooltip("Yatık kasaya yan yatırılarak konabilir mi (dolap, saat, gitar...)?")]
            public bool lyingOK;
            public string category;
            [Tooltip("Karton kolide kaçarlı paket: büyük eşyalar 3'lü, küçükler 5'li kutulanır (yalnızca sıralama için).")]
            [Range(1, 10)] public int packSize = 5;
            [HideInInspector] public int sizeVersion;   // gerçek boyut modelden hesaplandı mı (Loot > 1 menüsü)
            // ---- Kasa Defteri (durumsal sınıflar) ----
            [Tooltip("Her turda depoda bu eşyadan kaç tane var (0 = yok). Küçük eşyalar 10-20, büyükler 3-10. Değiştirince Loot > 2 ile kasaları yeniden diz.")]
            [Range(0, 50)] public int roundCount;
            [Tooltip("Rastgele depoda bir turda bu eşyadan EN FAZLA kaç tane çıkar (0 = otomatik: boyuna ve kilosuna göre; 2 m'lik saatten 1-2, küçük şeylerden 20'ye kadar). Üst sınır 20.")]
            [Range(0, GameRules.HardMaxCount)] public int maxCount;
            [Tooltip("Kilo (kg). Kasa Defteri'nden gelir, eşya kartında gösterilir.")] public float kg;
            [Tooltip("Bu eşyanın kendi durum tablosu: 7 sınıf (Çok kötü -> Efsanevi), her biri çıkma şansı (%) ve fiyat aralığı ($). Boşsa depo geneli yüzde bantları kullanılır.")]
            public ConditionPrice[] conditionPrices;
            [Tooltip("Orta sınıf piyasa değeri ve koleksiyon puanı (0-10) - tablo bunlardan hesaplanır.")] public float collector;
            [HideInInspector] public string ledgerId, ledgerStamp;
            public bool HasConditionTable
            {
                get
                {
                    if (conditionPrices == null || conditionPrices.Length != 7) return false;
                    float sum = 0f; foreach (var c in conditionPrices) if (c != null) sum += Mathf.Max(0f, c.chance);
                    return sum > 0f;
                }
            }
        }
        [Serializable]
        public sealed class ConditionPrice
        {
            [Tooltip("Çıkma şansı, yüzde")] public float chance;
            [Tooltip("Fiyat en az ($)")] public int min = 1;
            [Tooltip("Fiyat en çok ($)")] public int max = 1;
        }
        // New shapes are appended so existing catalog assets keep their serialized values.
        public enum SampleShape { Box, Mirror, Table, Chair, Radio, Lamp, Vase, Clock, Teapot, Globe, Toolbox }
        public Entry[] entries;
        [Tooltip("Açık: her oyunda hem kutular hem içindekiler rastgele (eşya türü, adet 5/10/15/20, hangi kasalar dolu). Kapalı: eski sabit liste (Tur başına adet).")]
        public bool randomRound = true;
        public RarityTier[] rarityTiers = DefaultTiers();
        public ConditionRule[] conditions = GameRules.DefaultConditions();

        public static RarityTier[] DefaultTiers() => new[] {
            new RarityTier { name = "Yaygın",   color = new Color(.78f, .8f, .84f),  weight = 2000, maxGroups = 3, priceFactor = 1f },
            new RarityTier { name = "Seyrek",   color = new Color(.35f, .85f, .5f),  weight = 800,  maxGroups = 2, priceFactor = 2.5f },
            new RarityTier { name = "Nadir",    color = new Color(.3f, .6f, 1f),     weight = 250,  maxGroups = 1, priceFactor = 7f },
            new RarityTier { name = "Epik",     color = new Color(.7f, .4f, .95f),   weight = 50,   maxGroups = 1, priceFactor = 20f },
            new RarityTier { name = "Efsanevi", color = new Color(1f, .75f, .2f),    weight = 10,   maxGroups = 1, priceFactor = 70f },
            new RarityTier { name = "Mitik",    color = new Color(1f, .3f, .35f),    weight = 2,    maxGroups = 1, priceFactor = 200f },
        };

        public RarityTier Tier(ItemRarity rarity)
        {
            var tiers = rarityTiers != null && rarityTiers.Length > 0 ? rarityTiers : DefaultTiers();
            return tiers[Mathf.Clamp((int)rarity, 0, tiers.Length - 1)];
        }

        public GameRules CreateRules(int crates, int groups, int[] crateKinds = null, int packedKind = -1) => new GameRules {
            crateCount = crates, totalGroups = groups, conditions = conditions, crateKinds = crateKinds, packedKind = packedKind,
            randomRound = randomRound, items = Array.ConvertAll(entries, ToRule) };

        /// <summary>Kasa başına ayrılan eşya anahtarlarını (sahnede kayıtlı) katalog sırasına çevirir; -1 = boş.</summary>
        public int[] CrateItemsFromKeys(string[] keys, int crates)
        {
            if (keys == null || keys.Length != crates) return null;
            var result = new int[crates];
            for (int i = 0; i < crates; i++)
            {
                result[i] = -1;
                if (string.IsNullOrEmpty(keys[i])) continue;
                for (int k = 0; k < entries.Length; k++) if (entries[k].key == keys[i]) { result[i] = k; break; }
            }
            return result;
        }

        /// <summary>Rastgele depoda bu eşyadan bir turda en fazla kaç tane çıkabilir (elle verilen sınır ya da boy/kiloya göre otomatik).</summary>
        public static int LimitOf(Entry e) { return ToRule(e).EffectiveMaxCount(); }

        static ItemRule ToRule(Entry e)
        {
            var r = new ItemRule {
                key = e.key, baseDollars = e.baseDollars, selectionWeight = e.selectionWeight, maxGroups = e.maxGroups,
                crateMask = e.crateMask, packSize = Mathf.Clamp(e.packSize <= 0 ? 5 : e.packSize, 1, 10), roundCount = Mathf.Clamp(e.roundCount, 0, GameRules.MaxRoundCount),
                maxCount = Mathf.Clamp(e.maxCount, 0, GameRules.HardMaxCount),
                sizeCm = Mathf.RoundToInt(Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z)) * 100f), grams = Mathf.Max(0, Mathf.RoundToInt(e.kg * 1000f)) };
            if (e.HasConditionTable)
            {
                r.condWeights = new int[7]; r.condMin = new int[7]; r.condMax = new int[7];
                for (int c = 0; c < 7; c++)
                {
                    var p = e.conditionPrices[c] ?? new ConditionPrice();
                    // yuzde -> tam sayi agirlik (0,5% = 50)
                    r.condWeights[c] = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, p.chance) * 100f), 0, GameRules.MaxSelectionWeight);
                    r.condMin[c] = Mathf.Clamp(p.min, 1, 10000000);
                    r.condMax[c] = Mathf.Clamp(Mathf.Max(p.max, r.condMin[c]), r.condMin[c], 10000000);
                }
                int sum = 0; foreach (int w in r.condWeights) sum += w;
                if (sum <= 0) { r.condWeights = null; r.condMin = null; r.condMax = null; }
            }
            return r;
        }
    }
}

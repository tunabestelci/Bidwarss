using System;
using System.IO;
using System.Linq;
using Bidwarss.Domain;
using UnityEditor;
using UnityEngine;

namespace Bidwarss.Editor
{
    // Brings the item list from the "Kasa Defteri" wiki (a bidwarss-catalog JSON file) into every ItemCatalog asset.
    // Existing models, colours and shapes are kept for items whose key is unchanged.
    public static class CatalogImporter
    {
        public const string DefaultJson = "Assets/Bidwarss/Data/ItemCatalog.json";

        [MenuItem("Bidwarss/Katalog/Wiki dosyasından içe aktar (JSON)...")]
        public static void ImportFromDialog()
        {
            string path = EditorUtility.OpenFilePanel("Kasa Defteri katalog dosyası", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                int count = ImportFile(path, true);
                EditorUtility.DisplayDialog("Katalog içe aktarıldı", count + " katalog güncellendi.\nSıralama tablosu yeni kurallarla ayrı bir listeye geçer.\n\"Bidwarss/Print Leaderboard Rules Hash\" ile yeni özeti görebilirsin.", "Tamam");
            }
            catch (Exception ex) { EditorUtility.DisplayDialog("Katalog içe aktarılamadı", ex.Message, "Tamam"); }
        }

        [MenuItem("Bidwarss/Katalog/Varsayılan kataloğu yeniden uygula")]
        public static void ReapplyDefault() { ImportFile(DefaultJson, false); }

        // Also reachable from the command line: -executeMethod Bidwarss.Editor.CatalogImporter.ImportFromArguments -catalog file.json
        public static void ImportFromArguments()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-catalog");
            if (at < 0 || at + 1 >= args.Length) throw new InvalidOperationException("-catalog <dosya.json> gerekli.");
            ImportFile(args[at + 1], true);
        }

        public static CatalogFileData Read(string path)
        {
            var data = JsonUtility.FromJson<CatalogFileData>(File.ReadAllText(path));
            string problem = data == null ? "Dosya okunamadı." : data.Problem();
            if (problem != null) throw new InvalidOperationException(problem);
            return data;
        }

        public static int ImportFile(string path, bool copyToDefault)
        {
            var data = Read(path);
            if (!data.tiers.SequenceEqual(GameRules.ConditionNames))
                Debug.LogWarning("Wiki sınıf adları oyundakinden farklı: " + string.Join(", ", data.tiers) + ". Sıra aynı olduğu için sınıflar sırayla eşleşir.");
            int updated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog == null) continue;
                Apply(catalog, data); updated++;
            }
            if (copyToDefault && Path.GetFullPath(path) != Path.GetFullPath(DefaultJson))
            {
                File.Copy(path, DefaultJson, true);
                AssetDatabase.ImportAsset(DefaultJson);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Katalog uygulandı: " + data.items.Length + " eşya, " + updated + " katalog.");
            return updated;
        }

        public static void Apply(ItemCatalog catalog, CatalogFileData data)
        {
            var old = catalog.entries ?? new ItemCatalog.Entry[0];
            catalog.entries = data.items.Select(item =>
            {
                var previous = old.FirstOrDefault(e => e.key == item.key);
                var entry = new ItemCatalog.Entry
                {
                    key = item.key, title = string.IsNullOrWhiteSpace(item.title) ? item.key : item.title.Trim(),
                    baseDollars = Math.Max(1, item.baseValue), selectionWeight = Math.Max(1, item.selectionWeight), maxCount = item.maxCount,
                    kg = item.kg, heightCm = item.heightCm, widthCm = item.widthCm, depthCm = item.depthCm, collector = item.collector,
                    color = previous != null ? previous.color : ColorFor(item.key),
                    visualPrefab = previous != null ? previous.visualPrefab : null,
                    sampleShape = ShapeFor(item, previous)
                };
                if (item.classes != null && item.classes.Length == GameRules.ConditionCount)
                {
                    entry.classChance = item.classes.Select(c => c.chance).ToArray();
                    entry.classMin = item.classes.Select(c => c.priceMin).ToArray();
                    entry.classMax = item.classes.Select(c => c.priceMax).ToArray();
                }
                return entry;
            }).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static ItemCatalog.SampleShape ShapeFor(CatalogItemData item, ItemCatalog.Entry previous)
        {
            string text = ((item.shape ?? "") + " " + item.key + " " + item.title).ToLowerInvariant();
            string[][] rules =
            {
                new[] { "mirror", "ayna" }, new[] { "table", "masa" }, new[] { "chair", "sandalye", "tabure", "koltuk" },
                new[] { "radio", "radyo" }, new[] { "lamp", "lamba", "mumluk", "avize" }, new[] { "clock", "saat" },
                new[] { "vase", "vazo", "değirmen", "degirmen", "pipo", "çaydanlık", "caydanlik" }, new[] { "sword", "katana", "kılıç", "kilic", "bıçak" }
            };
            var shapes = new[] { ItemCatalog.SampleShape.Mirror, ItemCatalog.SampleShape.Table, ItemCatalog.SampleShape.Chair, ItemCatalog.SampleShape.Radio,
                ItemCatalog.SampleShape.Lamp, ItemCatalog.SampleShape.Clock, ItemCatalog.SampleShape.Vase, ItemCatalog.SampleShape.Sword };
            for (int i = 0; i < rules.Length; i++)
                foreach (string word in rules[i]) if (text.Contains(word)) return shapes[i];
            return previous != null ? previous.sampleShape : ItemCatalog.SampleShape.Box;
        }

        // Stable, pleasant colour per key so a new item is recognisable without artwork.
        static Color ColorFor(string key)
        {
            uint hash = 2166136261;
            foreach (char c in key) { hash ^= c; hash *= 16777619; }
            return Color.HSVToRGB((hash % 360) / 360f, .55f, .85f);
        }
    }
}

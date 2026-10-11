// Her esyanin boyutunu tek tek listeler ve supheli olanlari isaretler (Assets/_Game/Loot/Esya-Boyut-Raporu.txt).
// Loot > 1 her calistiginda yazilir; Tools > Bidwars > Loot > Esya boyut raporu ile de elle alinir.
using System.IO;
using System.Linq;
using System.Text;
using Bidwarss;
using Bidwarss.Domain;
using UnityEditor;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public static class LootReport
    {
        public const string Path = "Assets/_Game/Loot/Esya-Boyut-Raporu.txt";

        [MenuItem("Tools/Bidwars/Loot/Eşya boyut raporu")]
        public static void Menu()
        {
            var catalog = LootCatalogBuilder.FindCatalog(false);
            if (catalog == null) { Debug.LogError("[Loot] Katalog yok: önce Loot > 1 çalıştır."); return; }
            Debug.Log(Write(catalog));
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(Path));
        }

        static string Cm(float v) { return Mathf.RoundToInt(v * 100f).ToString(); }

        public static string Write(ItemCatalog catalog)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EŞYA BOYUT RAPORU  (ölçüler cm: genişlik x yükseklik x derinlik)");
            sb.AppendLine("Eşya | prefab | ölçü | kg | tur başı en çok | kasa | kaynak | uyarı");
            int warn = 0;
            var rules = catalog.CreateRules(4, 1, new[] { (int)CrateKind.Small, (int)CrateKind.TallSide, (int)CrateKind.Lying, (int)CrateKind.Cardboard }, (int)CrateKind.Cardboard);
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var e = catalog.entries[i];
                var def = LootItemTable.FindKey(e.key);
                var guess = def == null ? LootItemTable.GuessFor(e.key + " " + (e.visualPrefab != null ? e.visualPrefab.name : "") + " " + e.title) : null;
                string source = def != null ? "tablo" : guess != null ? "TAHMİN (adından)" : "BİLİNMİYOR";
                var issues = new System.Collections.Generic.List<string>();
                float longest = Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z));
                if (def == null && guess == null) issues.Add("tablo dışı, varsayılan ölçü: gerçek ölçüyü gir");
                if (Mathf.Approximately(e.realSize.x, .4f) && Mathf.Approximately(e.realSize.y, .4f) && Mathf.Approximately(e.realSize.z, .4f)) issues.Add("40 cm varsayılan küp");
                if (def != null)
                {
                    float want = Mathf.Max(def.size.x, Mathf.Max(def.size.y, def.size.z));
                    if (longest < want * .7f || longest > want * 1.3f) issues.Add("tablodan sapıyor (tablo " + Cm(want) + " cm)");
                }
                if (longest < .12f && e.category != "Para" && e.category != "Saat" && e.roundCount > 0) issues.Add("12 cm altı: doğru mu?");
                if (e.kg <= 0f) issues.Add("kilo yok");
                if (e.kg > 0f && longest > 1.2f && e.kg < 3f) issues.Add("büyük ama çok hafif");
                if (e.kg > 40f && longest < .3f) issues.Add("küçük ama çok ağır");
                if (e.roundCount > 0 && e.crateMask == 0) issues.Add("hiçbir kasaya sığmıyor");
                if (issues.Count > 0) warn++;
                sb.AppendLine((i + 1) + ". " + e.title + " | " + (e.visualPrefab != null ? e.visualPrefab.name : "-") + " | "
                    + Cm(e.realSize.x) + " x " + Cm(e.realSize.y) + " x " + Cm(e.realSize.z) + " | " + e.kg.ToString("0.##") + " | " + rules.items[i].EffectiveMaxCount()
                    + " | " + (e.crateMask != 0 ? LootPools.MaskText(e.crateMask) : "-") + " | " + source + " | " + string.Join("; ", issues.ToArray()));
            }
            sb.AppendLine();
            sb.AppendLine("Toplam " + catalog.entries.Length + " eşya, " + warn + " uyarı.");
            try { File.WriteAllText(Path, sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(Path); }
            catch (System.Exception ex) { return "[Loot] Boyut raporu yazılamadı: " + ex.Message; }
            return "Eşya boyut raporu: " + Path + " (" + warn + " uyarı) - her eşyanın ölçüsü orada tek tek yazıyor.";
        }
    }
}

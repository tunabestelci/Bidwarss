// Tools > Bidwars > Loot > Loot Editörü
// Esya havuzunu, sans agirliklarini ve hangi kasadan ne cikacagini tek pencereden ayarlarsin.
using System;
using System.Linq;
using Bidwarss;
using Bidwarss.Domain;
using UnityEditor;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public sealed class LootWindow : EditorWindow
    {
        ItemCatalog catalog;
        Vector2 scroll;
        int tab;
        int poolView = -1;          // -1 = tum esyalar, 0..3 = o kasanin havuzu
        string search = "";
        int simRounds = 300;
        string simReport = "";

        [MenuItem("Tools/Bidwars/Loot/Loot Editörü")]
        public static void Open() { var w = GetWindow<LootWindow>("Loot Editörü"); w.minSize = new Vector2(760, 420); w.Show(); }

        void OnEnable() { catalog = LootCatalogBuilder.FindCatalog(false); }

        void OnGUI()
        {
            catalog = (ItemCatalog)EditorGUILayout.ObjectField("Katalog", catalog, typeof(ItemCatalog), false);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Katalog bulunamadı. Önce Tools > Bidwars > Loot > 1) Eşya kataloğunu kur.", MessageType.Info);
                if (GUILayout.Button("Kataloğu şimdi kur")) { LootCatalogBuilder.Build(true); catalog = LootCatalogBuilder.FindCatalog(false); }
                return;
            }
            if (catalog.rarityTiers == null || catalog.rarityTiers.Length != 6) catalog.rarityTiers = ItemCatalog.DefaultTiers();
            tab = GUILayout.Toolbar(tab, new[] { "Eşyalar", "Kasa Defteri (durum)", "Nadirlik", "Simülasyon" });
            EditorGUI.BeginChangeCheck();
            if (tab == 0) ItemsTab();
            else if (tab == 1) LedgerTab();
            else if (tab == 2) TiersTab();
            else SimTab();
            if (EditorGUI.EndChangeCheck()) { EditorUtility.SetDirty(catalog); }
        }

        // ---------------------------------------------------------------- Esyalar
        void ItemsTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var names = new[] { "Tüm eşyalar" }.Concat(CrateKinds.Names).ToArray();
                poolView = EditorGUILayout.Popup("Havuz", poolView + 1, names) - 1;
                search = EditorGUILayout.TextField("Ara", search);
            }
            if (poolView >= 0)
            {
                double total = Pool(poolView).Sum(e => (double)e.selectionWeight);
                EditorGUILayout.HelpBox(CrateKinds.Names[poolView] + " havuzu: " + Pool(poolView).Count() + " eşya, toplam ağırlık " + total.ToString("0") +
                    ". Yüzde = ağırlık / toplam. Yüzdeyi doğrudan yazabilirsin; diğer eşyalar sabit kalır.", MessageType.None);
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Eşya", GUILayout.Width(170));
                GUILayout.Label("Nadirlik", GUILayout.Width(80));
                GUILayout.Label("Ağırlık", GUILayout.Width(70));
                GUILayout.Label("%", GUILayout.Width(60));
                GUILayout.Label("Fiyat $", GUILayout.Width(60));
                GUILayout.Label("Maks.", GUILayout.Width(40));
                GUILayout.Label(new GUIContent("Adet", "Her turda depoda bu eşyadan kaç tane var. Değiştirince Loot > 2 ile kasaları yeniden diz."), GUILayout.Width(40));
                GUILayout.Label(new GUIContent("Sınır", "Rastgele depoda bir turda EN FAZLA kaç tane çıkar. Sol kutu elle sınır (0 = otomatik: boy ve kiloya göre), sağdaki sayı gerçekte uygulanan sınır. 2 m'lik saat 1-2, küçük şeyler 20."), GUILayout.Width(70));
                GUILayout.Label(new GUIContent("/kasa", "Bir kasaya/koliye en fazla kaç tane girer (ölçüye göre otomatik)."), GUILayout.Width(40));
                GUILayout.Label(new GUIContent("Kasa K/U/Y/C", "K küçük kasa, U uzun kasa (yandan), Y yatık kasa, C karton koli"), GUILayout.Width(160));
                GUILayout.Label("Boyut (G×Y×D m)", GUILayout.Width(190));
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var shown = catalog.entries.Where(e => poolView < 0 || CrateKinds.Allows(e.crateMask, poolView))
                .Where(e => string.IsNullOrEmpty(search) || (e.title ?? "").IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || (e.key ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(e => e.selectionWeight).ToList();
            foreach (var e in shown) Row(e);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Havuzları boyuttan yeniden hesapla")) Recompute(false);
                if (GUILayout.Button("Kasa seçimlerimi koru, sadece sığmayanları kapat")) Recompute(true);
                if (GUILayout.Button("Kaydet")) { EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
            }
        }

        System.Collections.Generic.IEnumerable<ItemCatalog.Entry> Pool(int kind) { return catalog.entries.Where(e => CrateKinds.Allows(e.crateMask, kind)); }

        void Row(ItemCatalog.Entry e)
        {
            var tier = catalog.Tier(e.rarity);
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.color; GUI.color = tier.color;
                GUILayout.Label(new GUIContent(e.title, e.key + "  [" + e.category + "]"), GUILayout.Width(170));
                GUI.color = old;
                var r = (ItemRarity)EditorGUILayout.Popup((int)e.rarity, catalog.rarityTiers.Select(t => t.name).ToArray(), GUILayout.Width(80));
                if (r != e.rarity) { e.rarity = r; e.selectionWeight = catalog.Tier(r).weight; e.maxGroups = catalog.Tier(r).maxGroups; e.color = catalog.Tier(r).color; }
                e.selectionWeight = Mathf.Clamp(EditorGUILayout.IntField(e.selectionWeight, GUILayout.Width(70)), 1, GameRules.MaxSelectionWeight);

                int view = poolView;
                if (view < 0) view = FirstKind(e.crateMask);
                double others = view < 0 ? 0 : Pool(view).Where(x => x != e).Sum(x => (double)x.selectionWeight);
                double pct = view < 0 ? 0 : 100.0 * e.selectionWeight / (others + e.selectionWeight);
                EditorGUI.BeginChangeCheck();
                double np = EditorGUILayout.DoubleField(Math.Round(pct, 3), GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck() && view >= 0 && others > 0)
                {
                    np = Math.Max(0.0001, Math.Min(99.0, np));
                    e.selectionWeight = (int)Math.Max(1, Math.Min(GameRules.MaxSelectionWeight, Math.Round(np / (100.0 - np) * others)));
                }
                e.baseDollars = Mathf.Max(1, EditorGUILayout.IntField(e.baseDollars, GUILayout.Width(60)));
                e.maxGroups = Mathf.Clamp(EditorGUILayout.IntField(e.maxGroups, GUILayout.Width(40)), 1, 30);
                e.roundCount = Mathf.Clamp(EditorGUILayout.IntField(e.roundCount, GUILayout.Width(40)), 0, GameRules.MaxRoundCount);
                e.maxCount = Mathf.Clamp(EditorGUILayout.IntField(e.maxCount, GUILayout.Width(30)), 0, GameRules.HardMaxCount);
                GUILayout.Label("≤" + ItemCatalog.LimitOf(e), GUILayout.Width(36));
                GUILayout.Label(e.packSize.ToString(), GUILayout.Width(40));
                for (int k = 0; k < CrateKinds.Count; k++)
                {
                    if (k == (int)CrateKind.Tall) continue;   // ustten acilan uzun kasa artik kullanilmiyor
                    bool on = CrateKinds.Allows(e.crateMask, k);
                    bool fits = LootPools.Fits(e, (CrateKind)k);
                    var c = GUI.backgroundColor; if (on && !fits) GUI.backgroundColor = new Color(1f, .5f, .4f);
                    bool nv = GUILayout.Toggle(on, new GUIContent("K-UYC".Substring(k, 1), CrateKinds.Names[k] + (fits ? "" : "\n(boyutuna göre SIĞMAZ)")), "Button", GUILayout.Width(36));
                    GUI.backgroundColor = c;
                    if (nv != on) e.crateMask = nv ? e.crateMask | (1 << k) : e.crateMask & ~(1 << k);
                }
                e.realSize = EditorGUILayout.Vector3Field(GUIContent.none, e.realSize, GUILayout.Width(190));
                e.visualPrefab = (GameObject)EditorGUILayout.ObjectField(e.visualPrefab, typeof(GameObject), false, GUILayout.Width(110));
            }
        }

        static int FirstKind(int mask) { for (int k = 0; k < CrateKinds.Count; k++) if (CrateKinds.Allows(mask, k)) return k; return -1; }

        void Recompute(bool onlyClose)
        {
            Undo.RecordObject(catalog, "Havuzları hesapla");
            foreach (var e in catalog.entries)
            {
                int m = LootPools.Mask(e);
                e.crateMask = onlyClose ? e.crateMask & m : m;
            }
            EditorUtility.SetDirty(catalog);
        }

        // ---------------------------------------------------------------- Kasa Defteri
        string ledgerOpen;
        void LedgerTab()
        {
            EditorGUILayout.HelpBox("Her eşya kendi 7 durum sınıfıyla çıkar (Çok kötü → Efsanevi): sınıfın çıkma şansı (%) ve o sınıfın fiyat aralığı ($). " +
                "Değerler Kasa Defteri sayfasından gelir. Sayfada değişiklik yaptıysan sayfadan \"JSON indir\" ile kasa-defteri.json'u al, " +
                "Assets/_Game/Loot klasörüne at ve \"Defterden aktar\"a bas.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Defterden aktar")) { LootLedger.Apply(catalog, true, true); AssetDatabase.SaveAssets(); }
                if (GUILayout.Button("Kasa Defteri sayfasını aç")) LootLedger.OpenPage();
                search = EditorGUILayout.TextField("Ara", search);
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Eşya", GUILayout.Width(200));
                GUILayout.Label("Kaynak", GUILayout.Width(70));
                GUILayout.Label("Kilo", GUILayout.Width(55));
                GUILayout.Label("Orta $", GUILayout.Width(60));
                GUILayout.Label("Kol.", GUILayout.Width(40));
                for (int c = 0; c < 7; c++) GUILayout.Label(GameRules.ConditionNames[c], GUILayout.Width(92));
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var e in catalog.entries.Where(e => string.IsNullOrEmpty(search) || (e.title ?? "").IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool open = ledgerOpen == e.key;
                    if (GUILayout.Button((open ? "▾ " : "▸ ") + e.title, EditorStyles.label, GUILayout.Width(200))) ledgerOpen = open ? null : e.key;
                    GUILayout.Label(string.IsNullOrEmpty(e.ledgerStamp) ? "tahmini" : "defter", GUILayout.Width(70));
                    e.kg = Mathf.Max(0f, EditorGUILayout.FloatField(e.kg, GUILayout.Width(55)));
                    e.baseDollars = Mathf.Max(1, EditorGUILayout.IntField(e.baseDollars, GUILayout.Width(60)));
                    e.collector = Mathf.Clamp(EditorGUILayout.FloatField(e.collector, GUILayout.Width(40)), 0f, 10f);
                    for (int c = 0; c < 7; c++)
                    {
                        var p = e.HasConditionTable ? e.conditionPrices[c] : null;
                        GUILayout.Label(p == null ? "-" : p.chance.ToString("0.#") + "% $" + p.min + "-" + p.max, GUILayout.Width(92));
                    }
                }
                if (ledgerOpen != e.key) continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    if (!e.HasConditionTable) EditorGUILayout.HelpBox("Tablo yok: depo geneli yüzde bantları kullanılır.", MessageType.None);
                    else
                        for (int c = 0; c < 7; c++)
                        {
                            var p = e.conditionPrices[c] ?? (e.conditionPrices[c] = new ItemCatalog.ConditionPrice());
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(30);
                                var col = GUI.color; GUI.color = ItemVisual.ConditionColors[c];
                                GUILayout.Label(GameRules.ConditionNames[c], GUILayout.Width(90));
                                GUI.color = col;
                                GUILayout.Label("şans %", GUILayout.Width(45));
                                p.chance = Mathf.Max(0f, EditorGUILayout.FloatField(p.chance, GUILayout.Width(55)));
                                GUILayout.Label("fiyat $", GUILayout.Width(45));
                                p.min = Mathf.Max(1, EditorGUILayout.IntField(p.min, GUILayout.Width(70)));
                                GUILayout.Label("-", GUILayout.Width(10));
                                p.max = Mathf.Max(p.min, EditorGUILayout.IntField(p.max, GUILayout.Width(70)));
                            }
                        }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(30);
                        if (GUILayout.Button("Orta $ ve koleksiyon puanından yeniden üret", GUILayout.Width(300)))
                        { Undo.RecordObject(catalog, "Durum tablosu"); e.conditionPrices = LootLedger.Table(e.baseDollars, e.collector); e.ledgerStamp = null; }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Nadirlik
        void TiersTab()
        {
            EditorGUILayout.HelpBox("Her nadirliğin varsayılan ağırlığı. Aşağıdaki \"Uygula\" bu değeri o nadirlikteki TÜM eşyalara yazar " +
                "(tek tek ellediklerini ezer). Örnek: Yaygın 2000, Efsanevi 10 ise efsanevi, yaygının 200 katı daha az çıkar.", MessageType.Info);
            double sum = catalog.entries.Sum(e => (double)e.selectionWeight);
            for (int i = 0; i < catalog.rarityTiers.Length; i++)
            {
                var t = catalog.rarityTiers[i];
                int count = catalog.entries.Count(e => (int)e.rarity == i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    t.name = EditorGUILayout.TextField(t.name, GUILayout.Width(100));
                    t.color = EditorGUILayout.ColorField(t.color, GUILayout.Width(60));
                    GUILayout.Label("Ağırlık", GUILayout.Width(45));
                    t.weight = Mathf.Clamp(EditorGUILayout.IntField(t.weight, GUILayout.Width(70)), 1, GameRules.MaxSelectionWeight);
                    GUILayout.Label("Maks. grup", GUILayout.Width(70));
                    t.maxGroups = Mathf.Clamp(EditorGUILayout.IntField(t.maxGroups, GUILayout.Width(40)), 1, 30);
                    GUILayout.Label("Fiyat çarpanı", GUILayout.Width(80));
                    t.priceFactor = EditorGUILayout.FloatField(t.priceFactor, GUILayout.Width(50));
                    GUILayout.Label(count + " eşya", GUILayout.Width(60));
                    double share = catalog.entries.Where(e => (int)e.rarity == i).Sum(e => (double)e.selectionWeight);
                    GUILayout.Label((sum > 0 ? 100.0 * share / sum : 0).ToString("0.00") + " %", GUILayout.Width(70));
                    if (GUILayout.Button("Uygula", GUILayout.Width(60)))
                    {
                        Undo.RecordObject(catalog, "Nadirlik uygula");
                        foreach (var e in catalog.entries.Where(x => (int)x.rarity == i)) { e.selectionWeight = t.weight; e.maxGroups = t.maxGroups; e.color = t.color; }
                    }
                }
            }
            if (GUILayout.Button("Varsayılan nadirlik tablosuna dön")) catalog.rarityTiers = ItemCatalog.DefaultTiers();
        }

        // ---------------------------------------------------------------- Simulasyon
        void SimTab()
        {
            simRounds = EditorGUILayout.IntSlider("Tur sayısı", simRounds, 20, 3000);
            if (GUILayout.Button("Simüle et")) simReport = Simulate();
            EditorGUILayout.HelpBox("Gerçek RoundEngine'i farklı tohumlarla çalıştırır. \"Turda görülme\" bir eşyanın bir turda en az bir kez çıkma oranıdır.", MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(simReport, EditorStyles.textArea, GUILayout.ExpandHeight(true), GUILayout.MinHeight(300));
            EditorGUILayout.EndScrollView();
        }

        string Simulate()
        {
            var world = UnityEngine.Object.FindFirstObjectByType<WarehouseWorld>();
            int[] kinds = world != null && world.crateKinds != null && world.crateKinds.Length > 0
                ? world.crateKinds.Select(k => (int)k).ToArray()
                : LootSceneBuilder.DefaultLayout.Select(k => (int)k).ToArray();
            int groups = world != null ? world.totalGroups : 12;
            if (world != null && catalog.randomRound && world.slots != null && world.slots.Length > 0) groups = Mathf.Min(groups, world.slots.Length);
            var rules = catalog.CreateRules(kinds.Length, groups, kinds, Array.IndexOf(kinds, (int)CrateKind.Cardboard) >= 0 ? (int)CrateKind.Cardboard : -1);
            if (world != null) rules.crateItems = catalog.CrateItemsFromKeys(world.crateItemKeys, kinds.Length);
            var sb = new System.Text.StringBuilder();
            try { rules.Validate(); } catch (Exception ex) { return "Kural hatası: " + ex.Message; }
            int n = catalog.entries.Length;
            var seen = new int[n]; var perKind = new double[CrateKinds.Count]; var kindCrates = new int[CrateKinds.Count];
            var perItemKind = new double[n, CrateKinds.Count];
            double totalDollars = 0; int empty = 0;
            for (int s = 0; s < simRounds; s++)
            {
                var eng = new RoundEngine(rules, 1000 + s * 7919);
                var any = new bool[n];
                var cnt = new int[kinds.Length];
                foreach (var it in eng.Items) { any[it.kind] = true; cnt[it.crate]++; perItemKind[it.kind, kinds[it.crate]]++; totalDollars += it.dollars; }
                for (int i = 0; i < n; i++) if (any[i]) seen[i]++;
                for (int c = 0; c < kinds.Length; c++) { perKind[kinds[c]] += cnt[c]; kindCrates[kinds[c]]++; if (cnt[c] == 0) empty++; }
            }
            sb.AppendLine("Kasa sayısı: " + kinds.Length + "   |   " + (rules.RandomActive ? "RASTGELE DEPO, palet bütçesi: " : "grup sayısı: ") + groups);
            if (catalog.randomRound && !rules.RandomActive) sb.AppendLine("UYARI: Rastgele depo açık ama kasalara eşya atanmamış. Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir çalıştır.");
            sb.AppendLine("Tur başına ortalama toplam değer: $" + (totalDollars / simRounds).ToString("0") + "   |   boş kasa: " + empty);
            for (int k = 0; k < CrateKinds.Count; k++)
                if (kindCrates[k] > 0) sb.AppendLine(CrateKinds.Names[k] + ": kasa başına ortalama " + (perKind[k] / kindCrates[k]).ToString("0.0") + " eşya");
            sb.AppendLine();
            sb.AppendLine("Eşya                              Nadirlik   Turda görülme   (küçük / - / uzun-yan / yatık / koli toplam adet)");
            for (int i = 0; i < n; i++)
            {
                var e = catalog.entries[i];
                string line = (e.title + "                                  ").Substring(0, 34) + (catalog.Tier(e.rarity).name + "          ").Substring(0, 11) +
                    (100.0 * seen[i] / simRounds).ToString("0.0").PadLeft(6) + " %      ";
                for (int k = 0; k < CrateKinds.Count; k++) line += perItemKind[i, k].ToString("0").PadLeft(5);
                sb.AppendLine(line);
            }
            var never = Enumerable.Range(0, n).Where(i => seen[i] == 0).Select(i => catalog.entries[i].title).ToArray();
            if (never.Length > 0) sb.AppendLine("\nHİÇ ÇIKMAYANLAR: " + string.Join(", ", never) + "  (ağırlığı çok düşük ya da hiçbir kasaya atanmamış)");
            return sb.ToString();
        }
    }
}

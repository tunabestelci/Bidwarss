// Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir
// - Ahşap kasa prefab'larından oyun sürümlerini üretir (Assets/_Game/Loot/Prefabs, ölçek 3.8x, CrateStage ekli)
// - Her turda katalogdaki HER eşyadan "Turda adet" kadar var. Her eşya türüne KENDİ kasaları ayrılır
//   (ceil(adet / kasa başına) tane); her kasa içine gireceğe göre ölçülür (gerçek kasa modeli, oyundaki şekliyle),
//   kasalar 10 konteynerin 20 sırasına toplam boy eşit olacak şekilde, türler karışık ve aralarında boşlukla dizilir (LootLayout).
//   WarehouseWorld.crateItemKeys: hangi kasa hangi eşyanın (RoundEngine eşyaları kendi kasalarına koyar).
//   Her eşya türünün paleti 10'luk (son palet kalan kadar). Silinmiş palet referanslarını temizler, eksikse boş raflara ekler.
// - Kasaları zemine oturtur (raycast + sınır kutusu), uzun kasayı konteyner kapısına, yatık kasayı koridora baktırır.
// - Siyah şeritleri (tavan kirişi, yük rayları) gizler. WarehouseWorld.boxes / crateKinds / crateItemKeys'i doldurur.
using System.IO;
using System.Linq;
using Bidwars.Cardboard;
using Bidwars.Cardboard.EditorTools;
using Bidwars.Crates;
using Bidwars.Crates.EditorTools;
using Bidwarss;
using Bidwarss.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bidwars.Loot.EditorTools
{
    public static class LootSceneBuilder
    {
        public const string GamePrefabFolder = "Assets/_Game/Loot/Prefabs";
        public const string StageName = "Ahşap Kasa";

        public const int PerContainer = 12;
        public const float RowZ = 2.55f;                       // iki sira kasa: konteyner yerel z = -RowZ / +RowZ (ortada 4+ m koridor)
        public static readonly float[] ColumnX = { 4.2f, 9.6f, 15.0f, 20.4f, 25.8f, 31.2f };
        const float FloorFallback = 0.172f;                    // konteyner yerel zemin yuksekligi (raycast tutmazsa)

        // Konteyner basina 12 kasa (izgara: s = sutun*2 + sira): 5 kucuk, 2 uzun (yandan), 2 yatik ahsap kasa + 3 karton koli.
        // 10 konteyner: 50 kucuk (5 istif) + 20 uzun (2) + 20 yatik (2) + 30 koli (12 istif, kolide 3-5'li paket) = 21 istif.
        // Her kasa her tur icindeki esyaya gore boyutlanir. Konteynerler arasinda dizilis kayar / aynalanir (hepsi ayni gorunmesin).
        const CrateKind S_ = CrateKind.Small, T_ = CrateKind.TallSide, Y_ = CrateKind.Lying, C_ = CrateKind.Cardboard;
        static readonly CrateKind[] Pattern = { C_, S_,  T_, S_,  Y_, C_,  S_, Y_,  T_, S_,  C_, S_ };

        public static CrateKind[] KindsOf(int container)
        {
            var list = new CrateKind[PerContainer];
            int shift = (container * 2) % PerContainer;          // her konteynerde bir sutun kayar
            bool mirror = container % 2 == 1;                     // tek numaralilarda siralar yer degistirir
            for (int s = 0; s < PerContainer; s++)
            {
                int src = (s + shift) % PerContainer;
                if (mirror) src ^= 1;
                list[s] = Pattern[src];
            }
            return list;
        }

        // Eski (turda adet yok) mod icin kasa listesi; manifest modunda her esya turunun kendi kasalari (LootLayout).
        public static System.Collections.Generic.List<LootLayout.Box> PlanBoxes(int containers) { return PlanBoxes(LootCatalogBuilder.FindCatalog(false), containers); }

        public static System.Collections.Generic.List<LootLayout.Box> PlanBoxes(ItemCatalog catalog, int containers) { return PlanBoxes(catalog, containers, 0); }

        /// <summary>slots: sahnedeki palet sayisi (0 = bilinmiyor). Rastgele depoda ayrilan kasa havuzu hem konteynerlere hem de
        /// paletlere gore otomatik kucultulur: boylece her turda kasalarin cogu dolu olur (bos konteyner/bos raf kalmaz).</summary>
        public static System.Collections.Generic.List<LootLayout.Box> PlanBoxes(ItemCatalog catalog, int containers, int slots)
        {
            var boxes = LootLayout.Boxes(catalog);
            if (boxes != null)
            {
                if (catalog.randomRound)
                {
                    float scale = 1f;
                    var kindsProbe = new[] { (int)CrateKind.Small, (int)CrateKind.TallSide, (int)CrateKind.Lying, (int)CrateKind.Cardboard };
                    var probeRules = catalog.CreateRules(4, 1, kindsProbe, (int)CrateKind.Cardboard);
                    for (int step = 0; step < 80; step++, scale *= 0.94f)
                    {
                        boxes = LootLayout.Boxes(catalog, scale);
                        float need, have; have = containers * 2 * (LootLayout.RowEnd - LootLayout.RowStart);
                        LootLayout.StackPlan(boxes, have);
                        LootLayout.Totals(boxes, containers, out need, out have);
                        int stacked = boxes.Count(b => b.under != null);
                        bool fitsContainers = need <= have * 0.97f;
                        bool fitsPallets = slots <= 0 || LootLayout.PalletsOf(boxes, probeRules) <= Mathf.CeilToInt(slots * 1.1f);
                        LastPlanNote = "ayrilan kasa havuzu %" + Mathf.RoundToInt(scale * 100) + " (" + boxes.Count + " kasa, " + stacked + " tanesi baska kasanin ustunde, konteyner doluluk %" + Mathf.RoundToInt(need / Mathf.Max(1f, have) * 100f) + ", " + LootLayout.PalletsOf(boxes, probeRules) + " palet gerekir / sahnede " + slots + ")";
                        if (fitsContainers && fitsPallets) break;
                    }
                    if (scale < 0.999f) Debug.LogWarning("[Loot] Rastgele depo: " + LastPlanNote + ". Konteyner ya da palet sayısı arttıkça havuz büyür.");
                }
                return boxes;
            }
            boxes = new System.Collections.Generic.List<LootLayout.Box>();
            for (int c = 0; c < containers; c++)
                foreach (var k in KindsOf(c))
                {
                    var b = new LootLayout.Box { kind = k, cell = new Vector3(0.4f, 0.3f, 0.3f), count = k == CrateKind.Cardboard ? 3 : 1 };
                    LootLayout.Estimate(b);
                    boxes.Add(b);
                }
            return boxes;
        }

        public static string LastPlanNote = "";

        public static CrateKind[] DefaultLayoutFor(int containers)
        {
            var boxes = PlanBoxes(containers);
            LootLayout.Arrange(boxes, containers);
            return boxes.Select(b => b.kind).ToArray();
        }
        public static CrateKind[] DefaultLayout { get { return DefaultLayoutFor(10); } }

        public static string SourcePrefabName(CrateKind kind)
        {
            switch (kind)
            {
                case CrateKind.Small: return "Crate_Wide";
                case CrateKind.Tall: return "Crate_Tall";
                case CrateKind.TallSide: return "Crate_TallSide";
                default: return "Crate_TallLying";
            }
        }

        public static string GamePrefabPath(CrateKind kind) { return kind == CrateKind.Cardboard ? CardboardSetup.PrefabPath : GamePrefabFolder + "/LootCrate_" + kind + ".prefab"; }

        // kasanin one baktigi (esyalarin dustugu) mesafe, metre
        static float DropDistance(CrateKind kind)
        {
            switch (kind)
            {
                case CrateKind.TallSide: return 2.3f;
                case CrateKind.Lying: return 2.2f;
                default: return 1.45f;
            }
        }

        [MenuItem("Tools/Bidwars/Loot/2) Kasaları depoya yerleştir")]
        public static void PlaceMenu() { Place(true); }

        public static string Place(bool verbose)
        {
            string root = CrateOpeningSetup.FindRoot();
            if (root == null) return Fail("CrateOpening paketi yok (Assets/_Game/CrateOpening).");
            bool missing = CrateOpeningSetup.CrateNames.Any(n => AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/" + n + ".prefab") == null);
            if (missing && !CrateOpeningSetup.Run(true)) return Fail("Kasa prefab'ları kurulamadı (Console'a bak).");

            Directory.CreateDirectory(GamePrefabFolder);
            foreach (CrateKind kind in System.Enum.GetValues(typeof(CrateKind))) if (kind != CrateKind.Cardboard) MakeGamePrefab(root, kind);
            // katalog: yeni esyalar + kasa maskesi gocleri (koli esyalari). Ayarlarin korunur.
            LootCatalogBuilder.Build(true);
            if (!CardboardSetup.Run(false) || AssetDatabase.LoadAssetAtPath<GameObject>(CardboardSetup.PrefabPath) == null)
                return Fail("Karton koli prefab'ı kurulamadı (Tools > Bidwars > Karton > Kurulum, Console'a bak).");
            AssetDatabase.SaveAssets();

            WarehouseWorld world = FindWorld();
            if (world == null)
            {
                // depo sahnesi acik degil: build ayarlarindaki ilk sahneyi ac
                var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
                if (first != null && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(first.path, OpenSceneMode.Single);
                    world = FindWorld();
                }
            }
            if (world == null) return Fail("Sahnede WarehouseWorld ('Warehouse State') yok. Depo sahnesini aç ve tekrar dene.");
            if (world.crates == null || world.crates.Length == 0) return Fail("WarehouseWorld.crates (konteynerler) boş.");

            int containers = world.crates.Length;
            var catalog = world.catalog != null ? world.catalog : LootCatalogBuilder.FindCatalog(false);
            // Once paletler: rastgele depoda istenen palet sayisi (DepotMaxItems / 10) kadar raf paleti eklenir; kasa havuzu buna gore secilir.
            int newPallets = 0;
            if (catalog != null && catalog.randomRound && catalog.entries != null)
            {
                var kindsProbe = new[] { (int)CrateKind.Small, (int)CrateKind.TallSide, (int)CrateKind.Lying, (int)CrateKind.Cardboard };
                newPallets = EnsureSlots(world, LootLayout.RandomPalletBudget(catalog, kindsProbe));
            }
            int slotCount = world.slots != null ? world.slots.Length : 0;
            var plan = PlanBoxes(catalog, containers, slotCount);
            int placed = 0, sunk = 0;

            for (int c = 0; c < containers; c++)
            {
                Transform container = world.crates[c];
                if (container == null) continue;
                HideBlackBars(container);
                // eski kasalar
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    var ch = container.GetChild(i);
                    if (ch.name.StartsWith(StageName)) Object.DestroyImmediate(ch.gameObject);
                }
            }
            Physics.SyncTransforms();

            // her kasayi icindekine gore sekillendirip olc (gercek kasa modeliyle): siradaki boyu kesin olsun
            Transform probeHome = world.crates.FirstOrDefault(t => t != null);
            if (probeHome != null) Measure(plan, probeHome);
            float worstGap = LootLayout.Arrange(plan, containers);

            var boxes = new Transform[plan.Count];
            var kinds = new CrateKind[plan.Count];
            var keys = new string[plan.Count];
            var supports = new int[plan.Count];
            for (int i = 0; i < supports.Length; i++) supports[i] = -1;
            for (int pass = 0; pass < 2; pass++)
            for (int id = 0; id < plan.Count; id++)
            {
                var p = plan[id];
                if ((p.tier > 0 ? 1 : 0) != pass) continue;   // once yerdekiler, sonra ustte duranlar
                CrateKind kind = p.kind;
                kinds[id] = kind; keys[id] = p.key;
                Transform container = p.container >= 0 && p.container < containers ? world.crates[p.container] : null;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GamePrefabPath(kind));
                if (container == null || prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                go.name = StageName + " " + (id + 1).ToString("000") + (p.key != null ? " (" + p.key + ")" : "");
                go.transform.localPosition = new Vector3(p.x, 0f, p.row == 0 ? -RowZ : RowZ);
                var stage = go.GetComponent<CrateStage>();
                var cardboard = go.GetComponent<CardboardBox>();
                Orient(go, stage, kind, container, p.row, (((p.container * 7 + id * 13) % 7) - 3) * (LootLayout.Jitter / 3f));

                // zemin: konteynerin icinden asagi raycast, sonra kasanin en alt noktasini zemine oturt
                Physics.SyncTransforms();
                float floorY = FloorHeight(container, go.transform.position, go);
                int underId = p.under != null ? plan.IndexOf(p.under) : -1;
                if (underId >= 0 && boxes[underId] != null)
                {
                    var underStage = boxes[underId].GetComponent<CrateStage>();
                    floorY = BodyBounds(boxes[underId].gameObject, underStage).max.y;   // alttaki kasanin ustu
                }
                supports[id] = underId;
                Bounds b = BodyBounds(go, stage);
                float shift = floorY + 0.002f - b.min.y;
                if (Mathf.Abs(shift) > 0.001f) sunk++;
                go.transform.position += Vector3.up * shift;

                if (stage != null)
                {
                    stage.world = world; stage.crateIndex = id; stage.kind = kind;
                    EditorUtility.SetDirty(stage);
                }
                if (cardboard != null)
                {
                    cardboard.world = world; cardboard.crateIndex = id;
                    EditorUtility.SetDirty(cardboard);
                }

                var target = go.GetComponent<InteractionTarget>();
                if (target == null) target = go.AddComponent<InteractionTarget>();
                target.kind = TargetKind.Box; target.id = id;
                EditorUtility.SetDirty(target);

                boxes[id] = go.transform;
                placed++;
            }

            world.boxes = boxes;
            world.crateKinds = kinds;
            world.crateItemKeys = keys.Any(k => k != null) ? keys : null;
            world.boxSupport = supports;
            int parcels = 0; foreach (var k in kinds) if (k == CrateKind.Cardboard) parcels++;
            var probe = new GameRules { crateCount = kinds.Length, crateKinds = System.Array.ConvertAll(kinds, k => (int)k), packedKind = (int)CrateKind.Cardboard };
            int groups = Mathf.Max(1, (boxes.Length - parcels) / GameRules.StackSize + probe.PackedGroups);
            groups = Mathf.Min(groups, 30);
            var real = catalog != null ? catalog.CreateRules(kinds.Length, 1, System.Array.ConvertAll(kinds, k => (int)k), (int)CrateKind.Cardboard) : null;
            if (real != null && real.Manifest) groups = real.ManifestStacks;
            if (real != null && catalog.randomRound && real.Manifest) groups = LootLayout.RandomPalletBudget(catalog, System.Array.ConvertAll(kinds, k => (int)k));   // rastgele depo: bir turun palet butcesi   // her turden adet kadar: 10'luk paletler
            if (worstGap < LootLayout.MinGap)
            {
                float need, have; LootLayout.Totals(plan, containers, out need, out have);
                Debug.LogWarning("[Loot] Kasalar konteynerlere sığmıyor (kasalar arası " + worstGap.ToString("0.00") + " m; gereken " + need.ToString("0") + " m, olan " + have.ToString("0") + " m). Loot Editörü'nde büyük eşyaların turdaki adedini azalt.");
            }
            newPallets += EnsureSlots(world, groups);
            if (catalog != null && catalog.randomRound && real != null && real.Manifest) groups = Mathf.Max(1, world.slots.Length);   // rastgele depo: sahnedeki TUM paletler tur butcesi
            if (world.slots.Length < groups)
            {
                Debug.LogWarning("[Loot] Sahnede " + world.slots.Length + " palet var, " + groups + " gerekiyor; istif sayısı palete göre kısıldı.");
                groups = world.slots.Length;
            }
            if (world.totalGroups != groups)
            {
                Debug.Log("[Loot] Istif sayisi " + world.totalGroups + " -> " + groups + " yapildi (ahsap kasaya 1, koliye 3-5 esya)" + (newPallets > 0 ? ", " + newPallets + " yeni palet boş raflara eklendi." : "."));
                world.totalGroups = groups;
            }
            EditorUtility.SetDirty(world);
            var scene = world.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var perKind = new int[CrateKinds.Count]; foreach (var k in kinds) perKind[(int)k]++;
            string fitNote = "";
            if (catalog != null && catalog.randomRound && real != null && real.Manifest)
            {
                var rr = catalog.CreateRules(kinds.Length, 1, System.Array.ConvertAll(kinds, k => (int)k), (int)CrateKind.Cardboard);
                rr.crateItems = catalog.CrateItemsFromKeys(keys, kinds.Length);
                long possible = 0, palletsNeeded = 0; int types = 0;
                for (int t = 0; t < rr.items.Length; t++)
                {
                    int reserved = 0, kind = rr.ManifestKind(rr.items[t]);
                    if (rr.crateItems != null) for (int c = 0; c < rr.crateItems.Length; c++) if (rr.crateItems[c] == t && (int)kinds[c] == kind) reserved++;
                    if (reserved == 0 || !rr.items[t].InPool) continue;
                    types++;
                    int cap = Mathf.Min(rr.items[t].EffectiveMaxCount(), reserved * Mathf.Max(1, rr.items[t].packSize));
                    possible += cap;
                    palletsNeeded += (cap + rr.items[t].PalletCap() - 1) / rr.items[t].PalletCap();
                }
                long slotCount = world.slots.Length;
                long perRound = System.Math.Min(palletsNeeded <= slotCount ? possible : possible * slotCount / System.Math.Max(1, palletsNeeded), GameRules.DepotMaxItems);
                fitNote = "\nDEPO DOLULUĞU: hedef " + GameRules.DepotMinItems + "–" + GameRules.DepotMaxItems + " eşya/tur. Şu an bir turda en çok ~" + perRound + " eşya çıkabilir ("
                    + types + " tür, " + kinds.Length + " kasa, türlerin üst sınırları toplamı " + possible + ", bunun için " + palletsNeeded + " palet gerekir, sahnede " + slotCount + "). " + LastPlanNote + ".";
                if (perRound < GameRules.DepotMinItems)
                {
                    var reasons = new System.Collections.Generic.List<string>();
                    if (palletsNeeded > slotCount) reasons.Add("palet yetmiyor (" + palletsNeeded + " gerek, sahnede " + slotCount + ": depoya daha fazla raf/palet ekle)");
                    if (possible < GameRules.DepotMinItems) reasons.Add("kasa/tür yetmiyor (konteynerlere daha fazla kasa sığmalı ya da yeni eşya türleri eklenmeli)");
                    fitNote += " HEDEFİN ALTINDA: " + string.Join("; ", reasons.ToArray()) + ".";
                }
            }
            string msg = "[Loot] " + placed + " kasa " + containers + " konteynere yerleştirildi (küçük " + perKind[(int)CrateKind.Small] + ", uzun " + perKind[(int)CrateKind.TallSide] +
                ", yatık " + perKind[(int)CrateKind.Lying] + ", koli " + perKind[(int)CrateKind.Cardboard] + "; " + sunk + " tanesi zemine oturtuldu), " + world.totalGroups + " palet" +
                (real != null && real.Manifest ? (catalog.randomRound ? ", RASTGELE depo: her turda farklı eşya ve kasalar" : ", turda " + real.items.Sum(r => real.ManifestKind(r) >= 0 ? r.roundCount : 0) + " eşya") : "") + ". Sahne kaydedildi: " + scene.path + fitNote;
            if (verbose) Debug.Log(msg);
            return msg;
        }

        // Istif (palet) sayisi yetmiyorsa: ilk paleti (yerlesim + 10 hucre), etiketini (ISTIF n) ve E cubugunu
        // kullanilmayan en yakin raflara kopyalar. Raflarin hepsi ayni model oldugu icin yerel konum ayni kalir.
        // Once silinmis (bos) palet referanslarini atar ve kalanlari yeniden numaralar.
        static int EnsureSlots(WarehouseWorld world, int needed)
        {
            CompactSlots(world);
            var slots = world.slots ?? new Transform[0];
            if (slots.Length >= needed || slots.Length == 0 || slots[0].parent == null) return 0;
            Transform template = slots[0], shelf0 = template.parent;
            var used = new System.Collections.Generic.HashSet<Transform>(slots.Where(t => t.parent != null).Select(t => t.parent));
            Vector3 center = Vector3.zero; foreach (var u in used) center += u.position; center /= Mathf.Max(1, used.Count);
            var scene = world.gameObject.scene;
            var shelves = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(false))
                .Where(t => t.name.StartsWith("Raf_") && !used.Contains(t) && t.childCount > 0 && t.GetComponentInChildren<StackLayout>(true) == null)
                .OrderBy(t => (t.position - center).sqrMagnitude).ToList();
            Transform label = null, bar = null;
            foreach (Transform c in shelf0)
            {
                if (c.name.StartsWith("ISTIF") && c.GetComponent<TextMesh>() != null) label = c;
                else if (c.name.StartsWith("E - Istif")) bar = c;
            }
            var newSlots = slots.ToList();
            var labels = (world.stackLabels ?? new TextMesh[0]).ToList();
            while (labels.Count < newSlots.Count) labels.Add(null);
            int made = 0;
            for (int i = slots.Length; i < needed && made < shelves.Count; i++, made++)
            {
                var shelf = shelves[made];
                // rafta onceki calistirmadan kalmis yetim etiket / cubuk varsa temizle
                for (int k = shelf.childCount - 1; k >= 0; k--)
                {
                    var ch = shelf.GetChild(k);
                    if ((ch.name.StartsWith("ISTIF") && ch.GetComponent<TextMesh>() != null) || ch.name.StartsWith("E - Istif")) Object.DestroyImmediate(ch.gameObject);
                }
                var go = Object.Instantiate(template.gameObject, shelf);
                go.name = "Bidwarss stack " + i;
                go.transform.localPosition = template.localPosition; go.transform.localRotation = template.localRotation; go.transform.localScale = template.localScale;
                go.SetActive(true);
                var it = go.GetComponent<InteractionTarget>(); if (it != null) { it.kind = TargetKind.Slot; it.id = i; }
                if (bar != null) Copy(bar, shelf, bar.name);
                TextMesh tm = null;
                if (label != null) { var l = Copy(label, shelf, "ISTIF " + (i + 1)); tm = l.GetComponent<TextMesh>(); tm.text = "ISTIF " + (i + 1); }
                newSlots.Add(go.transform); labels.Add(tm);
            }
            world.slots = newSlots.ToArray();
            world.stackLabels = labels.ToArray();
            return made;
        }

        // Silinmis palet referanslarini (ve etiketlerini) at, kalanlari 0..n-1 diye yeniden numarala.
        static void CompactSlots(WarehouseWorld world)
        {
            var slots = world.slots ?? new Transform[0];
            var labels = world.stackLabels ?? new TextMesh[0];
            var s = new System.Collections.Generic.List<Transform>(); var l = new System.Collections.Generic.List<TextMesh>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                s.Add(slots[i]); l.Add(i < labels.Length ? labels[i] : null);
            }
            if (s.Count != slots.Length) Debug.Log("[Loot] " + (slots.Length - s.Count) + " silinmiş palet referansı temizlendi, eksik paletler yeniden ekleniyor.");
            for (int i = 0; i < s.Count; i++)
            {
                s[i].name = "Bidwarss stack " + i;
                var it = s[i].GetComponent<InteractionTarget>(); if (it != null && it.kind == TargetKind.Slot) { it.id = i; EditorUtility.SetDirty(it); }
                if (l[i] != null) { l[i].text = "ISTIF " + (i + 1); l[i].gameObject.name = "ISTIF " + (i + 1); EditorUtility.SetDirty(l[i]); }
            }
            world.slots = s.ToArray();
            world.stackLabels = l.ToArray();
        }

        static GameObject Copy(Transform src, Transform parent, string name)
        {
            var go = Object.Instantiate(src.gameObject, parent);
            go.name = name;
            go.transform.localPosition = src.localPosition; go.transform.localRotation = src.localRotation; go.transform.localScale = src.localScale;
            return go;
        }

        // Kasanin yonu: acilan yuzun normali istenen yone baksin. Kucuk kasa ve koli ustten acilir, hafif egik durur.
        static void Orient(GameObject go, CrateStage stage, CrateKind kind, Transform container, int row, float jitter)
        {
            go.transform.localRotation = Quaternion.identity;
            if (kind == CrateKind.Small || kind == CrateKind.Cardboard || stage == null)
            {
                go.transform.rotation = Quaternion.AngleAxis(jitter, Vector3.up) * go.transform.rotation;
                return;
            }
            Vector3 toCenter = container.TransformDirection(new Vector3(0f, 0f, row == 0 ? 1f : -1f));
            Vector3 toDoor = container.TransformDirection(Vector3.left);          // yerel -X = konteyner kapisi
            Vector3 wanted = kind == CrateKind.TallSide ? toDoor : toCenter;
            Vector3 n = stage.crate != null ? stage.crate.OpeningNormal : Vector3.forward;
            n.y = 0f;
            if (n.sqrMagnitude > 1e-4f)
            {
                float yaw = Vector3.SignedAngle(n.normalized, wanted, Vector3.up);
                go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;
            }
        }

        // Ahsap kasalar: gercek kasa modeli, icine girecek esyaya gore (CrateStage.ChooseShape, oyundakiyle ayni) sekillenip
        // her iki sira yonunde olculur. Koliler: CratePacking.ParcelSize zaten dis olcu (LootLayout.Estimate).
        static void Measure(System.Collections.Generic.List<LootLayout.Box> plan, Transform container)
        {
            var cache = new System.Collections.Generic.Dictionary<string, float[]>();
            foreach (CrateKind kind in System.Enum.GetValues(typeof(CrateKind)))
            {
                if (kind == CrateKind.Cardboard || !plan.Any(b => b.kind == kind)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GamePrefabPath(kind));
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                try
                {
                    go.name = "olcum";
                    var stage = go.GetComponent<CrateStage>();
                    if (stage == null) continue;
                    Vector3 baseScale = go.transform.localScale;
                    foreach (var b in plan)
                    {
                        if (b.kind != kind) continue;
                        string key = b.cell.x.ToString("0.000") + "/" + b.cell.y.ToString("0.000") + "/" + b.cell.z.ToString("0.000") + "/" + b.count;
                        float[] m;
                        if (!cache.TryGetValue(kind + key, out m))
                        {
                            var cells = new Vector3[Mathf.Max(1, b.count)];
                            for (int i = 0; i < cells.Length; i++) cells[i] = b.cell;
                            Vector3 shape;
                            stage.ChooseShape(cells, 0, out shape);
                            go.transform.localScale = Vector3.Scale(baseScale, shape);
                            m = new float[4];
                            for (int row = 0; row < 2; row++)
                            {
                                go.transform.localPosition = new Vector3(0f, 0f, row == 0 ? -RowZ : RowZ);
                                Orient(go, stage, kind, container, row, 0f);
                                float lo, hi, across;
                                if (!Extent(go, container, out lo, out hi, out across)) { m = null; break; }
                                if (kind == CrateKind.Small)
                                {
                                    // hafif egik durur: kosesi kadar pay
                                    float pad = across * 0.5f * Mathf.Sin(LootLayout.Jitter * Mathf.Deg2Rad);
                                    lo -= pad; hi += pad;
                                }
                                m[row * 2] = lo - 0.01f; m[row * 2 + 1] = hi + 0.01f;
                            }
                            go.transform.localScale = baseScale;
                            cache[kind + key] = m;
                        }
                        if (m == null) continue;
                        b.lo[0] = m[0]; b.hi[0] = m[1]; b.lo[1] = m[2]; b.hi[1] = m[3];
                    }
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        // Kasanin konteyner yerel x'inde pivot'a gore kapladigi aralik (lo..hi) ve z'deki genisligi.
        static bool Extent(GameObject go, Transform container, out float lo, out float hi, out float across)
        {
            lo = float.MaxValue; hi = float.MinValue; across = 0f;
            float zMin = float.MaxValue, zMax = float.MinValue;
            float pivot = container.InverseTransformPoint(go.transform.position).x;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                Bounds lb;
                if (r is SkinnedMeshRenderer) lb = ((SkinnedMeshRenderer)r).localBounds;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; lb = mf.sharedMesh.bounds; }
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 q = container.InverseTransformPoint(r.transform.TransformPoint(corner));
                    lo = Mathf.Min(lo, q.x - pivot); hi = Mathf.Max(hi, q.x - pivot);
                    zMin = Mathf.Min(zMin, q.z); zMax = Mathf.Max(zMax, q.z);
                    any = true;
                }
            }
            if (!any) return false;
            across = zMax - zMin;
            return hi - lo > 0.05f;
        }

        // konteyner zemini: kasanin x/z'sinde yukaridan asagi, kasanin kendi collider'lari haric en yuksek yatay yuzey
        static float FloorHeight(Transform container, Vector3 at, GameObject self)
        {
            Vector3 origin = container.TransformPoint(container.InverseTransformPoint(at) + new Vector3(0f, 3f, 0f));
            origin.x = at.x; origin.z = at.z;
            float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(self.transform)) continue;
                if (h.collider.GetComponentInParent<CrateStage>() != null) continue;
                if (h.normal.y < 0.7f) continue;
                if (!h.collider.transform.IsChildOf(container)) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return float.IsNegativeInfinity(best) ? container.TransformPoint(new Vector3(0f, FloorFallback, 0f)).y : best;
        }

        // kasanin govdesinin dunya sinirlari (kapak, parcalar dahil degil: sadece asil kutu)
        static Bounds BodyBounds(GameObject go, CrateStage stage)
        {
            Transform body = stage != null && stage.crate != null && stage.crate.body != null ? stage.crate.body : go.transform;
            var rs = body.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // siyah seritler: tavan kirisi, yuk rayi, baglama rayi
        static void HideBlackBars(Transform container)
        {
            foreach (var t in container.GetComponentsInChildren<Transform>(true))
            {
                if (t == container) continue;
                string n = t.name;
                if (n.StartsWith("Ic_Tavan_Kirisi") || n.StartsWith("Yuk_Rayi") || n.StartsWith("Baglama_Rayi"))
                {
                    if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                }
            }
        }

        static string Fail(string msg) { Debug.LogError("[Loot] " + msg); return msg; }

        public static WarehouseWorld FindWorld()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var sc = SceneManager.GetSceneAt(s);
                if (!sc.isLoaded) continue;
                foreach (var r in sc.GetRootGameObjects())
                {
                    var w = r.GetComponentInChildren<WarehouseWorld>(true);
                    if (w != null) return w;
                }
            }
            return null;
        }

        static void MakeGamePrefab(string root, CrateKind kind)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/" + SourcePrefabName(kind) + ".prefab");
            if (src == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = "LootCrate_" + kind;
            inst.transform.localScale = Vector3.one * LootPools.CrateScale;
            var stage = inst.GetComponent<CrateStage>();
            if (stage == null) stage = inst.AddComponent<CrateStage>();
            stage.kind = kind;
            stage.crate = inst.GetComponent<Bidwars.Crates.CrateOpener>();
            // kasa icindekine gore serbestce buyur/kuculur; vidalar ve kapak hizli
            stage.shapeLimits = new Vector2(0.12f, 5f);
            stage.screwSeconds = 0.45f;
            stage.animationSpeed = 2.4f;
            PrefabUtility.SaveAsPrefabAsset(inst, GamePrefabPath(kind));
            Object.DestroyImmediate(inst);
        }
    }
}

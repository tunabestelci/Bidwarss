// Tools > Bidwars > Loot > 1) Eşya kataloğunu kur
// Assets/_Game/Items/Prefabs altindaki tum prefab'lari ItemCatalog'a esya olarak ekler (zaten ekliyse dokunmaz:
// senin yaptigin ayarlar korunur), boyutlarina gore hangi kasadan cikabilecegini hesaplar.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bidwarss;
using Bidwarss.Domain;
using UnityEditor;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public static class LootCatalogBuilder
    {
        public const string PrefabRoot = "Assets/_Game/Items/Prefabs";
        const string BackupFolder = "Assets/_Game/Loot/Backup";

        public static ItemCatalog FindCatalog(bool create)
        {
            string best = null;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemCatalog"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("/Backup/")) continue;
                if (best == null || p.Contains("GeneratedV2")) best = p;
            }
            if (best != null) return AssetDatabase.LoadAssetAtPath<ItemCatalog>(best);
            if (!create) return null;
            Directory.CreateDirectory("Assets/_Game/Loot");
            var cat = ScriptableObject.CreateInstance<ItemCatalog>();
            cat.entries = new ItemCatalog.Entry[0];
            AssetDatabase.CreateAsset(cat, "Assets/_Game/Loot/ItemCatalog.asset");
            return cat;
        }

        [MenuItem("Tools/Bidwars/Loot/1) Eşya kataloğunu kur (prefab'lardan)")]
        public static void BuildMenu() { Build(true); }

        public static string Build(bool verbose)
        {
            var catalog = FindCatalog(true);
            string path = AssetDatabase.GetAssetPath(catalog);

            // yedek (yalnizca bir kez)
            Directory.CreateDirectory(BackupFolder);
            string backup = BackupFolder + "/ItemCatalog_yedek.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemCatalog>(backup) == null) AssetDatabase.CopyAsset(path, backup);

            FitNotes.Length = 0;
            string fixes = FixItemPrefabs();
            Undo.RecordObject(catalog, "Eşya kataloğunu kur");
            if (catalog.rarityTiers == null || catalog.rarityTiers.Length != 6) catalog.rarityTiers = ItemCatalog.DefaultTiers();
            var list = new List<ItemCatalog.Entry>(catalog.entries ?? new ItemCatalog.Entry[0]);

            // prefab'i olmayan eski ornek (procedural) esyalar: gercek esyalarla degistirilir
            var sampleKeys = new HashSet<string> { "mirror", "table", "chair", "radio", "lamp", "vase", "clock", "teapot", "globe", "toolbox" };
            int removedSamples = list.RemoveAll(e => e.visualPrefab == null && sampleKeys.Contains(e.key));
            // prefab'i silinmis esyalar (ornek: yeniden import edilen telsiz) katalogdan cikar; yeni prefab'i asagida tekrar eklenir
            var gone = list.Where(e => e.visualPrefab == null).Select(e => e.title).ToList();
            list.RemoveAll(e => e.visualPrefab == null);

            var seen = new HashSet<string>(list.Select(e => e.key));
            var seenPrefabs = new HashSet<GameObject>(list.Where(e => e.visualPrefab != null).Select(e => e.visualPrefab));
            int added = 0, unknown = 0;
            var log = new System.Text.StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null || seenPrefabs.Contains(prefab)) continue;
                string rel = p.Substring(PrefabRoot.Length + 1);
                rel = rel.Substring(0, rel.Length - ".prefab".Length);
                var def = LootItemTable.Find(rel);
                ItemCatalog.Entry e;
                if (def != null) e = FromDef(catalog, def, prefab);
                else
                {
                    unknown++;
                    e = Generic(catalog, rel, prefab);
                    log.AppendLine("  ? tablo disi prefab, varsayilan degerlerle eklendi: " + rel + " (Loot Editörü'nde boyutunu/nadirligini gir)");
                }
                string key = e.key; int n = 2;
                while (seen.Contains(e.key)) e.key = key + "_" + (n++);
                seen.Add(e.key);
                e.crateMask = def != null ? def.mask : LootPools.Mask(e);
                if (e.crateMask == 0 && (def == null || def.mask != 0)) log.AppendLine("  ! hicbir kasaya sigmiyor: " + e.title + " (boyut: " + e.realSize + ") - boyutu kucult ya da kasa tipini degistir");
                list.Add(e);
                added++;
            }
            // eski surum: ustten acilan uzun kasa (bit 1) artik yok -> yandan acilan uzun kasaya (bit 2) cevir
            foreach (var e in list)
                if ((e.crateMask & 2) != 0) e.crateMask = (e.crateMask & ~2) | 4;
            // v5: pipo, tabancalar, biber degirmeni, samdan artik karton koliden cikar (bir kez tasinir; sonra Loot Editörü'nde serbestsin)
            int cardboardBit = 1 << (int)CrateKind.Cardboard;
            foreach (var e in list)
                if (LootPools.IsCardboardItem(e) && (e.crateMask & cardboardBit) == 0) e.crateMask = cardboardBit;
            // v4 (Kasa Defteri): ad, nadirlik, kasa turu, paket, kilo, gercek boyut tablodan (olculer defterden).
            // Bir kez yapilir (sizeVersion); sonra Loot Editörü'ndeki ayarlarin korunur. Fiyat/durum tablosu defterden gelir.
            var report = new System.Text.StringBuilder();
            foreach (var e in list)
            {
                var def = DefOf(e);
                if (def == null || e.sizeVersion >= SizeVersion) continue;
                Vector3 size = FitSize(e.visualPrefab, def.size, !def.byLength);
                report.AppendLine("  " + e.title + " -> " + def.title + ": " + Fmt(size) + " cm, " + def.kg + " kg, " + LootPools.MaskText(def.mask));
                var tier = catalog.Tier(def.rarity);
                e.title = def.title; e.category = def.category; e.rarity = def.rarity;
                e.selectionWeight = tier.weight; e.maxGroups = tier.maxGroups; e.color = tier.color;
                e.baseDollars = def.dollars; e.kg = def.kg; e.collector = def.collector;
                e.realSize = size;
                e.crateMask = def.mask;
                e.lyingOK = def.lyingOK;
                e.packSize = def.pack;
                e.ledgerId = def.ledger; e.ledgerStamp = null; e.conditionPrices = null;   // defter / formul yeniden doldurur
                e.roundCount = def.count;
                if (e.maxCount >= 20) e.maxCount = 0;   // eski surum / defter 20 yazmisti: 0 = boy ve kiloya gore otomatik
                e.sizeVersion = SizeVersion;
            }
            // tabloda olmayan (yeni atilan) esyalar: her turda boyuna gore 3-10 tane
            foreach (var e in list)
            {
                if (DefOf(e) != null || e.sizeVersion >= SizeVersion) continue;
                if (!ApplyGuess(catalog, e, report))
                {
                    float m = Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z));
                    e.roundCount = m < 0.35f ? 10 : m < 1f ? 5 : 3;
                    report.AppendLine("  ? ÖLÇÜSÜ BİLİNMİYOR: " + (e.visualPrefab != null ? e.visualPrefab.name : e.key) + " = " + Fmt(e.realSize) + " cm (varsayılan). LootItemTable'a satır ekle ya da Loot Editörü'nde ölçüsünü gir.");
                }
                if (e.maxCount >= 20) e.maxCount = 0;
                e.sizeVersion = SizeVersion;
            }
            if (fixes.Length > 0) log.AppendLine("Model düzeltmeleri:\n" + fixes);
            if (report.Length > 0) log.AppendLine("Tablodan güncellenen eşyalar (cm, G x Y x D):\n" + report);
            if (gone.Count > 0) log.AppendLine("  prefab'ı silinmiş, katalogdan çıkarıldı: " + string.Join(", ", gone.ToArray()));

            // sabit, okunur sira: kucukten buyuge
            list = list.OrderBy(e => Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z))).ThenBy(e => e.title, StringComparer.CurrentCulture).ToList();
            catalog.entries = list.ToArray();
            // Kasa Defteri: her esyanin 7 durum sinifi (sans + fiyat), kilo, olcu
            string ledger = LootLedger.Apply(catalog, false, false);
            log.AppendLine(ledger.Replace("[Loot] ", ""));
            SanitizeSizes(catalog, log);
            // kasa basina adet: tablodaki ust sinir, gercek olcuye gore sigdigi kadar (her calismada yeniden)
            int totalItems = 0;
            foreach (var e in catalog.entries)
            {
                int kind = LootPools.KindOf(e.crateMask);
                if (kind < 0) continue;
                var def = DefOf(e);
                int want = def != null ? def.pack : (kind == (int)CrateKind.Cardboard ? 5 : 3);
                if (kind == (int)CrateKind.Cardboard) want = 10;   // koli: koli boyuna sigdigi kadar (MaxPerCrate sinirlar)
                if (kind == (int)CrateKind.Small) want = SmallCratePack(e);   // ahsap kucuk kasa: kucuk esyadan birkac tane (3500-4500 esya icin)
                e.packSize = Mathf.Clamp(Mathf.Min(want, LootPools.MaxPerCrate(e, (CrateKind)kind)), 1, 10);
                totalItems += e.roundCount;
            }
            if (FitNotes.Length > 0) log.AppendLine(FitNotes.ToString());
            log.AppendLine(catalog.randomRound ? "Rastgele depo açık: her oyunda eşya türleri, adetler (5/10/15/20, boy ve kiloya göre sınırlı) ve dolu kasalar yeniden çekilir (" + catalog.entries.Count(x => x.roundCount > 0 && x.crateMask != 0) + " tür havuzda)." : "Her turda: " + totalItems + " eşya (" + catalog.entries.Count(x => x.roundCount > 0 && x.crateMask != 0) + " tür).");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            log.AppendLine(LootReport.Write(catalog));
            string msg = "[Loot] Katalog: " + catalog.entries.Length + " eşya (" + added + " yeni" + (removedSamples > 0 ? ", " + removedSamples + " eski örnek eşya çıkarıldı" : "") + (unknown > 0 ? ", " + unknown + " tablo dışı" : "") + "). Yedek: " + backup + "\n" + log;
            if (verbose) Debug.Log(msg);
            return msg;
        }

        const int SizeVersion = 11;

        // Kucuk ahsap kasada bir kasaya sigan adet: kucuk esya 10, orta 6, buyukce 3, boy boyu 1; kasa 80 kg'dan agir olmasin.
        static int SmallCratePack(ItemCatalog.Entry e)
        {
            float longest = Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z));
            int n = longest <= .45f ? 10 : longest <= .7f ? 6 : longest <= 1f ? 3 : 1;
            if (e.kg > 0f) n = Mathf.Min(n, Mathf.Max(1, Mathf.FloorToInt(80f / e.kg)));
            return Mathf.Max(1, n);
        }

        // Tablo satiri: once anahtar, olmazsa prefab yolu (anahtari degismis / tekrarlanmis eski girdiler de tablodan beslenir).
        static LootItemTable.Def DefOf(ItemCatalog.Entry e)
        {
            var def = LootItemTable.FindKey(e.key);
            if (def != null || e.visualPrefab == null) return def;
            string p = AssetDatabase.GetAssetPath(e.visualPrefab);
            if (string.IsNullOrEmpty(p) || !p.StartsWith(PrefabRoot + "/")) return null;
            p = p.Substring(PrefabRoot.Length + 1);
            if (p.EndsWith(".prefab")) p = p.Substring(0, p.Length - ".prefab".Length);
            return LootItemTable.Find(p);
        }

        // Son kontrol (her kurulumda): tablodaki gercek olcu esastir. Defter ya da model olcumu tablodan %30'dan fazla saparsa
        // (oyuncak motor, 10 cm atari gibi) tablodaki olcu kullanilir. Her esya tek tek loga yazilir.
        static void SanitizeSizes(ItemCatalog catalog, System.Text.StringBuilder log)
        {
            var fixedList = new System.Text.StringBuilder();
            foreach (var e in catalog.entries)
            {
                var def = DefOf(e);
                if (def == null) continue;
                float want = Mathf.Max(def.size.x, Mathf.Max(def.size.y, def.size.z));
                float have = Mathf.Max(e.realSize.x, Mathf.Max(e.realSize.y, e.realSize.z));
                float ratio = have / Mathf.Max(.001f, want);
                bool heightOff = def.byLength == false && Mathf.Abs(e.realSize.y / Mathf.Max(.001f, def.size.y) - 1f) > .3f;
                if (ratio < .7f || ratio > 1.3f || heightOff)
                {
                    fixedList.AppendLine("  " + e.title + ": " + Fmt(e.realSize) + " -> " + Fmt(def.size) + " cm (tablodaki gerçek ölçü)");
                    e.realSize = def.size;
                }
            }
            if (fixedList.Length > 0) log.AppendLine("Ölçüsü tabloya çekilen eşyalar (cm, G x Y x D):\n" + fixedList);
        }

        static string Fmt(Vector3 v) { return Mathf.RoundToInt(v.x * 100f) + "x" + Mathf.RoundToInt(v.y * 100f) + "x" + Mathf.RoundToInt(v.z * 100f); }

        // Modelin oranlari korunarak gercek olcuye getirilir. byHeight: model YUKSEKLIGI gercek boya (masa, dolap, buzdolabi);
        // degilse modelin en uzun kenari gercek en uzun kenara (kilic, gitar, ayna, teker).
        // Sonuc HER ZAMAN modelle ayni oranda: ItemVisual modeli tek bir carpanla (boy) olcekler, kasa da bu olcuye gore
        // boyutlanir. Oran tutmazsa (defter olculeri modelle uyusmuyor) yine modelin orani korunur, en uzun kenar esitlenir
        // ve not dusulur; eskiden defter olcusu aynen alinirdi, model o olcuye sigmayip kasadan tasiyordu (Japon kilici).
        public static Vector3 FitSize(GameObject prefab, Vector3 real, bool byHeight)
        {
            Vector3 measured = Measure(prefab);
            if (measured.x <= 1e-4f || measured.y <= 1e-4f || measured.z <= 1e-4f) return real;
            float longReal = Mathf.Max(real.x, Mathf.Max(real.y, real.z)), longModel = Mathf.Max(measured.x, Mathf.Max(measured.y, measured.z));
            float k = byHeight && real.y > 1e-3f ? real.y / measured.y : longReal / longModel;
            if (k <= 0f || float.IsNaN(k) || float.IsInfinity(k)) return real;
            Vector3 m = measured * k;
            float[] b = { real.x, real.y, real.z }, c = { m.x, m.y, m.z };
            Array.Sort(b); Array.Sort(c);
            for (int i = 1; i < 3; i++)
            {
                float r = c[i] / Mathf.Max(0.005f, b[i]);
                if (r > 2.5f || r < 0.4f)
                {
                    Vector3 keep = measured * (longReal / longModel);
                    FitNotes.AppendLine("  ! " + (prefab != null ? prefab.name : "?") + ": defter olcusu (" + Fmt(real) + ") modelin oranina uymuyor, model orani korundu: " + Fmt(keep) + " cm");
                    return keep;
                }
            }
            return m;
        }

        public static readonly System.Text.StringBuilder FitNotes = new System.Text.StringBuilder();

        public static Vector3 FitToLongest(GameObject prefab, float longest, Vector3 real) { return FitSize(prefab, real, false); }

        // Prefab'in oyundaki (ItemVisual ile ayni) olcusu: tum aktif Renderer'larin sinir kutusu, prefab kendi donusuyle.
        static Vector3 Measure(GameObject prefab)
        {
            if (prefab == null) return Vector3.zero;
            var inst = UnityEngine.Object.Instantiate(prefab);
            try
            {
                inst.transform.position = Vector3.zero;
                var rs = inst.GetComponentsInChildren<Renderer>().Where(x => x is MeshRenderer || x is SkinnedMeshRenderer).ToArray();   // parcacik/iz/cizgi cizicileri olcuyu sisirmesin
                if (rs.Length == 0) return Vector3.zero;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return b.size;
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }

        // ------------------------------------------------------------------ model duzeltmeleri (LootItemTable.Def.hide / align)
        // Esyanin prefab'inda fazlalik parcalari (zemin plakasi, yere uzanan kablo, etraftaki cicekler) kapatir, capraz/donuk
        // duran modeli eksenlere hizalar. Olcumden ONCE calisir; degisiklik yoksa prefab'a dokunmaz (her calismada ayni sonuc).
        public static string FixItemPrefabs()
        {
            var log = new System.Text.StringBuilder();
            foreach (var d in LootItemTable.All)
            {
                bool wantHide = d.hide != null && d.hide.Length > 0;
                if (!wantHide && d.align == LootItemTable.AlignNone) continue;
                string path = PrefabRoot + "/" + d.path + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    int hidden = 0, newly = 0;
                    if (wantHide)
                    {
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!HideMatch(r, d.hide)) continue;
                            hidden++;
                            if (r.gameObject.activeSelf) { r.gameObject.SetActive(false); newly++; changed = true; }
                        }
                        if (hidden == 0) log.AppendLine("  ! " + d.title + ": gizlenecek parça bulunamadı (" + string.Join(", ", d.hide.Take(4).ToArray()) + "...)");
                        else if (newly > 0) log.AppendLine("  " + d.title + ": " + newly + " fazlalık parça gizlendi");
                    }
                    if (d.align != LootItemTable.AlignNone && root.transform.childCount > 0)
                    {
                        Transform model = root.transform.GetChild(0);
                        Quaternion q = d.align == LootItemTable.AlignYaw ? YawAlign(root.transform) : AxisAlign(root.transform);
                        float angle = Quaternion.Angle(q, Quaternion.identity);
                        if (angle > 0.75f)
                        {
                            model.rotation = q * model.rotation;
                            changed = true;
                            log.AppendLine("  " + d.title + ": model " + angle.ToString("0.#") + " derece döndürülüp eksenlere hizalandı");
                        }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                catch (Exception ex) { log.AppendLine("  ! " + d.title + ": düzeltilemedi (" + ex.Message + ")"); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return log.ToString();
        }

        // "ad" = nesne adi aynen, "*son" = renderer'in materyallerinden birinin adi bununla biter
        static bool HideMatch(Renderer r, string[] tokens)
        {
            foreach (string t in tokens)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (t[0] == '*')
                {
                    string suffix = t.Substring(1);
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (string.Equals(r.gameObject.name, t, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // prefab kokunun uzayinda aktif mesh koseleri (en fazla ~40 bin, esit aralikli)
        static List<Vector3> ModelPoints(Transform root)
        {
            var pts = new List<Vector3>();
            var all = new List<KeyValuePair<Transform, Mesh>>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
                if (mf.sharedMesh != null && mf.GetComponent<Renderer>() != null) all.Add(new KeyValuePair<Transform, Mesh>(mf.transform, mf.sharedMesh));
            foreach (var sr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (sr.sharedMesh != null) all.Add(new KeyValuePair<Transform, Mesh>(sr.transform, sr.sharedMesh));
            long total = 0; foreach (var kv in all) total += kv.Value.vertexCount;
            int step = (int)Math.Max(1, total / 40000);
            foreach (var kv in all)
            {
                var v = kv.Value.vertices;
                for (int i = 0; i < v.Length; i += step) pts.Add(root.InverseTransformPoint(kv.Key.TransformPoint(v[i])));
            }
            return pts;
        }

        // En uzun dogrultu (temel bilesen) en yakin eksene oturur: capraz kilic dik durur, egik fener duzelir.
        static Quaternion AxisAlign(Transform root)
        {
            var p = ModelPoints(root);
            if (p.Count < 8) return Quaternion.identity;
            Vector3 mean = Vector3.zero; foreach (var v in p) mean += v; mean /= p.Count;
            double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var v0 in p)
            {
                Vector3 v = v0 - mean;
                xx += v.x * v.x; xy += v.x * v.y; xz += v.x * v.z; yy += v.y * v.y; yz += v.y * v.z; zz += v.z * v.z;
            }
            // en buyuk ozdeger yonu: kuvvet yontemi
            Vector3 e = new Vector3(1f, 1.3f, 0.7f).normalized;
            for (int it = 0; it < 200; it++)
            {
                var n = new Vector3((float)(xx * e.x + xy * e.y + xz * e.z), (float)(xy * e.x + yy * e.y + yz * e.z), (float)(xz * e.x + yz * e.y + zz * e.z));
                if (n.sqrMagnitude < 1e-20f) return Quaternion.identity;
                e = n.normalized;
            }
            Vector3 axis = Vector3.right; float best = -1f;
            foreach (var a in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                float dot = Mathf.Abs(Vector3.Dot(e, a));
                if (dot > best) { best = dot; axis = Vector3.Dot(e, a) >= 0f ? a : -a; }
            }
            return Quaternion.FromToRotation(e, axis);
        }

        // Yukari eksen etrafinda, ustten bakinca kapladigi alan en kucuk olacak sekilde (-45..45 derece) cevirir.
        static Quaternion YawAlign(Transform root)
        {
            var p = ModelPoints(root);
            if (p.Count < 8) return Quaternion.identity;
            float bestArea = float.MaxValue, bestAngle = 0f;
            for (float a = -45f; a < 45f; a += 0.5f)
            {
                Quaternion q = Quaternion.AngleAxis(a, Vector3.up);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var v0 in p)
                {
                    Vector3 v = q * v0;
                    if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
                }
                float area = (maxX - minX) * (maxZ - minZ);
                if (area < bestArea - 1e-7f || (Mathf.Abs(area - bestArea) <= 1e-7f && Mathf.Abs(a) < Mathf.Abs(bestAngle))) { bestArea = area; bestAngle = a; }
            }
            return Quaternion.AngleAxis(bestAngle, Vector3.up);
        }

        static ItemCatalog.Entry FromDef(ItemCatalog catalog, LootItemTable.Def d, GameObject prefab)
        {
            var tier = catalog.Tier(d.rarity);
            return new ItemCatalog.Entry
            {
                key = d.key, title = d.title, baseDollars = d.dollars, selectionWeight = tier.weight, maxGroups = tier.maxGroups,
                color = tier.color, visualPrefab = prefab, rarity = d.rarity, realSize = d.size, lyingOK = d.lyingOK, category = d.category,
                packSize = d.pack, kg = d.kg, collector = d.collector, ledgerId = d.ledger, crateMask = d.mask, roundCount = d.count,
            };
        }

        static ItemCatalog.Entry Generic(ItemCatalog catalog, string rel, GameObject prefab)
        {
            string name = Path.GetFileName(rel);
            string key = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            var tier = catalog.Tier(ItemRarity.Common);
            return new ItemCatalog.Entry
            {
                key = key.Length > 40 ? key.Substring(0, 40) : key, title = name.Replace('_', ' '), baseDollars = 80, selectionWeight = tier.weight, maxGroups = tier.maxGroups,
                color = tier.color, visualPrefab = prefab, rarity = ItemRarity.Common, realSize = new Vector3(.4f, .4f, .4f), category = "Diğer",
            };
        }

        // Tabloda olmayan eşya: addan gerçek hayat tahmini (bir kez). true = tahmin uygulandı.
        static bool ApplyGuess(ItemCatalog catalog, ItemCatalog.Entry e, System.Text.StringBuilder report)
        {
            string prefabName = e.visualPrefab != null ? e.visualPrefab.name : "";
            var g = LootItemTable.GuessFor(e.key + " " + prefabName + " " + e.title);
            if (g == null) return false;
            Vector3 size = FitSize(e.visualPrefab, g.size, g.byHeight);
            var tier = catalog.Tier(ItemRarity.Common);
            e.title = g.title; e.category = g.category; e.realSize = size; e.kg = g.kg; e.lyingOK = g.lying;
            e.baseDollars = g.dollars; e.roundCount = g.count; e.ledgerStamp = null; e.conditionPrices = null;
            e.rarity = ItemRarity.Common; e.selectionWeight = tier.weight; e.maxGroups = tier.maxGroups; e.color = tier.color;
            e.crateMask = LootPools.Mask(e);
            report.AppendLine("  ~ TAHMİNİ ÖLÇÜ: " + prefabName + " -> " + g.title + ": " + Fmt(size) + " cm, " + g.kg + " kg (LootItemTable'a satır ekleyerek kesinleştir)");
            return true;
        }
    }
}

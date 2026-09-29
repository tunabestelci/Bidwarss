// Kasa Avcisi - Depo Level Builder
// Menu: Tools > Depo > Level Builder
// DepoLayout.json'u okuyup sahnede tum depoyu (duvarlar, konteynerler, raflar, slotlar, tetikler,
// isiklar, yazilar) kurar. Tekrar tekrar calistirilabilir: eski DEPO_LEVEL silinip yenisi kurulur.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DepoLevel.EditorTools
{
    public class DepoLevelBuilder : EditorWindow
    {
        const string DefaultJsonPath = "Assets/DepoLevel/Data/DepoLayout.json";
        const string TextureFolder = "Assets/DepoLevel/Textures";
        const string RootName = "DEPO_LEVEL";

        public enum LightMode { Baked, Mixed, Realtime }

        TextAsset layoutJson;
        DepoPrefabMap prefabMap;
        string materialFolder = "Assets/DepoLevel/Materials";
        bool keepExistingMaterials = true;
        bool useTextures = true;
        bool useToon = true;
        bool includePlaceholders = true;
        bool includeRoof = true;
        bool slotColliders = true;
        bool markStatic = true;
        LightMode lightMode = LightMode.Baked;
        float lightIntensityMul = 1f;
        float lightRangeMul = 1f;
        bool addLightProbes = true;
        bool addReflectionProbes = true;
        bool setupLighting = true;
        bool addTestPlayer = true;
        Vector2 scroll;
        bool integration;
        public Dictionary<string,Transform> BuiltNodes { get; private set; }
        public GameObject BuiltRoot { get; private set; }
        public static TextAsset LoadLayout()
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultJsonPath);
            if(asset!=null)return asset;
            using(var file=File.OpenRead(DefaultJsonPath+".gz"))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))
            using(var reader=new StreamReader(gzip))return new TextAsset(reader.ReadToEnd());
        }
        public static GameObject BuildForBidwarss(out Dictionary<string,Transform> nodes)
        {
            var builder=CreateInstance<DepoLevelBuilder>();
            try
            {
                builder.layoutJson=LoadLayout();builder.integration=true;
                builder.includePlaceholders=false;builder.addTestPlayer=false;builder.slotColliders=false;
                builder.addReflectionProbes=false;builder.addLightProbes=false;
                builder.Build();nodes=builder.BuiltNodes;
                if(builder.BuiltRoot==null)throw new InvalidOperationException("Depo kurulamadi.");
                return builder.BuiltRoot;
            }
            finally {DestroyImmediate(builder);}
        }

        // derleme sirasinda
        Dictionary<string, Material> mats;
        string pipeline;
        Type tmpType;
        bool tmpReady;
        Font legacyFont;
        int statPrim, statLabel, statLight, statSlot, statTrig, statPrefab;

        [MenuItem("Tools/Depo/Level Builder")]
        public static void Open()
        {
            var w = GetWindow<DepoLevelBuilder>("Depo Level Builder");
            w.minSize = new Vector2(380, 520);
        }

        [MenuItem("Tools/Depo/Prefab Map Olustur")]
        public static void CreatePrefabMap()
        {
            EnsureFolder("Assets/DepoLevel");
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/DepoLevel/DepoPrefabMap.asset");
            var map = CreateInstance<DepoPrefabMap>();
            map.AddKnownKeys();
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = map;
            EditorGUIUtility.PingObject(map);
        }

        void OnEnable()
        {
            if (layoutJson == null) layoutJson = LoadLayout();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Kasa Avcisi - Depo Level Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("JSON'daki her sey metre cinsinden, Unity eksenlerinde. Build tusu sahnedeki eski DEPO_LEVEL'i silip yeniden kurar (Ctrl+Z ile geri alinabilir).", MessageType.None);

            layoutJson = (TextAsset)EditorGUILayout.ObjectField("Layout JSON", layoutJson, typeof(TextAsset), false);
            prefabMap = (DepoPrefabMap)EditorGUILayout.ObjectField(new GUIContent("Prefab Map (opsiyonel)", "Kendi modellerini anahtarlarla esle; bos anahtarlar ilkel kutu olarak kalir."), prefabMap, typeof(DepoPrefabMap), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gorunum", EditorStyles.boldLabel);
            materialFolder = EditorGUILayout.TextField("Materyal klasoru", materialFolder);
            keepExistingMaterials = EditorGUILayout.Toggle(new GUIContent("Mevcut materyalleri koru", "Acikken, daha once olusturulan ve senin duzenledigin materyallerin ustune yazilmaz."), keepExistingMaterials);
            useToon = EditorGUILayout.Toggle(new GUIContent("Cel-shade (Toon/DepoCel)", "URP'de murekkep cizgili, bantli toon shader. Kapaliysa URP Lit."), useToon);
            useTextures = EditorGUILayout.Toggle(new GUIContent("Basit dokulari kullan", "Beton, konteyner olugu, kasa, palet vb. (Assets/DepoLevel/Textures)"), useTextures);
            includeRoof = EditorGUILayout.Toggle("Tavan ve lambalar", includeRoof);
            includePlaceholders = EditorGUILayout.Toggle(new GUIContent("Konteynerlerde ornek esyalar", "Yer tutucu kasalar/variller. Oyunda gercek loot dogunca silinebilir."), includePlaceholders);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Oyun", EditorStyles.boldLabel);
            slotColliders = EditorGUILayout.Toggle(new GUIContent("Slotlara trigger collider", "Raycast ile slot secebilmek icin."), slotColliders);
            addTestPlayer = EditorGUILayout.Toggle(new GUIContent("Test oyuncusu ekle", "Spawn noktasina CharacterController'li FP oyuncu koyar. Sahnedeki diger Main Camera'lar kapatilir."), addTestPlayer);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Isik", EditorStyles.boldLabel);
            lightMode = (LightMode)EditorGUILayout.EnumPopup(new GUIContent("Isik modu", "62 nokta isik var: Baked onerilir (performans)."), lightMode);
            lightIntensityMul = EditorGUILayout.Slider("Siddet carpani", lightIntensityMul, 0.1f, 5f);
            lightRangeMul = EditorGUILayout.Slider("Menzil carpani", lightRangeMul, 0.5f, 2f);
            markStatic = EditorGUILayout.Toggle(new GUIContent("Static isaretle", "Contribute GI, batching, occlusion. Yer tutucu esyalar static olmaz."), markStatic);
            addLightProbes = EditorGUILayout.Toggle("Light probe izgarasi", addLightProbes);
            addReflectionProbes = EditorGUILayout.Toggle("Reflection probe'lar", addReflectionProbes);
            setupLighting = EditorGUILayout.Toggle(new GUIContent("Lighting ayarlarini kur", "Ortam isigi + DepoLightingSettings (Progressive GPU, 8 texel/m)."), setupLighting);

            EditorGUILayout.Space();
            GUI.enabled = layoutJson != null;
            if (GUILayout.Button("DEPOYU KUR", GUILayout.Height(36))) Build();
            GUI.enabled = true;

            EditorGUILayout.Space();
            if (GUILayout.Button("Prefab Map olustur (tum anahtarlarla)")) CreatePrefabMap();
            if (GUILayout.Button("Isiklari bake et (Generate Lighting)"))
            {
                if (EditorUtility.DisplayDialog("Bake", "Isik bake islemi baslasin mi? Bilgisayara gore birkac dakika surebilir.", "Baslat", "Vazgec"))
                    Lightmapping.BakeAsync();
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ BUILD
        void Build()
        {
            DepoLayoutFile L;
            try { L = JsonUtility.FromJson<DepoLayoutFile>(layoutJson.text); }
            catch (Exception e) { EditorUtility.DisplayDialog("Depo", "JSON okunamadi:\n" + e.Message, "Tamam"); return; }
            if (L == null || L.nodes == null || L.nodes.Length == 0) { EditorUtility.DisplayDialog("Depo", "JSON bos gorunuyor.", "Tamam"); return; }

            statPrim = statLabel = statLight = statSlot = statTrig = statPrefab = 0;
            PrepareText();
            if(integration && !tmpReady)tmpType=null;
            if (tmpType != null && !tmpReady)
            {
                int c = EditorUtility.DisplayDialogComplex("TextMeshPro",
                    "TextMeshPro Essential Resources projede yok gibi. Yazilar (konteyner numaralari, tabelalar) gorunmeyebilir.\n\nOnce Window > TextMeshPro > Import TMP Essential Resources yapman onerilir.",
                    "Simdi import et", "Vazgec", "Yine de kur");
                if (c == 0) { EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources"); return; }
                if (c == 1) return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    if (!EditorUtility.DisplayDialog("Depo", "Sahnede zaten bir DEPO_LEVEL var. Silinip yeniden kurulsun mu?", "Evet, yeniden kur", "Vazgec")) return;
                    Undo.DestroyObjectImmediate(go);
                }
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                EditorUtility.DisplayProgressBar("Depo", "Materyaller hazirlaniyor...", 0.05f);
                BuildMaterials(L);

                var byId = new Dictionary<string, Transform>(L.nodes.Length);
                BuiltNodes=byId;
                var ctx = new Dictionary<string, NodeCtx>(L.nodes.Length);
                var containers = new List<DepoContainer>();
                GameObject root = null;
                DepoPlayerSpawn spawn = null;

                for (int i = 0; i < L.nodes.Length; i++)
                {
                    var n = L.nodes[i];
                    if ((i & 127) == 0) EditorUtility.DisplayProgressBar("Depo", "Kuruluyor: " + n.name, 0.1f + 0.8f * i / L.nodes.Length);

                    Transform parent = null;
                    NodeCtx pc = new NodeCtx();
                    if (!string.IsNullOrEmpty(n.parent))
                    {
                        if (!byId.TryGetValue(n.parent, out parent)) continue; // atlanan ebeveyn -> cocuk da atlanir
                        ctx.TryGetValue(n.parent, out pc);
                    }

                    var c = pc;
                    if (n.tag == "placeholder") c.placeholder = true;
                    if (n.tag == "roof") c.roof = true;

                    if (c.placeholder && !includePlaceholders) continue;
                    bool visual = n.kind == "box" || n.kind == "cyl" || n.kind == "label";
                    if (c.roof && !includeRoof && visual) continue;
                    if (pc.suppressed && (n.kind == "box" || n.kind == "cyl") && !c.placeholder) continue;
                    if (pc.suppressed && n.kind == "light" && !pc.keepLights) continue;

                    GameObject go = CreateNode(n, parent, ref c);
                    if (go == null) continue;
                    if (root == null && parent == null) root = go;

                    byId[n.id] = go.transform;
                    ctx[n.id] = c;

                    if (n.tag == "container")
                    {
                        var dc = go.AddComponent<DepoContainer>();
                        var d = DepoUtil.ParseData(n.data);
                        int.TryParse(DepoUtil.Get(d, "id", "0"), out dc.containerNo);
                        dc.side = dc.containerNo <= 5 ? "Bati" : "Dogu";
                        containers.Add(dc);
                    }
                    if (n.tag == "placeholder" && parent != null)
                    {
                        var dc = parent.GetComponent<DepoContainer>();
                        if (dc != null) dc.placeholderRoot = go;
                    }
                    if (n.kind == "spawn") spawn = go.GetComponent<DepoPlayerSpawn>();
                }

                if (root == null) throw new Exception("Kok grup bulunamadi.");

                BuiltRoot=root;
                // Kok bileseni + bolgeler
                var lr = root.AddComponent<DepoLevelRoot>();
                lr.layoutVersion = L.version;
                if (L.zones != null)
                    foreach (var z in L.zones)
                    {
                        lr.zones.Add(new DepoZoneInfo
                        {
                            id = z.id,
                            displayName = z.name,
                            color = DepoUtil.Hex(z.color, Color.white),
                            rects = DepoUtil.ParseRects(z.rects),
                            teleportPoint = new Vector3(z.tx, 0f, z.tz),
                            teleportYaw = z.tyaw
                        });
                    }
                ApplyZoneColors(root, lr);

                foreach (var dc in containers)
                {
                    dc.CollectChildren();
                    foreach (var ls in dc.lootSpawns) ls.containerNo = dc.containerNo;
                }

                EditorUtility.DisplayProgressBar("Depo", "Isik ve probe'lar...", 0.93f);
                if (addLightProbes) BuildLightProbes(root.transform, L, containers);
                if (addReflectionProbes) BuildReflectionProbes(root.transform, L);
                if (setupLighting) SetupLighting();
                if (addTestPlayer && spawn != null) BuildTestPlayer(root.transform, spawn.transform);

                Undo.RegisterCreatedObjectUndo(root, "Depo Level kur");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;

                string msg = string.Format("Depo kuruldu ({0}).\n\nIlkel: {1}\nPrefab: {2}\nYazi: {3}\nIsik: {4}\nSlot: {5}\nTetik: {6}\nKonteyner: {7}\n\nSimdi: Window > Rendering > Lighting > Generate Lighting (veya pencerdeki bake tusu).",
                    pipeline, statPrim, statPrefab, statLabel, statLight, statSlot, statTrig, containers.Count);
                Debug.Log("[Depo] " + msg.Replace("\n", " "));
                if(!integration)EditorUtility.DisplayDialog("Depo", msg, "Tamam");
            }
            catch (Exception e)
            {
                if(integration)throw;
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Depo", "Kurulumda hata: " + e.Message, "Tamam");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        struct NodeCtx
        {
            public bool placeholder, roof, suppressed, keepLights;
        }

        GameObject CreateNode(DepoNodeDef n, Transform parent, ref NodeCtx c)
        {
            GameObject go;
            var data = DepoUtil.ParseData(n.data);
            var entry = prefabMap != null ? prefabMap.Find(n.prefab) : null;

            switch (n.kind)
            {
                case "group":
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    if (entry != null)
                    {
                        var inst = InstantiatePrefab(entry, go.transform, Vector3.zero);
                        if (inst != null)
                        {
                            c.suppressed = true;
                            c.keepLights = entry.keepJsonLights;
                            statPrefab++;
                        }
                    }
                    return go;

                case "box":
                case "cyl":
                    if (entry != null)
                    {
                        // kutu anahtari: pivot = kutunun alt ortasi
                        go = new GameObject(n.name);
                        SetLocal(go.transform, parent, n);
                        go.transform.localPosition -= new Vector3(0f, n.sy * 0.5f, 0f);
                        if (InstantiatePrefab(entry, go.transform, Vector3.zero) != null) { statPrefab++; return go; }
                        DestroyImmediate(go);
                    }
                    return CreatePrimitive(n, parent, c);

                case "label":
                    return CreateLabel(n, parent);

                case "light":
                    return CreateLight(n, parent);

                case "slot":
                {
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    var s = go.AddComponent<DepoShelfSlot>();
                    s.zoneId = DepoUtil.Get(data, "zone");
                    s.slotType = DepoUtil.Get(data, "type");
                    int.TryParse(DepoUtil.Get(data, "level", "0"), out s.level);
                    s.size = new Vector3(n.sx, n.sy, n.sz);
                    if (slotColliders)
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.isTrigger = true;
                        bc.size = s.size;
                    }
                    statSlot++;
                    return go;
                }

                case "zone":
                {
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    bc.size = new Vector3(n.sx, n.sy, n.sz);
                    var z = go.AddComponent<DepoZone>();
                    z.zoneId = DepoUtil.Get(data, "zone");
                    z.category = DepoUtil.Get(data, "category");
                    return go;
                }

                case "trigger":
                {
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    bc.size = new Vector3(n.sx, n.sy, n.sz);
                    var t = go.AddComponent<DepoTrigger>();
                    var type = DepoUtil.Get(data, "type", "Interact");
                    try { t.type = (DepoTriggerType)Enum.Parse(typeof(DepoTriggerType), type, true); }
                    catch { t.type = DepoTriggerType.Interact; }
                    t.zoneId = DepoUtil.Get(data, "zone");
                    t.subKind = DepoUtil.Get(data, "kind");
                    t.interactId = DepoUtil.Get(data, "id");
                    if (t.type == DepoTriggerType.Interact) t.onlyRigidbodies = false;
                    statTrig++;
                    return go;
                }

                case "loot":
                {
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    var l = go.AddComponent<DepoLootSpawn>();
                    int.TryParse(DepoUtil.Get(data, "container", "0"), out l.containerNo);
                    var digits = System.Text.RegularExpressions.Regex.Match(n.name ?? "", @"\d+$");
                    if (digits.Success) int.TryParse(digits.Value, out l.index);
                    return go;
                }

                case "spawn":
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    go.AddComponent<DepoPlayerSpawn>();
                    return go;

                default:
                    go = new GameObject(n.name);
                    SetLocal(go.transform, parent, n);
                    return go;
            }
        }

        static void SetLocal(Transform t, Transform parent, DepoNodeDef n)
        {
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = new Vector3(n.px, n.py, n.pz);
            t.localRotation = Quaternion.Euler(n.rx, n.ry, 0f);
            t.localScale = Vector3.one;
        }

        GameObject InstantiatePrefab(DepoPrefabMap.Entry e, Transform parent, Vector3 pos)
        {
            var inst = PrefabUtility.InstantiatePrefab(e.prefab) as GameObject;
            if (inst == null) return null;
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = pos + e.positionOffset;
            inst.transform.localRotation = Quaternion.Euler(e.rotationOffset);
            inst.transform.localScale = e.scale == Vector3.zero ? Vector3.one : e.scale;
            return inst;
        }

        GameObject CreatePrimitive(DepoNodeDef n, Transform parent, NodeCtx c)
        {
            bool cyl = n.kind == "cyl";
            // duvar resimleri (mural, sprey boya) Quad olur: -Z yuzu odaya bakar, UV yonu garanti
            bool quad = !cyl && !string.IsNullOrEmpty(n.mat) && (n.mat == "mural" || n.mat.StartsWith("graf_"));
            var go = GameObject.CreatePrimitive(cyl ? PrimitiveType.Cylinder : (quad ? PrimitiveType.Quad : PrimitiveType.Cube));
            go.name = n.name;
            SetLocal(go.transform, parent, n);
            if (quad)
            {
                go.transform.localScale = new Vector3(Mathf.Max(0.001f, n.sx), Mathf.Max(0.001f, n.sy), 1f);
                go.transform.localPosition -= go.transform.localRotation * new Vector3(0f, 0f, n.sz * 0.5f);
                var qc = go.GetComponent<Collider>();
                if (qc != null) DestroyImmediate(qc);
                var qr = go.GetComponent<MeshRenderer>();
                var qm = GetMaterial(n.mat, false);
                if (qm != null) qr.sharedMaterial = qm;
                qr.shadowCastingMode = ShadowCastingMode.Off;
                if (markStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
                statPrim++;
                return go;
            }
            go.transform.localScale = new Vector3(Mathf.Max(0.001f, n.sx), Mathf.Max(0.001f, cyl ? n.sy * 0.5f : n.sy), Mathf.Max(0.001f, n.sz));

            // collider: silindirlerde de kutu (kararli ve ucuz)
            var col = go.GetComponent<Collider>();
            if (cyl && col != null) { DestroyImmediate(col); col = null; if (n.nc != 1) go.AddComponent<BoxCollider>().size=new Vector3(1,2,1); }
            else if (n.nc == 1 && col != null) DestroyImmediate(col);

            var mr = go.GetComponent<MeshRenderer>();
            var m = GetMaterial(n.mat, cyl);
            if (m != null) mr.sharedMaterial = m;
            bool decal = matDefs != null && matDefs.TryGetValue(n.mat ?? "", out var md) && md.outline == 0;
            bool thin = Mathf.Min(n.sx, Mathf.Min(n.sy, n.sz)) < 0.03f;
            if (n.mat == "lamp" || (decal && thin)) mr.shadowCastingMode = ShadowCastingMode.Off;

            if (markStatic && !c.placeholder)
            {
                var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic;
                if (Mathf.Max(n.sx, Mathf.Max(n.sy, n.sz)) > 1.5f && Mathf.Min(n.sx, Mathf.Min(n.sy, n.sz)) > 0.05f) flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                // kucuk parcalar lightmap yerine light probe kullansin: bake cok hizlanir
                float maxDim = Mathf.Max(n.sx, Mathf.Max(n.sy, n.sz));
                if (addLightProbes && n.tag != "floor" && maxDim < 0.7f) mr.receiveGI = ReceiveGI.LightProbes;
            }
            statPrim++;
            return go;
        }

        // ------------------------------------------------------------------ YAZILAR
        void PrepareText()
        {
            tmpType = FindType("TMPro.TextMeshPro");
            tmpReady = false;
            if (tmpType != null)
            {
                try
                {
                    var settings = FindType("TMPro.TMP_Settings");
                    var p = settings != null ? settings.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Static) : null;
                    tmpReady = p != null && p.GetValue(null) != null;
                }
                catch { tmpReady = false; }
            }
            legacyFont = null;
            try { legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (legacyFont == null) { try { legacyFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        }

        static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        static void SetMember(object o, string name, object value)
        {
            if (o == null) return;
            var t = o.GetType();
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try
                {
                    if (p.PropertyType.IsEnum && value is string s) value = Enum.Parse(p.PropertyType, s);
                    p.SetValue(o, value);
                }
                catch { }
            }
        }

        static bool HasProperty(object o, string name)
        {
            return o != null && o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance) != null;
        }

        GameObject CreateLabel(DepoNodeDef n, Transform parent)
        {
            var go = new GameObject(n.name);
            SetLocal(go.transform, parent, n);
            Color col = Color.black;
            if (mats.TryGetValue(n.mat ?? "", out var m)) col = GetMatColor(m);
            float h = Mathf.Max(0.05f, n.sy) * 1.25f;
            float w = n.sx > 0f ? n.sx : h * Mathf.Max(1, (n.text ?? "").Length) * 0.6f;

            if (tmpType != null)
            {
                var comp = go.AddComponent(tmpType);
                SetMember(comp, "text", n.text ?? "");
                SetMember(comp, "color", col);
                SetMember(comp, "fontStyle", "Bold");
                SetMember(comp, "alignment", "Center");
                if (HasProperty(comp, "textWrappingMode")) SetMember(comp, "textWrappingMode", "NoWrap");
                else SetMember(comp, "enableWordWrapping", false);
                SetMember(comp, "overflowMode", "Overflow");
                SetMember(comp, "enableAutoSizing", true);
                SetMember(comp, "fontSizeMin", 0.05f);
                SetMember(comp, "fontSizeMax", 200f);
                SetMember(comp, "margin", Vector4.zero);
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(w, h);
                var r = go.GetComponent<Renderer>();
                if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            else if (legacyFont != null)
            {
                // yedek: TextMesh (TMP yoksa). Olcek yaklasiktir.
                var tm = go.AddComponent<TextMesh>();
                tm.text = n.text ?? "";
                tm.font = legacyFont;
                tm.fontSize = 64;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = col;
                float byHeight = n.sy / 6.4f;
                float byWidth = n.sx > 0f ? n.sx / (6.4f * 0.62f * Mathf.Max(1, tm.text.Length)) : byHeight;
                tm.characterSize = Mathf.Min(byHeight, byWidth);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = legacyFont.material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            statLabel++;
            return go;
        }

        // ------------------------------------------------------------------ ISIKLAR
        GameObject CreateLight(DepoNodeDef n, Transform parent)
        {
            var go = new GameObject(n.name);
            SetLocal(go.transform, parent, n);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = DepoUtil.Hex(n.lc, Color.white);
            l.range = Mathf.Max(1f, n.lr) * lightRangeMul;

            // JSON siddetleri "tavandan zemine ~1 birim aydinlatma" olacak sekilde verildi.
            // Ters-kare dusus (URP, HDRP ve bake) icin yukseklige gore olceklenir.
            float height = Mathf.Max(1f, go.transform.position.y);
            bool legacyFalloff = pipeline == "Built-in" && lightMode != LightMode.Baked;
            float intensity = legacyFalloff ? n.li * 1.2f : n.li * height * height * 0.8f;
            if (pipeline == "HDRP") intensity *= 40f; // HDRP fiziksel birimler: kabaca; gerekirse carpani ayarla
            l.intensity = intensity * lightIntensityMul;

            switch (lightMode)
            {
                case LightMode.Baked: l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft; break;
                case LightMode.Mixed: l.lightmapBakeType = LightmapBakeType.Mixed; l.shadows = LightShadows.Soft; break;
                default: l.lightmapBakeType = LightmapBakeType.Realtime; l.shadows = LightShadows.None; break;
            }
            l.shadowRadius = 0.25f; // bake gölgesi yumusakligi
            statLight++;
            return go;
        }

        void BuildLightProbes(Transform root, DepoLayoutFile L, List<DepoContainer> containers)
        {
            var go = new GameObject("LightProbes");
            go.transform.SetParent(root, false);
            var g = go.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            var a = L.bounds != null ? L.bounds.ana_depo : null;
            if (a == null || a.Length < 5) a = new float[] { -60, 0, 60, 90, 10f };
            float[] ys = { 0.5f, 1.9f, 4.5f };
            for (float x = a[0] + 2f; x <= a[2] - 1.9f; x += 4f)
                for (float z = a[1] + 2f; z <= a[3] - 1.9f; z += 4f)
                    foreach (var y in ys) pts.Add(new Vector3(x, y, z));

            var k = L.bounds != null ? L.bounds.koridor_binasi : null;
            float hw = L.bounds != null && L.bounds.koridor_genislik > 0 ? L.bounds.koridor_genislik * 0.5f : 1.5f;
            if (k != null && k.Length >= 5)
            {
                for (float z = k[1] + 0.6f; z <= k[3] - 0.3f; z += 2.2f)
                    foreach (var y in new[] { 0.5f, 1.9f, k[4] - 0.8f })
                    {
                        pts.Add(new Vector3(-hw + 0.4f, y, z));
                        pts.Add(new Vector3(hw - 0.4f, y, z));
                    }
            }
            float cl = 14f, cw = 3.4f, ch = 2.6f;
            if (L.bounds != null && L.bounds.konteyner != null && L.bounds.konteyner.Length >= 3) { cl = L.bounds.konteyner[0]; cw = L.bounds.konteyner[1]; ch = L.bounds.konteyner[2]; }
            foreach (var c in containers)
                for (float lx = 1.5f; lx <= cl - 1f; lx += 4f)
                    foreach (var lz in new[] { -cw * 0.3f, cw * 0.3f })
                        foreach (var y in new[] { 0.6f, ch * 0.45f, ch - 0.8f })
                            pts.Add(root.InverseTransformPoint(c.transform.TransformPoint(new Vector3(lx, y, lz))));

            g.probePositions = pts.ToArray();
        }

        void BuildReflectionProbes(Transform root, DepoLayoutFile L)
        {
            var a = L.bounds != null ? L.bounds.ana_depo : null;
            var k = L.bounds != null ? L.bounds.koridor_binasi : null;
            if (a != null && a.Length >= 5)
                MakeProbe(root, "ReflectionProbe_AnaDepo", new Vector3((a[0] + a[2]) * 0.5f, a[4] * 0.5f, (a[1] + a[3]) * 0.5f), new Vector3(a[2] - a[0], a[4], a[3] - a[1]));
            if (k != null && k.Length >= 5)
            {
                float w = L.bounds.koridor_genislik > 0 ? L.bounds.koridor_genislik : 3f;
                MakeProbe(root, "ReflectionProbe_Koridor", new Vector3(0f, k[4] * 0.5f, (k[1] + k[3]) * 0.5f), new Vector3(w, k[4], k[3] - k[1]));
            }
        }

        static void MakeProbe(Transform root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.size = size;
            p.boxProjection = true;
            p.resolution = 128;
        }

        void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.21f, 0.23f);

            const string path = "Assets/DepoLevel/DepoLightingSettings.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                ls = new LightingSettings { name = "DepoLightingSettings" };
                ls.bakedGI = true;
                ls.realtimeGI = false;
                ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                ls.lightmapResolution = 8f;
                ls.lightmapPadding = 2;
                ls.lightmapMaxSize = 2048;
                ls.directionalityMode = LightmapsMode.NonDirectional;
                ls.mixedBakeMode = MixedLightingMode.Shadowmask;
                ls.directSampleCount = 32;
                ls.indirectSampleCount = 256;
                ls.ao = true;
                ls.aoMaxDistance = 0.6f;
                EnsureFolder("Assets/DepoLevel");
                AssetDatabase.CreateAsset(ls, path);
                AssetDatabase.SaveAssets();
            }
            Lightmapping.lightingSettings = ls;
        }

        void BuildTestPlayer(Transform root, Transform spawn)
        {
            foreach (var cam in FindObjectsOfTypeCompat<Camera>())
            {
                if (cam.CompareTag("MainCamera") && !cam.transform.IsChildOf(root))
                {
                    Undo.RecordObject(cam.gameObject, "Kamera kapat");
                    cam.gameObject.SetActive(false);
                    Debug.Log("[Depo] Test oyuncusu icin kapatilan kamera: " + cam.name);
                }
            }
            var p = new GameObject("DepoTestPlayer");
            p.transform.SetParent(spawn.parent != null ? spawn.parent : root, false);
            p.transform.position = spawn.position;
            p.transform.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.3f; cc.slopeLimit = 50f;
            var camGo = new GameObject("Kamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(p.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var c = camGo.AddComponent<Camera>();
            c.nearClipPlane = 0.05f; c.farClipPlane = 250f; c.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            var tp = p.AddComponent<DepoTestPlayer>();
            tp.cameraPivot = camGo.transform;
        }

        static T[] FindObjectsOfTypeCompat<T>() where T : Component
        {
            var result=new List<T>();
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>());
            return result.ToArray();
        }

        // ------------------------------------------------------------------ MATERYALLER
        Dictionary<string, DepoMaterialDef> matDefs;
        bool toon;

        void BuildMaterials(DepoLayoutFile L)
        {
            mats = new Dictionary<string, Material>();
            matDefs = new Dictionary<string, DepoMaterialDef>();
            cylMats.Clear();
            Shader shader = PickShader(out pipeline);
            toon = false;
            if (pipeline == "URP" && useToon)
            {
                var ts = Shader.Find("Toon/DepoCel");
                if (ts != null) { shader = ts; toon = true; }
                else Debug.LogWarning("[Depo] Toon/DepoCel shader'i bulunamadi, URP Lit kullaniliyor.");
            }
            EnsureFolder(materialFolder);

            // zemin tiling'i icin boyutlar
            floorSize = new Dictionary<string, Vector2>();
            foreach (var n in L.nodes)
                if (n.tag == "floor" && !string.IsNullOrEmpty(n.mat) && !floorSize.ContainsKey(n.mat))
                    floorSize[n.mat] = new Vector2(n.sx, n.sz);

            foreach (var def in L.materials)
            {
                matDefs[def.key] = def;
                mats[def.key] = MakeMaterial(def, shader, "Depo_" + def.key, def.edge != 0);
            }
            cylShader = shader;
            AssetDatabase.SaveAssets();
        }

        Dictionary<string, Vector2> floorSize;
        Shader cylShader;

        Material MakeMaterial(DepoMaterialDef def, Shader shader, string assetName, bool edge)
        {
            string path = materialFolder.TrimEnd('/') + "/" + assetName + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && keepExistingMaterials && existing.shader == shader) return existing;

            var m = existing != null ? existing : new Material(shader);
            m.shader = shader;
            m.name = assetName;
            Color c = DepoUtil.Hex(def.color, Color.magenta);
            c.a = 1f;
            SetColor(m, "_BaseColor", c);
            SetColor(m, "_Color", c);
            SetFloat(m, "_Smoothness", def.smoothness);
            SetFloat(m, "_Glossiness", def.smoothness);
            SetFloat(m, "_Metallic", def.metallic);

            if (!string.IsNullOrEmpty(def.emission))
            {
                Color e = DepoUtil.Hex(def.emission, Color.white) * Mathf.Max(0.1f, def.emissionIntensity);
                if (toon) e *= 0.5f;
                if (pipeline == "HDRP") { SetColor(m, "_EmissiveColor", e * 200f); SetColor(m, "_EmissiveColorLDR", e); }
                else { m.EnableKeyword("_EMISSION"); SetColor(m, "_EmissionColor", e); }
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (toon)
            {
                SetFloat(m, "_EdgeLines", edge ? 1f : 0f);
                bool outline = def.outline != 0 && def.key != "lamp";
                SetFloat(m, "_OutlineWidth", outline ? 0.028f : 0f);
                m.SetShaderPassEnabled("SRPDefaultUnlit", outline);
            }

            if (useTextures)
            {
                string tex = TextureFor(def.key);
                if (tex != null)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + tex + ".png");
                    if (t == null) t = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + tex + ".jpg");
                    if (t != null)
                    {
                        Vector2 tiling = Vector2.one;
                        if (floorSize.TryGetValue(def.key, out var fs)) tiling = new Vector2(fs.x / 4f, fs.y / 4f);
                        if (def.tiling != null && def.tiling.Length >= 2) tiling = new Vector2(def.tiling[0], def.tiling[1]);
                        foreach (var prop in new[] { "_BaseMap", "_MainTex", "_BaseColorMap" })
                            if (m.HasProperty(prop)) { m.SetTexture(prop, t); m.SetTextureScale(prop, tiling); }
                    }
                }
            }

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // Silindirlerde UV kenar cizgisi dikis izi birakir: kenar cizgisiz ayri materyal.
        readonly Dictionary<string, Material> cylMats = new Dictionary<string, Material>();
        Material GetMaterial(string key, bool cylinder)
        {
            if (string.IsNullOrEmpty(key) || !mats.TryGetValue(key, out var m)) return null;
            if (!cylinder || !toon || !matDefs.TryGetValue(key, out var def) || def.edge == 0) return m;
            if (cylMats.TryGetValue(key, out var cm) && cm != null) return cm;
            cm = MakeMaterial(def, cylShader, "Depo_" + key + "_Silindir", false);
            cylMats[key] = cm;
            return cm;
        }

        static string TextureFor(string key)
        {
            if (key == "graf_band") return "depo_mural";
            if (key.StartsWith("graf_")) return "depo_" + key;
            if (key.StartsWith("cont_")) return "depo_container";
            if (key.StartsWith("contL_")) return "depo_container_door";
            switch (key)
            {
                case "floor_concrete":
                case "floor_corridor": return "depo_concrete";
                case "crate_wood":
                case "crate_green": return "depo_crate";
                case "pallet":
                case "plywood":
                case "wood": return "depo_planks";
                case "cardboard": return "depo_cardboard";
                case "pegboard": return "depo_pegboard";
                case "rug": return "depo_rug";
                case "roller": return "depo_roller";
                case "hazard": return "depo_hazard";
                case "slatwall": return "depo_slatwall";
                case "wire": return "depo_wire";
                case "mural": return "depo_mural";
                default: return null;
            }
        }

        static Shader PickShader(out string pipe)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            Shader s = null;
            if (rp != null)
            {
                string n = rp.GetType().Name;
                if (n.Contains("HDRenderPipeline")) { pipe = "HDRP"; s = Shader.Find("HDRP/Lit"); }
                else if (n.Contains("Universal")) { pipe = "URP"; s = Shader.Find("Universal Render Pipeline/Lit"); }
                else pipe = n;
                if (s == null && rp.defaultMaterial != null) s = rp.defaultMaterial.shader;
                if (s != null) return s;
            }
            pipe = "Built-in";
            return Shader.Find("Standard");
        }

        static Color GetMatColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.black;
        }

        static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
        static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

        void ApplyZoneColors(GameObject root, DepoLevelRoot lr)
        {
            foreach (var s in root.GetComponentsInChildren<DepoShelfSlot>(true)) s.gizmoColor = lr.GetZoneColor(s.zoneId);
            foreach (var z in root.GetComponentsInChildren<DepoZone>(true)) z.gizmoColor = lr.GetZoneColor(z.zoneId);
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

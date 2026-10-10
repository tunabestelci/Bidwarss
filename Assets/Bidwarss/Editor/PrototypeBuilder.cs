using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bidwarss.Editor
{
    public static class PrototypeBuilder
    {
        const string Root = "Assets/Bidwarss/GeneratedV2";

        [MenuItem("Bidwarss/Create Gameplay Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }
            // A finished scene is never overwritten by rerunning this command.
            bool finished = AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Warehouse.unity") != null;
            if (AssetDatabase.IsValidFolder(Root) && finished)
            {
                if (!Application.isBatchMode) EditorUtility.DisplayDialog("Bidwarss", "Generated klasoru zaten var. Var olan Warehouse sahnesini ac. Yeniden uretmek icin once klasoru yedekleyip kaldir.", "Tamam");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // An earlier run that failed half way leaves a folder without a scene. Nothing is deleted:
            // move it aside so the next run starts clean instead of stopping on the stale folder.
            if (AssetDatabase.IsValidFolder(Root))
            {
                string aside = AssetDatabase.GenerateUniqueAssetPath(Root + "_incomplete");
                string moveError = AssetDatabase.MoveAsset(Root, aside);
                if (!string.IsNullOrEmpty(moveError)) throw new System.InvalidOperationException("Yarim kalmis " + Root + " klasoru tasinamadi: " + moveError);
                Debug.LogWarning("Bidwarss: yarim kalmis uretim " + aside + " altina tasindi; yeniden uretiliyor.");
            }
            Directory.CreateDirectory(Root);
            // Shaders must be imported before Shader.Find, otherwise a first run in a fresh project fails.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var itemMaterial = MakeMaterial("Items", Color.white);
            var floor = MakeMaterial("Floor", new Color(.16f, .23f, .31f));
            var wall = MakeMaterial("Walls", new Color(.35f, .44f, .55f));
            var wood = MakeMaterial("Shelves", new Color(.73f, .47f, .23f));
            var marker = MakeMaterial("SlotMarkers", new Color(.23f, .8f, .78f));
            var crate = MakeMaterial("Crates", new Color(.85f, .46f, .15f));
            var playerMat = MakeMaterial("Players", new Color(.8f, .26f, .35f));
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.entries = new[]
            {
                Entry("mirror", "Ayna", 100, ItemCatalog.SampleShape.Mirror, new Color(.3f,.75f,.8f)),
                Entry("table", "Masa", 100, ItemCatalog.SampleShape.Table, new Color(.76f,.5f,.24f)),
                Entry("chair", "Sandalye", 60, ItemCatalog.SampleShape.Chair, new Color(.85f,.4f,.28f)),
                Entry("radio", "Radyo", 120, ItemCatalog.SampleShape.Radio, new Color(.4f,.65f,.44f)),
                Entry("lamp", "Lamba", 80, ItemCatalog.SampleShape.Lamp, new Color(.95f,.78f,.3f))
            };
            AssetDatabase.CreateAsset(catalog, Root + "/ItemCatalog.asset");
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                var pipeline = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(pipeline, Root + "/WarehousePipeline.asset");
                // Create() supplies a renderer; persist it with the pipeline.
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                if (renderers != null)
                    for (int i=0;i<renderers.arraySize;i++)
                    {
                        var renderer = renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (renderer != null && !AssetDatabase.Contains(renderer)) AssetDatabase.AddObjectToAsset(renderer, pipeline);
                    }
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
            }
            Cube("Depo zemini", new Vector3(0,-.15f,0), new Vector3(28,.3f,36), floor);
            Cube("Sol duvar", new Vector3(-14,2,0), new Vector3(.3f,4,36), wall);
            Cube("Sag duvar", new Vector3(14,2,0), new Vector3(.3f,4,36), wall);
            Cube("Arka duvar", new Vector3(0,2,18), new Vector3(28,4,.3f), wall);
            Cube("Giris duvari", new Vector3(0,2,-18), new Vector3(28,4,.3f), wall);
            var worldObject = new GameObject("Warehouse State", typeof(NetworkObject), typeof(WarehouseWorld));
            var world = worldObject.GetComponent<WarehouseWorld>();
            world.catalog=catalog; world.itemMaterial=itemMaterial;
            world.dustMaterial=new Material(Shader.Find("Bidwarss/Dust"));
            AssetDatabase.CreateAsset(world.dustMaterial,Root+"/Dust.mat");
            world.crates=new Transform[10]; world.itemOrigins=new Transform[10]; world.lids=new Transform[10];
            for(int i=0;i<10;i++)
            {
                float side=i<5?-1:1, z=-9+(i%5)*4.5f;
                var box=Cube("Kasa "+(i+1),new Vector3(side*11.5f,.65f,z),new Vector3(1.8f,1.3f,2.2f),crate);
                Target(box,TargetKind.Crate,i); world.crates[i]=box.transform;
                var hinge=new GameObject("Kapak mentese").transform;
                hinge.position=new Vector3(side*11.5f,1.35f,z+1.1f);
                var lid=Cube("Kapak",hinge.position+new Vector3(0,0,-1.1f),new Vector3(1.9f,.12f,2.25f),wood);
                Object.DestroyImmediate(lid.GetComponent<Collider>());
                lid.transform.SetParent(hinge,true); world.lids[i]=hinge;
                var origin=new GameObject("Esya alani "+i).transform;
                origin.position=new Vector3(side<0?-9.4f:7.4f,0,z-1.2f); world.itemOrigins[i]=origin;
                Label("KASA "+(i+1),new Vector3(side*11.5f,2.2f,z),.15f);
            }
            world.slots=new Transform[12]; world.stackLabels=new TextMesh[12];
            for(int i=0;i<12;i++)
            {
                var center=new Vector3(-5.4f+(i%4)*3.6f,0,-1+(i/4)*4);
                var pallet=Cube("Istif "+i,center+Vector3.up*.08f,new Vector3(1.4f,.16f,2.3f),wood);
                world.slots[i]=pallet.transform;
                Target(pallet,TargetKind.Slot,i);
                var button=Cube("Yerlestir "+i,center+new Vector3(0,.18f,-1.4f),new Vector3(1.4f,.25f,.35f),marker);
                Target(button,TargetKind.Slot,i);
                world.stackLabels[i]=Label("ISTIF",center+new Vector3(0,.65f,1.2f),.10f);
            }
            Label("BIDWARSS / ANA DEPO",new Vector3(0,3,16),.25f);
            Label("10 KASA   /   12 ISTIF   /   TEK EKIP",new Vector3(0,2.3f,16),.13f);
            var player = new GameObject("WarehousePlayer", typeof(NetworkObject), typeof(NetworkTransform));
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .3f;
            controller.center = Vector3.up * .9f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.up * .9f;
            body.transform.localScale = new Vector3(.6f, .9f, .6f);
            body.GetComponent<Renderer>().sharedMaterial = playerMat;
            player.AddComponent<WarehousePlayer>().body = body.transform;
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/WarehousePlayer.prefab");
            Object.DestroyImmediate(player);

            var networkObject = new GameObject("NetworkManager", typeof(UnityTransport), typeof(NetworkManager));
            var network = networkObject.GetComponent<NetworkManager>();
            network.NetworkConfig.NetworkTransport = networkObject.GetComponent<UnityTransport>();
            network.NetworkConfig.PlayerPrefab = prefab;
            network.NetworkConfig.TickRate = 30;
            network.NetworkConfig.EnableSceneManagement = true;
            network.NetworkConfig.ConnectionApproval = true;
            var prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabList.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(prefabList, Root + "/NetworkPrefabs.asset");
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            var preview = new GameObject("Lobby Camera", typeof(Camera), typeof(AudioListener));
            preview.transform.position = new Vector3(0, 11, -13);
            preview.transform.LookAt(new Vector3(0, 0, 3));
            preview.GetComponent<Camera>().backgroundColor = new Color(.08f, .12f, .18f);
            var menu = new GameObject("Session Menu").AddComponent<SessionMenu>();
            menu.network = network;
            menu.sceneWorld = world;
            menu.gameObject.AddComponent<WarehouseHud>();
            menu.gameObject.AddComponent<RunRecorder>();
            menu.gameObject.AddComponent<LeaderboardClient>();
            menu.lobbyCamera = preview.GetComponent<Camera>();
            var sun = new GameObject("Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            sun.intensity = 1.2f;
            EditorSceneManager.MarkSceneDirty(scene);
            string scenePath = Root + "/Warehouse.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            buildScenes.RemoveAll(s => s.path == scenePath);
            buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Leaderboard rules hash: " + world.Rules.Fingerprint());
            Debug.Log("Bidwarss: Warehouse sahnesi hazir. Play > Oda kur. Client icin ayni sahnenin build'ini ac.");
        }

        public static void BuildBatch() => Build();

        // The score service only accepts hashes on its allow-list (BIDWARSS_ALLOWED_RULES).
        [MenuItem("Bidwarss/Print Leaderboard Rules Hash")]
        public static void PrintRulesHash()
        {
            var world = Object.FindFirstObjectByType<WarehouseWorld>();
            if (world == null || world.catalog == null)
            {
                Debug.LogError("Acik sahnede WarehouseWorld ve katalog bulunamadi. Depo sahnesini ac.");
                return;
            }
            Debug.Log("Leaderboard rules hash: " + world.Rules.Fingerprint());
        }

        static ItemCatalog.Entry Entry(string key,string title,int dollars,ItemCatalog.SampleShape shape,Color color) =>
            new ItemCatalog.Entry {key=key,title=title,baseDollars=dollars,sampleShape=shape,color=color};

        static Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Bidwarss/PrototypeToon");
            if (shader == null) throw new System.InvalidOperationException("Bidwarss shader import failed.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
            return material;
        }

        static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Target(GameObject go, TargetKind kind, int id)
        {
            var target = go.AddComponent<InteractionTarget>();
            target.kind = kind;
            target.id = id;
        }

        static TextMesh Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = size;
            label.transform.position = position;
            return label;
        }
    }
}

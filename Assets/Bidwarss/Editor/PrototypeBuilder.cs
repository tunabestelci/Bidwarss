using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bidwarss.Editor
{
    public static class PrototypeBuilder
    {
        const string Root = "Assets/Bidwarss/Generated";

        [MenuItem("Bidwarss/Create Prototype Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }
            // Generated assets are intentionally never overwritten by rerunning this command.
            if (AssetDatabase.IsValidFolder(Root))
            {
                EditorUtility.DisplayDialog("Bidwarss", "Generated klasoru zaten var. Var olan Warehouse sahnesini ac. Yeniden uretmek icin once klasoru yedekleyip kaldir.", "Tamam");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
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
                Entry("Test kutusu A", new Color(.9f, .3f, .2f), new Vector3(.4f, .2f, .35f)),
                Entry("Test kutusu B", new Color(.3f, .8f, .35f), new Vector3(.3f, .4f, .3f)),
                Entry("Test kutusu C", new Color(.95f, .75f, .2f), new Vector3(.42f, .3f, .25f))
            };
            AssetDatabase.CreateAsset(catalog, Root + "/ItemCatalog.asset");

            Cube("Depo zemini", new Vector3(0, -.15f, 0), new Vector3(24, .3f, 28), floor);
            Cube("Sol duvar", new Vector3(-12, 2, 0), new Vector3(.3f, 4, 28), wall);
            Cube("Sag duvar", new Vector3(12, 2, 0), new Vector3(.3f, 4, 28), wall);
            Cube("Arka duvar", new Vector3(0, 2, 14), new Vector3(24, 4, .3f), wall);
            Cube("Giris duvari", new Vector3(0, 2, -14), new Vector3(24, 4, .3f), wall);

            var worldObject = new GameObject("Warehouse State", typeof(NetworkObject), typeof(WarehouseWorld));
            var world = worldObject.GetComponent<WarehouseWorld>();
            world.catalog = catalog;
            world.itemMaterial = itemMaterial;
            world.testItemSpawns = new Transform[10];
            for (int i = 0; i < 10; i++)
            {
                float side = i < 5 ? -1 : 1;
                float z = -8 + (i % 5) * 4;
                var box = Cube("Satin alinmis kasa " + (i + 1), new Vector3(side * 9.5f, .8f, z), new Vector3(2, 1.6f, 2.4f), crate);
                Target(box, TargetKind.Crate, i);
                var origin = new GameObject("Gecici test esyasi " + (i + 1)).transform;
                origin.position = new Vector3(-3 + (i % 5) * 1.5f, 0, -6 + (i / 5) * 1.5f);
                world.testItemSpawns[i] = origin;
                Label("KASA " + (i + 1), box.transform.position + Vector3.up * 1.4f, .2f);
            }
            world.slots = new Transform[60];
            for (int rack = 0; rack < 6; rack++)
            {
                Vector3 center = new Vector3(-3.5f + (rack % 3) * 3.5f, 0, 3 + (rack / 3) * 5);
                for (int level = 0; level < 2; level++)
                {
                    float y = .65f + level * .95f;
                    Cube("Raf tahtasi", center + Vector3.up * y, new Vector3(2.8f, .12f, .9f), wood);
                    for (int column = 0; column < 5; column++)
                    {
                        int id = rack * 10 + level * 5 + column;
                        var slot = Cube("Raf yeri " + id, center + new Vector3(-1f + column * .5f, y + .085f, -.05f),
                            new Vector3(.46f, .045f, .65f), marker);
                        Target(slot, TargetKind.Slot, id);
                        world.slots[id] = slot.transform;
                    }
                }
                for (int side = -1; side <= 1; side += 2)
                    Cube("Raf destek", center + new Vector3(side * 1.35f, 1, .32f), new Vector3(.12f, 2, .12f), wood);
            }
            Label("ANA DEPO", new Vector3(0, 3, 12), .5f);

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
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            var preview = new GameObject("Lobby Camera", typeof(Camera), typeof(AudioListener));
            preview.transform.position = new Vector3(0, 11, -13);
            preview.transform.LookAt(new Vector3(0, 0, 3));
            preview.GetComponent<Camera>().backgroundColor = new Color(.08f, .12f, .18f);
            var menu = new GameObject("Session Menu").AddComponent<SessionMenu>();
            menu.network = network;
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
            Debug.Log("Bidwarss: Warehouse sahnesi hazir. Play > Oda kur. Client icin ayni sahnenin build'ini ac.");
        }

        static ItemCatalog.Entry Entry(string title, Color color, Vector3 size) =>
            new ItemCatalog.Entry { title = title, color = color, size = size };

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

        static void Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = size;
            label.transform.position = position;
        }
    }
}

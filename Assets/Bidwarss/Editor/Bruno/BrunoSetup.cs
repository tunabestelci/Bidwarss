using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bidwarss.Editor
{
    [InitializeOnLoad]
    public static class BrunoSetup
    {
        const string Root = "Assets/Bidwarss/Characters/Bruno";
        const string Resource = Root + "/Resources/Bruno";
        const string ModelPath = Resource + "/Bruno.fbx";
        const string PrefabPath = Resource + "/BrunoCharacter.prefab";
        static bool building;
        static readonly Dictionary<string,string> Palette = new Dictionary<string,string>
        {
            {"Fur","89786C"},{"FurLight","B7A18A"},{"Muzzle","CCB59A"},{"Hair","39343B"},
            {"Shirt","25454A"},{"Vest","C96528"},{"VestEdge","E5893A"},{"Seam","693925"},
            {"Pants","383A42"},{"PantsLight","494A51"},{"Boot","9D703E"},{"BootLight","BA8D52"},
            {"Sole","272B32"},{"Ink","282630"},{"Eyes","EEE4B7"},{"Iris","7B6B3B"},{"Metal","B79A65"}
            ,{"ToolSteel","9DAEB5"},{"ToolDark","303641"},{"ToolOrange","D86B29"},{"ToolEdge","DBE2D9"}
        };
        static BrunoSetup() { EditorApplication.delayCall += EnsureInstalled; }
        public static void EnsureInstalled()
        {
            if (building || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { if (!building) EditorApplication.delayCall += EnsureInstalled; return; }
            if ((AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/BoxCutterTool.prefab") == null || AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/PryBarTool.prefab") == null) && AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/BrunoHands.fbx") != null && AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/BrunoBoxCutter.fbx") != null && AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/BrunoPryBar.fbx") != null)
                Install();
        }

        [MenuItem("Bidwarss/Bruno/Build Character Assets")]
        public static void Install()
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var shader = Shader.Find("Bidwarss/BrunoCel");
            if (model == null || shader == null) { Debug.LogError("Bruno FBX veya BrunoCel shader bulunamadi. Import isleminin bitmesini bekleyin."); return; }
            building = true;
            GameObject root = null;
            try
            {
                Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
                var materials = new Dictionary<string,Material>();
                foreach (var pair in Palette)
                {
                    string path = Root + "/Materials/" + pair.Key + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = pair.Key };
                        ColorUtility.TryParseHtmlString("#" + pair.Value, out Color color);
                        mat.SetColor("_BaseColor", color);
                        // Very small facial features should not grow an ink shell over the eyes.
                        mat.SetFloat("_OutlineWidth", pair.Key == "Eyes" || pair.Key == "Iris" || pair.Key == "Ink" ? 0 : .0018f);
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    materials.Add(pair.Key, mat);
                }
                string controllerPath = Resource + "/Bruno.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
                string[] names = { "Idle", "Walk", "CarryIdle", "CarryWalk", "Greet", "BoxCut", "Pry" };
                foreach (string name in names)
                    if (!clips.Any(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal)))
                        throw new InvalidOperationException("Eksik Bruno animasyonu: " + name);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (string name in names)
                {
                    var clip = clips.First(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal));
                    var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                    state.motion = clip;
                    if (name == "Idle") machine.defaultState = state;
                }
                root = new GameObject("BrunoCharacter");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localScale = Vector3.one * .82f;
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string name = slots[i] != null ? slots[i].name.Split('.')[0] : "Fur";
                        if (!materials.TryGetValue(name, out var replacement)) throw new InvalidOperationException("Unknown Bruno material: " + name);
                        slots[i] = replacement;
                    }
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    // Includes the greeting hand. Avoid animation disappearing at screen edges.
                    renderer.localBounds = new Bounds(new Vector3(0,1.2f,0), new Vector3(3.4f,3.1f,2.4f));
                }
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                root.AddComponent<BrunoMotion>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                BuildHands(materials);
                BuildTool("BrunoBoxCutter","BoxCutterTool",materials);
                BuildTool("BrunoPryBar","PryBarTool",materials);
                AssetDatabase.SaveAssets();
                Debug.Log("Bruno hazir. Mevcut oyuncular yeni oturumda Bruno kullanir. Onizleme: Bidwarss > Bruno > Open Character Preview.");
            }
            finally { if (root != null) Object.DestroyImmediate(root); building = false; }
        }

        static void BuildHands(Dictionary<string,Material> materials)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Resource + "/BrunoHands.fbx");
            if (model == null) throw new InvalidOperationException("BrunoHands.fbx bulunamadi.");
            var root = new GameObject("BrunoFirstPersonHands");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(root.transform,false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    var slots = renderer.sharedMaterials;
                    for (int i=0;i<slots.Length;i++)
                        if (slots[i]!=null && materials.TryGetValue(slots[i].name.Split('.')[0],out var mat)) slots[i]=mat;
                    renderer.sharedMaterials=slots;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    renderer.receiveShadows=false;
                    bool left=renderer.name=="HandLeft";
                    renderer.transform.localPosition=new Vector3(left?-.25f:.25f,-.28f,.44f);
                    renderer.transform.localScale=Vector3.one*.65f;
                }
                PrefabUtility.SaveAsPrefabAsset(root,Resource+"/BrunoFirstPersonHands.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void BuildTool(string modelName,string prefabName,Dictionary<string,Material> materials)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Resource+"/"+modelName+".fbx");
            if(model==null)throw new InvalidOperationException(modelName+".fbx missing");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                        if(slots[i]!=null && materials.TryGetValue(slots[i].name.Split('.')[0],out var mat))slots[i]=mat;
                    renderer.sharedMaterials=slots;
                }
                foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name.StartsWith("ToolTip",StringComparison.Ordinal))t.name="ToolTip";
                PrefabUtility.SaveAsPrefabAsset(root,Resource+"/"+prefabName+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }

        [MenuItem("Bidwarss/Bruno/Open Character Preview")]
        public static void OpenPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureInstalled();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("Once Build Character Assets komutunu calistirin."); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var cameraObject = new GameObject("Bruno review camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();camera.backgroundColor = new Color(.76f,.74f,.70f);camera.clearFlags = CameraClearFlags.SolidColor;camera.fieldOfView = 33;camera.nearClipPlane = .03f;
            camera.transform.position = new Vector3(2,1.7f,4);camera.transform.LookAt(new Vector3(0,1,0));
            var light = new GameObject("Character key", typeof(Light)).GetComponent<Light>();light.type = LightType.Directional;light.intensity = 1.1f;light.transform.rotation = Quaternion.Euler(40,-35,0);light.shadows = LightShadows.Soft;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);floor.name = "Review plinth";floor.transform.localScale = new Vector3(2.6f,.025f,2.6f);floor.transform.position = new Vector3(0,-.025f,0);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/FurLight.mat");floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var review = new GameObject("Review controls").AddComponent<BrunoPreview>();review.character = character.GetComponent<BrunoMotion>();review.reviewCamera = camera;
            Directory.CreateDirectory(Root + "/Preview");AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath(Root + "/Preview/BrunoReview.unity"));
            Selection.activeGameObject = character;
        }
    }
}

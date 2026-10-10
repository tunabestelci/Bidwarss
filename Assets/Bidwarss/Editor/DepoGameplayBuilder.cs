using System;
using System.Collections.Generic;
using System.Linq;
using DepoLevel;
using DepoLevel.EditorTools;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Bidwarss.Editor
{
    public static class DepoGameplayBuilder
    {
        [MenuItem("Bidwarss/Build Uploaded Depot (Co-op)")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)return;
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            const string template="Assets/Bidwarss/GeneratedV2/Warehouse.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(template)==null)PrototypeBuilder.Build();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(template)==null)throw new InvalidOperationException("Temel Warehouse sahnesi bulunamadi.");
            var scene=EditorSceneManager.OpenScene(template,OpenSceneMode.Single);
            System.IO.Directory.CreateDirectory("Assets/Bidwarss/GeneratedDepot");AssetDatabase.Refresh();
            string output=AssetDatabase.GenerateUniqueAssetPath("Assets/Bidwarss/GeneratedDepot/Bidwarss_Depot.unity");
            // Always work in a copy. Existing user scenes are never overwritten.
            if(!EditorSceneManager.SaveScene(scene,output,true))throw new InvalidOperationException("Sahne kopyalanamadi.");
            scene=EditorSceneManager.OpenScene(output,OpenSceneMode.Single);
            var world=scene.GetRootGameObjects().Select(g=>g.GetComponent<WarehouseWorld>()).First(w=>w!=null);
            var session=scene.GetRootGameObjects().Select(g=>g.GetComponent<SessionMenu>()).First(s=>s!=null);
            foreach(var root in scene.GetRootGameObjects())
                if(root!=world.gameObject&&root!=session.gameObject&&root.GetComponent<NetworkManager>()==null&&root.GetComponent<Camera>()==null&&root.GetComponent<Light>()==null)
                    Object.DestroyImmediate(root);
            var level=DepoLevelBuilder.BuildForBidwarss(out var nodes);
            var layout=JsonUtility.FromJson<DepoLayoutFile>(DepoLevelBuilder.LoadLayout().text);
            var containers=level.GetComponentsInChildren<DepoContainer>().OrderBy(c=>c.containerNo).ToArray();
            if(containers.Length!=10)throw new InvalidOperationException("On konteyner bekleniyordu.");
            world.crates=new Transform[10];world.itemOrigins=new Transform[10];world.lids=new Transform[10];
            for(int i=0;i<containers.Length;i++)
            {
                var container=containers[i].transform;world.crates[i]=container;
                var motion=container.gameObject.AddComponent<ContainerDoorMotion>();motion.world=world;motion.crateIndex=i;
                motion.left=PrepareDoor(container.Find("Kapi_Sol"),container,-3.6f,i);
                motion.right=PrepareDoor(container.Find("Kapi_Sag"),container,3.6f,i);
                world.itemOrigins[i]=Marker("Loot origin",container,new Vector3(4,.16f,-1.2f));
            }
            // One existing rack = ten existing shelf cells, not ten miniatures squeezed into one cell.
            var rackDefs=layout.nodes.Where(n=>n.prefab=="Raf_Standart").OrderBy(n=>n.px*n.px+n.pz*n.pz).ThenBy(n=>n.id,StringComparer.Ordinal).Take(world.totalGroups).ToArray();
            if(rackDefs.Length!=world.totalGroups)throw new InvalidOperationException("Yeterli raf yok.");
            world.slots=new Transform[rackDefs.Length];world.stackLabels=new TextMesh[rackDefs.Length];
            for(int i=0;i<rackDefs.Length;i++)
            {
                var rack=nodes[rackDefs[i].id];
                var cells=rack.GetComponentsInChildren<DepoShelfSlot>().OrderBy(s=>s.level).ThenBy(s=>s.transform.localPosition.x).ToArray();
                if(cells.Length!=10)throw new InvalidOperationException("Raf tam on hucre icermeli: "+rack.name);
                var anchor=Marker("Bidwarss stack "+i,rack,Vector3.zero);world.slots[i]=anchor;
                var stack=anchor.gameObject.AddComponent<StackLayout>();stack.cells=new Transform[10];
                for(int c=0;c<10;c++)
                {
                    stack.cells[c]=Marker("Item "+c,anchor,Vector3.zero);
                    stack.cells[c].SetPositionAndRotation(cells[c].BottomCenter+Vector3.up*.19f,cells[c].transform.rotation);
                }
                var target=rack.gameObject.AddComponent<InteractionTarget>();target.kind=TargetKind.Slot;target.id=i;
                var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="E - Istifle";plate.transform.SetParent(rack,false);
                plate.transform.localPosition=new Vector3(0,1.32f,-.44f);plate.transform.localScale=new Vector3(2.4f,.09f,.06f);
                plate.GetComponent<Renderer>().sharedMaterial=world.itemMaterial;
                world.stackLabels[i]=Label("ISTIF "+(i+1),rack,new Vector3(0,2.65f,-.46f),.1f);
            }
            session.spawnPoints=new Transform[4];
            var spawn=level.GetComponentInChildren<DepoPlayerSpawn>().transform;
            for(int i=0;i<4;i++)
            {
                session.spawnPoints[i]=Marker("Co-op spawn "+(i+1),level.transform,spawn.position+new Vector3(-2.4f+1.6f*i,.1f,0));
            }
            world.recoveryOrigin=Marker("Disconnected items",containers[9].transform,new Vector3(12,.16f,-1.5f));
            world.recoveryOrigin.localRotation=Quaternion.Euler(0,-90,0);world.recoveryColumns=6;
            Label("KURTARILAN ESYALAR",containers[9].transform,new Vector3(11,1.8f,0),.11f).transform.localRotation=Quaternion.Euler(0,90,0);
            session.lobbyCamera.transform.SetPositionAndRotation(new Vector3(0,5,-4),Quaternion.Euler(12,0,0));
            session.lobbyCamera.farClipPlane=240;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.65f,.7f);
            foreach(var root in scene.GetRootGameObjects())
            {
                var light=root.GetComponent<Light>();if(light!=null){light.intensity=.65f;light.shadows=LightShadows.None;}
            }
            // Baked fixtures are retained for optional final lighting, but the first run needs no bake.
            Physics.SyncTransforms();
            foreach(var point in session.spawnPoints)
                if(Physics.CheckCapsule(point.position+Vector3.up*.35f,point.position+Vector3.up*1.45f,.29f,~0,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Spawn engelle cakisiyor: "+point.name);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var buildScenes=EditorBuildSettings.scenes.Where(s=>s.path!=output).ToList();buildScenes.Insert(0,new EditorBuildSettingsScene(output,true));EditorBuildSettings.scenes=buildScenes.ToArray();
            Selection.activeGameObject=world.gameObject;
            Debug.Log("Leaderboard rules hash: "+world.Rules.Fingerprint());
            Debug.Log("Bidwarss depo hazir: "+output+" | 10 konteyner, "+world.totalGroups+" aktif raf, 4 spawn. Play > Oda Kur.");
        }
        static Transform PrepareDoor(Transform door,Transform container,float hingeZ,int id)
        {
            if(door==null)throw new InvalidOperationException("Konteyner kapisi eksik.");
            var children=door.Cast<Transform>().ToArray();foreach(var child in children)child.SetParent(container,true);
            door.localPosition=new Vector3(-.08f,0,hingeZ);
            foreach(var child in children)child.SetParent(door,true);
            foreach(var t in door.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            var body=door.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var target=door.gameObject.AddComponent<InteractionTarget>();target.kind=TargetKind.Crate;target.id=id;
            door.localRotation=Quaternion.Euler(0,180,0);return door;
        }
        static Transform Marker(string name,Transform parent,Vector3 local)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
        static TextMesh Label(string text,Transform parent,Vector3 local,float size)
        {
            var t=Marker(text,parent,local);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=DepoLevelBuilder.WorldTextMaterial(label.font);
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=64;label.characterSize=size;label.color=Color.white;return label;
        }
    }
}

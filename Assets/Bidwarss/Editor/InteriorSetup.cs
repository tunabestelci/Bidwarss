using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bidwarss.Editor
{
    public static class InteriorSetup
    {
        [MenuItem("Bidwarss/Interior/Export Current Scene For Setup")]
        public static void ExportScene()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path)){Debug.LogError("Once sahneyi kaydet.");return;}
            string path=EditorUtility.SaveFilePanel("Export interior and referenced models","","Bidwarss_Interior","unitypackage");
            if(string.IsNullOrEmpty(path))return;
            AssetDatabase.ExportPackage(scene.path,path,ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);
            Debug.Log("Bidwarss: sahne ve bagli modeller disa aktarildi: "+path);
        }

        [MenuItem("Bidwarss/Interior/Add Mesh Colliders To Selection")]
        public static void AddColliders()
        {
            var root=Selection.activeGameObject;
            if(root==null || !root.scene.IsValid() || EditorApplication.isPlaying)
            {Debug.LogError("Play kapaliyken Hierarchy'den ic mekanin ana nesnesini sec.");return;}
            int added=0,skipped=0;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Prepare interior collisions");
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null || filter.GetComponentInParent<Unity.Netcode.NetworkObject>()!=null ||
                    filter.GetComponentInParent<Rigidbody>()!=null || filter.GetComponentInParent<Animator>()!=null ||
                    filter.GetComponentInParent<Animation>()!=null || filter.GetComponentInParent<InteractionTarget>()!=null)
                {skipped++;continue;}
                // Existing parent or child collision setups remain authoritative.
                if(filter.GetComponentInParent<Collider>()!=null || filter.GetComponentInChildren<Collider>()!=null)
                {skipped++;continue;}
                var col=Undo.AddComponent<MeshCollider>(filter.gameObject);col.sharedMesh=filter.sharedMesh;col.convex=false;added++;
            }
            Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"Bidwarss: {added} sabit mesh collider eklendi, {skipped} nesne korundu/atlandi. Kapi bosluklari mesh geometrisini takip eder. Sahneyi kaydet.");
        }

        [MenuItem("Bidwarss/Interior/Create Playable Copy Of Current Scene")]
        public static void CreatePlayableCopy()
        {
            if(EditorApplication.isPlaying)return;
            var original=SceneManager.GetActiveScene();
            if(!original.IsValid() || string.IsNullOrEmpty(original.path))
            {Debug.LogError("Once ic mekan sahneni kaydet.");return;}
            foreach(var root in original.GetRootGameObjects())
                if(root.GetComponentInChildren<SessionMenu>(true)!=null)
                {Debug.LogError("Bu sahnede oyun sistemi zaten var. Tekrar eklenmedi.");return;}
            const string template="Assets/Bidwarss/GeneratedV2/Warehouse.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(template)==null)
            {Debug.LogError("Once Bidwarss > Create Gameplay Scene ile temel sahneyi olustur; sonra ic mekan sahneni yeniden ac.");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string path=AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.GetDirectoryName(original.path)+"/Bidwarss_Playable.unity");
            // Save-as-copy retains all interior references and preserves the original scene.
            if(!EditorSceneManager.SaveScene(original,path,true))return;
            var target=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var root in target.GetRootGameObjects())
            {
                // Gameplay supplies its own lobby and player cameras; avoid duplicate listeners.
                foreach(var camera in root.GetComponentsInChildren<Camera>(true))camera.enabled=false;
                foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            }
            var gameplay=EditorSceneManager.OpenScene(template,OpenSceneMode.Additive);
            string[] shell={"Depo zemini","Sol duvar","Sag duvar","Arka duvar","Giris duvari","Light"};
            foreach(var root in gameplay.GetRootGameObjects())
            {
                if(System.Array.IndexOf(shell,root.name)>=0)continue;
                SceneManager.MoveGameObjectToScene(root,target);
            }
            EditorSceneManager.CloseScene(gameplay,true);SceneManager.SetActiveScene(target);
            EditorSceneManager.SaveScene(target);
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s=>s.path==path);scenes.Insert(0,new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
            Debug.Log("Bidwarss: oyun sistemleri ic mekanin kopyasina eklendi: "+path+". Kasalar/paletler ornek koordinatlarda; mekanina gore yerlestir. Collider icin mekan kokunu secip Add Mesh Colliders To Selection kullan. Spawn konumunu Session Menu > Spawn Points ile ayarla.");
        }
    }
}

using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.Editor
{
    public static class StarterSceneBuilder
    {
        [MenuItem("Dawn Guard/1 - Create starter scene")]
        public static void CreateScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string folder="Assets/DawnGuard/Generated";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/DawnGuard","Generated");
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalog>(folder+"/DemoCatalog.asset");
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<GameCatalog>(); catalog.rules=DemoRules.Create();
                AssetDatabase.CreateAsset(catalog,folder+"/DemoCatalog.asset");
            }
            catalog.rules.Validate();
            if(catalog.surfaceTemplate==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null) shader=Shader.Find("Standard");
                catalog.surfaceTemplate=new Material(shader);
                AssetDatabase.CreateAsset(catalog.surfaceTemplate,AssetDatabase.GenerateUniqueAssetPath(folder+"/Surface.mat"));
            }
            if(catalog.tracerMaterial==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(shader==null) shader=Shader.Find("Sprites/Default");
                catalog.tracerMaterial=new Material(shader);
                AssetDatabase.CreateAsset(catalog.tracerMaterial,AssetDatabase.GenerateUniqueAssetPath(folder+"/Tracer.mat"));
            }
            EditorUtility.SetDirty(catalog);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Dawn Guard").AddComponent<GameRoot>(); root.catalog=catalog;
            // Unique scene path never overwrites a user's existing scene.
            string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/DawnGuardDemo.unity");
            EditorSceneManager.SaveScene(scene,path); AssetDatabase.SaveAssets();
            Selection.activeObject=root.gameObject;
            Debug.Log("Dawn Guard scene created: "+path+". Press Play. Existing catalog was preserved.");
        }
    }
}

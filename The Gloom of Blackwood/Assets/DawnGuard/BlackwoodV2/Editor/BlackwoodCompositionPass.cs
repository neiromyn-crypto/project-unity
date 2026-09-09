using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    // Phase 1 only: edit the existing scene. No catalog, AI, economy or lighting changes.
    public static class BlackwoodCompositionPass
    {
        const string ScenePath="Assets/DawnGuard/BlackwoodV2/Generated/UserMap/BlackwoodUserMap.unity";
        const string Folder="Assets/DawnGuard/BlackwoodV2/Generated/UserMap";
        [MenuItem("Dawn Guard/8 - Compose existing BlackwoodUserMap (phase 1)")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before editing the composition.");
            var active=EditorSceneManager.GetActiveScene();
            if(active.isDirty) throw new InvalidOperationException("Save current scene changes before applying the composition.");
            Directory.CreateDirectory("IntegrationEvidence/Phase1");
            if(!File.Exists("IntegrationEvidence/Phase1/BlackwoodUserMap.before.unity"))
                File.Copy(ScenePath,"IntegrationEvidence/Phase1/BlackwoodUserMap.before.unity");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BlackwoodRoot>()).Single();
            if(root.sceneEnvironment==null || root.pinePrefab==null || root.rubblePrefab==null)
                throw new InvalidOperationException("The existing scene must have the user's environment prefabs.");
            root.cameraSize=7.6f; root.cameraElevation=38f; root.cameraFocus=new Vector3(8,0,7.8f);
            string pinePath=AssetDatabase.GetAssetPath(root.pinePrefab);
            string rubblePath=AssetDatabase.GetAssetPath(root.rubblePrefab);
            var children=root.sceneEnvironment.Cast<Transform>().ToArray();
            int blocker=0;
            foreach(var child in children)
            {
                string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if(source==pinePath || child.name=="Phase 1 composition") UnityEngine.Object.DestroyImmediate(child.gameObject);
                else if(source==rubblePath)
                {
                    // Existing GridBoard obstacle cells remain exactly where the core puts them.
                    child.localRotation=Quaternion.Euler(0,(blocker%2)*180-17+(blocker*11)%34,0);
                    child.localScale=Vector3.one*(.78f+(blocker%4)*.015f); blocker++;
                }
            }
            var composition=new GameObject("Phase 1 composition").transform;
            composition.SetParent(root.sceneEnvironment,false);
            var forest=new GameObject("Irregular forest clusters").transform; forest.SetParent(composition,false);
            Vector2[] trees={
                new Vector2(-1.65f,1.4f),new Vector2(-3.45f,.3f),new Vector2(-3.1f,3.8f),new Vector2(-1.55f,5.1f),
                new Vector2(-2.2f,9.2f),new Vector2(-4.05f,8.0f),new Vector2(-3.3f,11.3f),new Vector2(-1.5f,13.2f),new Vector2(-3.2f,15.4f),
                new Vector2(17.7f,.7f),new Vector2(19.5f,2.1f),new Vector2(18.2f,4.5f),new Vector2(20.4f,5.6f),
                new Vector2(17.6f,8.2f),new Vector2(19.25f,10.0f),new Vector2(17.45f,12.7f),new Vector2(20.2f,13.6f),new Vector2(18.7f,15.6f),
                new Vector2(.4f,17.8f),new Vector2(2.4f,18.9f),new Vector2(4.2f,17.7f),new Vector2(3.1f,21.0f),
                new Vector2(11.3f,18.7f),new Vector2(13.4f,17.9f),new Vector2(15.6f,19.4f),new Vector2(12.2f,21.0f),
                new Vector2(-1.4f,-2.1f),new Vector2(18.6f,-2.1f)
            };
            for(int i=0;i<trees.Length;i++)
                Place(root.pinePrefab,forest,trees[i],.88f+(i*7%9)*.045f,(i*137+19)%360);
            var stones=new GameObject("Forest edge stones").transform; stones.SetParent(composition,false);
            Vector2[] rocks={new Vector2(-.9f,3.0f),new Vector2(-1.3f,3.9f),new Vector2(-.85f,11.6f),
                new Vector2(17.0f,6.4f),new Vector2(17.5f,7.1f),new Vector2(17.0f,14.4f),
                new Vector2(1.3f,16.9f),new Vector2(2.3f,17.1f),new Vector2(12.3f,16.9f),
                new Vector2(4.5f,-1.2f),new Vector2(5.3f,-1.7f),new Vector2(11.9f,-1.15f)};
            for(int i=0;i<rocks.Length;i++) Place(root.rubblePrefab,stones,rocks[i],.8f+(i%3)*.15f,i*73);
            var ground=root.sceneEnvironment.Find("Map ground").GetComponent<Renderer>();
            var shader=Shader.Find("Blackwood/Clearing Ground");
            if(shader==null) throw new InvalidOperationException("Clearing shader did not compile.");
            string path=Folder+"/ClearingGround.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(shader) {name="ClearingGround",enableInstancing=true};
                var source=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/MapGround.mat");
                if(source!=null && source.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap",new Vector2(12,12));
                AssetDatabase.CreateAsset(material,path);
            }
            ground.sharedMaterial=material;
            EditorUtility.SetDirty(root); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate(root);
            File.WriteAllText("IntegrationEvidence/Phase1/composition.txt","Camera 9.6 -> 7.6: apparent width +26.3%; elevation 50.5 -> 38 degrees.\n28 irregular pines, 12 edge rocks; 13 original blocked cells preserved.\nExisting scene, assets, catalog, save profile, lighting and grid coordinates retained.\n");
        }
        static void Place(GameObject prefab,Transform parent,Vector2 cell,float scale,float yaw)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.transform.localPosition=new Vector3(cell.x,0,cell.y);
            go.transform.localRotation=Quaternion.Euler(0,yaw,0); go.transform.localScale=Vector3.one*scale;
        }
        static void Validate(BlackwoodRoot root)
        {
            foreach(var r in root.sceneEnvironment.Find("Phase 1 composition").GetComponentsInChildren<Renderer>())
            {
                var b=r.bounds;
                if(b.min.x<16 && b.max.x>0 && b.min.z<16 && b.max.z>0)
                    throw new InvalidOperationException("Decoration overlaps a playable tile: "+r.name+" "+b);
            }
            if(root.catalog.rules.blockedCells.Length!=13 || root.catalog.rules.width!=16 || root.catalog.rules.depth!=16)
                throw new InvalidOperationException("Board layout changed unexpectedly.");
        }
    }
}

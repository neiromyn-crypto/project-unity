using System;
using System.Collections.Generic;
using System.Linq;
using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodUserAssetsBuilder
    {
        public const string SetPath="Assets/DawnGuard/BlackwoodV2/UserAssets.asset";
        private const string Base="Assets/DawnGuard/BlackwoodV2";

        [MenuItem("Dawn Guard/5 - Scan my assets")]
        public static void Scan()
        {
            var set=AssetDatabase.LoadAssetAtPath<BlackwoodAssetSet>(SetPath);
            if(set==null) { set=ScriptableObject.CreateInstance<BlackwoodAssetSet>(); AssetDatabase.CreateAsset(set,SetPath); }
            if(set.sourceCatalog==null)
            {
                var old=UnityEngine.Object.FindFirstObjectByType<GameRoot>();
                var current=UnityEngine.Object.FindFirstObjectByType<BlackwoodRoot>();
                set.sourceCatalog=old!=null ? old.catalog : current!=null ? current.catalog : AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/DawnGuard/Generated/DemoCatalog.asset");
            }
            set.shelter=Resolve(set.shelter,"Shelter"); set.camp=Resolve(set.camp,"Camp");
            set.generator=Resolve(set.generator,"Generator"); set.wall=Resolve(set.wall,"Wall");
            set.gun=Resolve(set.gun,"Gun"); set.lab=Resolve(set.lab,"Lab"); set.tesla=Resolve(set.tesla,"Tesla");
            set.drone=Resolve(set.drone,"Drone"); set.pine=Resolve(set.pine,"Pine");
            set.rubble=Resolve(set.rubble,"Rubble"); set.pad=Resolve(set.pad,"Pad");
            if(set.sourceCatalog!=null && set.sourceCatalog.enemies!=null)
                foreach(var b in set.sourceCatalog.enemies)
                {
                    if(b==null || b.prefab==null) continue;
                    if(b.definitionId=="walker" && set.zombie==null) set.zombie=b.prefab;
                    if(b.definitionId=="runner" && set.runner==null) set.runner=b.prefab;
                    if(b.definitionId=="brute" && set.brute==null) set.brute=b.prefab;
                }
            set.zombie=Resolve(set.zombie,"Zombie"); set.runner=Resolve(set.runner,"Runner"); set.brute=Resolve(set.brute,"Brute");
            if(set.zombie==null)
            {
                var candidates=new HashSet<GameObject>();
                foreach(var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    var instance=PrefabUtility.GetOutermostPrefabInstanceRoot(a.gameObject); if(instance==null) continue;
                    var source=PrefabUtility.GetCorrespondingObjectFromSource(instance); if(source==null) continue;
                    string name=(source.name+" "+AssetDatabase.GetAssetPath(source)).ToLowerInvariant();
                    if(name.Contains("zombie") || name.Contains("зомби") || name.Contains("walker")) candidates.Add(source);
                }
                if(candidates.Count==1) set.zombie=candidates.First();
                else if(candidates.Count>1) Debug.LogWarning("Several scene zombie prefabs found. Assign the intended one to UserAssets.asset.");
            }
            if(set.menuBackdrop==null) set.menuBackdrop=AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Art/MenuBackdrop.png");
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets(); Selection.activeObject=set;
            Debug.Log("Asset scan complete. Review UserAssets.asset. Existing assignments were preserved; ambiguous folders were not guessed.");
        }
        private static GameObject Resolve(GameObject current,string role)
        {
            if(current!=null) return current;
            string folder="Assets/GameArt/"+role;
            bool dedicated=AssetDatabase.IsValidFolder(folder);
            if(!dedicated) folder="Assets/GameArt";
            if(!AssetDatabase.IsValidFolder(folder)) return null;
            var paths=AssetDatabase.FindAssets("t:GameObject",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
            if(!dedicated) paths=paths.Where(p=>MatchesRole(System.IO.Path.GetFileNameWithoutExtension(p),role)).ToArray();
            var prefabs=paths.Where(p=>p.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)).ToArray();
            var choices=prefabs.Length>0 ? prefabs : paths;
            if(choices.Length==1) return AssetDatabase.LoadAssetAtPath<GameObject>(choices[0]);
            if(choices.Length>1) Debug.LogWarning("Ambiguous asset role "+role+": "+string.Join(", ",choices)+". Assign UserAssets.asset explicitly.");
            return null;
        }

        private static bool MatchesRole(string name,string role)
        {
            string key=new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());
            var aliases=new Dictionary<string,string[]> {
                {"Shelter",new[]{"shelter","убежище"}}, {"Camp",new[]{"camp","workercamp","лагерь"}},
                {"Generator",new[]{"generator","powertower","генератор","электровышка"}},
                {"Wall",new[]{"wall","стена"}}, {"Gun",new[]{"gun","machinegun","turret","пулемёт","пулемет"}},
                {"Lab",new[]{"lab","laboratory","лаборатория"}}, {"Tesla",new[]{"tesla","тесла"}},
                {"Drone",new[]{"drone","дрон"}}, {"Pine",new[]{"pine","tree","сосна","дерево"}},
                {"Rubble",new[]{"rubble","rocks","камни"}}, {"Pad",new[]{"pad","dronepad","площадка"}},
                {"Zombie",new[]{"zombie","walker","зомби"}}, {"Runner",new[]{"runner","бегун"}}, {"Brute",new[]{"brute","громила"}}
            };
            return aliases[role].Contains(key);
        }

        [MenuItem("Dawn Guard/6 - Build map with my assets")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before assembling the map.");
            var set=AssetDatabase.LoadAssetAtPath<BlackwoodAssetSet>(SetPath);
            if(set==null) throw new InvalidOperationException("Run Dawn Guard/5 - Scan my assets first.");
            var rules=BlackwoodBalance.Create(); var missing=new List<string>();
            foreach(var d in rules.buildings) if(set.Building(d.id)==null) missing.Add(d.id);
            if(set.drone==null) missing.Add("drone"); if(set.zombie==null) missing.Add("zombie");
            if(missing.Count>0) throw new InvalidOperationException("Missing required user assets: "+string.Join(", ",missing)+". Assign UserAssets.asset. No placeholder scene was created.");
            BlackwoodChecks.Run();
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(!AssetDatabase.IsValidFolder(Base+"/Generated")) AssetDatabase.CreateFolder(Base,"Generated");
            string folder=AssetDatabase.GenerateUniqueAssetPath(Base+"/Generated/UserMap");
            AssetDatabase.CreateFolder(Base+"/Generated",System.IO.Path.GetFileName(folder));
            var catalog=ScriptableObject.CreateInstance<GameCatalog>(); catalog.rules=rules;
            var buildings=new List<VisualBinding>();
            foreach(var d in rules.buildings)
                buildings.Add(new VisualBinding {definitionId=d.id,prefab=Wrap(set.Building(d.id),d.id,folder,d.width*.9f,d.depth*.9f,d.id=="wall" ? 1.0f : d.width==1 ? 1.7f : 2.3f,set.fitModelsToGrid,false)});
            catalog.buildings=buildings.ToArray();
            catalog.dronePrefab=Wrap(set.drone,"drone",folder,1.3f,1.3f,.65f,set.fitModelsToGrid,false);
            catalog.enemies=new[] {
                new VisualBinding {definitionId="walker",prefab=Wrap(set.zombie,"walker",folder,.7f,.7f,1.35f,set.fitModelsToGrid,true)},
                new VisualBinding {definitionId="runner",prefab=Wrap(set.runner!=null ? set.runner : set.zombie,"runner",folder,.6f,.6f,1.25f,set.fitModelsToGrid,true)},
                new VisualBinding {definitionId="brute",prefab=Wrap(set.brute!=null ? set.brute : set.zombie,"brute",folder,1f,1f,2f,set.fitModelsToGrid,true)}
            };
            if(set.runner==null || set.brute==null) Debug.LogWarning("Missing runner/brute visuals reuse the user's zombie at another height. Gameplay stats remain distinct.");
            var shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null) shader=Shader.Find("Standard");
            if(shader==null) throw new InvalidOperationException("URP or Built-in shader required.");
            var ground=set.groundMaterial!=null ? new Material(set.groundMaterial) : new Material(shader) {color=new Color(.31f,.37f,.32f)};
            AssetDatabase.CreateAsset(ground,folder+"/MapGround.mat");
            catalog.surfaceTemplate=new Material(shader) {color=Color.white}; AssetDatabase.CreateAsset(catalog.surfaceTemplate,folder+"/Surface.mat");
            if(set.sourceCatalog!=null) catalog.tracerMaterial=set.sourceCatalog.tracerMaterial;
            AssetDatabase.CreateAsset(catalog,folder+"/BlackwoodCatalog.asset");
            var pine=set.pine==null ? null : Wrap(set.pine,"pine",folder,1.5f,1.5f,3f,set.fitModelsToGrid,false);
            var rock=set.rubble==null ? null : Wrap(set.rubble,"rubble",folder,.95f,.95f,.8f,set.fitModelsToGrid,false);
            var pad=set.pad==null ? null : Wrap(set.pad,"pad",folder,1.8f,1.8f,.35f,set.fitModelsToGrid,false);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Blackwood V2 - User assets").AddComponent<BlackwoodRoot>();
            root.catalog=catalog; root.menuBackdrop=set.menuBackdrop; root.pinePrefab=pine; root.rubblePrefab=rock; root.padPrefab=pad;
            root.sceneEnvironment=new GameObject("Map environment").transform; root.sceneEnvironment.SetParent(root.transform,false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Map ground"; floor.transform.SetParent(root.sceneEnvironment,false);
            floor.transform.localPosition=new Vector3(8,-.15f,8); floor.transform.localScale=new Vector3(36,.3f,36); floor.GetComponent<Renderer>().sharedMaterial=ground;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            foreach(var cell in rules.blockedCells)
            {
                if(rock!=null) Place(rock,root.sceneEnvironment,new Vector3(cell.x+.5f,0,cell.z+.5f));
                else
                {
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Cube); marker.name="Missing rubble visual"; marker.transform.SetParent(root.sceneEnvironment,false);
                    marker.transform.localPosition=new Vector3(cell.x+.5f,.4f,cell.z+.5f); marker.transform.localScale=new Vector3(.95f,.8f,.95f);
                    marker.GetComponent<Renderer>().sharedMaterial=catalog.surfaceTemplate; UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                }
            }
            if(rock==null) Debug.LogWarning("Rubble model missing: blocked cells use visible markers. Replace the optional rubble reference for finished art.");
            if(pine!=null) for(int i=0;i<7;i++)
            {
                Place(pine,root.sceneEnvironment,new Vector3(-2,0,-1+i*3)); Place(pine,root.sceneEnvironment,new Vector3(18,0,-1+i*3));
                if(i>0 && i<6) Place(pine,root.sceneEnvironment,new Vector3(i*3-1,0,18));
            }
            if(pad!=null) Place(pad,root.sceneEnvironment,new Vector3(14,0,-2));
            root.startingPreview=new GameObject("Editor starting preview - removed in Play"); root.startingPreview.transform.SetParent(root.transform,false);
            var initial=new GameSession(rules);
            foreach(var b in initial.Buildings)
            {
                var def=rules.Building(b.definitionId); var prefab=buildings.First(x=>x.definitionId==b.definitionId).prefab;
                Place(prefab,root.startingPreview.transform,new Vector3(b.cell.x+def.width*.5f,0,b.cell.z+def.depth*.5f));
            }
            Place(catalog.dronePrefab,root.startingPreview.transform,new Vector3(initial.DroneX,2.2f,initial.DroneZ));
            var light=new GameObject("Editor preview light").AddComponent<Light>(); light.transform.SetParent(root.startingPreview.transform,false);
            light.type=LightType.Directional; light.intensity=1.1f; light.transform.rotation=Quaternion.Euler(50,-25,0);
            EditorSceneManager.SaveScene(scene,folder+"/BlackwoodUserMap.unity"); AssetDatabase.SaveAssets(); Selection.activeGameObject=root.gameObject;
            if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(8,0,8),Quaternion.Euler(55,0,0),18);
            Debug.Log("User map assembled: "+folder+"/BlackwoodUserMap.unity. Press Play. Review Animator parameters and any behaviour warnings before claiming integration complete.");
        }
        private static GameObject Place(GameObject prefab,Transform parent,Vector3 position)
        { var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent); go.transform.localPosition=position; return go; }
        private static GameObject Wrap(GameObject source,string id,string folder,float width,float depth,float height,bool fit,bool enemy)
        {
            var wrapper=new GameObject("User_"+id);
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,wrapper.transform); visual.name="Visual";
                foreach(var c in visual.GetComponentsInChildren<Collider>(true)) c.enabled=false;
                foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.useGravity=false; }
                foreach(var component in visual.GetComponentsInChildren<Behaviour>(true))
                {
                    if(component==null) { Debug.LogWarning("Missing script in user asset "+id+". Repair a copy before final integration."); continue; }
                    if(component.GetType().FullName=="UnityEngine.AI.NavMeshAgent") component.enabled=false;
                    else if(component is MonoBehaviour && !(component is BlackwoodModel) && !(component is BlackwoodEnemyView))
                        Debug.LogWarning("Review behaviour on user asset "+id+": "+component.GetType().FullName+". Do not run two movement/damage controllers.");
                }
                var renderers=visual.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                if(renderers.Length==0) throw new InvalidOperationException("No mesh renderer found in "+AssetDatabase.GetAssetPath(source));
                if(enemy)
                {
                    var adapter=wrapper.AddComponent<BlackwoodEnemyView>();
                    adapter.animator=visual.GetComponentInChildren<Animator>(true);
                    BlackwoodUserAssetSetup.ConfigureEnemy(adapter,folder,id);
                }
                Bounds bounds=BlackwoodUserAssetSetup.MeshBounds(renderers);
                if(fit)
                {
                    float factor=height/Mathf.Max(.001f,bounds.size.y);
                    if(!enemy) factor=Mathf.Min(factor,width/Mathf.Max(.001f,bounds.size.x),depth/Mathf.Max(.001f,bounds.size.z));
                    visual.transform.localScale*=factor;
                    bounds=BlackwoodUserAssetSetup.MeshBounds(renderers);
                }
                visual.transform.position-=id=="drone" ? bounds.center : new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                if(enemy)
                {
                    var old=visual.GetComponentInChildren<BlackwoodEnemyView>(true);
                    var view=wrapper.GetComponent<BlackwoodEnemyView>();
                    if(old!=null) { EditorUtility.CopySerialized(old,view); old.enabled=false; }
                    view.animator=visual.GetComponentInChildren<Animator>(true);
                    if(view.animator!=null) view.animator.applyRootMotion=false;
                    else Debug.LogWarning("Zombie "+id+" has no Animator. Visual will move but clips need setup.");
                }
                else if(id=="gun" || id=="drone")
                {
                    var model=wrapper.AddComponent<BlackwoodModel>(); var all=visual.GetComponentsInChildren<Transform>(true);
                    if(id=="gun") model.yaw=all.FirstOrDefault(t=>t.name=="Yaw" || t.name=="TurretHead");
                    else { var rotors=all.Where(t=>t.name.StartsWith("Rotor",StringComparison.OrdinalIgnoreCase) && !t.name.ToLowerInvariant().Contains("guard")).ToArray(); if(rotors.Length==4) model.rotors=rotors; }
                }
                string path=folder+"/User_"+id+".prefab";
                var result=PrefabUtility.SaveAsPrefabAsset(wrapper,path); if(result==null) throw new InvalidOperationException("Could not save "+path); return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
    }
}

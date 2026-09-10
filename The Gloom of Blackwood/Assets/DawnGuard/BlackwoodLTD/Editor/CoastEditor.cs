using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using DawnGuard.Unity;

namespace DawnGuard.BlackwoodLTD.Editor
{
    [InitializeOnLoad]
    public static class CoastEditor
    {
        public const string Folder="Assets/DawnGuard/BlackwoodLTD";
        public const string ScenePath=Folder+"/Scenes/BlackwoodCoast.unity";
        public const string Evidence="IntegrationEvidence/Coast";
        static double next;
        static CoastEditor()
        {
            EditorApplication.update+=Poll;
            CompilationPipeline.compilationStarted+=_=>{Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/compile-errors.txt","");File.WriteAllText(Evidence+"/compile-warnings.txt","");};
            CompilationPipeline.assemblyCompilationFinished+=(path,messages)=>{Directory.CreateDirectory(Evidence);foreach(var m in messages)File.AppendAllText(Evidence+(m.type==CompilerMessageType.Error?"/compile-errors.txt":"/compile-warnings.txt"),m.message+"\n");};
            CompilationPipeline.compilationFinished+=_=>{string errors=File.Exists(Evidence+"/compile-errors.txt")?File.ReadAllText(Evidence+"/compile-errors.txt"):"";File.WriteAllText(Evidence+"/compilation-result.txt",(errors.Length==0?"PASS":"FAIL")+" "+DateTime.UtcNow.ToString("O"));};
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence+"/status.txt",DateTime.UtcNow.ToString("O")+" play="+EditorApplication.isPlaying+" scene="+EditorSceneManager.GetActiveScene().path);
            string file=Evidence+"/command.txt";if(!File.Exists(file))return;string cmd=File.ReadAllText(file).Trim();File.Delete(file);
            try
            {
                if(cmd=="perf-before"||cmd=="perf-after")CoastForestPerformance.Begin(cmd.Substring(5));
                else if(cmd=="perf-step2-before"||cmd=="perf-step2-after")CoastForestPerformance.Begin(cmd.Substring(5));
                else if(cmd=="perf-step3-before"||cmd=="perf-step3-after")CoastForestPerformance.Begin(cmd.Substring(5));
                else if(cmd=="perf-step2-north")CoastForestPerformance.Begin("step2-north",new Vector3(22,0,28));
                else if(cmd=="map-checks")CoastLargeMapChecks.Run();
                else if(cmd=="map-play-check")CoastLargeMapChecks.PlayTest();
                else if(cmd=="drone-checks"){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool("Coast.VerifyDrone",true);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;}
                else if(cmd=="forest-build")CoastForestAssets.Build();
                else if(cmd=="forest-validate")CoastForestAssets.Validate();
                else if(cmd=="forest-inventory-before")CoastForestAssets.InventoryBaseline();
                else if(cmd=="optimize-export")CoastOptimization.Export();else if(cmd=="optimize-apply")CoastOptimization.Apply();else if(cmd=="setup")Setup();else if(cmd=="check-rollback")CheckRollback();else if(cmd=="checks")File.WriteAllText(Evidence+"/core-checks.txt",CoastChecks.Run());
                else if(cmd=="verify"){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool("Coast.Verify",true);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;}
                else if(cmd=="play"){EditorSceneManager.OpenScene(ScenePath);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;}
                else if(cmd=="stop")EditorApplication.isPlaying=false;
                else if(cmd=="build")BuildPlayer();else if(cmd=="refresh")AssetDatabase.Refresh();else throw new ArgumentException("Unknown coast command");
                File.WriteAllText(Evidence+"/command-result.txt",cmd+" OK "+DateTime.UtcNow.ToString("O"));
            }catch(Exception e){File.WriteAllText(Evidence+"/command-result.txt",cmd+" FAILED\n"+e);Debug.LogException(e);}
        }
        static void Ensure(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
        static T Load<T>(string path)where T:UnityEngine.Object{var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null)throw new FileNotFoundException(path);return a;}
        [MenuItem("Dawn Guard/Coast/Build coastal campaign")]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before setup");
            RunSceneTransaction(SetupScene);
        }
        static void RunSceneTransaction(Action action)
        {
            var active=SceneManager.GetActiveScene();var previous=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var previousPipeline=GraphicsSettings.defaultRenderPipeline;var previousQuality=QualitySettings.renderPipeline;
            try { action(); }
            catch
            {
                GraphicsSettings.defaultRenderPipeline=previousPipeline;QualitySettings.renderPipeline=previousQuality;
                // Keep existing scene objects in memory, including unsaved user edits.
                for(int i=SceneManager.sceneCount-1;i>=0;i--){var scene=SceneManager.GetSceneAt(i);if(!previous.Contains(scene))EditorSceneManager.CloseScene(scene,true);}
                if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);throw;
            }
        }
        static void CheckRollback()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var original=SceneManager.GetActiveScene();var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(test);
            EditorSceneManager.SaveScene(test,Evidence+"/rollback-fixture.unity");
            var marker=new GameObject("Unsaved transaction test");EditorSceneManager.MarkSceneDirty(test);
            try
            {
                try{RunSceneTransaction(()=>{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);throw new InvalidOperationException("Expected test failure");});}
                catch(InvalidOperationException e){if(e.Message!="Expected test failure")throw;}
                if(marker==null||!test.isLoaded||!test.isDirty||SceneManager.GetActiveScene()!=test)throw new Exception("Rollback lost unsaved scene state");
                File.WriteAllText(Evidence+"/rollback-check.txt","PASS: failed generation preserves loaded scene, unsaved object and dirty state.");
            }
            finally{EditorSceneManager.CloseScene(test,true);if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);}
        }
        static void SetupScene()
        {
            Directory.CreateDirectory(Evidence);var active=EditorSceneManager.GetActiveScene();if(active.isDirty)EditorSceneManager.SaveScene(active,Evidence+"/SceneBeforeCoast-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Ensure(Folder+"/Data");Ensure(Folder+"/Scenes");Ensure(Folder+"/Art/Materials");Ensure(Folder+"/Art/Icons");Ensure(Folder+"/Art/Prefabs");
            string catalogPath=Folder+"/Data/BlackwoodCoastCatalog.asset";var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(catalogPath);if(c==null){c=ScriptableObject.CreateInstance<CoastCatalog>();AssetDatabase.CreateAsset(c,catalogPath);}c.rules=CoastRules.Create();
            c.source=Load<GameCatalog>("Assets/DawnGuard/BlackwoodV2/Generated/UserMap/BlackwoodCatalog.asset");
            string existing="Assets/DawnGuard/BlackwoodV2/Generated/UserMap/";
            c.pine=Load<GameObject>(existing+"User_pine.prefab");c.rock=Load<GameObject>(existing+"User_rubble.prefab");c.pad=Load<GameObject>(existing+"User_pad.prefab");
            c.worker=WrapModel("CoastWorker");c.guardian=WrapModel("CoastGuardian");c.dock=WrapModel("CoastDock");c.lantern=WrapModel("CoastLantern");
            c.basalt=WrapModel("CoastBasalt");c.ore=WrapModel("CoastOre");c.fern=WrapModel("CoastFern");
            c.ground=Material("CoastGround","Blackwood/Coastal Ground",new Color(.3f,.4f,.2f));c.water=Material("CoastWater","Blackwood/Coastal Water",new Color(.08f,.28f,.35f));
            c.sand=Material("Sand","Universal Render Pipeline/Lit",new Color(.61f,.48f,.29f));c.path=Material("Path","Universal Render Pipeline/Lit",new Color(.34f,.28f,.16f));
            c.ink=Material("UIInk","Universal Render Pipeline/Unlit",new Color(.06f,.12f,.15f));c.mint=Material("Mint","Universal Render Pipeline/Unlit",new Color(.4f,.9f,.8f));c.orange=Material("Orange","Universal Render Pipeline/Unlit",new Color(1,.55f,.15f));
            string rp=Folder+"/Data/CoastPipeline.asset";if(!File.Exists(rp))AssetDatabase.CopyAsset(existing+"Blackwood_RPAsset.asset",rp);
            var pipeline=Load<UniversalRenderPipelineAsset>(rp);pipeline.shadowDistance=70;pipeline.mainLightShadowmapResolution=2048;pipeline.msaaSampleCount=2;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var staged=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);EditorSceneManager.SetActiveScene(staged);
            var go=new GameObject("Blackwood Coastal Campaign");var root=go.AddComponent<CoastRoot>();root.catalog=c;
            c.defenseIcons=new Sprite[5];string[] ids={"wall","gun","arcane","tesla","cryo"};for(int i=0;i<ids.Length;i++)c.defenseIcons[i]=RenderIcon(c.Defense(ids[i]),ids[i]);c.workerIcon=RenderIcon(c.worker,"worker");c.enemyIcon=RenderIcon(c.Enemy("walker"),"enemy");
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Select(s=>new EditorBuildSettingsScene(s.path,false)).ToList();scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
            File.WriteAllText(Evidence+"/core-checks.txt",CoastChecks.Run());File.WriteAllText(Evidence+"/setup.txt","New coastal scene, original models reused, four Blender additions, independent catalog/save, generated UI icons.\n"+ScenePath);
            Selection.activeGameObject=go;
            EditorSceneManager.OpenScene(ScenePath);
        }
        static GameObject WrapModel(string name)
        {
            var source=Load<GameObject>(Folder+"/Art/Models/"+name+".fbx");var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                string key=renderer.name.Split('_').Last();Color color;switch(key){case "Ivory":color=new Color(.76f,.79f,.72f);break;case "Orange":color=new Color(.95f,.31f,.055f);break;case "Cyan":color=new Color(.08f,.84f,.9f);break;case "Steel":color=new Color(.23f,.31f,.32f);break;case "Leaf":color=new Color(.22f,.36f,.10f);break;case "Wood":color=new Color(.29f,.18f,.085f);break;case "Stone":color=new Color(.31f,.37f,.40f);break;default:color=new Color(.045f,.085f,.105f);break;}
                var m=Material("Model_"+key,"Universal Render Pipeline/Lit",color);if(key=="Cyan"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.2f);}renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>m).ToArray();
            }
            var wrapper=new GameObject(name);instance.transform.SetParent(wrapper.transform,false);
            string dest=Folder+"/Art/Prefabs/"+name+".prefab";var result=PrefabUtility.SaveAsPrefabAsset(wrapper,dest);UnityEngine.Object.DestroyImmediate(wrapper);return result;
        }
        static Material Material(string name,string shader,Color color)
        {string path=Folder+"/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){var s=Shader.Find(shader);if(s==null)throw new Exception("Missing shader: "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,path);}if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(m);return m;}
        static Sprite RenderIcon(GameObject prefab,string name)
        {
            bool asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            var root=new GameObject("Icon studio");var obj=CoastWorld.Place(prefab,root.transform,new Vector3(200,0,0),2,2);
            foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
            var cameraGo=new GameObject("Icon camera");var camera=cameraGo.AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(203.5f,3.4f,-5);camera.transform.LookAt(new Vector3(200,.8f,0));camera.orthographic=true;camera.orthographicSize=1.65f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,0);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var lightGo=new GameObject("Icon key");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(45,-30,0);light.cullingMask=1<<30;
            if(name=="arcane"||name=="cryo"||name=="worker"){camera.transform.position=new Vector3(203.5f,3.4f,5);camera.transform.LookAt(new Vector3(200,.8f,0));light.transform.rotation=Quaternion.Euler(45,140,0);}
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);rt.Create();var old=RenderTexture.active;
            try{camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(256,256,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();string path=Folder+"/Art/Icons/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.SaveAndReimport();return Load<Sprite>(path);}
            finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraGo);UnityEngine.Object.DestroyImmediate(lightGo);}
        }
        [MenuItem("Dawn Guard/Coast/Build Windows player")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/BlackwoodCoast");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/BlackwoodCoast/BlackwoodCoast.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Evidence+"/build-result.txt",report.summary.result+"\nBytes: "+report.summary.totalSize+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Player build failed");
        }
    }
}



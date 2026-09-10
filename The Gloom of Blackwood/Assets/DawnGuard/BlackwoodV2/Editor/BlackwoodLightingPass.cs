using System;
using System.IO;
using System.Linq;
using DawnGuard.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodLightingPass
    {
        public const string Folder="Assets/DawnGuard/BlackwoodV2/Generated/UserMap";
        [MenuItem("Dawn Guard/9 - Apply Blackwood lighting and facades (phase 2)")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save existing scene edits first.");
            Directory.CreateDirectory("IntegrationEvidence/Phase2");
            var scene=EditorSceneManager.OpenScene(Folder+"/BlackwoodUserMap.unity");
            var root=UnityEngine.Object.FindAnyObjectByType<BlackwoodRoot>();
            string path=Folder+"/LightingProfile.asset";
            var settings=AssetDatabase.LoadAssetAtPath<BlackwoodLightingProfile>(path);
            if(settings==null) { settings=ScriptableObject.CreateInstance<BlackwoodLightingProfile>(); AssetDatabase.CreateAsset(settings,path); }
            string pipelinePath=Folder+"/Blackwood_RPAsset.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if(pipeline==null)
            {
                pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));
                pipeline.name="Blackwood_RPAsset"; AssetDatabase.CreateAsset(pipeline,pipelinePath);
            }
            pipeline.renderScale=1; pipeline.msaaSampleCount=2; pipeline.supportsHDR=true;
            pipeline.shadowDistance=42; pipeline.shadowCascadeCount=2; pipeline.mainLightShadowmapResolution=2048;
            pipeline.maxAdditionalLightsCount=4;
            var serializedPipeline=new SerializedObject(pipeline);
            serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue=true;
            serializedPipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
            serializedPipeline.FindProperty("m_SoftShadowQuality").intValue=1;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            settings.pipeline=pipeline;
            string volumePath=Folder+"/Blackwood_Grade.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); profile.name="Blackwood_Grade"; AssetDatabase.CreateAsset(profile,volumePath);
                var color=profile.Add<ColorAdjustments>(true); color.postExposure.Override(.15f); color.contrast.Override(9); color.saturation.Override(-3);
                color.colorFilter.Override(Color.white);
                var bloom=profile.Add<Bloom>(true); bloom.threshold.Override(1.05f); bloom.intensity.Override(.22f); bloom.scatter.Override(.55f); bloom.highQualityFiltering.Override(false);
                var vignette=profile.Add<Vignette>(true); vignette.intensity.Override(.16f); vignette.smoothness.Override(.55f);
                var tone=profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                foreach(var c in profile.components) AssetDatabase.AddObjectToAsset(c,profile);
            }
            if(profile.TryGet<ColorAdjustments>(out var grade)) { grade.postExposure.Override(.35f); grade.contrast.Override(5); EditorUtility.SetDirty(grade); }
            settings.postProcessing=profile; root.lightingProfile=settings;
            foreach(string id in new[]{"shelter","camp","generator","lab","gun"})
            {
                string prefabPath=AssetDatabase.GetAssetPath(root.catalog.buildings.Single(b=>b.definitionId==id).prefab);
                var contents=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var facing=contents.transform.Find("Facade facing");
                    if(facing==null)
                    {
                        var visual=contents.transform.Find("Visual");
                        if(visual==null) throw new InvalidOperationException("Expected existing Visual child in "+prefabPath);
                        facing=new GameObject("Facade facing").transform; facing.SetParent(contents.transform,false); visual.SetParent(facing,false);
                    }
                    facing.localRotation=Quaternion.Euler(0,id=="gun" ? 270 : 180,0);
                    if(id!="gun") AddLamps(contents,id);
                    PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
                }
                finally {PrefabUtility.UnloadPrefabContents(contents);}
            }
            var previewLight=root.startingPreview.GetComponentInChildren<Light>();
            if(previewLight!=null) {previewLight.color=settings.daySun; previewLight.intensity=settings.dayIntensity; previewLight.transform.rotation=Quaternion.Euler(48,-38,0);}
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(pipeline); EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("IntegrationEvidence/Phase2/setup.txt","Facades: shelter/camp/generator/lab +180 degrees in wrapper pivot; gun +270 degrees faces northern approach.\nScene-local Forward URP: HDR, 2x MSAA, 2 shadow cascades, 2048 atlas, Low soft shadows, max 4 additional lights/object; no local-light shadows.\nColor Adjustments neutral white filter, ACES, Bloom 0.22, Vignette 0.16. No global fog.\n");
        }
        static void AddLamps(GameObject contents,string id)
        {
            if(contents.transform.Find("Facade luminaires")!=null) return;
            string materialPath=Folder+(id=="generator" ? "/PowerLens.mat" : "/WarmLens.mat");
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.name=id=="generator" ? "PowerLens" : "WarmLens";
                material.SetColor("_BaseColor",id=="generator" ? new Color(1.1f,3.1f,4) : new Color(4,2.65f,1.2f));
                AssetDatabase.CreateAsset(material,materialPath);
            }
            var renderers=contents.GetComponentsInChildren<Renderer>(); Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            var lamps=new GameObject("Facade luminaires").transform; lamps.SetParent(contents.transform,false);
            // Two small emissive fixtures added to the wrapper, not painted over the user's textures.
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Entry light "+i,typeof(LineRenderer)); go.transform.SetParent(lamps,false);
                var line=go.GetComponent<LineRenderer>(); line.useWorldSpace=false; line.positionCount=2;
                line.sharedMaterial=material; line.startWidth=line.endWidth=.035f; line.numCapVertices=2;
                float x=(i==0 ? -.32f : .32f)*bounds.size.x;
                float y=Mathf.Min(bounds.max.y*.62f,.9f), z=bounds.min.z-.018f;
                line.SetPosition(0,new Vector3(x-.06f,y,z)); line.SetPosition(1,new Vector3(x+.06f,y,z));
                line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            }
        }
        public static void InspectFacades()
        {
            Directory.CreateDirectory("IntegrationEvidence/Phase2");
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalog>(Folder+"/BlackwoodCatalog.asset");
            string[] roles={"shelter","camp","generator","lab","gun","tesla"};
            var atlas=new Texture2D(1200,roles.Length*240,TextureFormat.RGB24,false);
            for(int row=0;row<roles.Length;row++)
            {
                var prefab=catalog.buildings.Single(b=>b.definitionId==roles[row]).prefab;
                for(int col=0;col<4;col++)
                {
                    var preview=new PreviewRenderUtility();
                    try
                    {
                        var go=UnityEngine.Object.Instantiate(prefab); go.transform.rotation=Quaternion.Euler(0,col*90,0);
                        preview.AddSingleGO(go);
                        preview.camera.orthographic=true; preview.camera.orthographicSize=1.45f;
                        preview.camera.transform.position=new Vector3(0,2.0f,-4); preview.camera.transform.LookAt(new Vector3(0,.65f,0));
                        preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=30;
                        preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.12f,.16f,.18f);
                        preview.lights[0].intensity=1.25f; preview.lights[0].transform.rotation=Quaternion.Euler(40,-35,0);
                        preview.lights[1].intensity=.6f;
                        preview.BeginPreview(new Rect(0,0,300,240),GUIStyle.none); preview.Render(true);
                        var target=preview.EndPreview(); var previous=RenderTexture.active;
                        RenderTexture.active=target as RenderTexture;
                        var shot=new Texture2D(300,240,TextureFormat.RGB24,false); shot.ReadPixels(new Rect(0,0,300,240),0,0); shot.Apply();
                        atlas.SetPixels(col*300,(roles.Length-1-row)*240,300,240,shot.GetPixels());
                        UnityEngine.Object.DestroyImmediate(shot); RenderTexture.active=previous;
                    }
                    finally {preview.Cleanup();}
                }
            }
            atlas.Apply(); File.WriteAllBytes("IntegrationEvidence/Phase2/facades-before.png",atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            File.WriteAllText("IntegrationEvidence/Phase2/facades.txt","Rows: shelter, camp, generator, lab, gun, tesla. Columns: current orientation + 0 / 90 / 180 / 270 degrees.\n");
        }
    }
}

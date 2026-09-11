using UnityEditor;
using UnityEngine;
using System.IO;

namespace DawnGuard.BlackwoodLTD.Editor
{
    public static class CoastGrenadeSetup
    {
        public const string Source="Assets/GameArt/Granate/Granate.fbx";
        public static void Measure(bool loaded)
        {
            CoastScenePreview.Rebuild();
            foreach(var root in Object.FindObjectsByType<CoastRoot>(FindObjectsInactive.Include))
            {
                if(root.Game==null||root.World==null)continue;
                root.Game.S.phase=CoastPhase.Night;root.Game.S.grenadeState=loaded?GrenadeState.LOADED:GrenadeState.AVAILABLE;root.Game.S.grenadeCharges=loaded?2:0;
                root.Game.S.droneX=18;root.Game.S.droneZ=12;root.World.Refresh(2);
            }
            CoastForestPerformance.Begin(loaded?"step6-loaded":"step6-unloaded");
        }
        public static void Build()
        {
            CoastScenePreview.Clear();const string folder=CoastEditor.Folder+"/Art/Grenade";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source);if(asset==null)throw new FileNotFoundException(Source);
            var wrapper=new GameObject("DroneGrenade");
            try
            {
                var visual=Object.Instantiate(asset,wrapper.transform);visual.name="Visual";new GameObject("Gameplay").transform.SetParent(wrapper.transform,false);
                string path=folder+"/Grenade.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameArt/Granate/Meshy_AI_Sci_fi_Missile_0910113127_image-to-3d-texture.png"));material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",0);material.SetFloat("_Smoothness",.3f);material.enableInstancing=true;EditorUtility.SetDirty(material);
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");c.grenadePrefab=PrefabUtility.SaveAsPrefabAsset(wrapper,folder+"/DroneGrenade.prefab");
                c.rules.grenade=new GrenadeSpec();c.rules.Enemy("runner").visualScale=.75f;c.rules.Enemy("brute").knockbackMultiplier=.35f;c.rules.Enemy("special").knockbackMultiplier=.65f;
                EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
            }finally{Object.DestroyImmediate(wrapper);CoastScenePreview.Clear();}
        }
    }
}

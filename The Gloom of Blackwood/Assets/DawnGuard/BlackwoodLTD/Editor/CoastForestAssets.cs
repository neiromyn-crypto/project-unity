using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGuard.BlackwoodLTD.Editor
{
    public static class CoastForestAssets
    {
        const string Folder=CoastEditor.Folder+"/Art/Forest";
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Requires Edit Mode");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");
            var temp=CoastWorld.Place(c.pine,null,Vector3.zero,1,100);
            try
            {
                var filters=temp.GetComponentsInChildren<MeshFilter>();if(filters.Length!=1)throw new Exception("Expected one imported pine mesh; inspect source before combining");
                var mf=filters[0];var matrix=mf.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                var files=new[]{"IntegrationEvidence/Performance/MeshWork/Output/Pine_LOD0.bwm","IntegrationEvidence/Optimization/Output/6b94df44e6badb941a5e754d984893a6_5461320835613783329.bwm","IntegrationEvidence/Performance/MeshWork/Output/Pine_LOD2.bwm"};
                var meshes=new Mesh[3];
                for(int level=0;level<3;level++)
                {
                    var mesh=CoastOptimization.Read(files[level],mf.sharedMesh);mesh.name="CoastPine_LOD"+level;
                    mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();mesh.normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
                    meshes[level]=Save(mesh,Folder+"/"+mesh.name+".asset");
                }
                var original=mf.GetComponent<Renderer>().sharedMaterials;var materials=new Material[original.Length];
                for(int i=0;i<original.Length;i++)
                {
                    var mat=new Material(original[i]){name="CoastPine_Shared_"+i};
                    // URP Lit supports instancing. One material asset shared by all trees and clusters.
                    if(mat.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Inspect tree shader instancing support: "+mat.shader.name);
                    mat.enableInstancing=true;materials[i]=Save(mat,Folder+"/"+mat.name+".mat");
                }
                var profile=ScriptableObject.CreateInstance<CoastForestProfile>();profile.name="CoastForestProfile";profile.near=meshes[0];profile.middle=meshes[1];profile.far=meshes[2];profile.materials=materials;
                c.forest=Save(profile,Folder+"/CoastForestProfile.asset");EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
                File.WriteAllText("IntegrationEvidence/Performance/forest-assets.txt","Source prefab preserved: "+AssetDatabase.GetAssetPath(c.pine)+"\nLOD triangles: "+string.Join(", ",meshes.Select(m=>m.triangles.Length/3))+"\nShared material assets: "+materials.Length+"\nShader: "+materials[0].shader.name+"; enableInstancing=true. SRP Batcher may take priority; actual batching is recorded in Stats.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);CoastScenePreview.Clear();}
        }
        static T Save<T>(T asset,string path)where T:UnityEngine.Object
        {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null){EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);return existing;}AssetDatabase.CreateAsset(asset,path);return asset;}
        public static void InventoryBaseline()
        {
            var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");var profile=c.forest;
            try{c.forest=null;CoastScenePreview.Rebuild();CoastForestPerformance.Inventory("before");}
            finally{c.forest=profile;CoastScenePreview.Rebuild();}
        }
        public static void Validate()
        {
            var forest=Resources.FindObjectsOfTypeAll<CoastForest>().Where(f=>f.gameObject.scene.IsValid()).ToArray();
            var report=new System.Collections.Generic.List<string>();
            Action<bool,string> check=(ok,message)=>{report.Add((ok?"PASS ":"FAIL ")+message);if(!ok){File.WriteAllLines("IntegrationEvidence/Performance/validation.txt",report);throw new Exception(message);}};
            check(forest.Length==2,"Gameplay and menu forests exist");
            var main=forest.Single(f=>f.individualTrees>0);var menu=forest.Single(f=>f.individualTrees==0);
            check(main.individualTrees==24,"24 individual gameplay trees");check(main.clusters==6&&main.clusterTrees==132,"132 distant crowns in 6 spatial clusters");check(menu.clusters==3&&menu.clusterTrees==40,"40 menu crowns in 3 clusters");
            var renderers=forest.SelectMany(f=>f.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            check(forest.Sum(f=>f.GetComponentsInChildren<Collider>(true).Length)==0,"Decorative forest has zero colliders");
            check(forest.All(f=>{var nav=f.GetComponent<global::Unity.AI.Navigation.NavMeshModifier>();return nav!=null&&nav.ignoreFromBuild&&nav.applyToChildren&&nav.AffectsAgentType(0);}),"All forest excluded from NavMesh generation, including child geometry");
            check(renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count()==1,"One shared material asset for every tree, LOD and cluster");
            check(renderers.All(r=>r.sharedMaterials.All(m=>m.enableInstancing&&m.shader.name=="Universal Render Pipeline/Lit")),"GPU instancing enabled on compatible URP Lit material");
            check(renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off)==8,"Only 8 near LOD0 renderers cast shadows");
            var lods=main.GetComponentsInChildren<LODGroup>();check(lods.Length==24&&lods.All(l=>l.GetLODs().Length==3),"24 LODGroups with 3 levels each");
            check(renderers.All(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<250000),"No original million-triangle mesh in forest renderers");
            // Preview and runtime use the same Build entry point. This checks the actual generated scene.
            File.WriteAllLines("IntegrationEvidence/Performance/validation.txt",report);
        }
    }
}

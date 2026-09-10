using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DawnGuard.Unity;

namespace DawnGuard.BlackwoodLTD.Editor
{
    // Geometry-only copies. Existing materials, hierarchy, pivots and source assets are retained.
    public static class CoastOptimization
    {
        public const string Work="IntegrationEvidence/Optimization";
        const string Output=CoastEditor.Folder+"/Art/Optimized";
        [Serializable] public class Job {public string id,name;public int vertices,triangles,target;public bool skinned;}
        [Serializable] public class Jobs {public List<Job> meshes=new List<Job>();}
        static string Id(Mesh mesh){string guid;long local;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out guid,out local);return guid+"_"+local;}
        static IEnumerable<KeyValuePair<GameObject,int>> Prefabs(CoastCatalog c)
        {
            yield return new KeyValuePair<GameObject,int>(c.pine,3500);yield return new KeyValuePair<GameObject,int>(c.rock,4500);yield return new KeyValuePair<GameObject,int>(c.pad,6000);
            yield return new KeyValuePair<GameObject,int>(c.source.dronePrefab,14000);
            foreach(var b in c.source.buildings)yield return new KeyValuePair<GameObject,int>(b.prefab,b.definitionId=="wall"?2500:b.definitionId=="gun"||b.definitionId=="tesla"?18000:26000);
            foreach(var e in c.source.enemies)yield return new KeyValuePair<GameObject,int>(e.prefab,14000);
        }
        public static void Export()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");CoastScenePreview.Clear();Directory.CreateDirectory(Work+"/Input");Directory.CreateDirectory(Work+"/Output");
            var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");
            var jobs=new Jobs();var meshes=new Dictionary<string,Mesh>();
            foreach(var pair in Prefabs(c))
            {
                if(pair.Key==null)continue;var list=pair.Key.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).Select(m=>m.sharedMesh).Concat(pair.Key.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(m=>m.sharedMesh!=null).Select(m=>m.sharedMesh)).Distinct().ToArray();
                long total=list.Sum(m=>(long)m.triangles.Length/3);
                foreach(var m in list)
                {
                    string id=Id(m);int tris=m.triangles.Length/3,target=Math.Max(96,(int)(pair.Value*(tris/(double)Math.Max(1,total))));
                    var old=jobs.meshes.Find(j=>j.id==id);if(old!=null){old.target=Math.Min(old.target,target);continue;}
                    if(tris<=target)continue;if(m.blendShapeCount>0)throw new Exception("Blend shapes need a dedicated optimization: "+m.name);
                    meshes[id]=m;jobs.meshes.Add(new Job{id=id,name=m.name,vertices=m.vertexCount,triangles=tris,target=target,skinned=m.bindposes.Length>0});
                }
            }
            foreach(var j in jobs.meshes)Write(meshes[j.id],Work+"/Input/"+j.id+".bwm",j.skinned);
            File.WriteAllText(Work+"/jobs.json",JsonUtility.ToJson(jobs,true));
        }
        internal static void Write(Mesh m,string path,bool skin)
        {
            using(var stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None,1048576))using(var w=new BinaryWriter(stream))
            {
                var v=m.vertices;var n=m.normals;var uv=m.uv;var weights=skin?m.boneWeights:null;
                w.Write(v.Length);w.Write(m.subMeshCount);w.Write(skin?1:0);
                for(int i=0;i<v.Length;i++){w.Write(v[i].x);w.Write(v[i].y);w.Write(v[i].z);var normal=n.Length>i?n[i]:Vector3.up;w.Write(normal.x);w.Write(normal.y);w.Write(normal.z);var tex=uv.Length>i?uv[i]:Vector2.zero;w.Write(tex.x);w.Write(tex.y);}
                if(skin)foreach(var b in weights){w.Write(b.boneIndex0);w.Write(b.boneIndex1);w.Write(b.boneIndex2);w.Write(b.boneIndex3);w.Write(b.weight0);w.Write(b.weight1);w.Write(b.weight2);w.Write(b.weight3);}
                for(int sub=0;sub<m.subMeshCount;sub++){var t=m.GetTriangles(sub);w.Write(t.Length);foreach(int i in t)w.Write(i);}
            }
        }
        internal static Mesh Read(string path,Mesh original)
        {
            using(var r=new BinaryReader(File.OpenRead(path)))
            {
                int count=r.ReadInt32(),subs=r.ReadInt32(),skin=r.ReadInt32();var v=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];
                for(int i=0;i<count;i++){v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());}
                var m=new Mesh{name=original.name+"_Game",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.vertices=v;m.normals=n;m.uv=uv;
                if(skin!=0){var bw=new BoneWeight[count];for(int i=0;i<count;i++)bw[i]=new BoneWeight{boneIndex0=r.ReadInt32(),boneIndex1=r.ReadInt32(),boneIndex2=r.ReadInt32(),boneIndex3=r.ReadInt32(),weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle()};m.boneWeights=bw;m.bindposes=original.bindposes;}
                m.subMeshCount=subs;for(int sub=0;sub<subs;sub++){int len=r.ReadInt32();var t=new int[len];for(int i=0;i<len;i++)t[i]=r.ReadInt32();m.SetTriangles(t,sub,false);}m.RecalculateBounds();m.RecalculateTangents();
                if(m.bounds.size.magnitude<original.bounds.size.magnitude*.75f||m.bounds.size.magnitude>original.bounds.size.magnitude*1.25f)throw new Exception("Changed silhouette bounds: "+original.name);
                return m;
            }
        }
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");CoastScenePreview.Clear();Directory.CreateDirectory(Output+"/Meshes");Directory.CreateDirectory(Output+"/Prefabs");AssetDatabase.Refresh();
            var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");var jobs=JsonUtility.FromJson<Jobs>(File.ReadAllText(Work+"/jobs.json"));
            var meshMap=new Dictionary<string,Mesh>();var prefabMap=new Dictionary<GameObject,GameObject>();long before=0,after=0;
            foreach(var pair in Prefabs(c).ToArray())
            {
                if(pair.Key==null||prefabMap.ContainsKey(pair.Key))continue;var instance=UnityEngine.Object.Instantiate(pair.Key);instance.name=pair.Key.name+"_Game";
                foreach(var mf in instance.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh!=null)mf.sharedMesh=Replace(mf.sharedMesh,jobs,meshMap,ref before,ref after);
                foreach(var sm in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(sm.sharedMesh!=null)sm.sharedMesh=Replace(sm.sharedMesh,jobs,meshMap,ref before,ref after);
                prefabMap[pair.Key]=PrefabUtility.SaveAsPrefabAsset(instance,Output+"/Prefabs/"+instance.name+".prefab");UnityEngine.Object.DestroyImmediate(instance);
            }
            var source=UnityEngine.Object.Instantiate(c.source);source.name="CoastOptimizedCatalog";source.dronePrefab=prefabMap[source.dronePrefab];foreach(var b in source.buildings)if(b.prefab!=null)b.prefab=prefabMap[b.prefab];foreach(var e in source.enemies)if(e.prefab!=null)e.prefab=prefabMap[e.prefab];
            string sourcePath=Output+"/CoastOptimizedCatalog.asset";var existing=AssetDatabase.LoadAssetAtPath<GameCatalog>(sourcePath);if(existing!=null){EditorUtility.CopySerialized(source,existing);UnityEngine.Object.DestroyImmediate(source);source=existing;}else AssetDatabase.CreateAsset(source,sourcePath);
            c.pine=prefabMap[c.pine];c.rock=prefabMap[c.rock];c.pad=prefabMap[c.pad];c.source=source;c.basalt=c.rock;EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
            File.WriteAllText(Work+"/applied.txt","PASS — optimized mesh copies\nUnique source triangles: "+before+"\nUnique game triangles: "+after+"\nMeshes: "+meshMap.Count+"\nPrefabs: "+prefabMap.Count);
        }
        static Mesh Replace(Mesh original,Jobs jobs,Dictionary<string,Mesh> cache,ref long before,ref long after)
        {
            string id=Id(original);if(!jobs.meshes.Exists(j=>j.id==id))return original;Mesh result;if(cache.TryGetValue(id,out result))return result;
            string output=Output+"/Meshes/"+id+".asset";var reduced=Read(Work+"/Output/"+id+".bwm",original);result=AssetDatabase.LoadAssetAtPath<Mesh>(output);if(result!=null){EditorUtility.CopySerialized(reduced,result);UnityEngine.Object.DestroyImmediate(reduced);}else{result=reduced;AssetDatabase.CreateAsset(result,output);}
            before+=original.triangles.Length/3;after+=result.triangles.Length/3;cache[id]=result;return result;
        }
    }
}

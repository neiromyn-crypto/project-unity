using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

namespace DawnGuard.BlackwoodLTD
{
    // Rendering only: no occupancy cells, colliders, agents or obstacles.
    public sealed class CoastForest : MonoBehaviour
    {
        public struct Tree
        {
            public Vector3 position;public float width,height,yaw;
            public Tree(Vector3 p,float w,float h,float y=0){position=p;width=w;height=h;yaw=y;}
        }
        public int individualTrees,clusterTrees,clusters;
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        public static void Build(Transform parent,CoastCatalog catalog,List<Tree> trees,bool menu=false,float campX=14,float depth=24)
        {
            var profile=catalog.forest;
            if(profile==null){foreach(var t in trees){var go=CoastWorld.Place(catalog.pine,parent,t.position,t.width,t.height);go.transform.Rotate(0,t.yaw,0);}return;}
            var root=new GameObject(menu?"Coast Forest — menu clusters":"Coast Forest — 24 near trees and distant clusters");root.transform.SetParent(parent,false);
            var forest=root.AddComponent<CoastForest>();var nav=root.AddComponent<NavMeshModifier>();nav.ignoreFromBuild=true;nav.applyToChildren=true;
            var near=menu?new HashSet<int>():new HashSet<int>(Enumerable.Range(0,trees.Count).OrderBy(i=>NearScore(trees[i],campX)).Take(24));
            int shadow=0;var groups=new Dictionary<int,List<Tree>>();
            for(int i=0;i<trees.Count;i++)
            {
                var t=trees[i];if(near.Contains(i))
                {
                    var tree=new GameObject("Tree "+i.ToString("D3"));tree.transform.SetParent(root.transform,false);tree.transform.localPosition=t.position;tree.transform.localRotation=Quaternion.Euler(0,t.yaw,0);tree.transform.localScale=Vector3.one*Scale(profile,t);
                    var lod=tree.AddComponent<LODGroup>();var renderers=new Renderer[3];var meshes=new[]{profile.near,profile.middle,profile.far};
                    bool casts=shadow++<8;
                    for(int j=0;j<3;j++)renderers[j]=Renderer(tree.transform,"LOD"+j,meshes[j],profile.materials,casts&&j==0);
                    lod.SetLODs(new[]{new LOD(.12f,new[]{renderers[0]}),new LOD(.035f,new[]{renderers[1]}),new LOD(.003f,new[]{renderers[2]})});lod.fadeMode=LODFadeMode.None;lod.RecalculateBounds();forest.individualTrees++;
                }
                else
                {
                    int key=menu?Mathf.Clamp((int)((t.position.x-91)/15),0,2):t.position.x<campX-7?(t.position.z<depth*.65f?0:1):t.position.x>campX+7?(t.position.z<depth*.65f?2:3):t.position.x<campX?4:5;
                    if(!groups.ContainsKey(key))groups[key]=new List<Tree>();groups[key].Add(t);
                }
            }
            foreach(var pair in groups)
            {
                // One object per spatial sector. Preserve original tree positions and silhouette.
                var combined=new Mesh{name="Forest sector "+pair.Key,indexFormat=IndexFormat.UInt32};
                var perMaterial=new List<Mesh>();
                for(int sub=0;sub<profile.middle.subMeshCount;sub++)
                {
                    var section=new Mesh{indexFormat=IndexFormat.UInt32};var instances=pair.Value.Select(t=>new CombineInstance{mesh=profile.middle,subMeshIndex=sub,transform=Matrix4x4.TRS(t.position,Quaternion.Euler(0,t.yaw,0),Vector3.one*Scale(profile,t))}).ToArray();
                    section.CombineMeshes(instances,true,true);perMaterial.Add(section);
                }
                combined.CombineMeshes(perMaterial.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);combined.RecalculateBounds();
                foreach(var mesh in perMaterial)Release(mesh);
                Renderer(root.transform,"Forest Cluster "+pair.Key+" ("+pair.Value.Count+" crowns)",combined,profile.materials,false);forest.ownedMeshes.Add(combined);forest.clusterTrees+=pair.Value.Count;forest.clusters++;
            }
        }
        static float NearScore(Tree t,float campX){float x=(t.position.x-campX)*.7f,z=t.position.z-7;return x*x+z*z;}
        static float Scale(CoastForestProfile p,Tree t){var b=p.near.bounds;return Mathf.Min(t.width/Mathf.Max(b.size.x,b.size.z),t.height/b.size.y);}
        static MeshRenderer Renderer(Transform parent,string name,Mesh mesh,Material[] materials,bool shadows)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;return r;
        }
        static void Release(UnityEngine.Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy(){foreach(var mesh in ownedMeshes)if(mesh!=null)Release(mesh);ownedMeshes.Clear();}
    }
}

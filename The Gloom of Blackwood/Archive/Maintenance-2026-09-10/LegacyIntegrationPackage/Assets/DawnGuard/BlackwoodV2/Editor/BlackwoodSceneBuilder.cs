using System;
using System.Collections.Generic;
using DawnGuard.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodSceneBuilder
    {
        private const string Base="Assets/DawnGuard/BlackwoodV2";
        [MenuItem("Dawn Guard/3 - Create Blackwood visual scene v2")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play mode first."); return; }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameCatalog source=null;
            var old=UnityEngine.Object.FindFirstObjectByType<GameRoot>(); if(old!=null) source=old.catalog;
            if(source==null) source=AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/DawnGuard/Generated/DemoCatalog.asset");
            var rules=BlackwoodBalance.Create(); rules.Validate(); BlackwoodChecks.Run();
            if(!AssetDatabase.IsValidFolder(Base+"/Generated")) AssetDatabase.CreateFolder(Base,"Generated");
            string folder=AssetDatabase.GenerateUniqueAssetPath(Base+"/Generated/Preview");
            AssetDatabase.CreateFolder(Base+"/Generated",System.IO.Path.GetFileName(folder));
            var maker=new BlackwoodPrefabMaker(folder);
            var catalog=ScriptableObject.CreateInstance<GameCatalog>(); catalog.rules=rules;
            var bindings=new List<VisualBinding>();
            foreach(var d in rules.buildings) bindings.Add(new VisualBinding {definitionId=d.id,prefab=maker.Build(d.id)});
            catalog.buildings=bindings.ToArray(); catalog.dronePrefab=maker.Build("drone");
            catalog.enemies=source!=null && source.enemies!=null ? (VisualBinding[])source.enemies.Clone() : new VisualBinding[0];
            catalog.surfaceTemplate=maker.Materials["charcoal"];
            if(source!=null) catalog.tracerMaterial=source.tracerMaterial;
            var pine=maker.Build("pine"); var rock=maker.Build("rubble"); var pad=maker.Build("pad"); maker.Build("ground");
            AssetDatabase.CreateAsset(catalog,folder+"/BlackwoodCatalog.asset");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Blackwood V2").AddComponent<BlackwoodRoot>();
            root.catalog=catalog; root.pinePrefab=pine; root.rubblePrefab=rock; root.padPrefab=pad;
            string backdrop=Base+"/Art/MenuBackdrop.png";
            var importer=AssetImporter.GetAtPath(backdrop) as TextureImporter;
            if(importer!=null) { importer.textureType=TextureImporterType.Default; importer.mipmapEnabled=false; importer.npotScale=TextureImporterNPOTScale.None; importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Compressed; importer.SaveAndReimport(); }
            root.menuBackdrop=AssetDatabase.LoadAssetAtPath<Texture2D>(backdrop);
            if(root.menuBackdrop==null) Debug.LogWarning("MenuBackdrop.png was not found. The menu will use its solid fallback color.");
            string path=folder+"/BlackwoodPreview.unity";
            EditorSceneManager.SaveScene(scene,path); AssetDatabase.SaveAssets();
            Selection.activeGameObject=root.gameObject;
            Debug.Log("Blackwood V2 created: "+path+". Press Play. Separate save: blackwood-v2.json. Existing scripts, scene, catalog and v1 save were preserved.");
        }
    }

    internal sealed class BlackwoodPrefabMaker
    {
        public readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        private readonly string folder,meshPath;
        private readonly Mesh box,cylinder,ring,cone;
        private int meshIndex;
        public BlackwoodPrefabMaker(string folder)
        {
            this.folder=folder;
            var shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null) shader=Shader.Find("Standard");
            if(shader==null) throw new InvalidOperationException("URP/Lit or Standard shader is required.");
            AddMaterial("ivory","#D8D2B8",shader); AddMaterial("teal","#315D60",shader);
            AddMaterial("charcoal","#223239",shader); AddMaterial("orange","#E89243",shader);
            AddMaterial("mint","#72D6C0",shader,true); AddMaterial("amber","#F4B95C",shader,true);
            AddMaterial("soil","#65715A",shader); AddMaterial("foliage","#3D6154",shader);
            AddMaterial("rock","#626B68",shader);
            box=MakeBox(); box.name="Unit chamfer box"; meshPath=folder+"/Meshes.asset"; AssetDatabase.CreateAsset(box,meshPath);
            cylinder=Prism(12,new float[] {.44f,.5f,.5f,.44f},new float[] {-.5f,-.43f,.43f,.5f}); cylinder.name="Beveled cylinder"; SaveMesh(cylinder);
            cone=Prism(9,new float[] {.5f,0},new float[] {-.5f,.5f}); cone.name="Pine cone"; SaveMesh(cone);
            ring=Ring(); ring.name="Rotor guard"; SaveMesh(ring);
        }
        private void AddMaterial(string id,string hex,Shader shader,bool glow=false)
        {
            Color color; ColorUtility.TryParseHtmlString(hex,out color);
            var m=new Material(shader) {name="BW_"+id,color=color,enableInstancing=true};
            if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",.22f);
            if(m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness",.22f);
            if(glow && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*.7f); }
            AssetDatabase.CreateAsset(m,folder+"/BW_"+id+".mat"); Materials.Add(id,m);
        }
        public GameObject Build(string id)
        {
            var root=new GameObject("BW_"+id); var t=root.transform;
            try
            {
                switch(id)
                {
                    case "shelter": Cabin(t,false,false); break;
                    case "camp": Cabin(t,true,false); break;
                    case "lab": Cabin(t,false,true); break;
                    case "generator": Generator(t); break;
                    case "wall": Wall(t); break;
                    case "gun": Gun(t); break;
                    case "tesla": Tesla(t); break;
                    case "drone": Drone(t); break;
                    case "pine": Pine(t); break;
                    case "rubble": Rubble(t); break;
                    case "pad": Pad(t); break;
                    case "ground": P(t,"Soil",Vector3.zero,new Vector3(1,.02f,1),"soil"); break;
                    default: throw new ArgumentException(id);
                }
                Combine(t);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/BW_"+id+".prefab");
                if(prefab==null) throw new InvalidOperationException("Prefab creation failed: "+id);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private GameObject P(Transform parent,string name,Vector3 p,Vector3 s,string mat,Mesh mesh=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.transform.localPosition=p; go.transform.localScale=s;
            go.GetComponent<MeshFilter>().sharedMesh=mesh ?? box; go.GetComponent<MeshRenderer>().sharedMaterial=Materials[mat]; return go;
        }
        private void Foot(Transform t,float size)
        {
            P(t,"Foundation",new Vector3(0,.10f,0),new Vector3(size,.2f,size),"charcoal");
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                P(t,"Clamp",new Vector3(x*(size*.5f-.12f),.22f,z*(size*.5f-.12f)),new Vector3(.21f,.25f,.21f),"orange");
        }
        private void Cabin(Transform t,bool camp,bool lab)
        {
            Foot(t,1.82f);
            float h=camp ? .9f : 1.1f;
            P(t,"Cabin",new Vector3(0,.2f+h*.5f,0),new Vector3(1.64f,h,1.56f),camp ? "teal" : "ivory");
            P(t,"Roof",new Vector3(0,.23f+h,0),new Vector3(1.76f,.16f,1.68f),"ivory");
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                P(t,"Roof clamp",new Vector3(x*.74f,.23f+h,z*.70f),new Vector3(.22f,.2f,.26f),"orange");
            P(t,"Door frame",new Vector3(.39f,.58f,-.80f),new Vector3(.50f,.84f,.08f),"charcoal");
            P(t,"Door",new Vector3(.39f,.58f,-.849f),new Vector3(.34f,.69f,.03f),camp ? "orange" : "teal");
            P(t,"Window frame",new Vector3(-.36f,.73f,-.80f),new Vector3(.67f,.49f,.08f),"teal");
            P(t,"Window glow",new Vector3(-.36f,.73f,-.85f),new Vector3(.53f,.34f,.02f),lab ? "mint" : "amber");
            P(t,"Side frame",new Vector3(.837f,.74f,.12f),new Vector3(.03f,.48f,.70f),"charcoal");
            P(t,"Side window",new Vector3(.857f,.74f,.12f),new Vector3(.02f,.32f,.54f),lab ? "mint" : "amber");
            P(t,"Door lamp",new Vector3(.39f,1.035f,-.85f),new Vector3(.31f,.05f,.04f),"amber");
            if(lab)
            {
                P(t,"Instrument",new Vector3(.30f,1.64f,.35f),new Vector3(.66f,.57f,.66f),"teal",cylinder);
                P(t,"Signal band",new Vector3(.30f,1.80f,.35f),new Vector3(.69f,.07f,.69f),"mint",cylinder);
                P(t,"Instrument cap",new Vector3(.30f,1.93f,.35f),new Vector3(.73f,.17f,.73f),"ivory",cylinder);
            }
            else if(camp)
            {
                P(t,"Roof vent",new Vector3(.40f,1.26f,.32f),new Vector3(.35f,.17f,.36f),"charcoal");
                P(t,"Tool case",new Vector3(-.66f,.40f,-.64f),new Vector3(.28f,.20f,.26f),"orange");
            }
            else
            {
                for(int i=-1;i<=1;i++) P(t,"Roof rib",new Vector3(i*.48f,1.44f,0),new Vector3(.12f,.12f,1.35f),"teal");
                P(t,"Beacon socket",new Vector3(.57f,1.55f,.52f),new Vector3(.17f,.23f,.17f),"charcoal",cylinder);
                P(t,"Beacon",new Vector3(.57f,1.72f,.52f),new Vector3(.12f,.18f,.12f),"amber",cylinder);
                P(t,"Aerial",new Vector3(-.65f,1.68f,.50f),new Vector3(.045f,.60f,.045f),"charcoal");
            }
            Markers(t,1.49f);
        }
        private void Generator(Transform t)
        {
            Foot(t,1.84f);
            P(t,"Generator",new Vector3(0,.59f,-.35f),new Vector3(1.1f,.75f,.75f),"orange");
            P(t,"Grille",new Vector3(0,.59f,-.74f),new Vector3(.75f,.5f,.05f),"charcoal");
            for(int i=0;i<3;i++) P(t,"Grille rib",new Vector3(0,.42f+i*.17f,-.78f),new Vector3(.70f,.05f,.03f),"teal");
            for(int i=-1;i<=1;i++)
            {
                float h=i==0 ? 1.16f : .94f;
                P(t,"Battery",new Vector3(i*.5f,.22f+h*.5f,.30f),new Vector3(.40f,h,.40f),"ivory",cylinder);
                P(t,"Battery band",new Vector3(i*.5f,.22f+h,.30f),new Vector3(.43f,.065f,.43f),"mint",cylinder);
                P(t,"Battery cap",new Vector3(i*.5f,.33f+h,.30f),new Vector3(.44f,.14f,.44f),"teal",cylinder);
            }
            P(t,"Mast",new Vector3(0,1.22f,.70f),new Vector3(.15f,1.95f,.15f),"teal");
            P(t,"Crossbar",new Vector3(0,1.95f,.70f),new Vector3(1.10f,.14f,.15f),"charcoal");
            P(t,"Mast beacon",new Vector3(-.45f,2.1f,.70f),new Vector3(.15f,.20f,.15f),"amber",cylinder);
            Markers(t,.99f);
        }
        private void Wall(Transform t)
        {
            P(t,"Concrete foot",new Vector3(0,.15f,0),new Vector3(.95f,.3f,.40f),"ivory");
            P(t,"Wall plate",new Vector3(0,.57f,0),new Vector3(.9f,.64f,.17f),"teal");
            foreach(int x in new[]{-1,1}) P(t,"Post",new Vector3(x*.39f,.59f,0),new Vector3(.16f,.78f,.25f),"orange");
            P(t,"Top rail",new Vector3(0,.92f,0),new Vector3(.94f,.10f,.21f),"charcoal");
            P(t,"Signal",new Vector3(0,.78f,-.105f),new Vector3(.35f,.04f,.02f),"amber");
            Markers(t,.57f);
        }
        private void Gun(Transform t)
        {
            Foot(t,.87f);
            P(t,"Pedestal",new Vector3(0,.38f,0),new Vector3(.56f,.38f,.56f),"teal",cylinder);
            var yaw=new GameObject("Yaw").transform; yaw.SetParent(t,false); yaw.localPosition=new Vector3(0,.58f,0);
            P(yaw,"Gun housing",new Vector3(0,.19f,-.06f),new Vector3(.57f,.34f,.46f),"ivory");
            P(yaw,"Ammo box",new Vector3(.31f,.17f,-.05f),new Vector3(.19f,.27f,.35f),"orange");
            foreach(int x in new[]{-1,1}) P(yaw,"Barrel",new Vector3(x*.14f,.19f,.30f),new Vector3(.09f,.09f,.32f),"charcoal");
            P(yaw,"Sensor",new Vector3(0,.36f,.15f),new Vector3(.10f,.07f,.04f),"mint");
            Combine(yaw); var model=t.gameObject.AddComponent<BlackwoodModel>(); model.yaw=yaw; Markers(t,.35f);
        }
        private void Tesla(Transform t)
        {
            Foot(t,.87f); P(t,"Insulator",new Vector3(0,.8f,0),new Vector3(.34f,1.2f,.34f),"ivory",cylinder);
            for(int i=0;i<3;i++)
            {
                P(t,"Coil",new Vector3(0,.83f+i*.23f,0),new Vector3(.68f,.10f,.68f),"teal",cylinder);
                P(t,"Coil band",new Vector3(0,.78f+i*.23f,0),new Vector3(.59f,.04f,.59f),"mint",cylinder);
            }
            P(t,"Electrode",new Vector3(0,1.53f,0),new Vector3(.37f,.28f,.37f),"orange",cylinder); Markers(t,.42f);
        }
        private void Drone(Transform t)
        {
            P(t,"Body",Vector3.zero,new Vector3(.43f,.19f,.56f),"ivory");
            P(t,"Stripe",new Vector3(0,.105f,0),new Vector3(.13f,.035f,.51f),"orange");
            P(t,"Camera",new Vector3(0,-.04f,.29f),new Vector3(.12f,.09f,.05f),"mint");
            P(t,"Gun",new Vector3(0,-.16f,.16f),new Vector3(.09f,.10f,.28f),"charcoal");
            var rotors=new List<Transform>();
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
            {
                var arm=P(t,"Arm",new Vector3(x*.25f,0,z*.25f),new Vector3(.15f,.09f,.47f),"teal"); arm.transform.localRotation=Quaternion.Euler(0,x*z*45,0);
                P(t,"Rotor guard",new Vector3(x*.41f,.015f,z*.41f),new Vector3(.43f,.10f,.43f),"charcoal",ring);
                var spin=new GameObject("Rotor").transform; spin.SetParent(t,false); spin.localPosition=new Vector3(x*.41f,.016f,z*.41f);
                P(spin,"Blade",Vector3.zero,new Vector3(.33f,.02f,.055f),"teal");
                P(spin,"Blade cross",Vector3.zero,new Vector3(.055f,.02f,.33f),"teal"); Combine(spin); rotors.Add(spin);
            }
            foreach(int x in new[]{-1,1}) P(t,"Skid",new Vector3(x*.19f,-.21f,0),new Vector3(.06f,.08f,.5f),"charcoal");
            t.gameObject.AddComponent<BlackwoodModel>().rotors=rotors.ToArray();
        }
        private void Pine(Transform t)
        {
            P(t,"Trunk",new Vector3(0,.50f,0),new Vector3(.18f,1,.18f),"charcoal",cylinder);
            for(int i=0;i<3;i++) P(t,"Foliage",new Vector3(0,1.12f+i*.65f,0),new Vector3(1.50f-i*.36f,1.4f-i*.2f,1.50f-i*.36f),"foliage",cone);
        }
        private void Rubble(Transform t)
        {
            P(t,"Rock",new Vector3(-.18f,.34f,.08f),new Vector3(.54f,.68f,.64f),"rock");
            P(t,"Rock",new Vector3(.26f,.24f,-.1f),new Vector3(.42f,.48f,.54f),"rock");
            var slab=P(t,"Slab",new Vector3(.16f,.42f,.22f),new Vector3(.45f,.6f,.15f),"ivory"); slab.transform.localRotation=Quaternion.Euler(0,20,-15);
        }
        private void Pad(Transform t)
        {
            Foot(t,1.8f); P(t,"Landing inset",new Vector3(0,.212f,0),new Vector3(1.43f,.025f,1.43f),"ivory");
            P(t,"Landing panel",new Vector3(0,.23f,0),new Vector3(1.27f,.025f,1.27f),"teal");
            P(t,"Mark",new Vector3(0,.25f,0),new Vector3(.65f,.02f,.20f),"orange");
            P(t,"Mark",new Vector3(0,.25f,0),new Vector3(.20f,.02f,.65f),"orange");
        }
        private void Markers(Transform t,float y)
        {
            var model=t.GetComponent<BlackwoodModel>(); if(model==null) model=t.gameObject.AddComponent<BlackwoodModel>();
            model.levelMarkers=new GameObject[2];
            for(int i=0;i<2;i++)
            {
                var marker=new GameObject("Level "+(i+2)); marker.transform.SetParent(t,false);
                P(marker.transform,"Rank tab",new Vector3(-.10f+i*.20f,y,-.10f),new Vector3(.12f,.055f,.22f),"orange");
                model.levelMarkers[i]=marker; marker.SetActive(false);
            }
        }
        private void Combine(Transform parent)
        {
            var batches=new Dictionary<Material,List<CombineInstance>>(); var remove=new List<GameObject>();
            foreach(var filter in parent.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.transform.parent!=parent) continue;
                var renderer=filter.GetComponent<MeshRenderer>(); if(renderer==null) continue;
                var material=renderer.sharedMaterial;
                if(!batches.ContainsKey(material)) batches.Add(material,new List<CombineInstance>());
                batches[material].Add(new CombineInstance {mesh=filter.sharedMesh,transform=parent.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                remove.Add(filter.gameObject);
            }
            foreach(var go in remove) UnityEngine.Object.DestroyImmediate(go);
            foreach(var batch in batches)
            {
                var mesh=new Mesh {name=parent.name+"_"+batch.Key.name+"_"+(meshIndex++)};
                mesh.CombineMeshes(batch.Value.ToArray(),true,true); mesh.RecalculateBounds(); SaveMesh(mesh);
                var go=new GameObject(batch.Key.name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
        }
        private void SaveMesh(Mesh m) { AssetDatabase.AddObjectToAsset(m,meshPath); }
        private static Mesh MakeBox()
        {
            Vector2[] p={new Vector2(.4f,-.5f),new Vector2(.5f,-.4f),new Vector2(.5f,.4f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.4f),new Vector2(-.5f,-.4f),new Vector2(-.4f,-.5f)};
            var rings=new List<Vector3[]>();
            for(int k=0;k<4;k++)
            {
                var a=new Vector3[8]; float s=k==0 || k==3 ? .9f : 1; float y=k==0 ? -.5f : k==1 ? -.45f : k==2 ? .45f : .5f;
                for(int i=0;i<8;i++) a[i]=new Vector3(p[i].x*s,y,p[i].y*s); rings.Add(a);
            }
            return ClosedRings(rings);
        }
        private static Mesh Prism(int n,float[] radius,float[] y)
        {
            var rings=new List<Vector3[]>();
            for(int k=0;k<radius.Length;k++)
            { var a=new Vector3[n]; for(int i=0;i<n;i++) { float angle=i*Mathf.PI*2/n; a[i]=new Vector3(Mathf.Cos(angle)*radius[k],y[k],Mathf.Sin(angle)*radius[k]); } rings.Add(a); }
            return ClosedRings(rings);
        }
        private static Mesh ClosedRings(List<Vector3[]> r)
        {
            var v=new List<Vector3>(); var indices=new List<int>(); int n=r[0].Length;
            for(int k=0;k<r.Count-1;k++) for(int i=0;i<n;i++) Quad(v,indices,r[k][i],r[k+1][i],r[k+1][(i+1)%n],r[k][(i+1)%n]);
            for(int i=0;i<n;i++)
            {
                Tri(v,indices,new Vector3(0,r[0][0].y,0),r[0][i],r[0][(i+1)%n]);
                int last=r.Count-1; Tri(v,indices,new Vector3(0,r[last][0].y,0),r[last][(i+1)%n],r[last][i]);
            }
            return Finish(v,indices);
        }
        private static Mesh Ring()
        {
            var v=new List<Vector3>(); var idx=new List<int>();
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                Vector3 oa=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f),ob=new Vector3(Mathf.Cos(b)*.5f,-.5f,Mathf.Sin(b)*.5f);
                Vector3 ia=new Vector3(Mathf.Cos(a)*.37f,-.5f,Mathf.Sin(a)*.37f),ib=new Vector3(Mathf.Cos(b)*.37f,-.5f,Mathf.Sin(b)*.37f);
                Quad(v,idx,oa,oa+Vector3.up,ob+Vector3.up,ob); Quad(v,idx,ib,ib+Vector3.up,ia+Vector3.up,ia);
                Quad(v,idx,ia+Vector3.up,ib+Vector3.up,ob+Vector3.up,oa+Vector3.up); Quad(v,idx,ia,oa,ob,ib);
            }
            return Finish(v,idx);
        }
        private static void Tri(List<Vector3> v,List<int> idx,Vector3 a,Vector3 b,Vector3 c)
        { int n=v.Count; v.Add(a); v.Add(b); v.Add(c); idx.Add(n); idx.Add(n+1); idx.Add(n+2); }
        private static void Quad(List<Vector3> v,List<int> idx,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        { Tri(v,idx,a,b,c); Tri(v,idx,a,c,d); }
        private static Mesh Finish(List<Vector3> v,List<int> idx)
        { var mesh=new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(idx,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
    }
}

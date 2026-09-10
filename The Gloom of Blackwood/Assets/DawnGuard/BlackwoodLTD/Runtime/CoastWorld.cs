using System;
using System.Collections.Generic;
using DawnGuard.BlackwoodV2;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastWorld : MonoBehaviour
    {
        CoastRoot root;CoastCatalog catalog;Transform terrain,units,menuActors;
        public Camera Camera {get;private set;}
        Light sun;float nightBlend,zoom=13.5f;Vector3 pan;
        Material ink,mint,orange,cyan,soil,pebble,grass;
        GameObject drone,preview;Transform[] droneRotors;
        LineRenderer range,selection,grid,construction;
        LineRenderer[] routes=new LineRenderer[3];
        sealed class Unit{public GameObject go;public Animator animator;public string kind;public Transform yaw;public Quaternion yawBase;public bool dying;public float deathTime;public Transform cargo;public Transform[] limbs;public Vector3 previous;public LineRenderer health,healthBack;}
        Dictionary<int,Unit> buildings=new Dictionary<int,Unit>(),enemies=new Dictionary<int,Unit>(),workers=new Dictionary<int,Unit>();
        List<Unit> corpses=new List<Unit>();Dictionary<string,Stack<Unit>> pool=new Dictionary<string,Stack<Unit>>();
        List<Unit> menuZombies=new List<Unit>();
        readonly List<int> remove=new List<int>();
        sealed class Fx{public LineRenderer line;public float remaining,duration;}
        Fx[] shots=new Fx[64];int nextShot;
        float constructionTime;Vector3 constructionTarget;int lastRevision=-1;
        static Color Hex(string hex){Color c;ColorUtility.TryParseHtmlString(hex,out c);return c;}
        static Material Mat(string name,Color c,bool lit=true){var m=new Material(Shader.Find(lit?"Universal Render Pipeline/Lit":"Universal Render Pipeline/Unlit"));m.name=name;m.color=c;if(lit){m.SetFloat("_Smoothness",.22f);m.SetFloat("_Metallic",.15f);}return m;}
        public void Initialize(CoastRoot owner,CoastCatalog assets)
        {
            root=owner;catalog=assets;terrain=new GameObject("Coast environment").transform;terrain.SetParent(transform);
            units=new GameObject("Campaign entities").transform;units.SetParent(transform);menuActors=new GameObject("Living menu").transform;menuActors.SetParent(transform);
            ink=Mat("Coal",Hex("#1B3038"));mint=Mat("Mint",Hex("#6AE1C9"),false);orange=Mat("Amber",Hex("#F2A04C"),false);cyan=Mat("Arcane",Hex("#73CFFE"),false);
            soil=Mat("Sand rocks",Hex("#76634B"));pebble=Mat("Slate",Hex("#647074"));grass=Mat("Undergrowth",Hex("#445B32"));
            Camera=new GameObject("Coastal Game Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();Camera.transform.SetParent(transform);Camera.orthographic=true;Camera.nearClipPlane=.1f;Camera.farClipPlane=150;Camera.backgroundColor=Hex("#17343F");Camera.clearFlags=CameraClearFlags.SolidColor;
            var extra=Camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=false;extra.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            sun=new GameObject("Coastal sun").AddComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;sun.transform.rotation=Quaternion.Euler(52,-28,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.fog=false;
            BuildTerrain();BuildCamp();BuildMenuActors();
            drone=Place(catalog.source.dronePrefab,units,new Vector3(14,2.2f,6),1.15f,.65f);var legacy=drone.GetComponentInChildren<BlackwoodModel>();droneRotors=legacy!=null?legacy.rotors:new Transform[0];
            preview=Part("Placement ghost",units,Vector3.zero,new Vector3(.97f,.08f,.97f),mint);preview.SetActive(false);
            range=Line("Range",mint,.028f,units);selection=Line("Selection",orange,.05f,units);grid=Line("Local grid",mint,.014f,units);construction=Line("Construction beam",cyan,.035f,units);
            for(int i=0;i<3;i++)routes[i]=Line("Route "+i,orange,.065f,terrain);
            for(int i=0;i<shots.Length;i++)shots[i]=new Fx {line=Line("Effect "+i,orange,.04f,units)};
            Refresh(0);
        }
        public static GameObject Place(GameObject prefab,Transform parent,Vector3 at,float width,float height)
        {
            GameObject holder=new GameObject(prefab==null?"Missing model":prefab.name);holder.transform.SetParent(parent,false);
            if(prefab!=null)
            {
                var model=Instantiate(prefab,holder.transform);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
                foreach(var c in model.GetComponentsInChildren<Collider>(true)){if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);}
                foreach(var l in model.GetComponentsInChildren<Light>(true))l.enabled=false;
                foreach(var b in model.GetComponentsInChildren<BlackwoodModel>(true))b.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length>0){Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    float scale=Mathf.Min(width/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z)),height/Mathf.Max(.01f,bounds.size.y));model.transform.localScale*=scale;
                    bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-holder.transform.position;}
            }
            holder.transform.position=at;return holder;
        }
        GameObject Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(Application.isPlaying)Destroy(go.GetComponent<Collider>());else DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        LineRenderer Line(string name,Material mat,float width,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=mat;l.startWidth=l.endWidth=width;l.numCapVertices=2;l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;l.positionCount=0;l.useWorldSpace=true;return l;
        }
        void BuildTerrain()
        {
            var ground=new GameObject("Shaped shore",typeof(MeshFilter),typeof(MeshRenderer));ground.transform.SetParent(terrain);
            const int columns=64,rows=40;var vertices=new Vector3[(columns+1)*(rows+1)];var indices=new List<int>();
            for(int x=0;x<=columns;x++)for(int z=0;z<=rows;z++){float xx=-16+x,shore=-1.4f+Mathf.Sin(xx*.27f)*.43f+Mathf.Sin(xx*.81f)*.15f;vertices[x*(rows+1)+z]=new Vector3(xx,0,Mathf.Lerp(shore,40,z/(float)rows));if(x<columns&&z<rows){int n=x*(rows+1)+z;indices.AddRange(new[]{n,n+1,n+rows+1,n+1,n+rows+2,n+rows+1});}}
            var mesh=new Mesh{name="Coastline mesh"};mesh.vertices=vertices;mesh.triangles=indices.ToArray();mesh.RecalculateNormals();ground.GetComponent<MeshFilter>().sharedMesh=mesh;ground.GetComponent<MeshRenderer>().sharedMaterial=catalog.ground;
            Part("Sea",terrain,new Vector3(14,-.14f,-13),new Vector3(90,.12f,30),catalog.water);
            var random=new System.Random(7341);
            for(int i=0;i<110;i++)
            {
                float x,z;if(i<70){bool left=i%2==0;x=left?-2-(float)random.NextDouble()*9:30+(float)random.NextDouble()*9;z=5+(float)random.NextDouble()*25;}
                else{x=-7+(float)random.NextDouble()*42;z=26+(float)random.NextDouble()*12;}
                var tree=Place(catalog.pine,terrain,new Vector3(x,0,z),2.5f,3.8f+(float)random.NextDouble()*1.8f);tree.transform.Rotate(0,random.Next(360),0);
            }
            for(int x=0;x<root.Game.Map.Width;x++)for(int z=0;z<root.Game.Map.Depth;z++)if(root.Game.Map.Rocks[x,z])
            {var rock=Place(catalog.basalt??catalog.rock,terrain,new Vector3(x+.5f,0,z+.5f),1.30f,1.2f);rock.transform.Rotate(0,random.Next(360),0);
                if(z>=22||((x==9||x==19)&&z>16&&z%3==0))Place(catalog.pine,terrain,new Vector3(x+.5f,.35f,z+.5f),1.6f,2.5f+(float)random.NextDouble());}
            for(int i=0;i<75;i++)
            {
                float x=-4+(float)random.NextDouble()*36,z=-2+(float)random.NextDouble()*28;
                if(x>1&&x<27&&z>4&&z<24)continue;
                float size=.15f+(float)random.NextDouble()*.55f;var rock=Place(catalog.basalt??catalog.rock,terrain,new Vector3(x,0,z),size,size*.6f);rock.transform.Rotate(0,random.Next(360),0);
            }
            foreach(float x in new[]{1.5f,26.5f})
            {Place(catalog.ore??catalog.rock,terrain,new Vector3(x,0,1.3f),2.8f,1.4f);Place(catalog.basalt??catalog.rock,terrain,new Vector3(x+.8f,0,.1f),1.8f,.95f);}
            Place(catalog.dock,terrain,new Vector3(11,-.15f,-1.7f),2.9f,1.3f);Place(catalog.dock,terrain,new Vector3(18,-.15f,-1.9f),2.9f,1.3f);
            if(catalog.fern!=null)for(int i=0;i<260;i++)
            {float x=-3+(float)random.NextDouble()*34,z=3+(float)random.NextDouble()*24;int xx=Mathf.Clamp((int)x,0,27),zz=Mathf.Clamp((int)z,0,23);
                if(x>1&&x<27&&z<22&&!root.Game.Map.Rocks[xx,zz]&&i%5!=0)continue;
                var fern=Place(catalog.fern,terrain,new Vector3(x,.01f,z),.6f+(float)random.NextDouble()*.55f,.4f);fern.transform.Rotate(0,random.Next(360),0);}
            for(int x=3;x<=25;x+=2)Part("Service track marker",terrain,new Vector3(x,.015f,2),new Vector3(.45f,.01f,.08f),soil);
            for(int front=0;front<3;front++){var e=root.Game.Map.Entrances[front];Label(terrain,CoastSession.FrontName(front),new Vector3(e.x+1,.09f,e.z+1.5f),.15f,Hex("#E2C8A2"));}
        }
        void BuildCamp()
        {
            Place(catalog.Defense("camp"),terrain,new Vector3(4.7f,0,5),3.5f,2.7f);Label(terrain,"КАЗАРМА",new Vector3(4.7f,.12f,3.6f),.13f,Color.white);
            Place(catalog.Defense("camp"),terrain,new Vector3(9,0,4.6f),2.9f,2.2f);Label(terrain,"ОРУЖЕЙНАЯ",new Vector3(9,.12f,3.4f),.12f,Color.white);
            Place(catalog.Defense("shelter"),terrain,new Vector3(14,0,5.2f),4.5f,3.0f);Label(terrain,"ЛАГЕРЬ",new Vector3(14,.12f,3.5f),.14f,Color.white);
            Place(catalog.Defense("lab"),terrain,new Vector3(20,0,5),3.3f,2.8f);Label(terrain,"ЛАБОРАТОРИЯ",new Vector3(20,.12f,3.5f),.115f,Color.white);
            Place(catalog.Defense("generator"),terrain,new Vector3(25,0,5),2.8f,2.7f);Label(terrain,"ЭНЕРГИЯ",new Vector3(25,.12f,3.4f),.12f,Color.white);
            Place(catalog.pad,terrain,new Vector3(24,0,1),2,.4f);
            Part("Supply depot",terrain,new Vector3(14,.28f,2),new Vector3(1.2f,.55f,.65f),ink);
            for(int i=0;i<4;i++)
            {
                float x=6+i*5;Place(catalog.lantern,terrain,new Vector3(x,0,6.8f),.4f,1.6f);
                var light=new GameObject("Camp light").AddComponent<Light>();light.transform.SetParent(terrain);light.transform.position=new Vector3(x,1.5f,5.8f);light.type=LightType.Point;light.range=4.3f;light.intensity=2.0f;light.color=Hex("#FFC277");light.shadows=LightShadows.None;
            }
        }
        public static void Label(Transform parent,string text,Vector3 position,float size,Color color)
        {
            var go=new GameObject(text);go.transform.SetParent(parent);go.transform.position=position;go.transform.rotation=Quaternion.Euler(50,0,0);
            var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=48;t.characterSize=size*.68f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        }
        Unit Actor(GameObject prefab,string kind,Vector3 at,float size,Transform parent)
        {
            var go=Place(prefab,parent,at,size*.95f,size*1.6f);var a=go.GetComponentInChildren<Animator>();
            if(a!=null){a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);
                // Imported animation bounds include every clip; normalize the actual current pose.
                Bounds actual=new Bounds();bool first=true;
                foreach(var skinned in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {var baked=new Mesh();skinned.BakeMesh(baked);foreach(var v in baked.vertices){Vector3 p=skinned.transform.TransformPoint(v);if(first){actual=new Bounds(p,Vector3.zero);first=false;}else actual.Encapsulate(p);}if(Application.isPlaying)Destroy(baked);else DestroyImmediate(baked);}
                if(!first&&actual.size.y>.01f){float factor=size*1.6f/actual.size.y;go.transform.localScale*=factor;Vector3 offset=(actual.center-at)*factor;offset.y=(actual.min.y-at.y)*factor;go.transform.GetChild(0).position-=offset;}}
            return new Unit{go=go,animator=a,kind=kind,previous=at};
        }
        void BuildMenuActors()
        {
            Part("Menu headland",menuActors,new Vector3(112,-.18f,8),new Vector3(55,.35f,20),catalog.ground);
            Part("Menu sea",menuActors,new Vector3(112,-.32f,-12),new Vector3(70,.1f,23),catalog.water);
            Place(catalog.Defense("camp"),menuActors,new Vector3(115,0,3),8.2f,5.8f);
            Place(catalog.Defense("lab"),menuActors,new Vector3(122,0,6),3.7f,3.1f);
            Place(catalog.pad,menuActors,new Vector3(119,0,0),2.5f,.35f);
            Place(catalog.source.dronePrefab,menuActors,new Vector3(119,1.7f,0),1.6f,.9f);
            var random=new System.Random(539);
            for(int i=0;i<40;i++){float x=91+(float)random.NextDouble()*45,z=14+(float)random.NextDouble()*7;Place(catalog.pine,menuActors,new Vector3(x,0,z),2.5f+(float)random.NextDouble()*2,5+i%4);}
            for(int i=0;i<18;i++){float x=99+(float)random.NextDouble()*31;var rock=Place(catalog.basalt??catalog.rock,menuActors,new Vector3(x,-.1f,-1.4f+Mathf.Sin(i)*.6f),1.7f+(float)random.NextDouble(),1.2f);rock.transform.Rotate(0,random.Next(360),0);}
            if(catalog.fern!=null)for(int i=0;i<40;i++)Place(catalog.fern,menuActors,new Vector3(100+(float)random.NextDouble()*30,0,2+(float)random.NextDouble()*10),.8f,.4f);
            for(int i=0;i<4;i++){var u=Actor(catalog.Enemy(i==0?"brute":"walker"),"menu",new Vector3(118+i*1.5f,0,9),i==0?2.0f:1.4f,menuActors);menuZombies.Add(u);}
            var lamp=new GameObject("Menu amber light").AddComponent<Light>();lamp.transform.SetParent(menuActors);lamp.transform.position=new Vector3(115,3,1);lamp.type=LightType.Point;lamp.range=9;lamp.intensity=6;lamp.color=Hex("#FFC078");
        }
        static void Animate(Unit u,bool moving,bool attacking,bool paused,float speed)
        {
            if(u.animator==null)return;u.animator.speed=paused?0:1;
            u.animator.SetFloat("Speed",speed);u.animator.SetBool("IsMoving",moving);u.animator.SetBool("IsAttacking",attacking);
        }
        public void Refresh(float dt)
        {
            var g=root.Game;bool menu=root.MenuOpen;menuActors.gameObject.SetActive(menu);terrain.gameObject.SetActive(!menu);units.gameObject.SetActive(!menu);
            float targetNight=menu?.45f:g.S.phase==CoastPhase.Night||g.S.phase==CoastPhase.Defeat?1:g.S.phase==CoastPhase.Day?Mathf.Clamp01((18-g.S.remaining)/18)*.35f:0;
            nightBlend=Mathf.MoveTowards(nightBlend,targetNight,dt*.5f);
            sun.color=Color.Lerp(Hex("#FFE9BC"),Hex("#ACC8EC"),nightBlend);sun.intensity=Mathf.Lerp(1.55f,.65f,nightBlend);
            RenderSettings.ambientSkyColor=Color.Lerp(Hex("#829AB0"),Hex("#485E80"),nightBlend);RenderSettings.ambientEquatorColor=Color.Lerp(Hex("#6D765B"),Hex("#314557"),nightBlend);RenderSettings.ambientGroundColor=Color.Lerp(Hex("#4A4937"),Hex("#223545"),nightBlend);
            Vector3 focus=menu?new Vector3(110.5f,1.0f,5):new Vector3(14,0,10.5f)+pan;float elevation=menu?26:50;
            float camSize=menu?7.3f:zoom;Camera.orthographicSize=Mathf.Lerp(Camera.orthographicSize,camSize,dt<=0?1:Mathf.Clamp01(dt*5));
            Vector3 desired=focus+new Vector3(0,Mathf.Sin(elevation*Mathf.Deg2Rad),-Mathf.Cos(elevation*Mathf.Deg2Rad))*40;
            Camera.transform.position=Vector3.Lerp(Camera.transform.position,desired,dt<=0?1:Mathf.Clamp01(dt*5));Camera.transform.LookAt(focus);
            if(menu){for(int i=0;i<menuZombies.Count;i++){var u=menuZombies[i];float t=Time.unscaledTime*.16f+i*1.7f;Vector3 pos=new Vector3(117+i*1.8f+Mathf.Sin(t)*1.1f,0,9+Mathf.Cos(t)*1.5f+i%2);Vector3 delta=pos-u.go.transform.position;u.go.transform.position=pos;if(delta.sqrMagnitude>.00001f)u.go.transform.rotation=Quaternion.LookRotation(delta);Animate(u,true,false,false,.7f);}return;}
            SyncBuildings();SyncEnemies(dt);SyncWorkers(dt);
            Vector3 droneTarget=new Vector3(g.S.droneX,2.2f,g.S.droneZ);
            if(constructionTime>0){constructionTime-=root.Paused?0:dt;droneTarget=constructionTarget+Vector3.up*2.2f;construction.positionCount=2;construction.SetPosition(0,drone.transform.position);construction.SetPosition(1,constructionTarget+Vector3.up*.3f);}else construction.positionCount=0;
            drone.transform.position=Vector3.Lerp(drone.transform.position,droneTarget,dt*5);if(!root.Paused&&droneRotors!=null)foreach(var rotor in droneRotors)if(rotor!=null)rotor.Rotate(0,850*dt,0,Space.Self);
            if(g.S.repairRemaining>0){var target=g.Building(g.S.repairTarget);if(target!=null){construction.positionCount=2;construction.SetPosition(0,drone.transform.position);construction.SetPosition(1,new Vector3(target.x+.5f,.5f,target.z+.5f));}}
            if(lastRevision!=g.Map.Revision){lastRevision=g.Map.Revision;for(int front=0;front<3;front++){var cells=g.Map.Route(front);routes[front].positionCount=cells.Count;for(int i=0;i<cells.Count;i++)routes[front].SetPosition(i,new Vector3(cells[i].x+1,.045f,cells[i].z+1));}}
            bool showRoutes=g.S.phase==CoastPhase.Day&&(root.SelectedTool!=null||root.Moving);for(int i=0;i<3;i++){routes[i].enabled=showRoutes;routes[i].startWidth=routes[i].endWidth=i==root.WatchedFront?.08f:.028f;}
            foreach(var fx in shots){if(root.Paused)continue;if(fx.remaining>0){fx.remaining-=dt;fx.line.enabled=fx.remaining>0;fx.line.widthMultiplier=Mathf.Max(.05f,fx.remaining/fx.duration);}}
            var selected=g.Building(root.SelectedBuilding);selection.positionCount=selected==null?0:5;if(selected!=null)Square(selection,selected.x+.5f,selected.z+.5f,.65f);
            if(root.SelectedTool==null&&selected!=null){var d=g.Rules.Defense(selected.kind);Circle(range,new Vector3(selected.x+.5f,.06f,selected.z+.5f),d.range);}
        }
        void SyncBuildings()
        {
            var g=root.Game;remove.Clear();foreach(var kv in buildings)if(g.Building(kv.Key)==null)remove.Add(kv.Key);foreach(int id in remove){Destroy(buildings[id].go);buildings.Remove(id);}
            foreach(var b in g.S.buildings)
            {
                Unit u;if(!buildings.TryGetValue(b.id,out u)){var go=Place(catalog.Defense(b.kind),units,new Vector3(b.x+.5f,0,b.z+.5f),b.kind=="wall"?.99f:1.2f,b.kind=="wall"?.75f:1.65f);u=new Unit{go=go,kind=b.kind};var model=go.GetComponentInChildren<BlackwoodModel>();if(model!=null){u.yaw=model.yaw;if(u.yaw!=null)u.yawBase=u.yaw.localRotation;}buildings[b.id]=u;
                    if(b.kind=="cryo")TintEmission(go,Hex("#76DAFF"));if(b.kind=="arcane")TintEmission(go,Hex("#A693FF"));}
                u.go.transform.position=new Vector3(b.x+.5f,0,b.z+.5f);var target=g.S.enemies.Find(e=>e.id==b.target);
                if(u.yaw!=null&&target!=null&&!root.Paused){var v=new Vector3(target.x-u.go.transform.position.x,0,target.z-u.go.transform.position.z);if(v.sqrMagnitude>.01f)u.yaw.rotation=Quaternion.LookRotation(v);}
            }
        }
        void TintEmission(GameObject go,Color color)
        {foreach(var r in go.GetComponentsInChildren<Renderer>())if(r.name.Contains("Cyan")){var m=r.material;m.color=color;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.3f);}}
        void SyncEnemies(float dt)
        {
            var g=root.Game;remove.Clear();foreach(var kv in enemies)if(!g.S.enemies.Exists(e=>e.id==kv.Key))remove.Add(kv.Key);
            foreach(int id in remove){var u=enemies[id];enemies.Remove(id);u.dying=true;u.deathTime=3.5f;if(u.animator!=null)u.animator.SetTrigger("Die");corpses.Add(u);}
            foreach(var e in g.S.enemies)
            {
                Unit u;if(!enemies.TryGetValue(e.id,out u)){Stack<Unit> stack;if(pool.TryGetValue(e.kind,out stack)&&stack.Count>0){u=stack.Pop();u.go.SetActive(true);u.dying=false;if(u.animator!=null){u.animator.Rebind();u.animator.Update(0);}}
                    else{float size=e.kind=="boss"?1.6f:e.kind=="brute"||e.kind=="armored"?1.0f:.72f;u=Actor(catalog.Enemy(e.kind),e.kind,new Vector3(e.x,0,e.z),size,units);}
                    enemies[e.id]=u;}
                if(u.health==null){u.healthBack=Line("Health background",ink,.09f,u.go.transform);u.health=Line("Health",e.kind=="boss"||e.kind=="sapper"?orange:e.kind=="armored"?cyan:mint,.045f,u.go.transform);}
                Vector3 pos=new Vector3(e.x+(e.id%3-1)*.18f,0,e.z+((e.id/3)%3-1)*.13f);Vector3 direction=pos-u.go.transform.position;
                u.go.transform.position=pos;if(e.attacking){var b=g.Building(e.wallTarget);direction=(b==null?new Vector3(14,0,5):new Vector3(b.x+.5f,0,b.z+.5f))-pos;}
                if(direction.sqrMagnitude>.0001f&&!root.Paused)u.go.transform.rotation=Quaternion.Slerp(u.go.transform.rotation,Quaternion.LookRotation(direction),dt*12);
                Animate(u,e.moving,e.attacking,root.Paused,e.moving?g.Rules.Enemy(e.kind).speed:0);
                float barHeight=e.kind=="boss"?2.9f:e.kind=="brute"||e.kind=="armored"?1.9f:1.45f;Vector3 bar=pos+Vector3.up*barHeight;
                u.healthBack.positionCount=u.health.positionCount=2;u.healthBack.SetPosition(0,bar-Vector3.right*.35f);u.healthBack.SetPosition(1,bar+Vector3.right*.35f);
                u.health.SetPosition(0,bar-Vector3.right*.34f-Vector3.forward*.012f);u.health.SetPosition(1,bar+Vector3.right*(-.34f+.68f*Mathf.Clamp01(e.hp/g.Rules.Enemy(e.kind).hp))-Vector3.forward*.012f);
            }
            for(int i=corpses.Count-1;i>=0;i--){var u=corpses[i];if(u.animator!=null)u.animator.speed=root.Paused?0:1;if(!root.Paused)u.deathTime-=dt;if(u.deathTime<=0){u.go.SetActive(false);Stack<Unit> stack;if(!pool.TryGetValue(u.kind,out stack)){stack=new Stack<Unit>();pool[u.kind]=stack;}stack.Push(u);corpses.RemoveAt(i);}}
        }
        void SyncWorkers(float dt)
        {
            foreach(var w in root.Game.S.workers)
            {
                Unit u;if(!workers.TryGetValue(w.id,out u)){var go=Place(catalog.worker,units,new Vector3(w.x,0,w.z),.75f,1.05f);var limbs=new List<Transform>();foreach(var t in go.GetComponentsInChildren<Transform>()){if(t.name=="LegLeft"||t.name=="LegRight")limbs.Add(t);}
                    u=new Unit{go=go,limbs=limbs.ToArray(),cargo=Array.Find(go.GetComponentsInChildren<Transform>(),t=>t.name=="Cargo")};workers[w.id]=u;}
                u.go.SetActive(w.job!=WorkerJob.Sheltered);Vector3 pos=new Vector3(w.x,0,w.z+(w.id%3-1)*.25f);var direction=pos-u.go.transform.position;u.go.transform.position=pos;
                if(direction.sqrMagnitude>.0001f&&!root.Paused)u.go.transform.rotation=Quaternion.Slerp(u.go.transform.rotation,Quaternion.LookRotation(direction),dt*12);
                if(u.cargo!=null)u.cargo.gameObject.SetActive(w.cargo>0);
                if(!root.Paused&&u.limbs!=null)for(int i=0;i<u.limbs.Length;i++)u.limbs[i].localRotation=Quaternion.Euler(Mathf.Sin(Time.time*12+i*Mathf.PI+w.id)*((w.job==WorkerJob.Outbound||w.job==WorkerJob.Inbound)?24:3),0,0);
            }
        }
        public void OnEvent(CoastEvent e)
        {
            if(e.kind=="build"){constructionTime=1.5f;constructionTarget=new Vector3(e.x,0,e.z);return;}
            if(e.kind=="gun"||e.kind=="tesla"||e.kind=="arcane"||e.kind=="cryo")
            {
                var fx=shots[nextShot++%shots.Length];fx.remaining=fx.duration=e.kind=="gun"?.10f:.23f;fx.line.enabled=true;fx.line.sharedMaterial=e.kind=="gun"?orange:e.kind=="arcane"?mint:cyan;fx.line.widthMultiplier=1;
                fx.line.positionCount=e.kind=="tesla"?5:2;
                Vector3 a=new Vector3(e.x,1.15f,e.z),b=new Vector3(e.toX,.75f,e.toZ);for(int i=0;i<fx.line.positionCount;i++){float t=i/(float)(fx.line.positionCount-1);fx.line.SetPosition(i,Vector3.Lerp(a,b,t)+(i>0&&i<4&&e.kind=="tesla"?Vector3.up*Mathf.Sin(i*5)*.25f:Vector3.zero));}
            }
        }
        public bool TryGround(Vector2 screen,out Vector3 point){var ray=Camera.ScreenPointToRay(screen);float distance;var plane=new Plane(Vector3.up,Vector3.zero);if(plane.Raycast(ray,out distance)){point=ray.GetPoint(distance);return true;}point=Vector3.zero;return false;}
        public void Preview(Vector2 screen)
        {
            string kind=root.SelectedTool;var moving=root.Game.Building(root.SelectedBuilding);if(root.Moving&&moving!=null)kind=moving.kind;
            if(kind==null||root.Game.S.phase!=CoastPhase.Day||root.Paused||root.Hud!=null&&root.Hud.ServiceOpen){ClearPreview();return;}
            Vector3 p;if(!TryGround(screen,out p))return;int x=Mathf.FloorToInt(p.x),z=Mathf.FloorToInt(p.z);string reason;
            bool valid=root.Game.CanPlace(kind,x,z,out reason,root.Moving?root.SelectedBuilding:0);preview.SetActive(true);preview.transform.position=new Vector3(x+.5f,.05f,z+.5f);preview.GetComponent<Renderer>().sharedMaterial=valid?mint:orange;
            Circle(range,new Vector3(x+.5f,.07f,z+.5f),root.Game.Rules.Defense(kind).range);LocalGrid(grid,x+.5f,z+.5f);
        }
        static void LocalGrid(LineRenderer l,float x,float z)
        {var points=new List<Vector3>();for(int i=0;i<=6;i++){float a=i%2==0?-3:3;points.Add(new Vector3(x+a,.06f,z-3+i));points.Add(new Vector3(x-a,.06f,z-3+i));}for(int i=0;i<=6;i++){float a=i%2==0?3:-3;points.Add(new Vector3(x+3-i,.06f,z+a));points.Add(new Vector3(x+3-i,.06f,z-a));}l.positionCount=points.Count;l.SetPositions(points.ToArray());}
        static void Square(LineRenderer l,float x,float z,float radius){l.positionCount=5;l.SetPositions(new[]{new Vector3(x-radius,.06f,z-radius),new Vector3(x-radius,.06f,z+radius),new Vector3(x+radius,.06f,z+radius),new Vector3(x+radius,.06f,z-radius),new Vector3(x-radius,.06f,z-radius)});}
        static void Circle(LineRenderer l,Vector3 c,float radius){if(radius<=0){l.positionCount=0;return;}l.positionCount=65;for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;l.SetPosition(i,c+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}}
        public void ClearPreview(){if(preview!=null)preview.SetActive(false);if(grid!=null)grid.positionCount=0;if(range!=null&&root.SelectedBuilding==0)range.positionCount=0;}
        public void Zoom(float delta){zoom=Mathf.Clamp(zoom+delta,9,16);}
        public void Pan(Vector2 delta){pan.x=Mathf.Clamp(pan.x-delta.x*.022f,-5,5);pan.z=Mathf.Clamp(pan.z-delta.y*.026f,-4,4);}
        public void ResetUnits(){foreach(var d in new[]{buildings,enemies,workers}){foreach(var kv in d)Destroy(kv.Value.go);d.Clear();}foreach(var u in corpses)Destroy(u.go);corpses.Clear();foreach(var stack in pool.Values)foreach(var u in stack)Destroy(u.go);pool.Clear();lastRevision=-1;}
    }
}


using System.Collections.Generic;
using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEngine;

namespace DawnGuard.BlackwoodV2
{
    public sealed class BlackwoodWorld : MonoBehaviour
    {
        private BlackwoodRoot root;
        private GameCatalog catalog;
        private ModelFactory factory;
        private readonly Dictionary<int,GameObject> buildings=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,GameObject> enemies=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,string> enemyIds=new Dictionary<int,string>();
        private readonly Dictionary<string,Stack<GameObject>> pool=new Dictionary<string,Stack<GameObject>>();
        private sealed class Dying { public GameObject go; public string kind; public float time; public BlackwoodEnemyView view; }
        private readonly List<Dying> dying=new List<Dying>();
        private readonly Dictionary<int,EnemyState> enemyStates=new Dictionary<int,EnemyState>();
        private readonly List<int> remove=new List<int>();
        private readonly HashSet<int> alive=new HashSet<int>();
        private Transform world;
        private Camera cameraView;
        private Light sun;
        private GameObject drone,grid,preview,selection;
        private readonly LineRenderer[] shots=new LineRenderer[48];
        private readonly float[] shotLife=new float[48];
        private int nextShot;
        private Material lineMaterial;
        private Cell previewCell;
        private bool hasPreview;

        public void Initialize(BlackwoodRoot root,GameCatalog catalog)
        {
            this.root=root; this.catalog=catalog;
            factory=new ModelFactory(catalog.surfaceTemplate);
            world=new GameObject("Generated world").transform; world.SetParent(transform,false);
            var cameraObject=new GameObject("Game Camera"); cameraObject.transform.SetParent(world);
            cameraView=cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            cameraView.orthographic=true;
            float elevation=root.cameraElevation*Mathf.Deg2Rad;
            cameraView.transform.position=root.cameraFocus+new Vector3(0,Mathf.Sin(elevation),-Mathf.Cos(elevation))*26;
            cameraView.transform.LookAt(root.cameraFocus);
            cameraView.nearClipPlane=.1f; cameraView.farClipPlane=100;
            cameraView.backgroundColor=new Color(.07f,.1f,.15f);
            sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world);
            sun.type=LightType.Directional; sun.transform.rotation=Quaternion.Euler(50,-25,0);
            sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.5f,.55f,.65f);
            if(root.sceneEnvironment==null)
            {
            factory.Part(world,"Forest floor",new Vector3(8,-.48f,8),new Vector3(42,.2f,42),new Color(.18f,.25f,.23f));
            factory.Part(world,"Ground",new Vector3(8,-.2f,8),new Vector3(17,.4f,17),new Color(.31f,.37f,.32f));
            foreach(var c in root.Game.Rules.blockedCells)
            {
                if(root.rubblePrefab!=null) { var rock=Instantiate(root.rubblePrefab,world); rock.transform.position=new Vector3(c.x+.5f,0,c.z+.5f); }
                else factory.Part(world,"Rubble",new Vector3(c.x+.5f,.4f,c.z+.5f),new Vector3(.96f,.8f,.96f),new Color(.4f,.4f,.38f));
            }
            Decorate();
            }
            foreach(var c in root.Game.Rules.spawnCells)
                factory.Part(world,"Spawn warning",new Vector3(c.x+.5f,.01f,c.z+.5f),new Vector3(.8f,.02f,.8f),new Color(.65f,.18f,.1f));
            grid=new GameObject("Build grid"); grid.transform.SetParent(world,false);
            for(int i=0;i<=16;i++)
            {
                factory.Part(grid.transform,"Grid X",new Vector3(i,.012f,8),new Vector3(.018f,.015f,16),new Color(.39f,.45f,.38f));
                factory.Part(grid.transform,"Grid Z",new Vector3(8,.012f,i),new Vector3(16,.015f,.018f),new Color(.39f,.45f,.38f));
            }
            drone=catalog.dronePrefab!=null ? Instantiate(catalog.dronePrefab,world) : factory.Drone(world);
            drone.transform.position=new Vector3(root.Game.DroneX,2.2f,root.Game.DroneZ);
            preview=factory.Part(world,"Placement preview",Vector3.zero,new Vector3(.94f,.04f,.94f),Color.green);
            selection=factory.Part(world,"Selection",Vector3.zero,new Vector3(1,.025f,1),new Color(1,.65f,.05f));
            preview.SetActive(false); selection.SetActive(false);
            var shader=Shader.Find("Universal Render Pipeline/Unlit"); if(shader==null) shader=Shader.Find("Sprites/Default");
            lineMaterial=catalog.tracerMaterial!=null ? new Material(catalog.tracerMaterial) : new Material(shader);
            for(int i=0;i<shots.Length;i++)
            {
                var go=new GameObject("Pooled tracer"); go.transform.SetParent(world,false);
                shots[i]=go.AddComponent<LineRenderer>(); shots[i].sharedMaterial=lineMaterial;
                shots[i].positionCount=2; shots[i].startWidth=.04f; shots[i].endWidth=.02f;
                shots[i].enabled=false;
            }
        }
        private GameObject BoundPrefab(VisualBinding[] bindings,string id)
        { if(bindings!=null) foreach(var b in bindings) if(b!=null && b.definitionId==id) return b.prefab; return null; }

        public void Refresh()
        {
            var game=root.Game;
            for(int i=dying.Count-1;i>=0;i--)
            {
                var d=dying[i]; d.view.SetPaused(root.Paused);
                if(!root.Paused) d.time-=Time.unscaledDeltaTime;
                if(d.time<=0) { ReturnToPool(d.go,d.kind); dying.RemoveAt(i); }
            }
            float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
            cameraView.orthographicSize=Mathf.Max(root.cameraSize,10f/Mathf.Max(.4f,cameraView.aspect));
            cameraView.clearFlags=CameraClearFlags.SolidColor;
            bool night=game.Phase==GamePhase.Night || game.Phase==GamePhase.Defeat;
            sun.intensity=Mathf.Lerp(sun.intensity,night ? .62f : 1.1f,blend*.25f);
            sun.color=night ? new Color(.49f,.68f,1) : new Color(1,.88f,.7f);
            RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,night ? new Color(.27f,.35f,.48f) : new Color(.56f,.59f,.53f),blend*.3f);
            grid.SetActive(game.Phase==GamePhase.Day && !root.Completed && !root.Paused);
            foreach(var b in game.Buildings)
            {
                GameObject go;
                var def=game.Rules.Building(b.definitionId);
                if(!buildings.TryGetValue(b.instanceId,out go))
                {
                    var prefab=BoundPrefab(catalog.buildings,def.id);
                    go=prefab!=null ? Instantiate(prefab,world) : factory.Building(def,world);
                    buildings.Add(b.instanceId,go);
                }
                go.transform.position=new Vector3(b.cell.x+def.width*.5f,0,b.cell.z+def.depth*.5f);
                var visual=go.GetComponent<BlackwoodModel>(); if(visual!=null) visual.RefreshBuilding(b,game,root.Paused);
            }
            remove.Clear(); foreach(var item in buildings) if(game.FindBuilding(item.Key)==null) remove.Add(item.Key);
            foreach(int id in remove) { Destroy(buildings[id]); buildings.Remove(id); }
            alive.Clear();
            foreach(var e in game.Enemies)
            {
                alive.Add(e.instanceId);
                GameObject go;
                if(!enemies.TryGetValue(e.instanceId,out go))
                {
                    Stack<GameObject> stack;
                    if(pool.TryGetValue(e.definitionId,out stack) && stack.Count>0) go=stack.Pop();
                    else
                    {
                        var prefab=BoundPrefab(catalog.enemies,e.definitionId);
                        go=prefab!=null ? Instantiate(prefab,world) : factory.Enemy(game.Rules.Enemy(e.definitionId),world);
                    }
                    go.SetActive(true); go.transform.position=new Vector3(e.x,0,e.z);
                    enemies.Add(e.instanceId,go); enemyIds.Add(e.instanceId,e.definitionId); enemyStates.Add(e.instanceId,e);
                    var animation=go.GetComponent<BlackwoodEnemyView>(); if(animation!=null) animation.ResetView();
                }
                Vector3 goal=new Vector3(e.x,0,e.z);
                Vector3 delta=goal-go.transform.position;
                if(delta.sqrMagnitude>.0001f) go.transform.rotation=Quaternion.Slerp(go.transform.rotation,Quaternion.LookRotation(delta),blend);
                go.transform.position=Vector3.Lerp(go.transform.position,goal,blend);
                var animationView=go.GetComponent<BlackwoodEnemyView>(); if(animationView!=null) animationView.Refresh(e,game,root.Paused);
            }
            remove.Clear(); foreach(var item in enemies) if(!alive.Contains(item.Key)) remove.Add(item.Key);
            foreach(int id in remove) Recycle(id,enemyStates[id].health<=0);
            Vector3 droneGoal=new Vector3(game.DroneX,2.2f,game.DroneZ);
            Vector3 heading=droneGoal-drone.transform.position; heading.y=0;
            if(heading.sqrMagnitude>.01f && !root.Paused) drone.transform.rotation=Quaternion.Slerp(drone.transform.rotation,Quaternion.LookRotation(heading),blend);
            drone.transform.position=Vector3.Lerp(drone.transform.position,droneGoal,blend);
            var selected=game.FindBuilding(root.SelectedBuilding);
            selection.SetActive(selected!=null && game.Phase==GamePhase.Day);
            if(selected!=null)
            {
                var def=game.Rules.Building(selected.definitionId);
                selection.transform.position=new Vector3(selected.cell.x+def.width*.5f,.028f,selected.cell.z+def.depth*.5f);
                selection.transform.localScale=new Vector3(def.width+.08f,.025f,def.depth+.08f);
            }
            preview.SetActive(hasPreview && game.Phase==GamePhase.Day && (root.SelectedBuild!=null || root.MovingBuilding));
            if(!root.Paused) for(int i=0;i<shots.Length;i++) if(shotLife[i]>0)
            { shotLife[i]-=Time.unscaledDeltaTime; if(shotLife[i]<=0) shots[i].enabled=false; }
        }
        private void ReturnToPool(GameObject go,string kind)
        {
            Stack<GameObject> stack;
            if(!pool.TryGetValue(kind,out stack)) { stack=new Stack<GameObject>(); pool.Add(kind,stack); }
            go.SetActive(false); stack.Push(go);
        }
        private void Recycle(int id,bool animate=false)
        {
            string kind=enemyIds[id]; var go=enemies[id]; var view=go.GetComponent<BlackwoodEnemyView>();
            float duration=animate && view!=null ? view.BeginDeath() : 0;
            if(duration>0) dying.Add(new Dying {go=go,kind=kind,time=duration,view=view});
            else ReturnToPool(go,kind);
            enemies.Remove(id); enemyIds.Remove(id); enemyStates.Remove(id);
        }
        public void RebuildUnits()
        {
            foreach(var d in dying) ReturnToPool(d.go,d.kind); dying.Clear();
            foreach(var item in buildings) Destroy(item.Value); buildings.Clear();
            remove.Clear(); foreach(var item in enemies) remove.Add(item.Key);
            foreach(int id in remove) Recycle(id);
            for(int i=0;i<shots.Length;i++) { shots[i].enabled=false; shotLife[i]=0; }
            hasPreview=false;
        }
        public bool TryGround(Vector2 screen,out Vector3 point)
        {
            var ray=cameraView.ScreenPointToRay(screen); float enter;
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out enter)) { point=ray.GetPoint(enter); return true; }
            point=Vector3.zero; return false;
        }
        public void Preview(Vector2 screen,string build,int moving)
        {
            Vector3 point;
            var b=root.Game.FindBuilding(moving);
            string id=b!=null ? b.definitionId : build;
            hasPreview=id!=null && TryGround(screen,out point);
            if(!hasPreview) return;
            TryGround(screen,out point);
            previewCell=new Cell(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.z));
            var def=root.Game.Rules.Building(id);
            bool valid=root.Game.Board.CanPlace(previewCell,def.width,def.depth,moving);
            foreach(var spawn in root.Game.Rules.spawnCells)
                if(spawn.x>=previewCell.x && spawn.x<previewCell.x+def.width && spawn.z>=previewCell.z && spawn.z<previewCell.z+def.depth) valid=false;
            if(moving==0) valid=valid && root.Game.Wallet.Credits>=def.cost && root.Game.PowerAvailable>=def.powerUse && root.Game.Day>=def.requiredDay && root.Game.Tech>=def.requiredTech;
            preview.transform.position=new Vector3(previewCell.x+def.width*.5f,.045f,previewCell.z+def.depth*.5f);
            preview.transform.localScale=new Vector3(def.width*.94f,.04f,def.depth*.94f);
            preview.GetComponent<Renderer>().sharedMaterial=factory.Material(valid ? Color.green : Color.red);
        }
        public void ShowShot(ShotEvent shot)
        {
            int i=nextShot++%shots.Length;
            shots[i].SetPosition(0,new Vector3(shot.fromX,shot.drone ? 2.2f : 1,shot.fromZ));
            shots[i].SetPosition(1,new Vector3(shot.toX,.6f,shot.toZ));
            shots[i].startColor=shots[i].endColor=shot.electric ? Color.cyan : new Color(1,.7f,.1f);
            shots[i].enabled=true; shotLife[i]=.09f;
        }
        private void Decorate()
        {
            if(root.pinePrefab!=null)
            {
                for(int i=0;i<12;i++)
                {
                    float x=-2+(i%6)*4; float z=i<6 ? 19 : -4;
                    var tree=Instantiate(root.pinePrefab,world); tree.transform.position=new Vector3(x,0,z);
                    tree.transform.localScale=Vector3.one*(.85f+(i%3)*.14f);
                }
            }
            // Decorative landing pad is outside the playable board: it cannot hide a legal tile.
            if(root.padPrefab!=null) { var pad=Instantiate(root.padPrefab,world); pad.transform.position=new Vector3(14,0,-2); }
        }
        private void OnDestroy() { if(factory!=null) factory.Dispose(); if(lineMaterial!=null) Destroy(lineMaterial); }
    }
}

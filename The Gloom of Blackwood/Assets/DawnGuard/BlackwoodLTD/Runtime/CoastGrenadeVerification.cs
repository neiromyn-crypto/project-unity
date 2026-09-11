#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastGrenadeVerification : MonoBehaviour
    {
        const string Folder="IntegrationEvidence/Step6";CoastRoot root;int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){if(!SessionState.GetBool("Coast.GrenadeTest",false))return;SessionState.SetBool("Coast.GrenadeTest",false);Directory.CreateDirectory(Folder);CoastRoot.TestSavePath=Path.GetFullPath("Temp/asset-test-"+Guid.NewGuid().ToString("N")+".json");var go=new GameObject("Operator verification");DontDestroyOnLoad(go);go.AddComponent<CoastGrenadeVerification>();}
        IEnumerator Start()
        {
            File.WriteAllText(Folder+"/play-result.txt","RUNNING");File.WriteAllText(Folder+"/play-checks.txt","");File.WriteAllText(Folder+"/errors.txt","");Application.logMessageReceived+=Log;
            var routine=Run();while(true){object step;try{if(!routine.MoveNext())break;step=routine.Current;}catch(Exception ex){File.WriteAllText(Folder+"/play-result.txt","FAIL\n"+ex);EditorApplication.isPlaying=false;yield break;}yield return step;}
            File.WriteAllText(Folder+"/play-result.txt","PASS "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=false;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Folder+"/errors.txt",message+"\n"+stack+"\n");}}
        void Check(bool ok,string name){File.AppendAllText(Folder+"/play-checks.txt",(ok?"PASS ":"FAIL ")+name+"\n");if(!ok)throw new Exception(name);}
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);root=FindAnyObjectByType<CoastRoot>();root.enabled=false;root.NewGame();var g=root.Game;var w=root.World;
            Check(w.Grenade!=null,"User grenade wrapper bound");
            var mesh=w.Grenade.Visual.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            Check(AssetDatabase.GetAssetPath(mesh)=="Assets/GameArt/Granate/Granate.fbx","Original user grenade mesh");
            Check(w.Grenade.Visual.GetComponentInChildren<Renderer>(true).sharedMaterial.GetTexture("_BaseMap")!=null,"Original grenade albedo bound");
            Check(!g.LoadGrenade(),"Day loading rejected");g.StartNight();Check(g.S.grenadeCharges==2,"Night starts with two charges");
            root.GrenadeAction();Check(g.S.grenadeState==GrenadeState.LOADING,"HUD starts physical loading");Check(!g.CommandDrone(20,20),"Loading mission cannot be interrupted by move");
            for(int i=0;i<80;i++){g.Tick(.05f);w.Refresh(.05f);root.Hud.Refresh();yield return null;if(g.S.grenadeState==GrenadeState.LOADED&&i>35)break;}
            Check(g.S.grenadeState==GrenadeState.LOADED&&g.S.grenadeCharges==2,"Loading completes at Pad, charge retained until drop");
            Check(w.Grenade.Visual.gameObject.activeInHierarchy&&w.Grenade.Visual.parent==w.Drone.transform,"Real grenade attached under drone");
            Check(w.Grenade.Visual.position.y<w.Drone.transform.position.y,"Grenade below drone");Capture("loaded");
            root.GrenadeAction();Check(root.GrenadeTargeting,"Loaded HUD enables ground targeting");
            var target=new Vector3(30,0,8);w.CameraRig.SetFocus(target);for(int i=0;i<45;i++){w.Refresh(.05f);yield return null;}
            Vector2 screen=w.Camera.WorldToScreenPoint(target);w.Preview(screen);Check(w.Grenade.PreviewVisible,"Reusable radius preview shown");Capture("target");
            float oldX=g.S.droneX,oldZ=g.S.droneZ;root.ClickBoard(screen);Check(g.S.grenadeMission&&!root.GrenadeTargeting,"Ground click confirms flight");Check(g.S.droneX==oldX&&g.S.droneZ==oldZ,"No teleport on targeting");
            int explosionEvents=0;g.Event+=e=>{if(e.kind=="grenade_explosion")explosionEvents++;};
            var freeFocus=new Vector3(20,0,15);w.CameraRig.SetFocus(freeFocus);
            for(int i=0;i<200&&g.S.grenadeState!=GrenadeState.DROPPED;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(g.S.grenadeState==GrenadeState.DROPPED&&g.S.grenadeCharges==1,"Arrival releases one charge");Check(Vector2.Distance(new Vector2(g.S.droneX,g.S.droneZ),new Vector2(30,8))<.05f,"Real flight reached target");
            Check(w.CameraRig.Focus==freeFocus,"Mission does not lock camera");Check(explosionEvents==0,"No instant click explosion");
            Check(w.Grenade.Visual.parent!=w.Drone.transform,"Grenade detached during drop");float height=w.Grenade.Visual.position.y;
            for(int i=0;i<6;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}Check(w.Grenade.Visual.position.y<height&&w.Grenade.Visual.position.y>.05f,"Visible ballistic fall");w.CameraRig.SetFocus(target);Capture("fall");
            while(g.S.grenadeTimer>.051f){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            var victim=Enemy(g,"runner",30,8);var survivor=Enemy(g,"walker",26.65f,8);var heavy=Enemy(g,"brute",30,7);
            float survivorHP=survivor.hp;while(g.S.grenadeState==GrenadeState.DROPPED){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(explosionEvents==1,"Exactly one AoE event");Check(!g.S.enemies.Contains(victim),"Killed enemy enters existing cleanup");Check(survivor.hp>0&&survivor.hp<survivorHP,"Edge survivor takes falloff damage");Check(heavy.hp>0&&heavy.hp<200,"Heavy damaged");
            Check(survivor.knockRemaining>0&&Mathf.Abs(survivor.knockX)>.1f,"Survivor knocked back");
            for(int i=0;i<5;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}Check(Mathf.Abs(g.EnemyX(survivor)-survivor.x-survivor.laneX)>.1f,"Knockback visibly displaces actor");Capture("blast");
            for(int i=0;i<40;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}Check(survivor.knockRemaining==0,"Survivor returns to path");Check(explosionEvents==1,"Explosion never repeats");
            Check(g.LoadGrenade()&&g.S.grenadeState==GrenadeState.RELOADING,"Second charge requires physical reload");
            for(int i=0;i<180&&g.S.grenadeState!=GrenadeState.LOADED;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(g.S.grenadeState==GrenadeState.LOADED,"Second physical pickup completes");Check(g.TargetGrenade(29,8),"Second mission accepted");
            for(int i=0;i<180&&g.GrenadeBusy;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(g.S.grenadeCharges==0&&!g.LoadGrenade()&&explosionEvents==2,"Two explosions maximum per night");
            Logic();
            root.NewGame();g=root.Game;g.StartNight();for(int i=0;i<600;i++)g.Tick(.05f);w.CameraRig.SetFocus(new Vector3(g.S.enemies[2].x,0,g.S.enemies[2].z));for(int i=0;i<45;i++){w.Refresh(.05f);yield return null;}Capture("crowd");
            File.WriteAllText(Folder+"/grenade-mesh.txt","triangles="+Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i))/3);
            root.NewGame();g=root.Game;g.StartNight();int roleIndex=0;foreach(var spec in g.Rules.enemies){Enemy(g,spec.id,17+3*roleIndex++,9).moving=true;}w.Refresh(.05f);yield return null;
            foreach(var actor in w.GetComponentsInChildren<CoastActorGrounding>().Where(a=>a.transform.parent.name!="OperatorAnchor"))
            {
                var a=actor.GetComponentInChildren<Animator>();var bones=a.GetComponentsInChildren<Transform>();
                foreach(var state in new[]{"Move","Attack","Death"}){a.Play(state,0,0);a.Update(.1f);var old=bones.Select(t=>t.localRotation).ToArray();a.Update(.23f);float delta=0;for(int i=0;i<bones.Length;i++)delta=Mathf.Max(delta,Quaternion.Angle(old[i],bones[i].localRotation));Check(delta>1,"Animated "+actor.name+" / "+state);}
                a.SetBool("IsMoving",true);a.Update(.3f);Check(a.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Death terminal: "+actor.name);
            }
            File.WriteAllText(Folder+"/core-regression.txt",CoastOperatorChecks.Run());Check(errors==0,"Zero runtime errors/exceptions");
        }
        static CoastEnemy Enemy(CoastSession g,string role,float x,float z)
        {var e=new CoastEnemy{id=g.S.nextId++,kind=role,x=x,z=z,nx=Mathf.Clamp(Mathf.FloorToInt(x-1),0,g.Map.Width-2),nz=Mathf.Clamp(Mathf.FloorToInt(z-1),7,g.Map.Depth-2),hp=g.Rules.Enemy(role).hp};g.S.enemies.Add(e);return e;}
        void Logic()
        {
            var rules=CoastRules.Create();rules.Enemy("walker").hp=200;var g=new CoastSession(rules);g.StartNight();g.S.spawnCursor=8;
            var center=Enemy(g,"walker",30,8);var edge=Enemy(g,"walker",33.5f,8);var heavy=Enemy(g,"brute",30,9);var normal=Enemy(g,"walker",29,8);var outside=Enemy(g,"walker",26,8);
            g.S.grenadeState=GrenadeState.DROPPED;g.S.grenadeX=30;g.S.grenadeZ=8;g.S.grenadeTimer=.05f;g.Tick(.05f);
            Check(Mathf.Abs(center.hp-100)<.01f&&Mathf.Abs(edge.hp-165)<.01f&&outside.hp==200,"Configured max/min/radius damage");
            float hn=new Vector2(heavy.knockX,heavy.knockZ).magnitude,nn=new Vector2(normal.knockX,normal.knockZ).magnitude;Check(Mathf.Abs(hn/nn-.35f)<.02f,"Heavy knockback 35 percent at equal distance");
            float before=normal.z;for(int i=0;i<50;i++)g.Tick(.05f);Check(normal.knockRemaining==0&&(normal.z!=before||normal.moving||normal.coreApproach),"Path resumes after displacement");
            g.S.grenadeState=GrenadeState.DROPPED;g.S.grenadeMission=true;g.S.grenadeTimer=.4f;g.S.grenadeCharges=1;
            var restored=new CoastSession(rules,JsonUtility.FromJson<CoastState>(JsonUtility.ToJson(g.S)));int restoredEvents=0;restored.Event+=e=>{if(e.kind=="grenade_explosion")restoredEvents++;};for(int i=0;i<20;i++)restored.Tick(.05f);Check(restoredEvents==1&&!restored.GrenadeBusy,"Mid-fall save resumes exactly once");Check(restored.S.grenadeCharges==g.S.grenadeCharges,"Grenade save roundtrip");
            var transition=new CoastSession(CoastRules.Create());transition.StartNight();transition.S.spawnCursor=8;transition.LoadGrenade();transition.Tick(.05f);transition.ContinueDay();Check(!transition.GrenadeBusy&&transition.CommandDrone(20,10),"Dawn cancels unfinished mission without locking drone");
            rules=CoastRules.Create();rules.campHP=100000;g=new CoastSession(rules);g.S.day=3;g.StartNight();float minimum=100,maxLane=0;int frames=0;
            while(frames++<5000&&g.S.phase==CoastPhase.Night)
            {
                g.Tick(.05f);foreach(var e in g.S.enemies)if(!e.coreApproach){maxLane=Mathf.Max(maxLane,new Vector2(e.laneX,e.laneZ).magnitude);CheckBounds(g,e);foreach(var other in g.S.enemies)if(other.id>e.id&&!other.coreApproach)minimum=Mathf.Min(minimum,Vector2.Distance(new Vector2(g.EnemyX(e),g.EnemyZ(e)),new Vector2(g.EnemyX(other),g.EnemyZ(other))));}
            }
            Check(minimum>.75f,"Crowd avoids complete overlap; min="+minimum);Check(maxLane>.3f,"Deterministic lanes used");Check(minimum>1.05f,"Moving bodies maintain readable spacing");Check(g.S.spawnCursor==13&&g.S.enemies.All(e=>e.coreApproach),"Three fronts reach Core");Check(g.S.enemies.Count(e=>e.attacking)==10,"Ten attack points preserved");
            File.WriteAllText(Folder+"/crowd.txt","minimumMovingSpacing="+minimum+" lane="+maxLane+" frames="+frames);
        }
        static void CheckBounds(CoastSession g,CoastEnemy e){float x=g.EnemyX(e),z=g.EnemyZ(e);if(x<0||x>=g.Map.Width||z<0||z>=g.Map.Depth||g.Map.Rocks[(int)x,(int)z])throw new Exception("Crowd out of bounds / inside rock");}
        void Capture(string name)
        {
            root.Hud.RefreshNow();const int width=1600,height=900;
            var camera=root.World.Camera;var canvas=root.Hud.Canvas;var scaler=canvas.GetComponent<CanvasScaler>();var rt=RenderTexture.GetTemporary(width,height,24);var oldActive=RenderTexture.active;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor;bool oldScaler=scaler.enabled;
            try{camera.targetTexture=rt;camera.aspect=width/(float)height;scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();root.Hud.RefreshNow();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());Destroy(tex);}
            finally{RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.aspect=oldAspect;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=oldScale;scaler.enabled=oldScaler;RenderTexture.ReleaseTemporary(rt);Canvas.ForceUpdateCanvases();}
        }
        void OnDestroy(){Application.logMessageReceived-=Log;CoastRoot.TestSavePath=null;}
    }
}
#endif

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
    public sealed class CoastAssetVerification : MonoBehaviour
    {
        const string Folder="IntegrationEvidence/AssetIntegration";CoastRoot root;int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){if(!SessionState.GetBool("Coast.AssetTest",false))return;SessionState.SetBool("Coast.AssetTest",false);Directory.CreateDirectory(Folder);CoastRoot.TestSavePath=Path.GetFullPath("Temp/asset-test-"+Guid.NewGuid().ToString("N")+".json");var go=new GameObject("Operator verification");DontDestroyOnLoad(go);go.AddComponent<CoastAssetVerification>();}
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
            yield return new WaitForSecondsRealtime(1);root=FindAnyObjectByType<CoastRoot>();root.enabled=false;var w=root.World;
            for(int i=0;i<80;i++){w.Refresh(.03f);yield return null;}
            var menu=w.transform.Find("Living menu");Check(menu.Find("Menu Operator")!=null&&menu.Find("Menu Command Core")!=null,"Menu contains real operator and core");
            Check(menu.GetComponentsInChildren<CoastActorGrounding>().Count(a=>a.transform.parent.name.StartsWith("MenuEnemyAnchor"))==3,"Only three menu enemies");
            var brute=menu.Find("MenuEnemyAnchor_brute").GetComponentInChildren<Animator>();Check(brute.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Menu Brute uses Idle_9");
            float feet=Mathf.Min(brute.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,brute.GetBoneTransform(HumanBodyBones.RightFoot).position.y);Check(feet>=0&&feet<.25f,"Brute feet sit on ground");Capture("menu");
            root.NewGame();var core=w.OperatorCore;var visual=core.transform.Find("Visual");var mesh=visual.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(AssetDatabase.GetAssetPath(mesh)=="Assets/GameArt/comandpunktforoperator/comandpunktforoperator.fbx"&&Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i))/3==1968376,"Core uses original full geometry");
            var renderers=visual.GetComponentsInChildren<Renderer>();Check(renderers.All(r=>r.sharedMaterials.All(m=>m.GetFloat("_Surface")==0&&m.GetColor("_BaseColor").a==1&&m.GetTexture("_BaseMap")!=null)),"Core opaque with real texture");
            var material=renderers[0].sharedMaterial;string matBefore=EditorJsonUtility.ToJson(material);
            var collider=core.transform.Find("DamageCollider").GetComponent<BoxCollider>();Check(collider!=null&&collider.isTrigger&&collider.GetComponentsInChildren<Renderer>().Length==0,"Separate invisible DamageCollider");
            Check(core.transform.Find("AttackPoints").childCount==10&&core.transform.Find("AttackPoints").GetComponentsInChildren<Renderer>().Length==0,"Ten invisible attack anchors created once");
            w.CameraRig.SetFocus(new Vector3(22,0,6));w.CameraRig.Zoom(8-w.CameraRig.Size);
            Check(w.Drone.Visual.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),"Drone has an enabled real mesh visual");
            for(int i=0;i<50;i++){w.Refresh(.04f);root.Hud.Refresh();yield return null;}
            Check(w.Drone.transform.position.y>2.5f,"Real drone takes off");Capture("core");
            var bounds=renderers[0].bounds;Check(Mathf.Abs(bounds.size.x-collider.size.x)<.02f&&Mathf.Abs(bounds.center.y-collider.bounds.center.y)<.02f,"Collider matches visual bounds");
            var op=core.OperatorAnimator;Check(op.runtimeAnimatorController.animationClips.All(c=>c.length>0&&c.frameRate>0),"Operator has no zero-frame gameplay clips");var opFeet=op.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();Check(opFeet.Length==2&&Mathf.Abs(opFeet.Min(t=>t.position.y)-root.catalog.operatorStandingHeight)<.25f,"Operator feet on measured platform surface");
            File.WriteAllText(Folder+"/geometry.txt","bounds="+bounds+"\ncollider="+collider.size+" center="+collider.center+"\nanchor="+core.transform.Find("OperatorAnchor").localPosition+"\nsourceScale="+visual.GetChild(0).localScale);
            var g=root.Game;var pointer=FindAnyObjectByType<CoastPointer>();var screen=w.Camera.WorldToScreenPoint(new Vector3(28,0,13));pointer.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=screen,button=UnityEngine.EventSystems.PointerEventData.InputButton.Right});Check(g.S.droneMoving,"RMB commands real drone");
            for(int i=0;i<70;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(Vector3.Distance(w.Drone.transform.position,new Vector3(28,2.6f,13))<.2f,"Drone arrives at target");root.FocusDrone();for(int i=0;i<45;i++){w.Refresh(.05f);yield return null;}var vp=w.Camera.WorldToViewportPoint(w.Drone.Position);Check(vp.x>.2f&&vp.x<.8f&&vp.y>.2f&&vp.y<.8f,"Camera focuses real visual");Capture("drone");
            g.CommandDrone(g.Map.DronePadX,g.Map.DronePadZ);for(int i=0;i<80;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}Check(!g.S.droneMoving&&Mathf.Abs(g.S.droneZ-g.Map.DronePadZ)<.01f,"Drone returns to pad and idles airborne");
            w.CameraRig.SetFocus(new Vector3(22,0,6));g.StartNight();g.S.spawnCursor=8;
            float normalHP=g.Rules.campHP;g.Rules.campHP=100000;g.S.campHP=100000;
            for(int i=0;i<12;i++)g.S.enemies.Add(new CoastEnemy{id=g.S.nextId++,kind=i==10?"brute":i==11?"runner":"walker",x=22,z=8,nx=g.Map.Goal.x,nz=g.Map.Goal.z,hp=i==10?200:i==11?32:48});
            for(int i=0;i<140;i++){for(int j=0;j<5;j++)g.Tick(.05f);w.Refresh(.05f);yield return null;}
            var attackers=g.S.enemies.Where(e=>e.attacking).ToArray();Check(attackers.Length==10,"Ten enemies occupy the perimeter; overflow waits");Check(attackers.Select(e=>e.attackSlot).Distinct().Count()==10,"Attack slots exclusively reserved");
            float min=float.MaxValue;for(int i=0;i<attackers.Length;i++)for(int j=i+1;j<attackers.Length;j++)min=Mathf.Min(min,Vector2.Distance(new Vector2(attackers[i].x,attackers[i].z),new Vector2(attackers[j].x,attackers[j].z)));Check(min>1.7f,"No stacked final attackers");
            Check(attackers.Any(e=>e.z<CoastMap.CoreZ)&&attackers.Any(e=>e.x<20)&&attackers.Any(e=>e.x>24),"Attackers surround multiple sides");
            Check(g.S.campHP<100000&&g.S.operatorHP==100,"Attack intervals damage core only");Capture("attack-ring");
            File.WriteAllText(Folder+"/attack-ring.txt",string.Join("\n",g.S.enemies.Select(e=>e.id+" slot="+e.attackSlot+" attacking="+e.attacking+" x="+e.x+" z="+e.z))+"\nminimumSpacing="+min);
            var victim=attackers[0];int freed=victim.attackSlot;g.S.enemies.Remove(victim);g.Tick(.05f);Check(g.S.enemies.Count(e=>e.attackSlot==freed)==1,"Freed slot claimed by waiting enemy");
            var copy=new CoastSession(g.Rules,JsonUtility.FromJson<CoastState>(JsonUtility.ToJson(g.S)));Check(copy.S.enemies.Count(e=>e.attackSlot>=0)==10,"Ring reservations survive save roundtrip");
            g.Rules.campHP=normalHP;g.S.campHP=normalHP;g.DamageCommandNode(normalHP);w.Refresh(.05f);Check(g.S.operatorHP==100&&g.S.phase==CoastPhase.Night,"Core breaking hit absorbed");g.DamageCommandNode(100);for(int i=0;i<50;i++){w.Refresh(.04f);yield return null;}Check(g.S.phase==CoastPhase.Defeat&&op.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Operator death and defeat");
            Check(EditorJsonUtility.ToJson(material)==matBefore&&renderers.All(r=>!r.HasPropertyBlock()),"Damage never mutates original visual or material");Capture("defeat");
            root.NewGame();g=root.Game;g.StartNight();int roleIndex=0;foreach(var spec in g.Rules.enemies){g.S.enemies.Add(new CoastEnemy{id=g.S.nextId++,kind=spec.id,x=17+3*roleIndex,z=9,nx=16+3*roleIndex,nz=8,hp=spec.hp,moving=true});roleIndex++;}w.Refresh(.05f);yield return null;
            var audit=new System.Text.StringBuilder();foreach(var actor in w.GetComponentsInChildren<CoastActorGrounding>().Where(a=>a.transform.parent.name!="OperatorAnchor"))
            {
                var a=actor.GetComponentInChildren<Animator>();var bones=a.GetComponentsInChildren<Transform>();Check(a.runtimeAnimatorController.animationClips.All(c=>c.length>0&&c.frameRate>0),"No empty gameplay clips: "+actor.name);
                foreach(var state in new[]{"Move","Attack","Death"}){a.Play(state,0,0);a.Update(.1f);var old=bones.Select(t=>t.localRotation).ToArray();a.Update(.23f);float delta=0;for(int i=0;i<bones.Length;i++)delta=Mathf.Max(delta,Quaternion.Angle(old[i],bones[i].localRotation));Check(delta>1,"Actual bones animate "+actor.name+" / "+state);audit.AppendLine(actor.name+" "+state+" boneDelta="+delta);}
                a.SetBool("IsMoving",true);a.Update(.3f);Check(a.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Death never returns to Move: "+actor.name);
            }
            File.WriteAllText(Folder+"/animation-audit.txt",audit.ToString());File.WriteAllText(Folder+"/core-regression.txt",CoastOperatorChecks.Run());
            root.NewGame();Check(root.Game.Build("gun",21,12)&&root.Game.Build("gun",18,18),"Test defense placeable");
            for(int night=1;night<=3;night++){g=root.Game;if(night==2)g.Build("gun",27,10);g.StartNight();int steps=0;while(g.S.phase==CoastPhase.Night&&steps<10000){for(int j=0;j<5&&g.S.phase==CoastPhase.Night;j++){g.Tick(.05f);steps++;}w.Refresh(.05f);yield return null;}Check(g.S.phase==(night==3?CoastPhase.Victory:CoastPhase.Debrief),"Night "+night+" regression");if(night<3)root.NextDay();}
            Check(errors==0,"No runtime errors or repeating exceptions");
        }
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

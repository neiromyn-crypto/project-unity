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
    public sealed class CoastLayoutVerification : MonoBehaviour
    {
        const string Folder="IntegrationEvidence/LayoutPass";CoastRoot root;int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){if(!SessionState.GetBool("Coast.LayoutTest",false))return;SessionState.SetBool("Coast.LayoutTest",false);Directory.CreateDirectory(Folder);CoastRoot.TestSavePath=Path.GetFullPath("Temp/layout-test-"+Guid.NewGuid().ToString("N")+".json");var go=new GameObject("Operator verification");DontDestroyOnLoad(go);go.AddComponent<CoastLayoutVerification>();}
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
            Check(w.transform.Find("Living menu/Menu Operator")!=null,"Menu contains operator on command platform");
            var grounded=w.GetComponentsInChildren<CoastActorGrounding>();Check(grounded.Length==4,"Four menu enemies active");
            foreach(var body in grounded){var a=body.GetComponentInChildren<Animator>();var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);Check(Mathf.Abs(Mathf.Min(foot.position.y,right.position.y)-body.transform.position.y)<.2f,"Menu feet on ground: "+body.name);}
            Capture("menu");root.NewGame();
            Check(Mathf.Abs(w.Drone.transform.position.y-.45f)<.01f,"Drone starts on pad");
            w.CameraRig.SetFocus(new Vector3(22,0,6));w.CameraRig.Zoom(8-w.CameraRig.Size);
            for(int i=0;i<50;i++){w.Refresh(.04f);root.Hud.Refresh();yield return null;}
            Check(w.Drone.transform.position.y>2.5f,"Drone takes off");Capture("base");
            var g=root.Game;int blocked;Check(g.Map.AllOpen(out blocked),"NORTH WEST EAST remain connected");
            Check(g.Map.Distances[17,8]>=0&&g.Map.Distances[25,8]>=0,"Left and right approaches to core remain open");
            File.WriteAllText(Folder+"/routes.txt",string.Join("\n",Enumerable.Range(0,3).Select(f=>CoastSession.FrontName(f)+": "+string.Join(" -> ",g.Map.Route(f).Select(c=>c.x+","+c.z)))));
            Check(w.ServiceAt(new Vector3(11.2f,0,4.7f))=="barracks"&&w.ServiceAt(new Vector3(16,0,2.8f))=="armory"&&w.ServiceAt(new Vector3(30,0,2.8f))=="lab"&&w.ServiceAt(new Vector3(35,0,4.7f))=="power","Service clicks match moved buildings");
            var pointer=root.Hud.GetComponentInChildren<CoastPointer>();if(pointer==null)pointer=FindAnyObjectByType<CoastPointer>();
            Vector3 target=new Vector3(28,0,13);var screen=w.Camera.WorldToScreenPoint(target);
            pointer.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=screen,button=UnityEngine.EventSystems.PointerEventData.InputButton.Right});
            Check(g.S.droneMoving&&Mathf.Abs(g.S.droneTargetX-28)<.1f,"Board right click issues drone flight command");
            for(int i=0;i<50;i++){g.Tick(.05f);w.Refresh(.05f);yield return null;}
            Check(Vector2.Distance(new Vector2(g.S.droneX,g.S.droneZ),new Vector2(28,13))<.1f,"Drone reaches commanded point");
            Check(Vector3.Distance(w.Drone.transform.position,new Vector3(28,2.6f,13))<.15f,"Rendered drone follows simulation");
            root.FocusDrone();for(int i=0;i<45;i++){w.Refresh(.05f);yield return null;}var vp=w.Camera.WorldToViewportPoint(w.Drone.Position);Check(vp.x>.2f&&vp.x<.8f&&vp.y>.2f&&vp.y<.8f,"Focus keeps flying drone visible");Capture("drone-flight");root.TogglePause();var pos=w.Drone.transform.position;w.Refresh(.2f);Check(w.Drone.transform.position==pos,"Drone stays still while paused");root.TogglePause();
            g.CommandDrone(-100,100);Check(g.S.droneTargetX==.8f&&g.S.droneTargetZ<g.Map.Depth,"Flight target clamped to map");
            var restored=new CoastSession(g.Rules,JsonUtility.FromJson<CoastState>(JsonUtility.ToJson(g.S)));Check(restored.S.droneMoving&&restored.S.droneTargetX==g.S.droneTargetX,"Drone command survives save roundtrip");
            w.CameraRig.SetFocus(new Vector3(22,0,6));g.StartNight();int index=0;
            foreach(var spec in g.Rules.enemies){g.S.enemies.Add(new CoastEnemy{id=g.S.nextId++,kind=spec.id,x=17+index*3,z=9,nx=16+index*3,nz=8,hp=spec.hp,moving=true});index++;}
            for(int i=0;i<40;i++){w.Refresh(.05f);yield return null;}
            var report=new System.Text.StringBuilder();
            foreach(var a in w.GetComponentsInChildren<Animator>())
            {
                var bones=a.GetComponentsInChildren<Transform>();var before=bones.Select(t=>t.localRotation).ToArray();
                a.Update(.23f);float max=0;for(int i=0;i<bones.Length;i++)max=Mathf.Max(max,Quaternion.Angle(before[i],bones[i].localRotation));
                report.AppendLine(a.transform.parent.parent.name+" stateMove="+a.GetCurrentAnimatorStateInfo(0).IsName("Move")+" human="+a.isHuman+" maxBoneDelta="+max);
                Check(max>1,"Animated bones actually move: "+a.transform.parent.parent.name);
                foreach(var c in a.runtimeAnimatorController.animationClips)report.AppendLine("  "+c.name+" human="+c.isHumanMotion);
            }
            File.WriteAllText(Folder+"/animation-audit.txt",report.ToString());Capture("actors");
            foreach(var a in w.GetComponentsInChildren<Animator>()){a.SetTrigger("Die");a.Update(0);a.Update(.3f);a.Update(.05f);Check(a.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Death transition: "+a.transform.parent.parent.name);}
            Capture("death");
            root.NewGame();g=root.Game;g.StartNight();g.DamageCommandNode(300);Check(g.S.operatorHP==100&&g.S.phase==CoastPhase.Night,"Core still absorbs breaking hit");g.DamageCommandNode(100);Check(g.S.phase==CoastPhase.Defeat,"Operator death still causes defeat");
            for(int i=0;i<60;i++){w.Refresh(.04f);root.Hud.Refresh();yield return null;}Capture("defeat");
            File.WriteAllText(Folder+"/core-regression.txt",CoastOperatorChecks.Run());
            root.NewGame();Check(root.Game.Build("gun",21,12)&&root.Game.Build("gun",18,18),"Test defense remains placeable");
            for(int night=1;night<=3;night++)
            {
                g=root.Game;if(night==2)Check(g.Build("gun",27,10),"Third test defender remains placeable");g.StartNight();int steps=0;
                while(g.S.phase==CoastPhase.Night&&steps<10000){for(int j=0;j<5&&g.S.phase==CoastPhase.Night;j++){g.Tick(.05f);steps++;}w.Refresh(.05f);yield return null;}
                Check(g.S.phase==(night==3?CoastPhase.Victory:CoastPhase.Debrief),"Night "+night+" completes on adjusted map");if(night<3)root.NextDay();
            }
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

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
    public sealed class CoastOperatorVerification : MonoBehaviour
    {
        const string Folder="IntegrationEvidence/OperatorCore";CoastRoot root;int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){if(!SessionState.GetBool("Coast.OperatorTest",false))return;SessionState.SetBool("Coast.OperatorTest",false);Directory.CreateDirectory(Folder);CoastRoot.TestSavePath=Path.GetFullPath("Temp/operator-test-"+Guid.NewGuid().ToString("N")+".json");var go=new GameObject("Operator verification");DontDestroyOnLoad(go);go.AddComponent<CoastOperatorVerification>();}
        IEnumerator Start()
        {
            File.WriteAllText(Folder+"/play-checks.txt","");File.WriteAllText(Folder+"/errors.txt","");Application.logMessageReceived+=Log;
            var routine=Run();while(true){object step;try{if(!routine.MoveNext())break;step=routine.Current;}catch(Exception ex){File.WriteAllText(Folder+"/play-result.txt","FAIL\n"+ex);EditorApplication.isPlaying=false;yield break;}yield return step;}
            File.WriteAllText(Folder+"/play-result.txt","PASS "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=false;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Folder+"/errors.txt",message+"\n"+stack+"\n");}}
        void Check(bool ok,string name){File.AppendAllText(Folder+"/play-checks.txt",(ok?"PASS ":"FAIL ")+name+"\n");if(!ok)throw new Exception(name);}
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);root=FindAnyObjectByType<CoastRoot>();Check(root!=null&&root.World!=null,"Play starts");root.enabled=false;root.NewGame();
            var w=root.World;w.CameraRig.SetFocus(new Vector3(root.Game.Map.CampX,0,7));w.CameraRig.Zoom(8-w.CameraRig.Size);
            for(int i=0;i<30;i++){w.Refresh(.05f);root.Hud.Refresh();yield return null;}
            Check(w.OperatorCore!=null&&w.OperatorCore.transform.name=="OperatorCoreRoot","Command node exists in running scene");Check(w.OperatorCore.transform.Find("OperatorAnchor")!=null,"Operator anchor exists");
            var animator=w.OperatorCore.OperatorAnimator;Check(animator!=null&&animator.runtimeAnimatorController!=null,"Operator animator connected");Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Operator calm idle, no constant spin");
            Capture("core-intact");
            var g=root.Game;g.StartNight();int iRole=0;
            foreach(var spec in g.Rules.enemies){g.S.enemies.Add(new CoastEnemy{id=g.S.nextId++,kind=spec.id,front=0,x=17+iRole*3,z=10,nx=16+iRole*3,nz=9,hp=spec.hp,moving=true});iRole++;}
            for(int i=0;i<30;i++){w.Refresh(.05f);yield return null;}Capture("enemy-roles");
            var actors=w.GetComponentsInChildren<Animator>();Check(g.Rules.enemies.Length==4&&g.S.enemies.Count==4,"Four enemy roles rendered through catalog bindings");
            File.WriteAllText(Folder+"/actor-bounds.txt",string.Join("\n",actors.Select(a=>a.name+" "+a.transform.rotation+" "+string.Join(";",a.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.bounds.ToString())))));
            Check(actors.All(a=>a.runtimeAnimatorController!=null),"Every actor has controller");
            g.DamageCommandNode(75);Check(g.S.campHP==225&&g.S.operatorHP==100,"Normal hit consumes only core integrity");w.Refresh(.05f);root.Hud.Refresh();Capture("core-hit");
            g.DamageCommandNode(1000);Check(g.S.campHP==0&&g.S.operatorHP==100&&g.S.phase==CoastPhase.Night,"Breaking hit leaves operator alive; core absorbs overflow");
            for(int i=0;i<15;i++){w.Refresh(.05f);root.Hud.Refresh();yield return null;}Capture("operator-vulnerable");
            g.DamageCommandNode(25);Check(g.S.operatorHP==75,"Next hit reaches operator");
            g.DamageCommandNode(100);Check(g.S.phase==CoastPhase.Defeat,"Operator zero HP means defeat");
            for(int i=0;i<18;i++){w.Refresh(.025f);root.Hud.Refresh();yield return null;}Capture("operator-death");Check(w.OperatorCore.DeathStarted&&animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Death animation plays only after defeat");
            for(int i=0;i<90;i++){w.Refresh(.025f);root.Hud.Refresh();yield return null;}Capture("defeat-ui");
            root.NewGame();w.Refresh(.05f);Check(!w.OperatorCore.DeathStarted&&root.Game.S.operatorHP==100,"New game restores live operator and core");
            Check(root.Game.Build("gun",21,12)&&root.Game.Build("gun",18,18),"Affordable initial defense placement");
            for(int night=1;night<=3;night++)
            {
                g=root.Game;if(night==2)Check(g.Build("gun",27,10),"Third defender affordable on second day");g.StartNight();int steps=0;
                while(g.S.phase==CoastPhase.Night&&steps<10000){for(int j=0;j<5&&g.S.phase==CoastPhase.Night;j++){g.Tick(.05f);steps++;}w.Refresh(.05f);root.Hud.Refresh();yield return null;}
                Check(g.S.phase==(night==3?CoastPhase.Victory:CoastPhase.Debrief),"Night "+night+" finishes with test defense; core="+g.S.campHP+", operator="+g.S.operatorHP+", kills="+g.S.killed);Capture("night-"+night+"-complete");if(night<3)root.NextDay();
            }
            Check(errors==0,"Zero runtime errors and repeating exceptions");
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

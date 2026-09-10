#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEditor;

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastVerification : MonoBehaviour
    {
        string folder;CoastRoot root;int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){if(!SessionState.GetBool("Coast.Verify",false))return;SessionState.SetBool("Coast.Verify",false);var go=new GameObject("Coastal Verification");DontDestroyOnLoad(go);var v=go.AddComponent<CoastVerification>();v.folder="IntegrationEvidence/Coast/play-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(v.folder);CoastRoot.TestSavePath=Path.GetFullPath(v.folder+"/test-save.json");}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;var routine=Verify();while(true){object current;try{if(!routine.MoveNext())break;current=routine.Current;}catch(Exception ex){File.WriteAllText("IntegrationEvidence/Coast/play-result.txt","FAILED "+folder+"\n"+ex);Debug.LogException(ex);EditorApplication.isPlaying=false;yield break;}yield return current;}
            File.WriteAllText("IntegrationEvidence/Coast/play-result.txt","PASS "+folder);EditorApplication.isPlaying=false;
        }
        void Log(string c,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(folder+"/errors.txt",c+"\n"+stack+"\n");}}
        void Check(bool value,string message){if(!value)throw new Exception(message);File.AppendAllText(folder+"/checks.txt","PASS "+message+"\n");}
        IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(2);root=FindFirstObjectByType<CoastRoot>();Check(root!=null&&root.Game!=null,"Coastal scene starts");root.enabled=false;
            Check(!ShaderUtil.ShaderHasError(root.catalog.ground.shader)&&!ShaderUtil.ShaderHasError(root.catalog.water.shader),"Coastal shaders compile without errors");
            Check(root.MenuOpen,"Menu pauses campaign");float timer=root.Game.S.remaining;var animators=root.World.GetComponentsInChildren<Animator>().Where(a=>a.isActiveAndEnabled).ToArray();Check(animators.Length>=4,"Four animated zombies in live menu");
            root.World.Refresh(.1f);Capture("menu",1600,900);Capture("menu-wide",1950,900);yield return new WaitForSecondsRealtime(.4f);Check(root.Game.S.remaining==timer,"Menu does not run economy");
            root.NewGame();for(int i=0;i<35;i++){root.World.Refresh(.05f);yield return null;}root.Hud.Refresh();Capture("day-start",1600,900);
            Check(root.Game.Build("gun",14,17),"Gun placement");CoastChecks.NorthernMaze(root.Game);root.World.Refresh(.05f);root.Hud.Refresh();Capture("day-maze",1600,900);Capture("day-wide",1950,900);
            for(int i=0;i<450;i++)root.Game.Tick(.05f);root.World.Refresh(.05f);root.Hud.OpenService("barracks");root.Hud.Refresh();Capture("economy",1600,900);root.Hud.CloseService();
            Check(root.Game.S.delivered>=16,"Worker cargo visibly funds wallet");root.Save();var restored=new CoastSession(root.catalog.rules,JsonUtility.FromJson<CoastState>(File.ReadAllText(CoastRoot.TestSavePath)));Check(restored.S.stone==root.Game.S.stone,"Saved stone restores");
            root.Game.StartNight();for(int i=0;i<420;i++){root.Game.Tick(.05f);root.World.Refresh(.05f);if(i%30==0)yield return null;}
            root.Hud.Refresh();Capture("night",1600,900);Capture("night-wide",1950,900);
            int ticks=0;while(root.Game.S.phase==CoastPhase.Night&&ticks++<5000){root.Game.Tick(.05f);if(ticks%50==0){root.World.Refresh(.05f);yield return null;}}
            Check(root.Game.S.phase==CoastPhase.Debrief,"Maze strategy clears first night");Check(root.Game.S.killed==14,"All first-wave enemies killed");root.World.Refresh(.05f);root.Hud.Refresh();Capture("dawn-report",1600,900);
            root.NextDay();int dayCredits=root.Game.S.credits,dayStone=root.Game.S.stone;Check(root.HasCheckpoint,"Dawn checkpoint exists");
            root.Game.S.credits=0;root.Game.S.stone=0;root.Game.S.campHP=0;root.Game.S.phase=CoastPhase.Defeat;root.RetryDay();
            Check(root.Game.S.phase==CoastPhase.Day&&root.Game.S.day==2&&root.Game.S.credits==dayCredits&&root.Game.S.stone==dayStone&&root.Game.S.campHP==600,"Retry restores day, resources and camp");
            Check(CoastRoot.TestSavePath!=null&&CoastRoot.TestSavePath.Contains(Path.GetFullPath(folder)),"Verification uses isolated save path");
            File.WriteAllText(folder+"/core-checks.txt",CoastChecks.Run());Check(errors==0,"No runtime errors");
        }
        void Capture(string name,int width,int height)
        {
            root.Hud.RefreshNow();
            var camera=root.World.Camera;var canvas=root.Hud.Canvas;var scaler=canvas.GetComponent<CanvasScaler>();var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();var oldActive=RenderTexture.active;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor;bool oldScaler=scaler.enabled;
            try{camera.targetTexture=rt;camera.aspect=width/(float)height;scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=height/900f;Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+name+".png",tex.EncodeToPNG());Destroy(tex);
                int magenta=0;foreach(var pixel in tex.GetPixels32())if(pixel.r>200&&pixel.b>200&&pixel.g<35)magenta++;Check(magenta<width*height/200,"No error-material coverage: "+name);
                foreach(var button in canvas.GetComponentsInChildren<Button>()){var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);Check(corners.All(p=>{var s=camera.WorldToScreenPoint(p);return s.x>=-1&&s.x<=width+1&&s.y>=-1&&s.y<=height+1;}),"Button inside frame: "+button.name+" / "+name);}}
            finally{RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.aspect=oldAspect;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=oldScale;scaler.enabled=oldScaler;rt.Release();Destroy(rt);Canvas.ForceUpdateCanvases();}
        }
        void OnDestroy(){Application.logMessageReceived-=Log;CoastRoot.TestSavePath=null;}
    }
}
#endif

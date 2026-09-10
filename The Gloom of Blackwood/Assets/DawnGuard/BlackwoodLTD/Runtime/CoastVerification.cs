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
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastVerification : MonoBehaviour
    {
        string folder;CoastRoot root;int errors;bool droneOnly;
        string ResultPath => droneOnly?folder+"/result.txt":"IntegrationEvidence/Coast/play-result.txt";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){bool drone=SessionState.GetBool("Coast.VerifyDrone",false);if(!drone&&!SessionState.GetBool("Coast.Verify",false))return;SessionState.SetBool("Coast.Verify",false);SessionState.SetBool("Coast.VerifyDrone",false);var go=new GameObject("Coastal Verification");DontDestroyOnLoad(go);var v=go.AddComponent<CoastVerification>();v.droneOnly=drone;v.folder=drone?"IntegrationEvidence/DroneReadability":"IntegrationEvidence/Coast/play-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(v.folder);if(drone)File.WriteAllText(v.folder+"/checks.txt","");CoastRoot.TestSavePath=Path.GetFullPath(drone?"Temp/coast-drone-"+Guid.NewGuid().ToString("N")+".json":v.folder+"/test-save.json");}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;var routine=droneOnly?VerifyDrone():Verify();while(true){object current;try{if(!routine.MoveNext())break;current=routine.Current;}catch(Exception ex){File.WriteAllText(ResultPath,"FAILED "+folder+"\n"+ex);Debug.LogException(ex);EditorApplication.isPlaying=false;yield break;}yield return current;}
            File.WriteAllText(ResultPath,"PASS "+folder+" "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=false;
        }
        void Log(string c,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(folder+"/errors.txt",c+"\n"+stack+"\n");}}
        void Check(bool value,string message){if(!value)throw new Exception(message);File.AppendAllText(folder+"/checks.txt","PASS "+message+"\n");}
        IEnumerator VerifyDrone()
        {
            yield return new WaitForSecondsRealtime(2);
            root=FindAnyObjectByType<CoastRoot>();Check(root!=null&&root.World!=null&&root.Hud!=null,"BlackwoodCoast Play Mode starts");
            root.enabled=false;root.EnterGame();var world=root.World;var drone=world.Drone;var camera=world.Camera;
            for(int i=0;i<45;i++){world.Refresh(.05f);yield return null;}
            Check(drone.transform.localScale==Vector3.one&&Vector3.Distance(drone.Visual.localScale,Vector3.one*1.3f)<.001f,"Only Visual scales +30%; drone root scale stays 1");
            Check(Mathf.Abs(drone.transform.position.y-2.2f)<.001f&&drone.Visual.localPosition==Vector3.up*.25f,"Logic altitude unchanged; visual offset +0.25");
            var marker=drone.GroundMarker.GetComponent<MeshRenderer>();
            Check(drone.GroundMarker.GetComponents<Collider>().Length==0&&drone.GroundMarker.GetComponents<Rigidbody>().Length==0,"Ground marker has no physics components");
            Check(marker.shadowCastingMode==ShadowCastingMode.Off&&!marker.receiveShadows&&marker.sharedMaterial.GetFloat("_ZWrite")==0,"Transparent unlit ground marker; no shadows or depth writes");
            Check(!ShaderUtil.ShaderHasError(marker.sharedMaterial.shader),"Marker shader compiles");
            Check(drone.GetComponentsInChildren<Light>(true).All(l=>!l.enabled),"No active drone lights");
            var originalGround=drone.GroundMarker;var originalMaterial=marker.sharedMaterial;
            int transforms=root.GetComponentsInChildren<Transform>(true).Length,canvases=root.GetComponentsInChildren<Canvas>(true).Length;
            Check(canvases==1,"Markers reuse the single existing HUD Canvas");
            world.FocusDrone();for(int i=0;i<35;i++)world.Refresh(.05f);root.Hud.RefreshNow();
            Check(!root.Hud.DroneIndicatorVisible,"Onscreen drone hides edge indicator");Capture("day",1600,900);
            // Exercise the whole enlarged map without changing simulation rules or user saves.
            foreach(var p in new[]{new Vector2(6,29),new Vector2(38,30),new Vector2(40,8),new Vector2(5,9)})
            {
                root.Game.S.droneX=p.x;root.Game.S.droneZ=p.y;
                for(int i=0;i<40;i++){world.Refresh(.05f);root.Hud.Refresh();yield return null;}
                var a=drone.transform.position;var b=drone.GroundMarker.position;
                Check(Mathf.Abs(a.x-b.x)<.001f&&Mathf.Abs(a.z-b.z)<.001f&&Mathf.Abs(b.y-.035f)<.001f,"Ground marker follows X/Z at "+p);
                Check(Quaternion.Angle(drone.OverheadMarker.rotation,camera.transform.rotation)<.01f,"Overhead marker faces camera at "+p);
            }
            root.Game.S.droneX=38;root.Game.S.droneZ=30;
            world.CameraRig.SetFocus(new Vector3(6,0,8));for(int i=0;i<45;i++)world.Refresh(.05f);root.Hud.RefreshNow();
            Check(root.Hud.DroneIndicatorVisible,"Offscreen northeast drone shows indicator");Capture("offscreen",1600,900);Capture("offscreen-wide",1950,900);
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
#if ENABLE_INPUT_SYSTEM
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try{InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));InputSystem.Update();root.SendMessage("Update");}
            finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.RemoveDevice(keyboard);}
#else
            Check(false,"Input System must be enabled");
#endif
            Check(world.CameraRig.Focus.x>30&&world.CameraRig.Focus.z>29,"Space through Input System requests Focus Drone");
            Check(Vector3.Distance(oldPosition,camera.transform.position)<15&&Quaternion.Angle(oldRotation,camera.transform.rotation)<.01f,"Focus starts smoothly without a camera teleport or rotation snap");
            for(int i=0;i<50;i++){world.Refresh(.05f);root.Hud.Refresh();yield return null;}
            var viewport=camera.WorldToViewportPoint(drone.Position);
            Check(Mathf.Abs(viewport.x-.5f)<.025f&&Mathf.Abs(viewport.y-.5f)<.025f&&!root.Hud.DroneIndicatorVisible,"Focus settles near center and hides indicator");Capture("focused",1600,900);
            var focused=world.CameraRig.Focus;world.CameraRig.Move(Vector2.left,.3f);world.Pan(new Vector2(40,20));world.Zoom(.6f);
            Check(world.CameraRig.Focus.x<focused.x&&world.CameraRig.Size>10,"Manual move, drag and zoom remain available after focus");
            world.CameraRig.SetFocus(new Vector3(-999,0,999));Check(world.CameraRig.Focus==new Vector3(4,0,33),"Large-map camera bounds retained");
            world.FocusDrone();world.CameraRig.Zoom(13-world.CameraRig.Size);for(int i=0;i<45;i++)world.Refresh(.05f);Capture("day-max-zoom",1600,900);
            root.Game.S.phase=CoastPhase.Night;for(int i=0;i<50;i++)world.Refresh(.05f);Capture("night-max-zoom",1600,900);
            Check(marker.sharedMaterial==originalMaterial&&drone.GroundMarker==originalGround,"Same marker and material reused across movement and day/night");
            Check(root.GetComponentsInChildren<Transform>(true).Length==transforms&&root.GetComponentsInChildren<Canvas>(true).Length==canvases,"No accumulated objects or new Canvases after camera/movement checks");
            root.OpenMenu();root.Hud.RefreshNow();Check(!root.Hud.DroneIndicatorVisible,"Menu hides edge indicator");
            Check(root.Hud.Canvas.GetComponentsInChildren<Graphic>(true).All(g=>g.GetComponent<CanvasRenderer>()!=null),"All HUD graphics have CanvasRenderer");
            Check(errors==0,"Console: zero errors/exceptions throughout drone Play Mode checks");
        }
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
            try{camera.targetTexture=rt;camera.aspect=width/(float)height;scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=height/900f;Canvas.ForceUpdateCanvases();root.Hud.RefreshNow();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+name+".png",tex.EncodeToPNG());Destroy(tex);
                int magenta=0;foreach(var pixel in tex.GetPixels32())if(pixel.r>200&&pixel.b>200&&pixel.g<35)magenta++;Check(magenta<width*height/200,"No error-material coverage: "+name);
                foreach(var button in canvas.GetComponentsInChildren<Button>()){var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);Check(corners.All(p=>{var s=camera.WorldToScreenPoint(p);return s.x>=-1&&s.x<=width+1&&s.y>=-1&&s.y<=height+1;}),"Button inside frame: "+button.name+" / "+name);}}
            finally{RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.aspect=oldAspect;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=oldScale;scaler.enabled=oldScaler;rt.Release();Destroy(rt);Canvas.ForceUpdateCanvases();}
        }
        void OnDestroy(){Application.logMessageReceived-=Log;CoastRoot.TestSavePath=null;}
    }
}
#endif

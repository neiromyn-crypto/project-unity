#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DawnGuard.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DawnGuard.BlackwoodV2
{
    // Opt-in Editor verification. Excluded from player builds; all saves go to a unique test directory.
    public sealed class BlackwoodPlayVerification : MonoBehaviour
    {
        string folder;
        BlackwoodRoot root;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if(!SessionState.GetBool("Blackwood.VerifyPlay",false)) return;
            SessionState.SetBool("Blackwood.VerifyPlay",false);
            string folder="IntegrationEvidence/play-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            BlackwoodRoot.ValidationSavePath=Path.GetFullPath(folder+"/test-profile.json");
            var go=new GameObject("Blackwood automated verification"); DontDestroyOnLoad(go);
            go.AddComponent<BlackwoodPlayVerification>().folder=folder;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;
            var sequence=Verify();
            while(true)
            {
                object current;
                try { if(!sequence.MoveNext()) break; current=sequence.Current; }
                catch(Exception ex)
                {
                    File.AppendAllText(folder+"/checks.txt","FAILED: "+ex+"\n");
                    Debug.LogException(ex); File.WriteAllText("IntegrationEvidence/play-result.txt","FAILED "+folder+"\n"+ex);
                    EditorApplication.isPlaying=false; yield break;
                }
                yield return current;
            }
            File.WriteAllText("IntegrationEvidence/play-result.txt","PASS "+folder);
            EditorApplication.isPlaying=false;
        }
        void Log(string condition,string trace,LogType type)
        {
            if(type==LogType.Exception || type==LogType.Error || type==LogType.Warning)
                File.AppendAllText(folder+"/runtime-log.txt",type+": "+condition+"\n"+trace+"\n");
        }
        void OnDestroy() { Application.logMessageReceived-=Log; }
        void Check(bool value,string message)
        { if(!value) throw new InvalidOperationException(message); File.AppendAllText(folder+"/checks.txt","PASS "+message+"\n"); }
        Button ButtonWith(string label)
        { return root.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<Text>(true).text==label); }
        void Click(string label) { var b=ButtonWith(label); Check(b.interactable,"Button enabled: "+label); b.onClick.Invoke(); }
        void Refresh() { root.World.Refresh(); root.Hud.Refresh(); }
        static EnemyState Spawn(GameSession g,string id,Cell cell)
        {
            typeof(GameSession).GetMethod("SpawnEnemy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{id,cell});
            return g.Enemies[g.Enemies.Count-1];
        }
        GameObject EnemyObject(int id)
        {
            var field=typeof(BlackwoodWorld).GetField("enemies",BindingFlags.Instance|BindingFlags.NonPublic);
            return ((System.Collections.Generic.Dictionary<int,GameObject>)field.GetValue(root.World))[id];
        }
        IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(1);
            root=FindAnyObjectByType<BlackwoodRoot>();
            Check(root!=null && root.Game!=null && root.Hud!=null,"BlackwoodRoot initialized in Play Mode");
            root.enabled=false; // Drive the same fixed-step core deterministically while real Animator/UI keep rendering.
            Check(root.startingPreview==null,"Editor starting preview removed; no duplicate starting buildings");
            Check(FindObjectsByType<DawnGuard.Unity.GameRoot>().Length==0,"No old GameRoot in integration scene");
            Check(root.Game.Buildings.Count==3 && root.Game.Wallet.Credits==120,"Fresh isolated test profile");
            Check(root.Paused,"Main menu pauses simulation");
            Capture("menu-16x9",1600,900); Capture("menu-19_5x9",1950,900);
            Click("НАСТРОЙКИ"); yield return new WaitForSecondsRealtime(.15f);
            Check(root.Hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("ЧАСТОТА КАДРОВ")),"Settings opens");
            var settings=root.Hud.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Settings");
            settings.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>().text=="НАЗАД").onClick.Invoke();
            var enter=root.Hud.GetComponentsInChildren<Button>(true).Single(b=> {string t=b.GetComponentInChildren<Text>(true).text; return t=="В ЛАГЕРЬ" || t.StartsWith("ПРОДОЛЖИТЬ  /  ДЕНЬ");});
            enter.onClick.Invoke(); yield return new WaitForSecondsRealtime(.15f); Refresh();
            Check(!root.Paused,"Menu enters the campaign");
            Capture("day-16x9",1600,900); Capture("day-19_5x9",1950,900);
            string error;
            for(int x=6;x<=8;x++) Check(root.Game.Construction.TryBuild("wall",new Cell(x,9),out error),"Wall placement "+x+": "+error);
            Check(root.Game.Wallet.Credits==0,"Three-wall strategy costs 120");
            root.StartNight();
            for(int i=0;i<1600 && root.Game.Phase==GamePhase.Night;i++)
            {
                root.Game.Tick(.05f); Refresh();
                if(i==140) { Capture("night-16x9",1600,900); Capture("night-19_5x9",1950,900); }
                if(i%5==0) yield return null;
            }
            Check(root.Game.Day==2 && root.Game.Shelter.health>0,"Three-wall first night reaches day two");
            root.NewGame(); Check(root.Game.Wallet.Credits==120,"New expedition resets only test profile");
            Check(root.Game.Construction.TryBuild("gun",new Cell(7,9),out error),"Gun start");
            root.StartNight();
            for(int i=0;i<1600 && root.Game.Phase==GamePhase.Night;i++)
            { root.Game.Tick(.05f); Refresh(); if(i%5==0) yield return null; }
            Check(root.Game.Day==2 && root.Game.Shelter.health>0,"Gun first night reaches day two");

            root.NewGame();
            // Controlled fixtures isolate attack/death/pooling from drone kills, without changing the saved catalog.
            var fixture=BlackwoodBalance.Create(); fixture.droneRange=.01f;
            var game=new GameSession(fixture);
            for(int x=6;x<=8;x++) game.Construction.TryBuild("wall",new Cell(x,9),out error);
            typeof(BlackwoodRoot).GetMethod("Attach",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(root,new object[]{game});
            root.World.RebuildUnits(); game.StartNight();
            var enemy=Spawn(game,"walker",new Cell(7,10)); game.Tick(.05f); Refresh();
            var go=EnemyObject(enemy.instanceId); var view=go.GetComponent<BlackwoodEnemyView>();
            Check(view.animator.avatar.isValid && view.animator.avatar.isHuman,"Existing humanoid Avatar is valid in Play");
            Check(!view.animator.applyRootMotion,"Root motion is disabled");
            Check(enemy.attackTargetId!=0 && view.animator.GetBool("IsAttacking"),"Animator uses exact core attack target");
            yield return new WaitForSecondsRealtime(.3f);
            Check(view.animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"),"Actual Attack Animator state plays");
            Capture("attack-fixture",1600,900);
            root.TogglePause(); Refresh(); float remaining=game.Remaining;
            yield return new WaitForSecondsRealtime(.25f);
            Check(game.Remaining==remaining && view.animator.speed==0,"Pause freezes simulation and Animator");
            root.TogglePause(); Refresh();
            int target=enemy.attackTargetId; game.FindBuilding(target).health=.01f; game.Tick(.05f); Refresh();
            Check(game.FindBuilding(target)==null && enemy.attackTargetId==0 && !view.animator.GetBool("IsAttacking"),"Destroyed wall clears core and Animator target");
            int before=game.Wallet.Credits; enemy.health=0; game.Tick(.05f); Refresh();
            Check(game.Wallet.Credits==before+2,"Logical death awards one reward");
            Check(go.activeSelf,"Death body remains visible until clip ends");
            yield return new WaitForSecondsRealtime(.3f);
            Check(view.animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Real death clip plays");
            float deadline=Time.realtimeSinceStartup+view.deathSeconds+.2f;
            while(Time.realtimeSinceStartup<deadline) { Refresh(); yield return null; }
            Check(!go.activeSelf,"Death returns the body to pool");
            Check(game.Wallet.Credits==before+2,"Visual death adds no duplicate reward");
            var next=Spawn(game,"walker",new Cell(6,14)); Refresh();
            Check(EnemyObject(next.instanceId)==go,"Next spawn reuses pooled zombie");
            Check(!view.animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && !view.animator.GetBool("IsAttacking"),"Pooled Animator resets out of death and attack");
            game.Tick(.05f); Refresh(); yield return new WaitForSecondsRealtime(.3f);
            Check(view.animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"Real Walking clip plays after respawn");
            // Retreat is live removal: it must not queue Death.
            var snap=game.DayStart; snap.day=1; var retreat=GameSession.Restore(fixture,snap);
            typeof(BlackwoodRoot).GetMethod("Attach",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(root,new object[]{retreat});
            root.World.RebuildUnits(); retreat.StartNight();
            var live=Spawn(retreat,"walker",new Cell(0,15)); Refresh(); var liveGo=EnemyObject(live.instanceId);
            foreach(var e in fixture.enemies) e.speed=.001f;
            for(int i=0;i<810;i++) retreat.Tick(.05f);
            Refresh(); Check(retreat.Day==2 && !liveGo.activeSelf,"Live dawn retreat skips death animation");
            File.AppendAllText(folder+"/checks.txt","Capture method: Unity URP SingleCameraRequest in Play Mode, actual scene and UI, 1600x900 and 1950x900.\n");
        }
        void Capture(string name,int width,int height)
        {
            Refresh();
            var camera=root.GetComponentsInChildren<Camera>().Single();
            var canvas=root.Hud.GetComponentInChildren<Canvas>(); var scaler=canvas.GetComponent<CanvasScaler>();
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); rt.Create();
            var oldTarget=camera.targetTexture; float oldAspect=camera.aspect; var oldMode=canvas.renderMode;
            var oldCamera=canvas.worldCamera; var oldScale=canvas.scaleFactor; bool oldScaler=scaler.enabled;
            var oldActive=RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.aspect=(float)width/height;
                scaler.enabled=false; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera;
                canvas.planeDistance=1; canvas.scaleFactor=(float)height/900;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
                RenderTexture.active=rt;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG()); Destroy(image);
                Check(root.Hud.GetComponentsInChildren<Button>().All(b=>Inside(b.GetComponent<RectTransform>(),camera,width,height)),"Visible buttons inside "+width+"x"+height+" at "+name);
            }
            finally
            {
                RenderTexture.active=oldActive; camera.targetTexture=oldTarget; camera.aspect=oldAspect;
                canvas.renderMode=oldMode; canvas.worldCamera=oldCamera; canvas.scaleFactor=oldScale; scaler.enabled=oldScaler;
                rt.Release(); Destroy(rt); Canvas.ForceUpdateCanvases();
            }
        }
        static bool Inside(RectTransform r,Camera c,int w,int h)
        {
            var corners=new Vector3[4]; r.GetWorldCorners(corners);
            return corners.All(p=> { var s=c.WorldToScreenPoint(p); return s.x>=-1 && s.x<=w+1 && s.y>=-1 && s.y<=h+1; });
        }
    }
}
#endif

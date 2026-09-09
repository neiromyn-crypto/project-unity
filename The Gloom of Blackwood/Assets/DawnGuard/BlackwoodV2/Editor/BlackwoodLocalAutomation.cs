using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    // Fixed local commands only; no network listener or arbitrary code execution.
    [InitializeOnLoad]
    public static class BlackwoodLocalAutomation
    {
        public const string Evidence="IntegrationEvidence";
        static double next;
        static BlackwoodLocalAutomation() { EditorApplication.update+=Poll; }
        [InitializeOnEnterPlayMode]
        static void RefreshOnly(EnterPlayModeOptions options)
        {
            if(!File.Exists(Evidence+"/refresh-only.txt")) return;
            File.Delete(Evidence+"/refresh-only.txt");
            EditorApplication.isPlaying=false;
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next=EditorApplication.timeSinceStartup+1;
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence+"/editor-status.txt",DateTime.UtcNow.ToString("O")+" play="+EditorApplication.isPlaying+" scene="+EditorSceneManager.GetActiveScene().path);
            string path=Evidence+"/command.txt";
            if(!File.Exists(path)) return;
            string command=File.ReadAllText(path).Trim(); File.Delete(path);
            try
            {
                if(command=="refresh") { AssetDatabase.Refresh(); return; }
                if(command=="inventory") Inventory();
                else if(command=="compose") BlackwoodCompositionPass.Apply();
                else if(command=="integrate") BlackwoodUserAssetSetup.Integrate();
                else if(command=="validate") BlackwoodIntegrationChecks.Run();
                else if(command=="verify-play")
                {
                    if(!EditorSceneManager.GetActiveScene().path.Contains("/UserMap")) throw new InvalidOperationException("Open the generated user map first.");
                    SessionState.SetBool("Blackwood.VerifyPlay",true);
                    EditorApplication.isPlaying=true;
                }
                else if(command=="checks") BlackwoodChecks.Run();
                else if(command=="build") BlackwoodUserAssetsBuilder.Build();
                else if(command=="play") EditorApplication.isPlaying=true;
                else if(command=="stop") EditorApplication.isPlaying=false;
                else throw new InvalidOperationException("Unknown command: "+command);
                File.WriteAllText(Evidence+"/command-result.txt",command+" OK "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception ex) { File.WriteAllText(Evidence+"/command-result.txt",command+" FAILED\n"+ex); Debug.LogException(ex); }
        }
        public static void Inventory()
        {
            var report=new StringBuilder();
            report.AppendLine("SCENES");
            for(int i=0;i<EditorSceneManager.sceneCount;i++) { var s=EditorSceneManager.GetSceneAt(i); report.AppendLine(s.path+" dirty="+s.isDirty); }
            foreach(var guid in AssetDatabase.FindAssets("t:GameObject",new[]{"Assets/GameArt","Assets/Enemy"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine("\nMODEL "+path);
                foreach(var r in go.GetComponentsInChildren<Renderer>(true)) report.AppendLine("  renderer "+r.name+" bounds="+r.bounds+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m==null ? "NULL" : m.name+" ["+m.shader.name+"]")));
                foreach(var a in go.GetComponentsInChildren<Animator>(true)) report.AppendLine("  animator avatar="+(a.avatar==null ? "NULL" : a.avatar.name+" valid="+a.avatar.isValid+" human="+a.avatar.isHuman)+" controller="+a.runtimeAnimatorController);
                foreach(var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__"))) report.AppendLine("  clip "+c.name+" length="+c.length+" human="+c.humanMotion+" events="+c.events.Length);
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true)) report.AppendLine("  behaviour "+(b==null ? "MISSING" : b.GetType().FullName));
                report.AppendLine("  hierarchy "+string.Join(" / ",go.GetComponentsInChildren<Transform>(true).Select(t=>t.name)));
            }
            File.WriteAllText(Evidence+"/asset-inventory.txt",report.ToString());
        }
    }
}

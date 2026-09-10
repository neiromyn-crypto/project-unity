using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGuard.BlackwoodLTD.Editor
{
    // Disposable edit-time view. Campaign state and authored scenes are never overwritten.
    [InitializeOnLoad]
    public static class CoastScenePreview
    {
        static GameObject preview;static Scene previewScene;static double next;
        static CoastScenePreview()
        {EditorApplication.update+=Update;AssemblyReloadEvents.beforeAssemblyReload+=Clear;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Clear();};EditorSceneManager.sceneSaving+=(s,p)=>Clear();}
        public static void Clear(){if(preview!=null)Object.DestroyImmediate(preview);preview=null;}
        static void Update()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var scene=SceneManager.GetActiveScene();if(scene.path!=CoastEditor.ScenePath){Clear();return;}
            if(preview!=null&&previewScene==scene)return;Clear();
            CoastRoot source=null;foreach(var go in scene.GetRootGameObjects()){source=go.GetComponent<CoastRoot>();if(source!=null)break;}
            if(source==null||source.catalog==null)return;
            preview=new GameObject("Coast preview — generated, Play starts campaign");preview.hideFlags=HideFlags.DontSave;previewScene=scene;
            var root=preview.AddComponent<CoastRoot>();root.InitializePreview(source.catalog);
            foreach(var t in preview.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.DontSave;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(14,0,12),Quaternion.Euler(50,0,0),21,false,true);
        }
    }
}


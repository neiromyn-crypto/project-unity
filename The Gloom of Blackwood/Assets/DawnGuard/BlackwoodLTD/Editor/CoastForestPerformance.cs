using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace DawnGuard.BlackwoodLTD.Editor
{
    // A controlled, real Scene View repaint sweep, not a player FPS claim.
    [InitializeOnLoad]
    public static class CoastForestPerformance
    {
        const string Folder="IntegrationEvidence/Performance";
        static readonly FrameTiming[] timings=new FrameTiming[1]; static SceneView view; static string label; static int frame;
        static double last,nextStatus; static bool running,requested;
        static readonly List<double> intervals=new List<double>();
        static readonly List<string> samples=new List<string>();
        static Vector3 oldPivot,sweepCenter; static Quaternion oldRotation; static float oldSize; static bool oldOrtho;
        static bool oldProfiler,oldBinary,oldProfileEditor; static string oldLog;
        static CoastForestPerformance(){EditorApplication.update+=Update;SceneView.duringSceneGui+=Repaint;AssemblyReloadEvents.beforeAssemblyReload+=Cancel;}
        public static void Begin(string name,Vector3? center=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||running)throw new InvalidOperationException("Requires idle Edit Mode");
            Directory.CreateDirectory(Folder);label=name;Inventory(name);
            view=EditorWindow.GetWindow<SceneView>();view.Show();view.Focus();oldPivot=view.pivot;oldRotation=view.rotation;oldSize=view.size;oldOrtho=view.orthographic;
            sweepCenter=center??new Vector3(14,0,12);view.LookAt(sweepCenter,Quaternion.Euler(50,0,0),21,false,true);
            intervals.Clear();samples.Clear();samples.Add("frame,repaint_interval_ms,cpu_frame_ms,triangles,vertices,draw_calls,set_pass_calls");
            frame=0;last=0;requested=false;running=true;
            oldProfiler=Profiler.enabled;oldBinary=Profiler.enableBinaryLog;oldLog=Profiler.logFile;oldProfileEditor=ProfilerDriver.profileEditor;ProfilerDriver.profileEditor=true;
            Profiler.logFile=Path.GetFullPath(Folder+"/"+name+".raw");Profiler.enableBinaryLog=true;Profiler.enabled=true;
        }
        static void Update()
        {
            if(running&&EditorApplication.timeSinceStartup>nextStatus){nextStatus=EditorApplication.timeSinceStartup+1;File.WriteAllText(Folder+"/progress.txt",label+" frame="+frame+" requested="+requested+" window="+view.position+" UTC="+DateTime.UtcNow.ToString("O"));}
            if(!running||requested||view==null)return;
            // Repeat exactly the same slow horizontal camera sweep before and after.
            view.pivot=sweepCenter+Vector3.right*Mathf.Sin(frame*.06f)*1.5f;requested=true;view.Repaint();
        }
        static void Repaint(SceneView current)
        {
            if(!running||current!=view||Event.current.rawType!=EventType.Repaint||!requested)return;
            FrameTimingManager.CaptureFrameTimings(); bool hasTiming=FrameTimingManager.GetLatestTimings(1,timings)>0; double now=EditorApplication.timeSinceStartup;double ms=(now-last)*1000;last=now;requested=false;
            if(frame>=15){intervals.Add(ms);samples.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F3},{2:F3},{3},{4},{5},{6}",frame-15,ms,hasTiming?timings[0].cpuFrameTime:double.NaN,UnityStats.triangles,UnityStats.vertices,UnityStats.drawCalls,UnityStats.setPassCalls));}
            frame++;if(frame>=95)Finish();
        }
        static void Finish()
        {
            if(!running)return;running=false;RestoreProfiler();
            File.WriteAllLines(Folder+"/"+label+"-samples.csv",samples);
            var sorted=intervals.OrderBy(x=>x).ToArray();
            File.WriteAllText(Folder+"/"+label+"-summary.txt",string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "UTC: {0:O}\nScene View repaint sweep, forest enabled; 15 warmup + {1} samples\nWindow: {2} x {3}; graphics: {4}; GPU: {5}\nMedian repaint interval: {6:F3} ms\nP95 repaint interval: {7:F3} ms\nMean repaint interval: {8:F3} ms\nStats are Editor global rendering counters sampled from the Scene View repaint. Repaint interval includes editor overhead and scheduling; it is not standalone player FPS.\n",DateTime.UtcNow,sorted.Length,view.position.width,view.position.height,SystemInfo.graphicsDeviceType,SystemInfo.graphicsDeviceName,sorted[sorted.Length/2],sorted[(int)(sorted.Length*.95)],sorted.Average()));
            Capture(view.camera,Folder+"/"+label+"-scene.png");view.LookAt(oldPivot,oldRotation,oldSize,oldOrtho,true);
        }
        static void RestoreProfiler(){Profiler.enabled=false;Profiler.enableBinaryLog=oldBinary;Profiler.logFile=oldLog;ProfilerDriver.profileEditor=oldProfileEditor;Profiler.enabled=oldProfiler;}
        static void Cancel(){if(!running)return;running=false;RestoreProfiler();}
        public static void Capture(Camera camera,string path)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        public static void Inventory(string name)
        {
            var roots=Resources.FindObjectsOfTypeAll<CoastRoot>().Where(r=>r.gameObject.scene.IsValid()&&r.World!=null).ToArray();
            var lines=new List<string>{"hierarchy,active,mesh_asset,triangles,colliders,cast_shadows,material_assets,instancing,lod_group,instance_id,bounds_center_x,bounds_center_y,bounds_center_z"};long total=0;int count=0;
            foreach(var root in roots)foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!PathOf(mf.transform).Contains("User_pine")&&!PathOf(mf.transform).Contains("Coast Forest"))continue;
                var r=mf.GetComponent<Renderer>();var m=mf.sharedMesh;if(r==null||m==null)continue;long tris=0;for(int s=0;s<m.subMeshCount;s++)tris+=(long)m.GetIndexCount(s)/3;total+=tris;count++;
                lines.Add(string.Join(",",new[]{PathOf(mf.transform).Replace(',', ' '),mf.gameObject.activeInHierarchy.ToString(),AssetDatabase.GetAssetPath(m),tris.ToString(),mf.GetComponentsInChildren<Collider>(true).Length.ToString(),r.shadowCastingMode.ToString(),string.Join(";",r.sharedMaterials.Select(mat=>AssetDatabase.GetAssetPath(mat))),string.Join(";",r.sharedMaterials.Select(mat=>mat!=null&&mat.enableInstancing?"true":"false")),(mf.GetComponentInParent<LODGroup>()!=null).ToString(),mf.GetEntityId().ToString(),r.bounds.center.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture),r.bounds.center.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture),r.bounds.center.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)}));
            }
            File.WriteAllLines(Folder+"/"+name+"-inventory.csv",lines);File.WriteAllText(Folder+"/"+name+"-inventory-summary.txt","Mesh renderers including inactive menu and all LOD levels: "+count+"\nTriangle instances including inactive/all LOD levels: "+total+"\n");
        }
        static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
    }
}

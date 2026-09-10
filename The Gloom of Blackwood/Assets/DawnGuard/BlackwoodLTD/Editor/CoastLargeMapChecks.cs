using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DawnGuard.Core;

namespace DawnGuard.BlackwoodLTD.Editor
{
    [InitializeOnLoad]
    public static class CoastLargeMapChecks
    {
        const string Folder="IntegrationEvidence/LargeMap";
        static readonly List<string> exceptions=new List<string>();
        static double until;static int frames,lastFrame=-1;static bool playingTest;
        static CoastLargeMapChecks(){if(SessionState.GetBool("Coast.MapSmoke",false))CoastRoot.TestSavePath=Path.GetFullPath(Folder+"/smoke-save.json");Application.logMessageReceived+=Log;EditorApplication.update+=Update;}
        static void Log(string condition,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)exceptions.Add(type+": "+condition);}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);var report=new List<string>();
            Action<bool,string> check=(ok,message)=>{report.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",report);if(!ok)throw new Exception(message);};
            var root=Resources.FindObjectsOfTypeAll<CoastRoot>().First(r=>r.gameObject.scene.IsValid()&&r.World!=null);var map=root.Game.Map;
            check(map.Width==44&&map.Depth==36,"Grid 44 x 36, unchanged cell scale");int blocked;
            check(map.AllOpen(out blocked),"All three 2x2-clearance routes reach shelter");
            var svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='880' height='760' viewBox='0 0 440 380'><rect width='440' height='380' fill='#23343b'/>");
            int rocks=0;for(int x=0;x<map.Width;x++)for(int z=0;z<map.Depth;z++){if(map.Rocks[x,z])rocks++;svg.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"<rect x='{0}' y='{1}' width='9' height='9' fill='{2}'/>",x*10,(35-z)*10,map.Rocks[x,z]?"#87918b":z<7?"#a68b62":"#344e3a");}
            check(rocks>200&&rocks<600,"Natural ridge cells: "+rocks+"; remaining space supports player maze decisions");
            string[] colors={"#ffc65b","#62daf5","#e896ed"};
            for(int f=0;f<3;f++)
            {
                var route=map.Route(f);check(route.Count>30&&route.Last().Equals(map.Goal),"Front "+f+" route length "+(route.Count-1));
                int turns=0;for(int i=2;i<route.Count;i++)if(route[i].x-route[i-1].x!=route[i-1].x-route[i-2].x||route[i].z-route[i-1].z!=route[i-1].z-route[i-2].z)turns++;
                check(turns>=3,"Front "+f+" natural direction changes: "+turns);
                // Prove a different connected path exists when a middle route footprint is occupied.
                int alternatives=0;for(int i=5;i<route.Count-5;i++)
                {var cell=route[i];map.Occupancy[cell.x,cell.z]=-1;var distances=map.Field(new[]{map.Goal});var spawn=map.Entrances[f];if(distances[spawn.x,spawn.z]>=0)alternatives++;map.Occupancy[cell.x,cell.z]=0;}
                check(alternatives>=3,"Front "+f+" alternative path survives "+alternatives+" tested local closures");
                svg.Append("<polyline fill='none' stroke='"+colors[f]+"' stroke-width='2' points='");foreach(var cell in route)svg.Append((cell.x*10+10)+","+((35-cell.z)*10)+" ");svg.Append("'/>");
            }
            check(map.CanBuildCell(18,18)&&map.CanBuildCell(25,26)&&map.CanBuildCell(21,12),"Candidate kill zones remain buildable (18,18), (25,26), (21,12)");
            var portals=new[]{new[]{new Cell(14,30),new Cell(15,30),new Cell(16,30),new Cell(17,30),new Cell(18,30),new Cell(19,30)},new[]{new Cell(6,21),new Cell(7,21)},new[]{new Cell(40,13),new Cell(41,13)}};
            for(int f=0;f<3;f++)
            {foreach(var cell in portals[f])map.Occupancy[cell.x,cell.z]=-1;var field=map.Field(new[]{map.Goal});var spawn=map.Entrances[f];check(field[spawn.x,spawn.z]>=0,"Front "+f+" whole shortcut can be closed; another corridor remains, length="+field[spawn.x,spawn.z]);foreach(var cell in portals[f])map.Occupancy[cell.x,cell.z]=0;}
            svg.Append("<text x='10' y='375' fill='white' font-size='10'>SOUTH / SAFE COAST — NORTH gold, WEST cyan, EAST violet</text></svg>");File.WriteAllText(Folder+"/maze-routes.svg",svg.ToString());
            var rig=root.World.CameraRig;var old=rig.Focus;rig.SetFocus(new Vector3(-999,0,999));check(rig.Focus.x==4&&rig.Focus.z==33,"Camera clamps northwest request");rig.SetFocus(new Vector3(999,0,-999));check(rig.Focus.x==40&&rig.Focus.z==5,"Camera clamps southeast request");
            rig.Zoom(-999);check(rig.Size==8,"Readable minimum zoom");rig.Zoom(999);check(rig.Size==13,"Maximum zoom cannot expose the whole 36-cell depth");rig.Zoom(-3);rig.SetFocus(old);
            foreach(var point in new[]{new Vector3(map.CampX,0,10.5f),new Vector3(22,0,30),new Vector3(8,0,26),new Vector3(36,0,21)})
            {rig.SetFocus(point);root.World.Refresh(0);CoastForestPerformance.Capture(root.World.Camera,Folder+"/camera-"+(int)point.x+"-"+(int)point.z+".png");}
            rig.SetFocus(old);root.World.Refresh(0);
            var camera=root.World.Camera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize;
            try{camera.orthographicSize=25;camera.transform.position=new Vector3(22,48,-22);camera.transform.LookAt(new Vector3(22,0,19));CoastForestPerformance.Capture(camera,Folder+"/overview.png");}finally{camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;}
            CoastForestAssets.Validate();check(true,"Forest pass invariants retained");
            check(exceptions.Count==0,"No errors/exceptions during scene generation and camera checks");
            File.WriteAllText(Folder+"/exceptions.txt",string.Join("\n",exceptions));
        }
        public static void PlayTest()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");exceptions.Clear();Directory.CreateDirectory(Folder);CoastRoot.TestSavePath=Path.GetFullPath(Folder+"/smoke-save.json");SessionState.SetBool("Coast.MapSmoke",true);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;
        }
        static void Update()
        {
            if(!SessionState.GetBool("Coast.MapSmoke",false))return;
            if(!EditorApplication.isPlaying){CoastRoot.TestSavePath=Path.GetFullPath(Folder+"/smoke-save.json");return;}
            var root=UnityEngine.Object.FindAnyObjectByType<CoastRoot>();if(root==null||root.World==null||root.Hud==null)return;
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!playingTest){root.EnterGame();root.TogglePause();until=EditorApplication.timeSinceStartup+10;frames=0;playingTest=true;}
            root.World.CameraRig.SetFocus(new Vector3(22+Mathf.Sin(frames*.03f)*16,0,19+Mathf.Cos(frames*.03f)*13));frames++;
            if(EditorApplication.timeSinceStartup<until)return;
            File.WriteAllText(Folder+"/play-smoke.txt",(exceptions.Count==0?"PASS":"FAIL")+" — 10 second paused runtime camera sweep; frames="+frames+"; errors/exceptions="+exceptions.Count+"\n"+string.Join("\n",exceptions));
            SessionState.SetBool("Coast.MapSmoke",false);playingTest=false;EditorApplication.isPlaying=false;CoastRoot.TestSavePath=null;
        }
    }
}

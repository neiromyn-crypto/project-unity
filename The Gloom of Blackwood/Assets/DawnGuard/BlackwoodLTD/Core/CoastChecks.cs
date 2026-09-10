using System;
using System.Collections.Generic;
using System.Text;
namespace DawnGuard.BlackwoodLTD
{
    public static class CoastChecks
    {
        static void Check(bool value,string name,StringBuilder report){if(!value)throw new Exception("FAIL: "+name);report.AppendLine("PASS: "+name);}
        static CoastSession New(){return new CoastSession(CoastRules.Create());}
        public static void NorthernMaze(CoastSession g)
        {
            for(int x=13;x<=15;x++){if(!g.Build("wall",x,20))throw new Exception(g.LastError);if(!g.Build("wall",x,14))throw new Exception(g.LastError);}
        }
        public static string FirstNightReport()
        {
            var r=new StringBuilder();foreach(string mode in new[]{"empty","gun_straight","gun_maze","two_guns_maze"})
            {
                var g=New();if(mode.Contains("maze")){g.S.stone+=16;NorthernMaze(g);}
                if(mode!="empty"&&!g.Build("gun",14,17))throw new Exception(g.LastError);
                if(mode=="two_guns_maze"&&!g.Build("gun",14,11))throw new Exception(g.LastError);
                int steps=0;g.StartNight();while(g.S.phase==CoastPhase.Night&&steps++<8000)g.Tick(.05f);
                r.AppendLine(mode+": "+g.S.phase+" hp="+g.S.campHP.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" killed="+g.S.killed+" seconds="+(steps*.05f).ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
            }return r.ToString();
        }
        public static string Run()
        {
            var r=new StringBuilder();var g=New();int blocked;Check(g.Map.AllOpen(out blocked),"All 3 fronts have clearance",r);
            int original=g.Map.Route(0).Count;g.S.stone+=16;NorthernMaze(g);Check(g.Map.Route(0).Count>original,"Maze lengthens northern route",r);
            int cr=g.S.credits,st=g.S.stone;Check(!g.Build("wall",16,20),"Sealing two-cell gap rejected",r);Check(g.S.credits==cr&&g.S.stone==st,"Rejected placement spends nothing",r);
            Check(!g.Build("wall",14,2),"Protected worker road rejects building",r);
            g=New();for(int i=0;i<400;i++)g.Tick(.05f);Check(g.S.stone==116,"Two complete cargo deliveries credit exactly 16",r);
            Check(g.Hire(),"Worker training starts",r);int old=g.S.workers.Count;g.StartNight();for(int i=0;i<40;i++)g.Tick(.05f);Check(g.S.workers.Count==old,"Night cannot accelerate training",r);
            g=New();g.S.day=2;Check(g.ActivateLab(),"Lab commissioning",r);Check(g.Research(1),"Research starts",r);for(int i=0;i<401;i++)g.Tick(.05f);Check((g.S.tech&1)!=0,"Research unlocks defense",r);
            var saved=g.S.Copy();var restored=new CoastSession(g.Rules,saved);g.Tick(.05f);Check(restored.S.remaining!=g.S.remaining,"Save restore isolates mutable state",r);
            Check(!g.Support(0,false),"Support unavailable in day",r);
            g=New();g.StartNight();for(int i=0;i<500&&g.S.phase==CoastPhase.Night;i++)g.Tick(.05f);Check(g.S.killed==0,"Drone causes no kills",r);
            while(g.S.phase==CoastPhase.Night)g.Tick(.05f);Check(g.S.phase==CoastPhase.Defeat,"No-defense first night loses",r);
            g=New();g.Build("gun",14,17);NorthernMaze(g);g.StartNight();for(int i=0;i<250;i++)g.Tick(.05f);var resumed=new CoastSession(g.Rules,g.S);
            for(int i=0;i<1000;i++){g.Tick(.05f);resumed.Tick(.05f);}Check(g.S.phase==resumed.S.phase&&g.S.killed==resumed.S.killed&&g.S.credits==resumed.S.credits&&g.S.campHP==resumed.S.campHP,"Mid-wave restore produces same battle outcome",r);
            var bad=New().S.Copy();bad.workers[0].id=bad.nextId;bool rejected=false;try{new CoastSession(CoastRules.Create(),bad);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid saved entity id rejected",r);
            r.Append(FirstNightReport());r.AppendLine("FULL CAMPAIGN — no injected resources, physical daytime deliveries:");r.Append(CoastCampaignChecks.Run());return r.ToString();
        }
    }
}

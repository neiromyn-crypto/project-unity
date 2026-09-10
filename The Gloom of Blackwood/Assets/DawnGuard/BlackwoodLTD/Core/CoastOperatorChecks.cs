using System;
using System.Collections.Generic;
using System.Text;
namespace DawnGuard.BlackwoodLTD
{
    public static class CoastOperatorChecks
    {
        public static string Run()
        {
            var result=new StringBuilder();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);result.AppendLine("PASS "+name);};
            var g=new CoastSession(CoastRules.Create());check(g.S.campHP==300&&g.S.operatorHP==100,"New session has 300 integrity and 100 operator HP");g.StartNight();g.DamageCommandNode(10);check(g.S.campHP==290&&g.S.operatorHP==100,"Integrity protects operator");g.DamageCommandNode(1000);check(g.S.campHP==0&&g.S.operatorHP==100&&g.S.phase==CoastPhase.Night,"Breaking hit is absorbed by core");g.DamageCommandNode(99);check(g.S.operatorHP==1,"Operator vulnerable only after breach");
            var copy=new CoastSession(g.Rules,g.S);check(copy.S.operatorHP==1&&copy.S.campHP==0,"Broken core and live operator survive save roundtrip");g.DamageCommandNode(1);check(g.S.phase==CoastPhase.Defeat&&g.S.operatorHP==0,"Operator death causes defeat");new CoastSession(g.Rules,g.S);check(true,"Defeated state is valid on load");
            g=new CoastSession(CoastRules.Create());g.StartNight();g.S.spawnCursor=8;
            Array.Clear(g.Map.Rocks,0,g.Map.Rocks.Length);
            for(int x=0;x<g.Map.Width;x++)if(x!=21&&x!=22)g.Map.Rocks[x,12]=true;
            var wall=new CoastBuilding{id=g.S.nextId++,kind="wall",x=21,z=12,hp=180};g.S.buildings.Add(wall);
            var ordinary=new CoastBuilding{id=g.S.nextId++,kind="wall",x=3,z=20,hp=180};g.S.buildings.Add(ordinary);
            g.Map.Rebuild(g.S.buildings);var enemy=new CoastEnemy{id=g.S.nextId++,kind="walker",front=0,x=22,z=17,nx=21,nz=16,hp=48};g.S.enemies.Add(enemy);
            for(int i=0;i<300;i++)g.Tick(.05f);
            check(wall.hp<180&&ordinary.hp==180,"Ordinary walker attacks only reconnecting barrier when path is blocked");
            g.S.buildings.Remove(wall);g.Map.Rebuild(g.S.buildings);for(int i=0;i<1200&&g.S.campHP>=300;i++)g.Tick(.05f);
            check(g.S.campHP<300&&ordinary.hp==180,"Reopened path leads to command node, not unrelated building");
            var rules=CoastRules.Create();check(rules.enemies.Length==4&&rules.waves.Length==3,"Four configured roles and three foundation waves");
            check(Array.TrueForAll(rules.waves[0].groups,p=>p.kind=="walker"),"Night 1 basic only");check(Array.Exists(rules.waves[1].groups,p=>p.kind=="runner"),"Night 2 introduces fast role");check(Array.Exists(rules.waves[2].groups,p=>p.kind=="brute"),"Night 3 introduces heavy role");
            return result.ToString();
        }
    }
}

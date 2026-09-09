using System;
using System.Collections.Generic;

namespace DawnGuard.Core.Diagnostics
{
    // Plain C# checks of game invariants; callable without UnityEngine or NUnit.
    public static class SimulationChecks
    {
        public static string RunAll()
        {
            var passed=new List<string>();
            Check("Initial camp has no walls or turrets",()=> {
                var g=New(); Assert(g.Wallet.Credits==120,"starting credits");
                foreach(var b in g.Buildings) Assert(b.definitionId!="wall" && b.definitionId!="gun","no defenses");
            },passed);
            Check("One tower exhausts starting budget",()=> {
                var g=New(); string e;
                Assert(g.Construction.TryBuild("gun",new Cell(7,9),out e),e);
                Assert(g.Wallet.Credits==0,"budget");
                Assert(!g.Construction.TryBuild("wall",new Cell(6,9),out e),"cannot buy both");
            },passed);
            Check("Three walls exhaust starting budget",()=> {
                var g=New(); string e;
                for(int x=6;x<=8;x++) Assert(g.Construction.TryBuild("wall",new Cell(x,9),out e),e);
                Assert(g.Wallet.Credits==0,"walls budget");
            },passed);
            Check("Overlap and out-of-board do not spend credits",()=> {
                var g=New(); string e;
                Assert(!g.Construction.TryBuild("wall",new Cell(7,5),out e),"overlap");
                Assert(!g.Construction.TryBuild("wall",new Cell(-1,0),out e),"bounds");
                Assert(g.Wallet.Credits==120,"transaction");
            },passed);
            Check("Day 1 has no forced timer; night forbids building",()=> {
                var g=New(); string e;
                for(int i=0;i<2000;i++) g.Tick(.05f);
                Assert(g.Phase==GamePhase.Day,"untimed tutorial");
                g.StartNight(); Assert(!g.Construction.TryBuild("wall",new Cell(6,9),out e),"night lock");
                Assert(!g.StartNight(),"no duplicate night transition");
            },passed);
            Check("Cancel a new purchase; move reserves footprint",()=> {
                var g=New(); string e;
                g.Construction.TryBuild("wall",new Cell(6,9),out e);
                int id=g.Board.Occupant(new Cell(6,9));
                Assert(g.Construction.TryMove(id,new Cell(8,9),out e),e);
                Assert(g.Board.Occupant(new Cell(6,9))==0,"old footprint freed");
                Assert(g.Construction.TrySell(id,out e),e);
                Assert(g.Wallet.Credits==120,"full fresh refund");
                Assert(!g.Construction.TrySell(id,out e),"no duplicate refund");
            },passed);
            Check("Power brownout preserves buildings",()=> {
                var g=New();
                var lab=g.AddBuilding(g.Rules.Building("lab"),new Cell(10,7),false,160);
                var coil=g.AddBuilding(g.Rules.Building("tesla"),new Cell(6,9),false,200);
                g.RefreshPower(); Assert(lab.powered && coil.powered,"powered");
                BuildingState generator=null;
                foreach(var b in g.Buildings) if(b.definitionId=="generator") generator=b;
                g.DamageBuilding(generator,10000);
                Assert(!lab.powered && !coil.powered,"brownout");
                Assert(g.FindBuilding(coil.instanceId)!=null,"not destroyed by lost power");
            },passed);
            Check("Snapshot is a deep copy; invalid versions are rejected",()=> {
                var g=New(); var save=g.Snapshot();
                float hp=g.Shelter.health; save.buildings[0].health=1;
                Assert(g.Shelter.health==hp,"snapshot alias");
                var restored=GameSession.Restore(g.Rules,g.Snapshot());
                Assert(restored.Wallet.Credits==120 && restored.Buildings.Count==3,"round trip");
                save.schemaVersion=99; bool rejected=false;
                try { GameSession.Restore(g.Rules,save); } catch(ArgumentException) { rejected=true; }
                Assert(rejected,"schema guard");
            },passed);
            Check("Enemy attacks blocked chokepoint, never walks through wall",()=> {
                var g=New(); string e;
                for(int x=6;x<=8;x++) g.Construction.TryBuild("wall",new Cell(x,9),out e);
                g.StartNight(); g.SpawnEnemy("walker",new Cell(7,10));
                var enemy=g.Enemies[0]; var wall=g.FindBuilding(g.Board.Occupant(new Cell(7,9)));
                float hp=wall.health;
                // Move drone away; core enemy movement must still collide logically.
                for(int i=0;i<10;i++) g.Tick(.05f,-1,-1);
                Assert(enemy.z>=10.49f,"must not cross intact wall");
                bool anyDamaged=false;
                foreach(var b in g.Buildings) if(b.definitionId=="wall" && b.health<hp) anyDamaged=true;
                Assert(anyDamaged,"attack instead of permanent deadlock");
            },passed);
            Check("Kill rewards are credited once",()=> {
                var g=New(); g.SpawnEnemy("walker",new Cell(0,0));
                g.Enemies[0].health=0; g.RemoveDeadEnemies(); g.RemoveDeadEnemies();
                Assert(g.Wallet.Credits==124,"one reward");
            },passed);
            Check("Invalid time step rejected",()=> {
                bool rejected=false; try { New().Tick(float.NaN); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Assert(rejected,"NaN rejected");
            },passed);
            Check("First wave can complete with tower and active drone",()=> {
                var g=New(); string e; g.Construction.TryBuild("gun",new Cell(7,9),out e); g.StartNight();
                for(int i=0;i<2000 && g.Phase==GamePhase.Night;i++) g.Tick(.05f);
                Assert(g.Phase==GamePhase.Day && g.Day==2,"first tower route");
            },passed);
            Check("First wave can complete with three walls and active drone",()=> {
                var g=New(); string e;
                for(int x=6;x<=8;x++) g.Construction.TryBuild("wall",new Cell(x,9),out e);
                g.StartNight();
                for(int i=0;i<2000 && g.Phase==GamePhase.Night;i++) g.Tick(.05f);
                Assert(g.Phase==GamePhase.Day && g.Day==2,"first wall route");
            },passed);
            Check("Defeat stops simulation; restart returns dawn economy",()=> {
                var g=New(); string e; g.Construction.TryBuild("gun",new Cell(7,9),out e);
                g.StartNight(); g.DamageBuilding(g.Shelter,10000);
                Assert(g.Phase==GamePhase.Defeat,"defeat");
                var retry=GameSession.Restore(g.Rules,g.DayStart);
                Assert(retry.Wallet.Credits==120 && retry.Buildings.Count==3,"replan checkpoint");
            },passed);
            return "PASS: "+passed.Count+" checks\n"+string.Join("\n",passed.ToArray());
        }
        private static GameSession New() { return new GameSession(DemoRules.Create()); }
        private static void Assert(bool value,string label) { if(!value) throw new Exception("Check failed: "+label); }
        private static void Check(string name,Action action,List<string> passed) { action(); passed.Add(name); }
    }
}

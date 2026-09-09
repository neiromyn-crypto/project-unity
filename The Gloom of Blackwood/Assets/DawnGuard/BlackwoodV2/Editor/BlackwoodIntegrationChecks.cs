using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEditor;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodIntegrationChecks
    {
        static void Assert(bool value,string label) { if(!value) throw new InvalidOperationException(label); }
        public static void Run()
        {
            var passed=new List<string>();
            passed.Add(DawnGuard.Core.Diagnostics.SimulationChecks.RunAll());
            BlackwoodChecks.Run(); passed.Add("PASS 11 Blackwood economy checks");
            var r=BlackwoodBalance.Create(); string error;
            foreach(bool walls in new[]{false,true})
            {
                var g=new GameSession(r);
                if(walls) for(int x=6;x<=8;x++) Assert(g.Construction.TryBuild("wall",new Cell(x,9),out error),error);
                else Assert(g.Construction.TryBuild("gun",new Cell(7,9),out error),error);
                g.StartNight(); for(int i=0;i<1600 && g.Phase==GamePhase.Night;i++) g.Tick(.05f);
                Assert(g.Day==2 && g.Shelter.health>0,"First night survival");
                passed.Add("PASS first night "+(walls?"three walls":"gun")+" credits="+g.Wallet.Credits+" HP="+g.Shelter.health);
            }
            var combatRules=BlackwoodBalance.Create(); combatRules.droneRange=.01f;
            var combat=new GameSession(combatRules);
            for(int x=6;x<=8;x++) Assert(combat.Construction.TryBuild("wall",new Cell(x,9),out error),error);
            combat.StartNight();
            var enemy=Spawn(combat,"walker",new Cell(7,10)); combat.Tick(.05f);
            int firstTarget=enemy.attackTargetId;
            Assert(firstTarget!=0 && combat.FindBuilding(firstTarget).definitionId=="wall","Exact attack target missing");
            Assert(combat.FindBuilding(firstTarget).health<180,"Attack must damage the target");
            combat.FindBuilding(firstTarget).health=.01f; combat.Tick(.05f);
            Assert(combat.FindBuilding(firstTarget)==null && enemy.attackTargetId==0,"Destroyed target not cleared");
            bool retargeted=false;
            for(int i=0;i<450 && combat.Phase==GamePhase.Night;i++)
            { combat.Tick(.05f); if(enemy.attackTargetId!=0 && enemy.attackTargetId!=firstTarget) {retargeted=true; break;} }
            Assert(retargeted,"Enemy must select another target after barrier destruction");
            passed.Add("PASS exact attack target, wall damage, destruction, movement and target change");
            int credits=combat.Wallet.Credits; enemy.health=0; combat.Tick(.05f);
            Assert(combat.Wallet.Credits==credits+2,"One kill reward"); combat.Tick(.05f);
            Assert(combat.Wallet.Credits==credits+2,"No duplicate kill reward"); passed.Add("PASS reward exactly once");

            var finalRules=BlackwoodBalance.Create(); finalRules.droneRange=.01f;
            foreach(var e in finalRules.enemies) { e.speed=.001f; e.damagePerSecond=.001f; }
            var snapshot=new GameSession(finalRules).Snapshot(); snapshot.day=8;
            var final=GameSession.Restore(finalRules,snapshot); final.StartNight();
            for(int i=0;i<1820;i++) final.Tick(.05f);
            Assert(final.Phase==GamePhase.Night && final.Remaining==0 && final.Enemies.Count>0,"Final night requires cleanup");
            foreach(var e in final.Enemies) e.health=0; final.Tick(.05f);
            Assert(final.Phase==GamePhase.Victory,"Final cleanup victory"); passed.Add("PASS final night timer cannot bypass cleanup");
            var retreatSnapshot=new GameSession(finalRules).Snapshot();
            var retreat=GameSession.Restore(finalRules,retreatSnapshot); retreat.StartNight();
            for(int i=0;i<810;i++) retreat.Tick(.05f);
            Assert(retreat.Day==2 && retreat.Wallet.Credits==240,"Retreat cannot award kill money"); passed.Add("PASS dawn retreat has no kill rewards");

            string folder="IntegrationEvidence/save-check-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(folder);
            var save=new SaveService(folder+"/profile.json"); var gsave=new GameSession(r);
            gsave.Construction.TryBuild("gun",new Cell(7,9),out error);
            Assert(save.Save(gsave,false),save.Warning); var saved=save.Load(r);
            Assert(saved.resume.credits==0 && saved.resume.buildings.Length==4,"Day purchases survive restore");
            gsave.StartNight(); save.Save(gsave,false); saved=save.Load(r);
            Assert(saved.resume.credits==120 && saved.resume.buildings.Length==3,"Night restore uses day start");
            save.BeginNewGame(); Assert(Directory.GetFiles(folder,"*.old-*").Length>0,"New game archives profile");
            passed.Add("PASS SaveService day restore, night checkpoint and new-game archive");
            var set=AssetDatabase.LoadAssetAtPath<BlackwoodAssetSet>(BlackwoodUserAssetsBuilder.SetPath);
            Assert(set!=null && set.zombie!=null,"User asset map missing");
            File.WriteAllLines("IntegrationEvidence/unity-integration-checks.txt",passed);
            Debug.Log("BLACKWOOD INTEGRATION CHECKS PASSED");
        }
        public static EnemyState Spawn(GameSession game,string id,Cell cell)
        {
            typeof(GameSession).GetMethod("SpawnEnemy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{id,cell});
            return game.Enemies[game.Enemies.Count-1];
        }
    }
}

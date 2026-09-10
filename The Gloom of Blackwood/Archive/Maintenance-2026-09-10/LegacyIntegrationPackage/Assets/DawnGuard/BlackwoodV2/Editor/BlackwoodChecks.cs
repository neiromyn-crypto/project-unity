using System;
using DawnGuard.Core;
using UnityEditor;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodChecks
    {
        [MenuItem("Dawn Guard/4 - Check Blackwood economy v2")]
        public static void Run()
        {
            int passed=0; var r=BlackwoodBalance.Create(); string error;
            var walls=new GameSession(r);
            Check(walls.Buildings.Count==3 && walls.Wallet.Credits==120 && walls.PowerUsed==1 && walls.PowerCapacity==5,"Initial choice and power",ref passed);
            walls.Tick(.25f); Check(walls.Remaining==75 && walls.Phase==GamePhase.Day,"First day untimed",ref passed);
            for(int x=6;x<=8;x++) if(!walls.Construction.TryBuild("wall",new Cell(x,9),out error)) throw new Exception(error);
            Check(walls.Wallet.Credits==0 && walls.PowerUsed==1,"Three-wall branch",ref passed);
            var gun=new GameSession(r);
            Check(gun.Construction.TryBuild("gun",new Cell(7,9),out error) && gun.Wallet.Credits==0 && gun.PowerUsed==3,"Gun branch",ref passed);
            gun.StartNight();
            // Remove spawned enemies via their real health field. This checks accounting and dawn,
            // not combat difficulty or a particular player strategy.
            for(int i=0;i<1600 && gun.Phase==GamePhase.Night;i++)
            { foreach(var enemy in gun.Enemies) enemy.health=0; gun.Tick(.05f); }
            Check(gun.Day==2 && gun.Phase==GamePhase.Day && gun.Wallet.Credits==136,"Dawn income plus eight actual kill rewards",ref passed);
            gun.Wallet.Add(1000);
            Check(gun.Construction.TryBuild("lab",new Cell(0,0),out error) && gun.PowerUsed==5,"Lab uses remaining power",ref passed);
            int balance=gun.Wallet.Credits;
            Check(!gun.Construction.TryBuild("gun",new Cell(0,3),out error) && gun.Wallet.Credits==balance,"Rejected build cannot charge wallet",ref passed);
            int generator=0; foreach(var b in gun.Buildings) if(b.definitionId=="generator") generator=b.instanceId;
            Check(gun.Construction.TryUpgrade(generator,out error) && gun.PowerCapacity==10,"Generator upgrade unlocks capacity",ref passed);
            Check(gun.Construction.TryResearch(out error) && gun.Tech==1,"Research unlock",ref passed);
            var restored=GameSession.Restore(r,JsonUtility.FromJson<DaySnapshot>(JsonUtility.ToJson(gun.Snapshot())));
            Check(restored.Wallet.Credits==gun.Wallet.Credits && restored.PowerCapacity==10 && restored.Tech==1,"Snapshot round trip",ref passed);
            Check(r.waves.Length==8 && r.Building("camp").dailyIncome==45 && r.Building("camp").cost==180,"Campaign and camp payback parameters",ref passed);
            Debug.Log("BLACKWOOD V2: "+passed+" / 11 economy checks passed. Combat balance, UI and device performance require Play mode.");
        }
        private static void Check(bool value,string name,ref int passed)
        { if(!value) throw new InvalidOperationException("Blackwood check failed: "+name); passed++; }
    }
}

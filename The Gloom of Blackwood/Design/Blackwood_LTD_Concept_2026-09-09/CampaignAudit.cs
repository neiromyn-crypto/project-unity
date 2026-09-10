using System;
using System.Text;
using DawnGuard.BlackwoodLTD;
public static class CoastCampaignAudit
{
    static void DayWait(CoastSession g,float seconds){for(int i=0;i<(int)(seconds*20)&&g.S.phase==CoastPhase.Day;i++)g.Tick(.05f);}
    static void Fight(CoastSession g){g.StartNight();int n=0;while(g.S.phase==CoastPhase.Night&&n++<10000)g.Tick(.05f);}
    static float Score(CoastSession g){Fight(g);return g.S.campHP*20+g.S.killed*100-g.S.elapsed*.1f;}
    static void BestTower(CoastSession g,string kind,StringBuilder log)
    {
        float best=-float.MaxValue;int bx=-1,bz=0;string why;
        for(int z=9;z<=18;z++)for(int x=5;x<23;x++)if(g.CanPlace(kind,x,z,out why))
        {var test=new CoastSession(g.Rules,g.S);if(!test.Build(kind,x,z))continue;float score=Score(test);if(score>best){best=score;bx=x;bz=z;}}
        if(bx>=0){g.Build(kind,bx,bz);log.AppendLine("  build "+kind+" "+bx+","+bz);}else log.AppendLine("  cannot build "+kind);
    }
    public static string Run()
    {
        var log=new StringBuilder();var g=new CoastSession(CoastRules.Create());
        for(int day=1;day<=8;day++)
        {
            if(g.S.phase==CoastPhase.Debrief)g.ContinueDay();
            log.AppendLine("DAY "+day+" credits="+g.S.credits+" stone="+g.S.stone);
            if(day==1){g.Hire();DayWait(g,25);g.Build("gun",14,17);CoastChecks.NorthernMaze(g);}
            if(day==2){BestTower(g,"gun",log);g.Hire();}
            if(day==3){g.ActivateArmory();g.UpgradeWeapon(0);DayWait(g,16);BestTower(g,"gun",log);}
            if(day==4){g.ActivateLab();g.Research(1);DayWait(g,21);BestTower(g,"arcane",log);}
            if(day==5){if(g.PowerUsed+2>g.Capacity)g.UpgradePower();BestTower(g,"arcane",log);}
            if(day==6){g.Research(4);DayWait(g,21);if(g.PowerUsed+3>g.Capacity)g.UpgradePower();BestTower(g,"tesla",log);g.UpgradeWeapon(0);DayWait(g,16);}
            if(day==7){if(g.PowerUsed+2>g.Capacity)g.UpgradePower();BestTower(g,"arcane",log);}
            if(day==8){if(g.PowerUsed+3>g.Capacity)g.UpgradePower();BestTower(g,"tesla",log);g.UpgradeWeapon(1);}
            if(g.S.campHP<600)g.Repair(0);
            DayWait(g,g.S.remaining+.1f);if(g.S.phase==CoastPhase.Day)g.StartNight();int ticks=0;while(g.S.phase==CoastPhase.Night&&ticks++<10000)g.Tick(.05f);
            log.AppendLine("  "+g.S.phase+" hp="+g.S.campHP.ToString("F0")+" killed="+g.S.killed+" remaining="+g.S.enemies.Count+" power="+g.PowerUsed+"/"+g.Capacity+" credits="+g.S.credits);
            if(g.S.phase==CoastPhase.Defeat)break;
        }
        return log.ToString();
    }
}


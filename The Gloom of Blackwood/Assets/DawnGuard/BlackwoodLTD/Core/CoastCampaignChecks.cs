using System;
using System.Text;
namespace DawnGuard.BlackwoodLTD
{
    public static class CoastCampaignChecks
    {
        static void Must(bool ok,CoastSession g){if(!ok)throw new Exception("Campaign action failed: "+g.LastError);}
        static void Wait(CoastSession g,float seconds){for(int i=0;i<(int)(seconds*20)&&g.S.phase==CoastPhase.Day;i++)g.Tick(.05f);}
        public static string Run(bool finalUpgrade=true)
        {
            var log=new StringBuilder();var g=new CoastSession(CoastRules.Create());
            for(int day=1;day<=8;day++)
            {
                if(day>1)g.ContinueDay();
                if(day==1){Must(g.Hire(),g);Wait(g,25);Must(g.Build("gun",14,17),g);CoastChecks.NorthernMaze(g);}
                if(day==2){Must(g.Build("gun",10,12),g);Must(g.Hire(),g);}
                if(day==3){Must(g.ActivateArmory(),g);Must(g.UpgradeWeapon(0),g);Wait(g,16);Must(g.Build("gun",22,13),g);}
                if(day==4){Must(g.ActivateLab(),g);Must(g.Research(1),g);Wait(g,21);}
                if(day==5)Must(g.Build("arcane",15,17),g);
                if(day==6){Must(g.Research(4),g);Wait(g,21);Must(g.UpgradePower(),g);Must(g.Build("tesla",17,9),g);if(finalUpgrade){Must(g.UpgradeWeapon(0),g);Wait(g,16);}}
                if(day==7){Must(g.UpgradePower(),g);Must(g.Build("arcane",13,18),g);}
                if(day==8){Must(g.Build("tesla",12,9),g);Must(g.UpgradeWeapon(1),g);}
                if(g.S.campHP<600)Must(g.Repair(0),g);
                Wait(g,g.S.remaining+.1f);int ticks=0;
                while(g.S.phase==CoastPhase.Night&&ticks++<10000)g.Tick(.05f);
                log.AppendLine("Night "+day+": "+g.S.phase+"; HP="+g.S.campHP.ToString("F0")+"; kills="+g.S.killed+"; credits="+g.S.credits+"; stone="+g.S.stone+"; workers="+g.S.workers.Count);
                if(g.S.phase==CoastPhase.Defeat)break;
            }
            if(finalUpgrade&&g.S.phase!=CoastPhase.Victory)throw new Exception("Reference strategy cannot finish campaign\n"+log);
            return log.ToString();
        }
    }
}

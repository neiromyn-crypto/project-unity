using DawnGuard.Core;

namespace DawnGuard.BlackwoodV2
{
    // Independent preset. Never mutates DemoRules or a user's existing catalog.
    public static class BlackwoodBalance
    {
        public static GameRules Create()
        {
            var r=DemoRules.Create();
            r.startingCredits=120; r.daySeconds=75; r.dawnReward=75;
            var camp=r.Building("camp"); camp.cost=180; camp.dailyIncome=45;
            camp.upgradeCost=120; camp.powerUse=1; camp.maxLevel=2;
            var generator=r.Building("generator"); generator.powerSupply=5; generator.upgradeCost=100;
            var gun=r.Building("gun"); gun.powerUse=2; gun.upgradeCost=70;
            r.Building("wall").upgradeCost=40;
            r.Enemy("walker").reward=2; r.Enemy("runner").reward=3; r.Enemy("brute").reward=15;
            r.waves=new[] {
                W("Северный проход",40,G("walker",8,.4f,1.8f)),
                W("Бегуны в лесу",50,G("walker",10,1,2.4f),G("runner",4,16,2.8f)),
                W("Тяжёлые шаги",60,G("walker",12,1,2.2f),G("brute",1,20,1)),
                W("Двойной натиск",65,G("walker",14,1,2),G("runner",6,16,2.5f)),
                W("Лес проснулся",70,G("walker",16,1,2),G("runner",8,20,2),G("brute",1,35,1)),
                W("Проверка обороны",75,G("walker",18,1,2),G("runner",10,18,2),G("brute",2,35,8)),
                W("Перед рассветом",80,G("walker",20,1,2),G("runner",12,18,2),G("brute",3,38,8)),
                W("Последний рубеж",90,G("walker",24,1,2),G("runner",12,20,2),G("brute",4,38,8))
            };
            r.Validate(); return r;
        }
        private static WaveDefinition W(string title,float duration,params SpawnGroup[] groups)
        { return new WaveDefinition {title=title,duration=duration,groups=groups}; }
        private static SpawnGroup G(string id,int count,float start,float interval)
        { return new SpawnGroup {enemyId=id,count=count,startAt=start,interval=interval}; }
    }
}

using System;
using System.Collections.Generic;
using DawnGuard.Core;

namespace DawnGuard.BlackwoodLTD
{
    public enum CoastPhase { Day, Night, Debrief, Victory, Defeat }
    public enum WorkerJob { Outbound, Mining, Inbound, Unloading, Sheltered }
    public enum TargetOrder { First, Nearest, Strongest }

    [Serializable] public sealed class DefenseSpec
    {
        public string id, title, description;
        public int credits, stone, power, unlock;
        public float hp=180, damage, cooldown=1, range=4.5f;
    }
    [Serializable] public sealed class EnemySpec
    {
        public string id, title;
        public float hp, speed, dps, physical=1;
        public int reward;
    }
    [Serializable] public sealed class CoastSpawn
    {
        public string kind;
        public int front, count;
        public float start, interval;
    }
    [Serializable] public sealed class CoastWave
    {
        public string title, advice;
        public CoastSpawn[] groups;
    }
    [Serializable] public sealed class CoastRules
    {
        public int version=1, width=28, depth=24, startingCredits=220, startingStone=100, dawnCredits=100;
        public float firstDay=120, daySeconds=90, campHP=600;
        public DefenseSpec[] defenses;
        public EnemySpec[] enemies;
        public CoastWave[] waves;
        public DefenseSpec Defense(string id) { foreach(var d in defenses) if(d.id==id) return d; throw new ArgumentException("Unknown defense: "+id); }
        public EnemySpec Enemy(string id) { foreach(var d in enemies) if(d.id==id) return d; throw new ArgumentException("Unknown enemy: "+id); }
        static CoastSpawn G(string id,int side,int count,float time,float interval=1.3f)
        { return new CoastSpawn {kind=id,front=side,count=count,start=time,interval=interval}; }
        static CoastWave W(string title,string advice,params CoastSpawn[] groups)
        { return new CoastWave {title=title,advice=advice,groups=groups}; }
        public static CoastRules Create()
        {
            return new CoastRules {
                defenses=new[] {
                    new DefenseSpec {id="wall",title="БАРЬЕР",description="Направляет поток. Проход должен оставаться открытым.",stone=12,hp=180},
                    new DefenseSpec {id="gun",title="ПУЛЕМЁТ",description="Одиночный физический урон. Поставьте у поворота.",credits=90,stone=20,power=2,damage=10,cooldown=.5f,range=5,hp=160},
                    new DefenseSpec {id="arcane",title="АРКАННЫЙ СТРАЖ",description="Магический удар пробивает броню.",credits=115,stone=30,power=2,unlock=1,damage=36,cooldown=1.8f,hp=170},
                    new DefenseSpec {id="tesla",title="ТЕСЛА",description="Цепь поражает до трёх врагов: 18 / 12 / 8.",credits=130,stone=35,power=3,unlock=4,damage=18,cooldown=1.2f,range=4,hp=160},
                    new DefenseSpec {id="cryo",title="КРИО-СТРАЖ",description="Замедляет группу на 25%. Нужен соседний источник урона.",credits=100,stone=25,power=2,unlock=2,damage=6,cooldown=1.5f,range=4,hp=160}
                },
                enemies=new[] {
                    new EnemySpec {id="walker",title="ЗОМБИ",hp=48,speed=.85f,dps=10,reward=2},
                    new EnemySpec {id="runner",title="БЕГУН",hp=32,speed=1.45f,dps=8,reward=3},
                    new EnemySpec {id="brute",title="ГРОМИЛА",hp=260,speed=.6f,dps=24,reward=12},
                    new EnemySpec {id="sapper",title="САПЁР",hp=160,speed=.8f,dps=10,reward=8},
                    new EnemySpec {id="armored",title="БРОНИРОВАННЫЙ",hp=300,speed=.6f,dps=24,reward=14,physical=.6f},
                    new EnemySpec {id="boss",title="СЕРДЦЕ ТЬМЫ",hp=2400,speed=.45f,dps=40,reward=80,physical=.75f}
                },
                waves=new[] {
                    W("Первый рубеж","Север: соедините скальные опоры барьерами и прикройте поворот.",G("walker",0,14,1)),
                    W("Шорох на западе","Новый вход на западе. Бегуны быстро проходят прямой участок.",G("walker",0,14,1),G("runner",1,8,9,1.8f)),
                    W("Тяжёлые шаги","Громилы идут с востока. Подготовьте сильный одиночный урон.",G("walker",0,18,1),G("runner",2,6,8),G("brute",2,2,20,9)),
                    W("Три направления","Все входы активны. Общий рубеж должен прикрывать боковые потоки.",G("walker",0,12,1),G("walker",1,12,3),G("runner",2,10,12),G("brute",1,2,28,8)),
                    W("Трещина в стене","Сапёры с востока метят барьеры. Сохраните заряд ремонта.",G("walker",0,10,1),G("walker",1,10,3),G("runner",2,10,12),G("brute",0,3,22,6),G("sapper",2,2,28,12)),
                    W("Железная кожа","Броня ослабляет физические атаки. Нужна магическая защита.",G("walker",1,18,1),G("runner",2,16,7),G("brute",0,5,20,5),G("armored",2,3,35,8)),
                    W("Перед бурей","Последняя возможность усилить оборону перед финалом.",G("walker",0,18,1),G("runner",1,18,7),G("brute",2,4,20,5),G("sapper",1,2,32,8),G("armored",0,4,40,8)),
                    W("Последняя ночь","Убейте Сердце тьмы и зачистите все три направления.",G("walker",0,22,1),G("runner",1,20,6),G("brute",2,6,20,5),G("sapper",2,2,32,9),G("armored",1,4,42,9),G("boss",0,1,48))
                }
            };
        }
    }
    [Serializable] public sealed class CoastBuilding
    {
        public int id,x,z,paidCredits,paidStone,boughtDay;
        public string kind;
        public float hp,cooldown,damageDone,activeSeconds,boost;
        public int target;
        public bool powered=true;
        public TargetOrder order;
        public CoastBuilding Copy() {return (CoastBuilding)MemberwiseClone();}
    }
    [Serializable] public sealed class CoastEnemy
    {
        public int id,front,nx,nz,wallTarget;
        public string kind;
        public float x,z,hp,slow,attackTimer,stalled;
        public bool moving,attacking;
        public CoastEnemy Copy() {return (CoastEnemy)MemberwiseClone();}
    }
    [Serializable] public sealed class CoastWorker
    {
        public int id,node,cargo;
        public float x,z,timer;
        public WorkerJob job;
        public CoastWorker Copy() {return (CoastWorker)MemberwiseClone();}
    }
    [Serializable] public sealed class CoastState
    {
        public int schemaVersion=1,rulesVersion=1,nextId=1,day=1,credits,stone,tech,generatorLevel,harvestLevel;
        public bool lab,armory;
        public int[] weaponLevels=new int[4], leaks=new int[3];
        public CoastPhase phase;
        public float remaining,elapsed,campHP,hireRemaining,labRemaining,armoryRemaining;
        public int queuedWorker,queuedTech,queuedWeapon=-1,spawnCursor,delivered,killed,supportCharges=2;
        public float supportCooldown,droneX=14,droneZ=6,repairRemaining;
        public int repairTarget;
        public List<CoastBuilding> buildings=new List<CoastBuilding>();
        public List<CoastEnemy> enemies=new List<CoastEnemy>();
        public List<CoastWorker> workers=new List<CoastWorker>();
        public CoastState Copy()
        {
            var s=(CoastState)MemberwiseClone(); s.weaponLevels=(int[])weaponLevels.Clone();s.leaks=(int[])leaks.Clone();
            s.buildings=buildings.ConvertAll(b=>b.Copy());s.enemies=enemies.ConvertAll(e=>e.Copy());s.workers=workers.ConvertAll(w=>w.Copy());return s;
        }
    }
    public struct CoastEvent
    {
        public string kind;public int source,target,amount;public float x,z,toX,toZ;
    }
}


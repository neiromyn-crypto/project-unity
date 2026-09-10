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
        public string id, title,role;
        public float hp, speed, damage, attackInterval=1, visualScale=.72f, physical=1;
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
        public int version=4, width=44, depth=36, startingCredits=220, startingStone=100, dawnCredits=100;
        // campHP is retained as the existing serialized integrity field; it now protects the command node.
        public float firstDay=120, daySeconds=90, campHP=300, operatorHP=100;
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
                    new EnemySpec {id="walker",title="БРОДЯГА",role="Basic Walker",hp=48,speed=.85f,damage=10,attackInterval=1.25f,reward=2,visualScale=.82f},
                    new EnemySpec {id="runner",title="КИБЕР-ДЕМОН",role="Fast Enemy",hp=32,speed=1.45f,damage=6,attackInterval=.8f,reward=3,visualScale=.9f},
                    new EnemySpec {id="brute",title="ТОЛСТЯК",role="Heavy / Brute",hp=200,speed=.6f,damage=24,attackInterval=2,reward=12,visualScale=1.15f},
                    new EnemySpec {id="special",title="КОСТЯНАЯ ВЕДЬМА",role="Special Enemy",hp=90,speed=.8f,damage=12,attackInterval=1.5f,reward=8,visualScale=.9f}
                },
                waves=new[] {
                    W("Защитить оператора","8 бродяг с севера. Прикройте путь к командному узлу.",G("walker",0,8,1,2.5f)),
                    W("Быстрый прорыв","Север и запад: 8 бродяг и 4 кибер-демона.",G("walker",0,8,1,2.5f),G("runner",1,4,12,3)),
                    W("Тяжёлый контакт","8 бродяг, 4 кибер-демона и 1 толстяк с востока.",G("walker",0,8,1,2.5f),G("runner",1,4,12,3),G("brute",2,1,24))
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
        public int attackSlot=-1,ringWaypoint;public bool coreApproach;
        public int id,front,nx,nz,wallTarget;
        public string kind;
        public float x,z,hp,slow,attackTimer,stalled;
        public bool moving,attacking,breached;
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
        public float remaining,elapsed,campHP,operatorHP,hireRemaining,labRemaining,armoryRemaining;
        public int queuedWorker,queuedTech,queuedWeapon=-1,spawnCursor,delivered,killed,supportCharges=2;
        public float supportCooldown,droneX=14,droneZ=6,repairRemaining;
        public float droneTargetX,droneTargetZ;public bool droneMoving;
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


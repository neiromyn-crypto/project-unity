using System;
using System.Collections.Generic;
using DawnGuard.Core;

namespace DawnGuard.BlackwoodLTD
{
    public sealed partial class CoastSession
    {
        public readonly CoastRules Rules;
        public readonly CoastMap Map;
        public CoastState S {get;private set;}
        public event Action<CoastEvent> Event;
        struct SpawnEntry {public float time;public int front,order;public string kind;}
        readonly List<SpawnEntry> schedule=new List<SpawnEntry>();
        public string LastError {get;private set;}
        public int Capacity {get{return 8+S.generatorLevel*4;}}
        public int PowerUsed {get{int p=0;foreach(var b in S.buildings)if(b.powered)p+=Rules.Defense(b.kind).power;return p;}}
        public int Unspawned {get{return Math.Max(0,schedule.Count-S.spawnCursor);}}
        public int ThreatCount {get{return S.enemies.Count+Unspawned;}}
        public int WorkerPrice {get{return S.workers.Count<4?60:90;}}
        public CoastWave Wave {get{return Rules.waves[S.day-1];}}
        public CoastSession(CoastRules rules,CoastState saved=null)
        {
            Rules=rules;Map=new CoastMap(rules.width,rules.depth);
            S=saved==null?new CoastState {rulesVersion=rules.version,droneX=Map.CampX,credits=rules.startingCredits,stone=rules.startingStone,campHP=rules.campHP,operatorHP=rules.operatorHP,remaining=rules.firstDay}:saved.Copy();
            if(saved==null){AddWorker();AddWorker();}else ValidateSave();
            Map.Rebuild(S.buildings);int blocked;if(!Map.AllOpen(out blocked))throw new ArgumentException("Saved map blocks a front");
            BuildSchedule();if(S.spawnCursor<0||S.spawnCursor>schedule.Count)throw new ArgumentException("Invalid saved wave cursor");RefreshPower();
        }
        void ValidateSave()
        {
            if(S.schemaVersion!=1||S.rulesVersion!=Rules.version||S.day<1||S.day>Rules.waves.Length||S.credits<0||S.stone<0||S.workers.Count>6||S.workers.Count<2||
                !Finite(S.campHP)||S.campHP<0||S.campHP>Rules.campHP||!Finite(S.operatorHP)||S.operatorHP<0||S.operatorHP>Rules.operatorHP||!Finite(S.remaining)||S.remaining<0||S.remaining>Rules.firstDay||!Finite(S.elapsed)||S.elapsed<0||
                S.tech<0||S.tech>7||S.generatorLevel<0||S.generatorLevel>2||S.harvestLevel<0||S.harvestLevel>1||S.weaponLevels==null||S.weaponLevels.Length!=4||S.leaks==null||S.leaks.Length!=3||
                S.queuedWeapon < -1||S.queuedWeapon>3||S.supportCharges<0||S.supportCharges>2||!Enum.IsDefined(typeof(CoastPhase),S.phase))throw new ArgumentException("Invalid coast save");
            if(S.nextId<1||S.queuedWorker<0||S.queuedWorker>1||(S.queuedTech!=0&&S.queuedTech!=1&&S.queuedTech!=2&&S.queuedTech!=4)||
                !Finite(S.hireRemaining)||S.hireRemaining<0||S.hireRemaining>20||!Finite(S.labRemaining)||S.labRemaining<0||S.labRemaining>20||!Finite(S.armoryRemaining)||S.armoryRemaining<0||S.armoryRemaining>15||
                !Finite(S.supportCooldown)||S.supportCooldown<0||!Finite(S.droneX)||!Finite(S.droneZ)||!Finite(S.repairRemaining)||S.repairRemaining<0||S.repairRemaining>4)throw new ArgumentException("Invalid saved timers");
            if((S.operatorHP<=0)!=(S.phase==CoastPhase.Defeat))throw new ArgumentException("Operator HP and defeat state disagree");
            var ids=new HashSet<int>();var occupied=new HashSet<Cell>();
            foreach(var b in S.buildings)
            {
                var d=Rules.Defense(b.kind);if(!ids.Add(b.id)||!occupied.Add(new Cell(b.x,b.z))||b.id<1||b.id>=S.nextId||!Map.CanBuildCell(b.x,b.z)||!Finite(b.hp)||b.hp<=0||b.hp>d.hp||b.paidCredits<0||b.paidStone<0||!Finite(b.cooldown)||!Finite(b.boost)||!Enum.IsDefined(typeof(TargetOrder),b.order))throw new ArgumentException("Invalid saved building");
                Map.Occupancy[b.x,b.z]=b.id;
            }
            foreach(var w in S.workers)if(!ids.Add(w.id)||w.id<1||w.id>=S.nextId||w.node<0||w.node>1||w.cargo<0||w.cargo>10||!Finite(w.x)||w.x<0||w.x>28||!Finite(w.z)||w.z<0||w.z>7||!Finite(w.timer)||w.timer<0||!Enum.IsDefined(typeof(WorkerJob),w.job))throw new ArgumentException("Invalid worker");
            foreach(var e in S.enemies){var d=Rules.Enemy(e.kind);if(!ids.Add(e.id)||e.front<0||e.front>2||!Finite(e.hp)||e.hp<=0||e.hp>d.hp||!Finite(e.x)||!Finite(e.z)||!Map.InBounds((int)e.x,(int)e.z))throw new ArgumentException("Invalid enemy");}
            foreach(var level in S.weaponLevels)if(level<0||level>2)throw new ArgumentException("Invalid weapon level");
        }
        static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
        bool Fail(string message){LastError=message;return false;}
        bool Day(){return S.phase==CoastPhase.Day||Fail("Доступно только днём");}
        bool Spend(int credits,int stone)
        {
            if(S.credits<credits||S.stone<stone)return Fail("Нужно: "+credits+" КР. + "+stone+" КАМ.");
            S.credits-=credits;S.stone-=stone;return true;
        }
        public bool CanPlace(string kind,int x,int z,out string reason,int moving=0)
        {
            reason="";var d=Rules.Defense(kind);int blocked;
            if(S.phase!=CoastPhase.Day)reason="Строительство доступно днём";
            else if(!Map.TryPlacement(S.buildings,x,z,moving,out blocked))reason=blocked>=0?"Перекрыт путь: "+FrontName(blocked):"Занято или вне зоны строительства";
            else if(moving==0 && (S.tech&d.unlock)!=d.unlock)reason="Нужно исследование в лаборатории";
            else if(moving==0 && PowerUsed+d.power>Capacity)reason="Не хватает энергии";
            else if(moving==0 && (S.credits<d.credits||S.stone<d.stone))reason="Недостаточно ресурсов";
            return reason.Length==0;
        }
        public bool Build(string kind,int x,int z)
        {
            string reason;if(!CanPlace(kind,x,z,out reason))return Fail(reason);
            var d=Rules.Defense(kind);if(!Spend(d.credits,d.stone))return false;
            var b=new CoastBuilding{id=S.nextId++,kind=kind,x=x,z=z,hp=d.hp,paidCredits=d.credits,paidStone=d.stone,boughtDay=S.day};S.buildings.Add(b);
            Map.Rebuild(S.buildings);RefreshPower();Emit("build",b.id,0,x+.5f,z+.5f);LastError="";return true;
        }
        public CoastBuilding Building(int id){return S.buildings.Find(b=>b.id==id);}
        public bool Move(int id,int x,int z)
        {
            var b=Building(id);if(b==null)return Fail("Выберите защиту");
            if(b.x==x&&b.z==z)return false;
            string reason;if(!CanPlace(b.kind,x,z,out reason,id))return Fail(reason);
            int fee=b.boughtDay==S.day&&b.hp>=Rules.Defense(b.kind).hp?0:Math.Max(2,(int)Math.Ceiling(Rules.Defense(b.kind).stone*.2));
            if(!Spend(0,fee))return false;b.x=x;b.z=z;Map.Rebuild(S.buildings);Emit("build",b.id,0,x+.5f,z+.5f);return true;
        }
        public bool Sell(int id)
        {
            if(!Day())return false;var b=Building(id);if(b==null)return Fail("Выберите защиту");
            bool fresh=b.boughtDay==S.day&&b.hp>=Rules.Defense(b.kind).hp;
            S.credits+=fresh?b.paidCredits:b.paidCredits/2;S.stone+=fresh?b.paidStone:b.paidStone/2;
            S.buildings.Remove(b);Map.Rebuild(S.buildings);RefreshPower();return true;
        }
        public bool Repair(int id)
        {
            if(!Day())return false;var b=Building(id);float missing=b==null?Rules.campHP-S.campHP:Rules.Defense(b.kind).hp-b.hp;
            if(missing<=0)return Fail("Ремонт не нужен");if(!Spend(0,(int)Math.Ceiling(missing/8)))return false;
            if(b==null)S.campHP=Rules.campHP;else b.hp=Rules.Defense(b.kind).hp;return true;
        }
        public bool Hire()
        {
            if(!Day())return false;if(S.queuedWorker!=0)return Fail("Обучение уже идёт");if(S.workers.Count>=6)return Fail("Все 6 мест заняты");
            if(!Spend(WorkerPrice,20))return false;S.queuedWorker=1;S.hireRemaining=20;return true;
        }
        void AddWorker()
        {
            int left=S.workers.FindAll(w=>w.node==0).Count;int right=S.workers.Count-left;
            S.workers.Add(new CoastWorker{id=S.nextId++,node=left<=right?0:1,x=14,z=2,job=WorkerJob.Outbound});
        }
        public bool UpgradeHarvest(){if(!Day())return false;if(S.harvestLevel>0)return Fail("Добыча уже улучшена");if(!Spend(100,50))return false;S.harvestLevel=1;return true;}
        public bool UpgradePower(){if(!Day())return false;if(S.generatorLevel>=2)return Fail("Максимальная мощность");if(!Spend(80,60))return false;S.generatorLevel++;RefreshPower();return true;}
        public bool ActivateLab(){if(!Day())return false;if(S.day<2)return Fail("Лаборатория доступна со дня 2");if(S.lab)return Fail("Лаборатория уже работает");if(!Spend(60,30))return false;S.lab=true;return true;}
        public bool ActivateArmory(){if(!Day())return false;if(S.day<2)return Fail("Оружейная доступна со дня 2");if(S.armory)return Fail("Оружейная уже работает");if(!Spend(50,25))return false;S.armory=true;return true;}
        public bool Research(int flag)
        {
            if(flag!=1&&flag!=2&&flag!=4)return Fail("Неизвестная технология");if(!Day())return false;if(!S.lab)return Fail("Сначала включите лабораторию");
            if((S.tech&flag)!=0)return Fail("Технология открыта");if(S.queuedTech!=0)return Fail("Исследование уже идёт");if(flag==4&&S.day<3)return Fail("Тесла доступна со дня 3");
            if(!Spend(flag==4?50:40,flag==4?30:20))return false;S.queuedTech=flag;S.labRemaining=20;return true;
        }
        public static int WeaponIndex(string kind){return kind=="gun"?0:kind=="arcane"?1:kind=="tesla"?2:kind=="cryo"?3:-1;}
        public bool UpgradeWeapon(int index)
        {
            if(!Day())return false;if(!S.armory)return Fail("Сначала включите оружейную");if(index<0||index>3)return false;
            if(S.queuedWeapon>=0)return Fail("Улучшение уже идёт");int level=S.weaponLevels[index];if(level>=2)return Fail("Максимальный уровень");
            if(!Spend(level==0?60:110,level==0?30:60))return false;S.queuedWeapon=index;S.armoryRemaining=15;return true;
        }
        public void Prioritize(int id){var b=Building(id);if(b!=null)b.order=(TargetOrder)(((int)b.order+1)%3);}
        public void RefreshPower(){int left=Capacity;foreach(var b in S.buildings){int need=Rules.Defense(b.kind).power;b.powered=need<=left;if(b.powered)left-=need;}}
        public void ContinueDay()
        {
            if(S.phase!=CoastPhase.Debrief)return;S.phase=CoastPhase.Day;S.remaining=Rules.daySeconds;S.delivered=0;S.killed=0;S.leaks=new int[3];
            foreach(var w in S.workers)if(w.job==WorkerJob.Sheltered)w.job=WorkerJob.Outbound;
        }
        public bool StartNight()
        {
            if(!Day())return false;int blocked;if(!Map.AllOpen(out blocked))return Fail("Перекрыт путь");
            S.phase=CoastPhase.Night;S.elapsed=0;S.spawnCursor=0;S.supportCharges=2;S.supportCooldown=0;S.repairRemaining=0;BuildSchedule();
            foreach(var b in S.buildings){b.cooldown=0;b.damageDone=0;b.activeSeconds=0;}
            foreach(var w in S.workers)if(w.job!=WorkerJob.Sheltered&&w.job!=WorkerJob.Unloading)w.job=WorkerJob.Inbound;
            Emit("night",0,0,14,8);return true;
        }
        void BuildSchedule()
        {
            schedule.Clear();int order=0;foreach(var g in Wave.groups)for(int i=0;i<g.count;i++)schedule.Add(new SpawnEntry{kind=g.kind,front=g.front,time=g.start+i*g.interval,order=order++});
            schedule.Sort((a,b)=>{int c=a.time.CompareTo(b.time);return c==0?a.order.CompareTo(b.order):c;});
        }
        public void Tick(float dt)
        {
            if(!Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            if(S.phase!=CoastPhase.Day&&S.phase!=CoastPhase.Night)return;
            TickWorkers(dt);S.supportCooldown=Math.Max(0,S.supportCooldown-dt);
            if(S.phase==CoastPhase.Day)
            {
                if(S.queuedWorker!=0&&(S.hireRemaining-=dt)<=0){S.queuedWorker=0;S.hireRemaining=0;AddWorker();Emit("hire",0,0,5,4);}
                if(S.queuedTech!=0&&(S.labRemaining-=dt)<=0){S.tech|=S.queuedTech;S.queuedTech=0;S.labRemaining=0;Emit("research",0,0,21,4);}
                if(S.queuedWeapon>=0&&(S.armoryRemaining-=dt)<=0){S.weaponLevels[S.queuedWeapon]++;S.queuedWeapon=-1;S.armoryRemaining=0;Emit("research",0,0,9,4);}
                S.remaining=Math.Max(0,S.remaining-dt);if(S.remaining<=0)StartNight();return;
            }
            S.elapsed+=dt;while(S.spawnCursor<schedule.Count&&schedule[S.spawnCursor].time<=S.elapsed)
            {
                var entry=schedule[S.spawnCursor++];var cell=Map.Entrances[entry.front];
                S.enemies.Add(new CoastEnemy{id=S.nextId++,kind=entry.kind,front=entry.front,x=cell.x+1,z=cell.z+1,nx=cell.x,nz=cell.z,hp=Rules.Enemy(entry.kind).hp});
            }
            TickSupport(dt);TickEnemies(dt);if(S.phase!=CoastPhase.Night)return;TickTowers(dt);RemoveDead();
            if(S.spawnCursor==schedule.Count&&S.enemies.Count==0)
            {
                if(S.day==Rules.waves.Length)S.phase=CoastPhase.Victory;
                else{S.credits+=Rules.dawnCredits;S.day++;S.phase=CoastPhase.Debrief;}
                Emit("dawn",0,0,14,5);
            }
        }
        void TickWorkers(float dt)
        {
            bool working=S.phase==CoastPhase.Day&&S.remaining>10;
            foreach(var w in S.workers)
            {
                float rockX=w.node==0?2:26;
                if(!working&&(w.job==WorkerJob.Outbound||w.job==WorkerJob.Mining))w.job=WorkerJob.Inbound;
                if(w.job==WorkerJob.Sheltered){if(working)w.job=WorkerJob.Outbound;else continue;}
                if(w.job==WorkerJob.Outbound){if(MovePoint(ref w.x,ref w.z,rockX,2,3.2f*dt)){w.job=WorkerJob.Mining;w.timer=11;}}
                else if(w.job==WorkerJob.Mining){if((w.timer-=dt)<=0){w.timer=0;w.cargo=8+S.harvestLevel*2;w.job=WorkerJob.Inbound;}}
                else if(w.job==WorkerJob.Inbound){if(MovePoint(ref w.x,ref w.z,14,2,3.2f*dt)){w.job=WorkerJob.Unloading;w.timer=1;}}
                else if(w.job==WorkerJob.Unloading&&(w.timer-=dt)<=0){w.timer=0;if(w.cargo>0){int cargo=w.cargo;w.cargo=0;S.stone+=cargo;S.delivered+=cargo;Emit("delivery",w.id,0,14,2,cargo);}w.job=working?WorkerJob.Outbound:WorkerJob.Sheltered;}
            }
        }
        internal static bool MovePoint(ref float x,ref float z,float tx,float tz,float step)
        {float dx=tx-x,dz=tz-z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<=step){x=tx;z=tz;return true;}if(d>0){x+=dx/d*step;z+=dz/d*step;}return false;}
        public static string FrontName(int front){return front==0?"СЕВЕР":front==1?"ЗАПАД":"ВОСТОК";}
        void Emit(string kind,int source,int target,float x,float z,int amount=0,float tx=0,float tz=0)
        {if(Event!=null)Event(new CoastEvent{kind=kind,source=source,target=target,x=x,z=z,amount=amount,toX=tx,toZ=tz});}
    }
}

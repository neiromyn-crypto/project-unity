using System;
using System.Collections.Generic;

namespace DawnGuard.Core
{
    public sealed class GameSession
    {
        private readonly List<BuildingState> buildings=new List<BuildingState>();
        private readonly List<EnemyState> enemies=new List<EnemyState>();
        private int nextId=1;
        private readonly WaveDirector waves=new WaveDirector();
        public readonly CombatSystem Combat=new CombatSystem();
        public GameRules Rules { get; private set; }
        public GridBoard Board { get; private set; }
        public Wallet Wallet { get; private set; }
        public BuildingService Construction { get; private set; }
        public IReadOnlyList<BuildingState> Buildings { get { return buildings; } }
        public IReadOnlyList<EnemyState> Enemies { get { return enemies; } }
        public GamePhase Phase { get; private set; }
        public int Day { get; private set; }
        public int Tech { get; private set; }
        public int DroneLevel { get; private set; }
        public int PowerCapacity { get; private set; }
        public int PowerUsed { get; private set; }
        public int PowerAvailable { get { return PowerCapacity-PowerUsed; } }
        public float Remaining { get; private set; }
        public float DroneX { get; private set; }
        public float DroneZ { get; private set; }
        public int PriorityEnemyId { get; set; }
        public DaySnapshot DayStart { get; private set; }
        public event Action<ShotEvent> Shot;
        public event Action PhaseChanged;
        public BuildingState Shelter
        {
            get { foreach(var b in buildings) if(Rules.Building(b.definitionId).role==BuildingRole.Shelter) return b; return null; }
        }

        public GameSession(GameRules rules) : this(rules,true) { }
        private GameSession(GameRules rules,bool start)
        {
            rules.Validate(); Rules=rules;
            Board=new GridBoard(rules.width,rules.depth,rules.blockedCells);
            Wallet=new Wallet(rules.startingCredits);
            Construction=new BuildingService(this);
            Day=1; DroneLevel=1; Phase=GamePhase.Day; Remaining=rules.daySeconds;
            DroneX=7.5f; DroneZ=8;
            if(start)
            {
                AddBuilding(rules.Building("shelter"),new Cell(7,5),true,0);
                AddBuilding(rules.Building("camp"),new Cell(3,4),true,0);
                AddBuilding(rules.Building("generator"),new Cell(11,4),true,0);
                RefreshPower(); DayStart=Snapshot();
            }
        }

        public void Tick(float dt,float moveX=0,float moveZ=0)
        {
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<0 || dt>0.25f)
                throw new ArgumentOutOfRangeException("dt","Use fixed steps <= 0.25 seconds.");
            if(Phase==GamePhase.Day)
            {
                // First day is untimed so a new player can understand both options.
                if(Day>1) { Remaining=Math.Max(0,Remaining-dt); if(Remaining<=0) StartNight(); }
                return;
            }
            if(Phase!=GamePhase.Night) return;
            if(float.IsNaN(moveX) || float.IsInfinity(moveX)) moveX=0;
            if(float.IsNaN(moveZ) || float.IsInfinity(moveZ)) moveZ=0;
            float length=(float)Math.Sqrt(moveX*moveX+moveZ*moveZ);
            if(length>1) { moveX/=length; moveZ/=length; }
            DroneX=Math.Max(0.5f,Math.Min(Rules.width-0.5f,DroneX+moveX*Rules.droneSpeed*dt));
            DroneZ=Math.Max(0.5f,Math.Min(Rules.depth-0.5f,DroneZ+moveZ*Rules.droneSpeed*dt));
            waves.Tick(this,dt);
            Remaining=Math.Max(0,Rules.waves[Day-1].duration-waves.Elapsed);
            Combat.Tick(this,dt);
            if(Phase!=GamePhase.Night) return;
            bool cleared=waves.AllSpawned && enemies.Count==0;
            bool dawn=Remaining<=0 && Day<Rules.waves.Length;
            if(cleared || dawn) FinishNight();
        }

        public bool StartNight()
        {
            if(Phase!=GamePhase.Day) return false;
            Phase=GamePhase.Night; Remaining=Rules.waves[Day-1].duration;
            waves.Begin(Rules.waves[Day-1]); Combat.Reset();
            foreach(var b in buildings) b.cooldown=0;
            RefreshPower(); NotifyPhase();
            return true;
        }

        private void FinishNight()
        {
            enemies.Clear(); PriorityEnemyId=0;
            if(Day>=Rules.waves.Length) { Phase=GamePhase.Victory; NotifyPhase(); return; }
            int income=Rules.dawnReward;
            foreach(var b in buildings) income+=Rules.Building(b.definitionId).dailyIncome*b.level;
            Wallet.Add(income);
            Day++; Remaining=Rules.daySeconds; Phase=GamePhase.Day;
            DayStart=Snapshot(); NotifyPhase();
        }

        internal BuildingState AddBuilding(BuildingDefinition def,Cell cell,bool permanent,int paid)
        {
            var b=new BuildingState { instanceId=nextId++,definitionId=def.id,cell=cell,
                health=def.maxHealth,permanent=permanent,paidCredits=paid,purchasedDay=Day };
            Board.Place(b.instanceId,cell,def.width,def.depth); buildings.Add(b); return b;
        }
        public BuildingState FindBuilding(int id)
        { foreach(var b in buildings) if(b.instanceId==id) return b; return null; }
        public float MaxHealth(BuildingState b)
        { return Rules.Building(b.definitionId).maxHealth*(1+0.5f*(b.level-1)); }
        internal void RemoveBuilding(BuildingState b)
        { Board.Remove(b.instanceId); buildings.Remove(b); RefreshPower(); }
        internal void DamageBuilding(BuildingState b,float amount)
        {
            b.health=Math.Max(0,b.health-amount);
            if(b.health>0) return;
            bool shelter=Rules.Building(b.definitionId).role==BuildingRole.Shelter;
            RemoveBuilding(b);
            if(shelter) { Phase=GamePhase.Defeat; NotifyPhase(); }
        }
        internal void SpawnEnemy(string id,Cell cell)
        {
            enemies.Add(new EnemyState { instanceId=nextId++,definitionId=id,cell=cell,
                x=cell.x+0.5f,z=cell.z+0.5f,health=Rules.Enemy(id).health });
        }
        internal void RemoveDeadEnemies()
        {
            for(int i=enemies.Count-1;i>=0;i--) if(enemies[i].health<=0)
            {
                Wallet.Add(Rules.Enemy(enemies[i].definitionId).reward);
                if(PriorityEnemyId==enemies[i].instanceId) PriorityEnemyId=0;
                enemies.RemoveAt(i);
            }
        }
        internal void EmitShot(ShotEvent shot) { if(Shot!=null) Shot(shot); }
        private void NotifyPhase() { if(PhaseChanged!=null) PhaseChanged(); }
        internal void SetTech(int tech) { Tech=tech; }
        internal void SetDroneLevel(int level) { DroneLevel=level; }
        public bool HasPoweredLab()
        { foreach(var b in buildings) if(b.powered && Rules.Building(b.definitionId).role==BuildingRole.Laboratory) return true; return false; }
        public void RefreshPower()
        {
            PowerCapacity=PowerUsed=0;
            foreach(var b in buildings) PowerCapacity+=Rules.Building(b.definitionId).powerSupply*b.level;
            // Stable creation order determines brownout priority. Unpowered buildings remain intact.
            foreach(var b in buildings)
            {
                int demand=Rules.Building(b.definitionId).powerUse;
                b.powered=demand==0 || PowerUsed+demand<=PowerCapacity;
                if(b.powered) PowerUsed+=demand;
            }
        }

        public DaySnapshot Snapshot()
        {
            if(Phase!=GamePhase.Day) throw new InvalidOperationException("Only preparation snapshots are supported.");
            var copy=new BuildingState[buildings.Count];
            for(int i=0;i<copy.Length;i++) copy[i]=buildings[i].Copy();
            return new DaySnapshot { day=Day,credits=Wallet.Credits,tech=Tech,droneLevel=DroneLevel,
                dayRemaining=Remaining,buildings=copy };
        }

        public static GameSession Restore(GameRules rules,DaySnapshot save,DaySnapshot dayStart=null)
        {
            if(save==null || save.schemaVersion!=1 || save.day<1 || save.day>rules.waves.Length ||
                save.credits<0 || save.tech<0 || save.tech>1 || save.droneLevel<1 || save.droneLevel>3 ||
                save.buildings==null || !Finite(save.dayRemaining) || save.dayRemaining<0 || save.dayRemaining>rules.daySeconds)
                throw new ArgumentException("Invalid or unsupported save.");
            var game=new GameSession(rules,false);
            game.Day=save.day; game.Wallet=new Wallet(save.credits); game.Tech=save.tech;
            game.DroneLevel=save.droneLevel; game.Remaining=save.dayRemaining;
            var ids=new HashSet<int>(); int shelterCount=0;
            foreach(var source in save.buildings)
            {
                if(source==null) throw new ArgumentException("Null building.");
                var def=rules.Building(source.definitionId);
                if(source.instanceId<1 || source.instanceId==int.MaxValue || !ids.Add(source.instanceId) ||
                    source.level<1 || source.level>def.maxLevel || !Finite(source.health) || source.health<=0 ||
                    source.health>def.maxHealth*(1+0.5f*(source.level-1)) || source.paidCredits<0 ||
                    source.purchasedDay<1 || source.purchasedDay>save.day)
                    throw new ArgumentException("Invalid saved building.");
                foreach(var spawn in rules.spawnCells)
                    if(spawn.x>=source.cell.x && spawn.x<source.cell.x+def.width &&
                        spawn.z>=source.cell.z && spawn.z<source.cell.z+def.depth)
                        throw new ArgumentException("Building overlaps spawn.");
                var b=source.Copy(); b.cooldown=0;
                game.Board.Place(b.instanceId,b.cell,def.width,def.depth);
                game.buildings.Add(b); game.nextId=Math.Max(game.nextId,b.instanceId+1);
                if(def.role==BuildingRole.Shelter) shelterCount++;
            }
            if(shelterCount!=1) throw new ArgumentException("Save needs exactly one shelter.");
            game.RefreshPower();
            if(dayStart!=null)
            {
                var checkedStart=Restore(rules,dayStart);
                if(checkedStart.Day!=game.Day) throw new ArgumentException("Checkpoint day mismatch.");
                game.DayStart=checkedStart.Snapshot();
            }
            else game.DayStart=game.Snapshot();
            return game;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}

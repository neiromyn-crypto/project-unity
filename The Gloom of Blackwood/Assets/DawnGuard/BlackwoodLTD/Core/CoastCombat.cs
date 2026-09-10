using System;
using System.Collections.Generic;
using DawnGuard.Core;

namespace DawnGuard.BlackwoodLTD
{
    public sealed partial class CoastSession
    {
        readonly Dictionary<int,int[,]> sapperFields=new Dictionary<int,int[,]>();
        readonly Dictionary<int,int[,]> breachFields=new Dictionary<int,int[,]>();
        int sapperRevision=-1;
        public static float PersonalSpace(string kind){return kind=="boss"?1.9f:kind=="brute"||kind=="armored"?1.55f:1.3f;}
        bool AdvanceEnemy(CoastEnemy e,float step)
        {
            float tx=e.nx+1,tz=e.nz+1,dx=tx-e.x,dz=tz-e.z,length=(float)Math.Sqrt(dx*dx+dz*dz);
            if(length<.00001f)return true;dx/=length;dz/=length;
            foreach(var other in S.enemies)
            {
                if(other.id==e.id||other.hp<=0||other.attacking||other.coreApproach)continue;
                int ours=Map.Distances[e.nx,e.nz],theirs=Map.Distances[other.nx,other.nz];
                if(theirs>ours||theirs==ours&&other.id>e.id)continue;
                float ox=other.x-e.x,oz=other.z-e.z,along=ox*dx+oz*dz;if(along<=0)continue;
                float across=ox*dz-oz*dx,gap=(PersonalSpace(e.kind)+PersonalSpace(other.kind))*.5f;
                if(Math.Abs(across)>=gap)continue;
                float clearance=along-(float)Math.Sqrt(Math.Max(0,gap*gap-across*across));step=Math.Min(step,Math.Max(0,clearance));
            }
            return MovePoint(ref e.x,ref e.z,tx,tz,step);
        }
        void TickEnemies(float dt)
        {
            if(sapperRevision!=Map.Revision){sapperFields.Clear();breachFields.Clear();sapperRevision=Map.Revision;}

            foreach(var e in S.enemies)
            {
                if(e.hp<=0)continue;var spec=Rules.Enemy(e.kind);e.slow=Math.Max(0,e.slow-dt);e.attacking=false;
                if(e.coreApproach){TickCoreRing(e,dt);if(S.phase==CoastPhase.Defeat)return;continue;}
                // A clear route always wins. Only a barrier whose removal reconnects this
                // enemy to the command node is eligible when the route is actually blocked.
                if(Map.Distances[e.nx,e.nz]<0&&TickSapper(e,dt))continue;
                e.wallTarget=0;
                if(!e.moving)
                {
                    if(e.nx==Map.Goal.x&&e.nz==Map.Goal.z)
                    {
                        e.coreApproach=true;e.attackSlot=-1;e.ringWaypoint=0;TickCoreRing(e,dt);
                        if(S.phase==CoastPhase.Defeat)return;continue;
                    }
                    Cell next;if(!Map.Next(new Cell(e.nx,e.nz),Map.Distances,out next)){e.stalled+=dt;continue;}
                    e.nx=next.x;e.nz=next.z;e.moving=true;
                }
                float slow=e.slow>0?(e.kind=="boss"?.875f:.75f):1;
                if(AdvanceEnemy(e,spec.speed*slow*dt))e.moving=false;
            }
        }
        void TickCoreRing(CoastEnemy e,float dt)
        {
            var spec=Rules.Enemy(e.kind);
            if(e.attackSlot<0)
            {
                float best=float.MaxValue;
                for(int slot=0;slot<CoastMap.AttackPointCount;slot++)
                {
                    bool occupied=false;foreach(var other in S.enemies)if(other!=e&&other.hp>0&&other.coreApproach&&other.attackSlot==slot){occupied=true;break;}
                    if(occupied)continue;
                    float distance=Distance2(e.x,e.z,Map.AttackX(slot),Map.AttackZ(slot));
                    if(distance<best){best=distance;e.attackSlot=slot;}
                }
                if(e.attackSlot<0)
                {
                    int queue=0;foreach(var other in S.enemies)if(other.hp>0&&other.coreApproach&&other.attackSlot<0&&other.id<e.id)queue++;
                    float x=Map.CampX+(queue%2==0?-1:1)*(5+(queue/2)*1.7f);
                    e.moving=!MovePoint(ref e.x,ref e.z,Math.Max(1,Math.Min(Map.Width-1,x)),8.5f,spec.speed*dt);return;
                }
                e.ringWaypoint=0;
            }
            // Walk around the outer ring in short chords; never cut through the platform.
            float targetX=Map.AttackX(e.ringWaypoint),targetZ=Map.AttackZ(e.ringWaypoint);
            bool reached=MovePoint(ref e.x,ref e.z,targetX,targetZ,spec.speed*dt);e.moving=!reached;
            if(!reached)return;
            if(e.ringWaypoint!=e.attackSlot){int direction=e.attackSlot<=CoastMap.AttackPointCount/2?1:-1;e.ringWaypoint=(e.ringWaypoint+direction+CoastMap.AttackPointCount)%CoastMap.AttackPointCount;e.moving=true;return;}
            e.attacking=true;e.moving=false;
            if(!e.breached){S.leaks[e.front]++;e.breached=true;}
            e.attackTimer-=dt;if(e.attackTimer<=0){DamageCommandNode(spec.damage);e.attackTimer=Math.Max(.1f,spec.attackInterval);}
        }
        public void DamageCommandNode(float amount)
        {
            if(!Finite(amount)||amount<0)throw new ArgumentOutOfRangeException("amount");
            if(S.phase!=CoastPhase.Night||amount==0)return;
            if(S.campHP>0)
            {
                S.campHP=Math.Max(0,S.campHP-amount);Emit("core_hit",0,0,Map.CampX,5.2f);
                // The intact core absorbs this entire hit. The next hit can reach the operator.
                if(S.campHP==0)Emit("core_broken",0,0,Map.CampX,5.2f);
            }
            else
            {
                S.operatorHP=Math.Max(0,S.operatorHP-amount);Emit("operator_hit",0,0,Map.CampX,5.2f);
                if(S.operatorHP==0){S.phase=CoastPhase.Defeat;Emit("defeat",0,0,Map.CampX,5.2f);}
            }
        }
        bool TickSapper(CoastEnemy e,float dt)
        {
            var wall=Building(e.wallTarget);
            if(wall==null||wall.kind!="wall")
            {
                int best=int.MaxValue;wall=null;
                int ax=Math.Max(0,Math.Min(Map.Width-2,(int)Math.Round(e.x-1))),az=Math.Max(7,Math.Min(Map.Depth-2,(int)Math.Round(e.z-1)));
                foreach(var b in S.buildings)if(b.kind=="wall")
                {
                    int[,] reopened;
                    if(!breachFields.TryGetValue(b.id,out reopened))
                    {
                        int old=Map.Occupancy[b.x,b.z];Map.Occupancy[b.x,b.z]=0;
                        try{reopened=Map.Field(new[]{Map.Goal});}finally{Map.Occupancy[b.x,b.z]=old;}
                        breachFields[b.id]=reopened;
                    }
                    if(reopened[ax,az]<0)continue;
                    int[,] f=WallField(b);int score=f[ax,az];if(score>=0&&score<best){best=score;wall=b;}
                }
                if(wall==null){e.wallTarget=0;return false;}e.wallTarget=wall.id;e.moving=false;e.nx=ax;e.nz=az;e.attackTimer=0;
            }
            var field=WallField(wall);
            if(!e.moving&&field[e.nx,e.nz]==0)
            {
                e.attacking=true;if((e.attackTimer-=dt)<=0){var spec=Rules.Enemy(e.kind);wall.hp-=spec.damage;e.attackTimer=Math.Max(.1f,spec.attackInterval);Emit("wall_hit",e.id,wall.id,wall.x+.5f,wall.z+.5f);
                    if(wall.hp<=0){S.buildings.Remove(wall);Map.Rebuild(S.buildings);RefreshPower();sapperFields.Clear();breachFields.Clear();sapperRevision=Map.Revision;e.wallTarget=0;Emit("collapse",wall.id,0,wall.x+.5f,wall.z+.5f);}}
                return true;
            }
            if(!e.moving){Cell next;if(!Map.Next(new Cell(e.nx,e.nz),field,out next)){e.wallTarget=0;return false;}e.nx=next.x;e.nz=next.z;e.moving=true;}
            if(AdvanceEnemy(e,Rules.Enemy(e.kind).speed*(e.slow>0?.75f:1)*dt))e.moving=false;
            return true;
        }
        int[,] WallField(CoastBuilding wall)
        {
            int[,] result;if(sapperFields.TryGetValue(wall.id,out result))return result;
            var targets=new List<Cell>();
            for(int x=wall.x-2;x<=wall.x+1;x++)for(int z=wall.z-2;z<=wall.z+1;z++)
                if(Map.Fits(x,z)&&Distance2(x+1,z+1,wall.x+.5f,wall.z+.5f)<=3)targets.Add(new Cell(x,z));
            result=Map.Field(targets);sapperFields[wall.id]=result;return result;
        }
        static float Distance2(float x,float z,float xx,float zz){float dx=x-xx,dz=z-zz;return dx*dx+dz*dz;}
        void TickTowers(float dt)
        {
            foreach(var b in S.buildings)
            {
                var spec=Rules.Defense(b.kind);if(spec.damage<=0||!b.powered)continue;
                b.boost=Math.Max(0,b.boost-dt);b.cooldown-=dt;if(b.cooldown>0)continue;
                CoastEnemy target=null;float best=float.MaxValue;
                foreach(var e in S.enemies)
                {
                    float distance=Distance2(b.x+.5f,b.z+.5f,e.x,e.z);if(e.hp<=0||distance>spec.range*spec.range)continue;
                    int ax=Math.Max(0,Math.Min(Map.Width-1,e.nx)),az=Math.Max(0,Math.Min(Map.Depth-1,e.nz));
                    float score=b.order==TargetOrder.Nearest?distance:b.order==TargetOrder.Strongest?-e.hp:Map.Distances[ax,az]+distance*.0001f;
                    if(score<best){best=score;target=e;}
                }
                b.target=target==null?0:target.id;if(target==null)continue;
                b.cooldown=spec.cooldown/(b.boost>0?1.25f:1);b.activeSeconds+=spec.cooldown;
                float multiplier=1+.2f*S.weaponLevels[WeaponIndex(b.kind)];
                Hit(b,target,spec.damage*multiplier,b.kind=="gun",b.x+.5f,b.z+.5f);
                if(b.kind=="tesla")
                {
                    var hit=new HashSet<int>{target.id};var previous=target;
                    for(int jump=0;jump<2;jump++)
                    {
                        CoastEnemy chosen=null;float closest=1.75f*1.75f;
                        foreach(var e in S.enemies)if(e.hp>0&&!hit.Contains(e.id)){float d=Distance2(e.x,e.z,previous.x,previous.z);if(d<closest){closest=d;chosen=e;}}
                        if(chosen==null)break;hit.Add(chosen.id);Hit(b,chosen,(jump==0?12:8)*multiplier,false,previous.x,previous.z);previous=chosen;
                    }
                }
                if(b.kind=="cryo")foreach(var e in S.enemies)if(e.hp>0&&Distance2(e.x,e.z,target.x,target.z)<=2.25f)e.slow=2;
            }
        }
        void Hit(CoastBuilding b,CoastEnemy e,float amount,bool physical,float x,float z)
        {
            float damage=amount*(physical?Rules.Enemy(e.kind).physical:1);b.damageDone+=Math.Min(damage,Math.Max(0,e.hp));e.hp-=damage;
            Emit(b.kind,b.id,e.id,x,z,0,e.x,e.z);
        }
        void RemoveDead()
        {
            for(int i=S.enemies.Count-1;i>=0;i--)if(S.enemies[i].hp<=0){var e=S.enemies[i];S.credits+=Rules.Enemy(e.kind).reward;S.killed++;Emit("death",e.id,0,e.x,e.z);S.enemies.RemoveAt(i);}
        }
        public bool Support(int id,bool repair)
        {
            if(S.phase!=CoastPhase.Night)return Fail("Поддержка доступна ночью");if(S.supportCharges<=0||S.supportCooldown>0)return Fail("Нет заряда или идёт перезарядка");
            var b=Building(id);if(b==null)return Fail("Выберите защиту на поле");
            if(repair){if(b.hp>=Rules.Defense(b.kind).hp)return Fail("Ремонт не нужен");if(!Spend(0,10))return false;S.repairTarget=id;S.repairRemaining=4;S.droneMoving=false;}
            else {if(Rules.Defense(b.kind).damage<=0||!b.powered)return Fail("Выберите работающего защитника");b.boost=6;}
            S.supportCharges--;S.supportCooldown=18;Emit("support",id,0,b.x+.5f,b.z+.5f);return true;
        }
        public bool CommandDrone(float x,float z)
        {
            if(!Finite(x)||!Finite(z))return false;
            if(S.phase!=CoastPhase.Day&&S.phase!=CoastPhase.Night)return false;
            if(S.repairRemaining>0)return Fail("Дрон занят ремонтом");
            S.droneTargetX=Math.Max(.8f,Math.Min(Map.Width-.8f,x));S.droneTargetZ=Math.Max(.8f,Math.Min(Map.Depth-.8f,z));S.droneMoving=true;return true;
        }
        void TickDrone(float dt)
        {if(S.droneMoving&&S.repairRemaining<=0&&MovePoint(ref S.droneX,ref S.droneZ,S.droneTargetX,S.droneTargetZ,7*dt))S.droneMoving=false;}
        void TickSupport(float dt)
        {
            if(S.repairRemaining<=0)return;var b=Building(S.repairTarget);if(b==null){S.repairRemaining=0;return;}
            if(MovePoint(ref S.droneX,ref S.droneZ,b.x+.5f,b.z+.5f,7*dt)){b.hp=Math.Min(Rules.Defense(b.kind).hp,b.hp+20*dt);S.repairRemaining=Math.Max(0,S.repairRemaining-dt);}
        }
    }
}

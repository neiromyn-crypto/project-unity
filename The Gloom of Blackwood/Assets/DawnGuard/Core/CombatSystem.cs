using System;

namespace DawnGuard.Core
{
    public sealed class CombatSystem
    {
        private readonly Pathfinder paths=new Pathfinder();
        private float droneCooldown, fieldRefresh;
        public float BoostRemaining { get; private set; }
        public float BoostCooldown { get; private set; }
        public float RocketCooldown { get; private set; }
        public int BoostTarget { get; private set; }

        public void Reset()
        {
            droneCooldown=fieldRefresh=BoostRemaining=BoostCooldown=RocketCooldown=0;
            BoostTarget=0; paths.Invalidate();
        }

        public void Tick(GameSession game,float dt)
        {
            BoostRemaining=Math.Max(0,BoostRemaining-dt);
            BoostCooldown=Math.Max(0,BoostCooldown-dt);
            RocketCooldown=Math.Max(0,RocketCooldown-dt);
            fieldRefresh-=dt;
            if(fieldRefresh<=0) { paths.Invalidate(); fieldRefresh=1f; }
            MoveEnemies(game,dt);
            if(game.Phase!=GamePhase.Night) return;
            droneCooldown-=dt;
            if(droneCooldown<=0)
            {
                var target=FindTarget(game,game.DroneX,game.DroneZ,game.Rules.droneRange,true);
                if(target!=null)
                {
                    float damage=game.Rules.droneDamage*(1+0.3f*(game.DroneLevel-1));
                    if(BoostRemaining>0) damage*=0.5f;
                    Fire(game,target,game.DroneX,game.DroneZ,damage,true,false);
                    droneCooldown=1f/game.Rules.droneShotsPerSecond;
                }
            }
            foreach(var b in game.Buildings)
            {
                var def=game.Rules.Building(b.definitionId);
                if(def.role!=BuildingRole.Turret || !b.powered) continue;
                b.cooldown-=dt;
                if(b.cooldown>0) continue;
                float x=b.cell.x+def.width*0.5f, z=b.cell.z+def.depth*0.5f;
                var target=FindTarget(game,x,z,def.range,false);
                if(target==null) continue;
                float damage=def.damage*(1+0.4f*(b.level-1));
                if(BoostRemaining>0 && BoostTarget==b.instanceId) damage*=2;
                Fire(game,target,x,z,damage,false,def.id=="tesla");
                b.cooldown=1f/def.shotsPerSecond;
            }
            game.RemoveDeadEnemies();
        }

        private void MoveEnemies(GameSession game,float dt)
        {
            foreach(var enemy in game.Enemies)
            {
                if(enemy.health<=0) continue;
                var def=game.Rules.Enemy(enemy.definitionId);
                if(!enemy.travelling)
                {
                    Cell next;
                    if(!paths.TryNext(game,enemy,out next)) continue;
                    var blocking=game.FindBuilding(game.Board.Occupant(next));
                    if(blocking!=null)
                    {
                        game.DamageBuilding(blocking,def.damagePerSecond*dt);
                        if(game.Phase!=GamePhase.Night) return;
                        continue;
                    }
                    enemy.nextCell=next;
                    enemy.travelling=true;
                }
                float tx=enemy.nextCell.x+0.5f, tz=enemy.nextCell.z+0.5f;
                float dx=tx-enemy.x, dz=tz-enemy.z;
                float distance=(float)Math.Sqrt(dx*dx+dz*dz);
                float step=def.speed*dt;
                if(distance<=step)
                {
                    enemy.x=tx; enemy.z=tz;
                    enemy.cell=enemy.nextCell;
                    enemy.travelling=false;
                }
                else { enemy.x+=dx/distance*step; enemy.z+=dz/distance*step; }
            }
        }

        public bool TryBoost(GameSession game,out string error)
        {
            error="";
            if(game.Phase!=GamePhase.Night || BoostCooldown>0)
            { error="Усиление пока недоступно"; return false; }
            BuildingState best=null;
            float distance=3.5f*3.5f;
            foreach(var b in game.Buildings)
            {
                var def=game.Rules.Building(b.definitionId);
                if(def.role!=BuildingRole.Turret || !b.powered) continue;
                float dx=b.cell.x+def.width*0.5f-game.DroneX;
                float dz=b.cell.z+def.depth*0.5f-game.DroneZ;
                float d=dx*dx+dz*dz;
                if(d<=distance) { distance=d; best=b; }
            }
            if(best==null) { error="Подлетите к работающей башне"; return false; }
            BoostTarget=best.instanceId; BoostRemaining=5; BoostCooldown=18;
            return true;
        }

        public bool TryRocket(GameSession game,out string error)
        {
            error="";
            if(game.Phase!=GamePhase.Night || RocketCooldown>0)
            { error="Ракета пока недоступна"; return false; }
            var target=FindTarget(game,game.DroneX,game.DroneZ,game.Rules.droneRange,true);
            if(target==null) { error="Нет цели в радиусе"; return false; }
            float x=target.x,z=target.z;
            foreach(var enemy in game.Enemies)
            {
                float dx=enemy.x-x,dz=enemy.z-z;
                if(enemy.health>0 && dx*dx+dz*dz<=2.25f) enemy.health-=65;
            }
            game.EmitShot(new ShotEvent { fromX=game.DroneX,fromZ=game.DroneZ,toX=x,toZ=z,drone=true });
            RocketCooldown=12;
            game.RemoveDeadEnemies();
            return true;
        }

        private static EnemyState FindTarget(GameSession game,float x,float z,float range,bool priority)
        {
            EnemyState best=null;
            float distance=range*range;
            foreach(var e in game.Enemies)
            {
                if(e.health<=0) continue;
                float dx=e.x-x,dz=e.z-z,d=dx*dx+dz*dz;
                if(d>range*range) continue;
                if(priority && e.instanceId==game.PriorityEnemyId) return e;
                if(d<=distance) { distance=d; best=e; }
            }
            return best;
        }

        private static void Fire(GameSession game,EnemyState target,float x,float z,float damage,bool drone,bool electric)
        {
            target.health-=damage;
            game.EmitShot(new ShotEvent { fromX=x,fromZ=z,toX=target.x,toZ=target.z,drone=drone,electric=electric });
        }
    }
}

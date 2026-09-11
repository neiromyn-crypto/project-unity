using System;

namespace DawnGuard.BlackwoodLTD
{
    [Serializable] public sealed class GrenadeSpec
    {
        public int charges=2;
        public float radius=3.5f,maxDamage=100,minDamage=35,loadSeconds=1.2f,fallSeconds=.75f;
        public float knockDistance=2.2f,knockSeconds=.8f;
    }

    public sealed partial class CoastSession
    {
        public bool GrenadeBusy => S.grenadeMission||S.grenadeState==GrenadeState.LOADING||S.grenadeState==GrenadeState.RELOADING||S.grenadeState==GrenadeState.DROPPED;
        public float EnemyX(CoastEnemy e) => e.x+e.laneX+KnockFraction(e)*e.knockX;
        public float EnemyZ(CoastEnemy e) => e.z+e.laneZ+KnockFraction(e)*e.knockZ;
        float KnockFraction(CoastEnemy e){float t=1-e.knockRemaining/Rules.grenade.knockSeconds;return e.knockRemaining>0?(float)Math.Sin(Math.PI*t):0;}
        bool CrowdFree(float x,float z,float radius)
        {
            for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2){int cx=(int)Math.Floor(x+a*radius),cz=(int)Math.Floor(z+b*radius);if(!Map.InBounds(cx,cz)||Map.Rocks[cx,cz]||Map.Occupancy[cx,cz]!=0)return false;}return true;
        }
        public bool LoadGrenade()
        {
            if(S.phase!=CoastPhase.Night)return Fail("Гранаты доступны ночью");
            if(GrenadeBusy||S.grenadeState==GrenadeState.LOADED||S.grenadeCharges<=0||S.repairRemaining>0)return Fail("Нет свободного заряда или дрон занят");
            S.grenadeState=S.grenadeCharges==Rules.grenade.charges?GrenadeState.LOADING:GrenadeState.RELOADING;
            S.grenadeTimer=Rules.grenade.loadSeconds;S.droneTargetX=Map.DronePadX;S.droneTargetZ=Map.DronePadZ;S.droneMoving=true;return true;
        }
        public bool TargetGrenade(float x,float z)
        {
            if(S.phase!=CoastPhase.Night||S.grenadeState!=GrenadeState.LOADED||GrenadeBusy||S.grenadeCharges<=0||S.repairRemaining>0)return Fail("Сначала загрузите гранату");
            if(!Finite(x)||!Finite(z)||x<.8f||x>Map.Width-.8f||z<.8f||z>Map.Depth-.8f)return Fail("Выберите точку на поле");
            S.grenadeX=x;S.grenadeZ=z;S.droneTargetX=x;S.droneTargetZ=z;S.droneMoving=true;S.grenadeMission=true;return true;
        }
        void TickGrenade(float dt)
        {
            if(S.phase!=CoastPhase.Night)return;
            if(S.grenadeState==GrenadeState.LOADING||S.grenadeState==GrenadeState.RELOADING)
            {
                if(!S.droneMoving&&(S.grenadeTimer=Math.Max(0,S.grenadeTimer-dt))<=0){S.grenadeState=GrenadeState.LOADED;Emit("grenade_loaded",0,0,S.droneX,S.droneZ);}return;
            }
            if(S.grenadeMission&&!S.droneMoving&&S.grenadeState==GrenadeState.LOADED)
            {S.grenadeState=GrenadeState.DROPPED;S.grenadeCharges--;S.grenadeTimer=Rules.grenade.fallSeconds;Emit("grenade_drop",0,0,S.grenadeX,S.grenadeZ);return;}
            if(S.grenadeState!=GrenadeState.DROPPED)return;
            S.grenadeTimer=Math.Max(0,S.grenadeTimer-dt);if(S.grenadeTimer>0)return;
            ExplodeGrenade();S.grenadeState=GrenadeState.AVAILABLE;S.grenadeMission=false;
        }
        void ExplodeGrenade()
        {
            var spec=Rules.grenade;
            foreach(var e in S.enemies)
            {
                if(e.hp<=0)continue;float dx=EnemyX(e)-S.grenadeX,dz=EnemyZ(e)-S.grenadeZ,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d>spec.radius)continue;
                float edge=d/spec.radius;e.hp-=spec.maxDamage+(spec.minDamage-spec.maxDamage)*edge;
                if(e.hp<=0)continue;
                if(d<.001f){double a=e.id*2.399963;dx=(float)Math.Cos(a);dz=(float)Math.Sin(a);d=1;}
                float push=spec.knockDistance*(1-.65f*edge)*Rules.Enemy(e.kind).knockbackMultiplier;
                // Clip the entire outward segment against terrain; return follows the same safe segment.
                float allowed=0;for(int i=1;i<=12;i++){float t=push*i/12;if(!CrowdFree(e.x+e.laneX+dx/d*t,e.z+e.laneZ+dz/d*t,.4f))break;allowed=t;}
                e.knockX=dx/d*allowed;e.knockZ=dz/d*allowed;e.knockRemaining=spec.knockSeconds;e.attacking=false;
            }
            Emit("grenade_explosion",0,0,S.grenadeX,S.grenadeZ);
        }
    }
}

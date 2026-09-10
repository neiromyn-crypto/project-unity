using System;
using System.Collections.Generic;
using DawnGuard.Core;

namespace DawnGuard.BlackwoodLTD
{
    // Nodes represent the southwest corner of a free 2x2 footprint. Four-connected
    // traversal checks corners and makes the preview and combat obey identical clearance.
    public sealed class CoastMap
    {
        public readonly int Width,Depth;
        public readonly Cell[] Entrances;
        public readonly Cell Goal;
        public float CampX {get{return Goal.x+1;}}
        public const int AttackPointCount=10;
        public const float CoreZ=5.2f,CoreWidth=4.4f,AttackRadius=3.1f,AttackRadiusZ=2.8f;
        public float AttackX(int slot){return CampX+(float)Math.Sin(slot*Math.PI*2/AttackPointCount)*AttackRadius;}
        public float AttackZ(int slot){return CoreZ+(float)Math.Cos(slot*Math.PI*2/AttackPointCount)*AttackRadiusZ;}
        public float DronePadX {get{return CampX+5.2f;}}
        public float DronePadZ {get{return 1.2f;}}
        public float CampOffsetX {get{return CampX-14;}}
        public readonly bool[,] Rocks;
        public int[,] Occupancy;
        public int[,] Distances {get;private set;}
        public int Revision {get;private set;}
        public CoastMap(int width,int depth)
        {
            Width=width;Depth=depth;Rocks=new bool[width,depth];Occupancy=new int[width,depth];
            Goal=new Cell(width/2-1,7);
            Entrances=width>=40&&depth>=34?new[]{new Cell(width/2-1,depth-2),new Cell(0,26),new Cell(width-2,21)}:new[]{new Cell(13,22),new Cell(0,14),new Cell(width-2,14)};
            if(width>=40&&depth>=34)
            {
                // Staggered ridges, two or more wide exits per band. Rocks alone define
                // the maze skeleton; all decorative forest remains outside this grid.
                Ridge(0,30,13,31);Ridge(20,30,33,31);
                Ridge(8,23,24,24);Ridge(31,23,width-1,24);
                Ridge(0,16,15,17);Ridge(23,16,35,17);
                Ridge(6,18,7,19);Ridge(6,23,7,27);Ridge(0,21,3,22);
                Ridge(36,26,37,depth-2);Ridge(34,10,35,20);
                Ridge(36,13,39,14);Ridge(42,13,width-1,14);
                Ridge(10,10,16,11);Ridge(28,11,32,12);
                // A short central outcrop creates two approaches to the command apron.
                Ridge(20,10,23,10);
                Ridge(11,27,13,28);Ridge(26,19,28,20);
                Rebuild(new List<CoastBuilding>());return;
            }
            for(int z=14;z<depth;z++) {Rock(9,z);Rock(10,z);Rock(18,z);Rock(19,z);}
            for(int x=0;x<width;x++) {if(x<11 || x>17) {Rock(x,22);Rock(x,23);}}
            Rock(11,20);Rock(12,20);Rock(16,14);Rock(17,14);
            for(int x=0;x<5;x++) Rock(x,10);
            for(int x=23;x<28;x++) Rock(x,10);
            for(int x=3;x<=6;x++) Rock(x,18);
            for(int x=21;x<=24;x++) Rock(x,18);
            Rebuild(new List<CoastBuilding>());
        }
        void Rock(int x,int z) {if(x>=0&&z>=0&&x<Width&&z<Depth) Rocks[x,z]=true;}
        void Ridge(int x0,int z0,int x1,int z1){for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)Rock(x,z);}
        public bool InBounds(int x,int z) {return x>=0&&z>=0&&x<Width&&z<Depth;}
        public bool CanBuildCell(int x,int z,int ignored=0)
        {
            if(!InBounds(x,z)||z<9||z>Depth-3||Rocks[x,z]||Occupancy[x,z]!=0&&Occupancy[x,z]!=ignored) return false;
            foreach(var e in Entrances) if(Math.Abs(x-e.x)<2&&Math.Abs(z-e.z)<2) return false;
            return true;
        }
        bool Free(int x,int z) {return InBounds(x,z)&&z>=7&&!Rocks[x,z]&&Occupancy[x,z]==0;}
        public bool Fits(int x,int z) {return Free(x,z)&&Free(x+1,z)&&Free(x,z+1)&&Free(x+1,z+1);}
        public void Rebuild(List<CoastBuilding> buildings)
        {
            Array.Clear(Occupancy,0,Occupancy.Length);
            foreach(var b in buildings) if(InBounds(b.x,b.z)) Occupancy[b.x,b.z]=b.id;
            Distances=Field(new[]{Goal});Revision++;
        }
        public int[,] Field(IEnumerable<Cell> goals)
        {
            var d=new int[Width,Depth]; for(int x=0;x<Width;x++)for(int z=0;z<Depth;z++)d[x,z]=-1;
            var q=new Queue<Cell>();foreach(var g in goals) if(Fits(g.x,g.z)&&d[g.x,g.z]<0){d[g.x,g.z]=0;q.Enqueue(g);}
            while(q.Count>0)
            {
                var c=q.Dequeue();foreach(var n in Neighbours(c)) if(Fits(n.x,n.z)&&d[n.x,n.z]<0){d[n.x,n.z]=d[c.x,c.z]+1;q.Enqueue(n);}
            }return d;
        }
        public IEnumerable<Cell> Neighbours(Cell c)
        {
            if(c.z>0)yield return new Cell(c.x,c.z-1);if(c.x>0)yield return new Cell(c.x-1,c.z);
            if(c.x+1<Width)yield return new Cell(c.x+1,c.z);if(c.z+1<Depth)yield return new Cell(c.x,c.z+1);
        }
        public bool AllOpen(out int blocked)
        {for(int i=0;i<Entrances.Length;i++){var e=Entrances[i];if(Distances[e.x,e.z]<0){blocked=i;return false;}}blocked=-1;return true;}
        public bool TryPlacement(List<CoastBuilding> buildings,int x,int z,int ignored,out int blocked)
        {
            blocked=-1;if(!CanBuildCell(x,z,ignored))return false;
            int oldX=-1,oldZ=-1;
            if(ignored>0)foreach(var b in buildings)if(b.id==ignored){oldX=b.x;oldZ=b.z;Occupancy[oldX,oldZ]=0;break;}
            Occupancy[x,z]=-1;var old=Distances;Distances=Field(new[]{Goal});bool ok=AllOpen(out blocked);
            Occupancy[x,z]=0;if(oldX>=0)Occupancy[oldX,oldZ]=ignored;Distances=old;return ok;
        }
        public bool Next(Cell current,int[,] field,out Cell next)
        {
            next=current;int best=int.MaxValue;
            foreach(var n in Neighbours(current)) if(Fits(n.x,n.z)&&field[n.x,n.z]>=0&&field[n.x,n.z]<best){best=field[n.x,n.z];next=n;}
            return best<int.MaxValue;
        }
        public List<Cell> Route(int front,int max=300)
        {
            var path=new List<Cell>();Cell c=Entrances[front];if(Distances[c.x,c.z]<0)return path;
            path.Add(c);while(!c.Equals(Goal)&&path.Count<max){Cell n;if(!Next(c,Distances,out n)||n.Equals(c))break;c=n;path.Add(c);}return path;
        }
    }
}

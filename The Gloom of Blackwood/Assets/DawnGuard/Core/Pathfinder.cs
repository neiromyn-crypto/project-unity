using System;
using System.Collections.Generic;

namespace DawnGuard.Core
{
    // Reverse Dijkstra field per enemy type, shared by all enemies of that type.
    // Cost compares walking around an obstacle with time required to destroy it.
    public sealed class Pathfinder
    {
        private sealed class Field
        {
            public int revision = -1;
            public float[,] cost;
        }
        private readonly Dictionary<string,Field> fields=new Dictionary<string,Field>();
        public void Invalidate() { fields.Clear(); }

        public bool TryNext(GameSession game, EnemyState enemy, out Cell next)
        {
            next=enemy.cell;
            var def=game.Rules.Enemy(enemy.definitionId);
            Field field;
            if (!fields.TryGetValue(def.id,out field) || field.revision!=game.Board.Revision)
            {
                field=Build(game,def);
                fields[def.id]=field;
            }
            float best=float.PositiveInfinity;
            foreach (var c in game.Board.Neighbours(enemy.cell))
            {
                float score=EntryCost(game,def,c)+field.cost[c.x,c.z];
                if (score<best) { best=score; next=c; }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static float EntryCost(GameSession game, EnemyDefinition def, Cell cell)
        {
            var b=game.FindBuilding(game.Board.Occupant(cell));
            return 1f/def.speed+(b==null ? 0 : b.health/def.damagePerSecond);
        }

        private static Field Build(GameSession game, EnemyDefinition def)
        {
            int w=game.Board.Width, d=game.Board.Depth;
            var dist=new float[w,d];
            var used=new bool[w,d];
            for (int x=0;x<w;x++) for (int z=0;z<d;z++) dist[x,z]=float.PositiveInfinity;
            var shelter=game.Shelter;
            if (shelter!=null)
            {
                var sd=game.Rules.Building(shelter.definitionId);
                for(int x=shelter.cell.x;x<shelter.cell.x+sd.width;x++)
                    for(int z=shelter.cell.z;z<shelter.cell.z+sd.depth;z++) dist[x,z]=0;
            }
            for (int i=0;i<w*d;i++)
            {
                float best=float.PositiveInfinity;
                var current=new Cell(-1,-1);
                for(int x=0;x<w;x++) for(int z=0;z<d;z++)
                    if(!used[x,z] && dist[x,z]<best) { best=dist[x,z]; current=new Cell(x,z); }
                if(current.x<0) break;
                used[current.x,current.z]=true;
                float candidate=best+EntryCost(game,def,current);
                foreach(var prev in game.Board.Neighbours(current))
                    if(candidate<dist[prev.x,prev.z]) dist[prev.x,prev.z]=candidate;
            }
            return new Field { revision=game.Board.Revision, cost=dist };
        }
    }
}

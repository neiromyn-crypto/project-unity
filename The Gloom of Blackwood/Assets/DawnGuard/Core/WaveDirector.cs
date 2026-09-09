using System.Collections.Generic;

namespace DawnGuard.Core
{
    public sealed class WaveDirector
    {
        private struct Entry { public float time; public string id; public int order; }
        private readonly List<Entry> queue=new List<Entry>();
        private int cursor;
        public float Elapsed { get; private set; }
        public bool AllSpawned { get { return cursor>=queue.Count; } }
        public void Begin(WaveDefinition wave)
        {
            queue.Clear(); cursor=0; Elapsed=0;
            int order=0;
            foreach(var group in wave.groups)
                for(int i=0;i<group.count;i++) queue.Add(new Entry {
                    time=group.startAt+i*group.interval, id=group.enemyId, order=order++ });
            queue.Sort((a,b)=> { int c=a.time.CompareTo(b.time); return c!=0 ? c : a.order.CompareTo(b.order); });
        }
        public void Tick(GameSession game,float dt)
        {
            Elapsed+=dt;
            while(cursor<queue.Count && queue[cursor].time<=Elapsed)
            {
                var cell=game.Rules.spawnCells[cursor%game.Rules.spawnCells.Length];
                game.SpawnEnemy(queue[cursor].id,cell);
                cursor++;
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace DawnGuard.Core
{
    public sealed class GridBoard
    {
        private readonly int[,] occupancy;
        private readonly bool[,] rocks;
        public int Width { get; private set; }
        public int Depth { get; private set; }
        public int Revision { get; private set; }
        public GridBoard(int width, int depth, Cell[] blocked)
        {
            Width=width; Depth=depth;
            occupancy=new int[width, depth]; rocks=new bool[width, depth];
            foreach (var c in blocked) rocks[c.x,c.z]=true;
        }
        public bool Contains(Cell c) { return c.x>=0 && c.z>=0 && c.x<Width && c.z<Depth; }
        public bool IsRock(Cell c) { return !Contains(c) || rocks[c.x,c.z]; }
        public int Occupant(Cell c) { return Contains(c) ? occupancy[c.x,c.z] : -1; }
        public bool CanPlace(Cell origin, int width, int depth, int ignoreId=0)
        {
            for (int x=origin.x; x<origin.x+width; x++)
                for (int z=origin.z; z<origin.z+depth; z++)
                {
                    var c=new Cell(x,z);
                    if (IsRock(c) || (Occupant(c)!=0 && Occupant(c)!=ignoreId)) return false;
                }
            return true;
        }
        public void Place(int id, Cell origin, int width, int depth)
        {
            if (id<=0 || !CanPlace(origin,width,depth)) throw new InvalidOperationException("Occupied footprint.");
            for (int x=origin.x; x<origin.x+width; x++)
                for (int z=origin.z; z<origin.z+depth; z++) occupancy[x,z]=id;
            Revision++;
        }
        public void Remove(int id)
        {
            for (int x=0; x<Width; x++) for (int z=0; z<Depth; z++)
                if (occupancy[x,z]==id) occupancy[x,z]=0;
            Revision++;
        }
        public IEnumerable<Cell> Neighbours(Cell c)
        {
            var candidates=new[] { new Cell(c.x+1,c.z), new Cell(c.x-1,c.z),
                new Cell(c.x,c.z+1), new Cell(c.x,c.z-1) };
            foreach (var item in candidates) if (!IsRock(item)) yield return item;
        }
    }
}

using System;

namespace DawnGuard.Core
{
    public enum GamePhase { Day, Night, Victory, Defeat }
    public enum BuildingRole { Shelter, Camp, Generator, Wall, Turret, Laboratory }

    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int x, z;
        public Cell(int x, int z) { this.x = x; this.z = z; }
        public bool Equals(Cell other) { return x == other.x && z == other.z; }
        public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
        public override int GetHashCode() { return (x * 397) ^ z; }
    }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public string id, title;
        public BuildingRole role;
        public int width = 1, depth = 1, cost, maxHealth = 100;
        public int requiredDay = 1, requiredTech, powerUse, powerSupply, dailyIncome;
        public float range, damage, shotsPerSecond;
        public int upgradeCost = 100, maxLevel = 3;
    }

    [Serializable]
    public sealed class EnemyDefinition
    {
        public string id, title;
        public float health = 50, speed = 1, damagePerSecond = 10;
        public int reward = 4;
        public float size = 0.65f;
    }

    [Serializable]
    public sealed class SpawnGroup
    {
        public string enemyId;
        public int count;
        public float startAt, interval = 1;
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string title;
        public float duration = 60;
        public SpawnGroup[] groups;
    }

    [Serializable]
    public sealed class GameRules
    {
        public int width = 16, depth = 16, startingCredits = 120;
        public float daySeconds = 90, droneSpeed = 5, droneRange = 4.5f;
        public float droneDamage = 12, droneShotsPerSecond = 2;
        public int dawnReward = 60;
        public BuildingDefinition[] buildings;
        public EnemyDefinition[] enemies;
        public WaveDefinition[] waves;
        public Cell[] blockedCells, spawnCells;

        public BuildingDefinition Building(string id)
        {
            foreach (var item in buildings) if (item.id == id) return item;
            throw new ArgumentException("Unknown building: " + id);
        }
        public EnemyDefinition Enemy(string id)
        {
            foreach (var item in enemies) if (item.id == id) return item;
            throw new ArgumentException("Unknown enemy: " + id);
        }

        public void Validate()
        {
            if (width < 4 || depth < 4 || startingCredits < 0 || daySeconds <= 0 ||
                droneSpeed <= 0 || droneRange <= 0 || droneDamage <= 0 || droneShotsPerSecond <= 0)
                throw new ArgumentException("Invalid game rules.");
            if (buildings == null || enemies == null || waves == null || waves.Length == 0 ||
                spawnCells == null || spawnCells.Length == 0 || blockedCells == null)
                throw new ArgumentException("Missing definitions.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var b in buildings)
            {
                if (string.IsNullOrEmpty(b.id) || !ids.Add(b.id) || b.width < 1 || b.depth < 1 ||
                    b.cost < 0 || b.maxHealth <= 0 || b.powerUse < 0 || b.powerSupply < 0 ||
                    b.dailyIncome < 0 || b.upgradeCost < 0 || b.maxLevel < 1 ||
                    b.requiredDay < 1 || b.requiredTech < 0 || b.requiredTech > 1 ||
                    (b.role == BuildingRole.Turret && (b.range <= 0 || b.damage <= 0 || b.shotsPerSecond <= 0)))
                    throw new ArgumentException("Invalid building: " + b.id);
            }
            ids.Clear();
            foreach (var e in enemies)
                if (string.IsNullOrEmpty(e.id) || !ids.Add(e.id) || e.health <= 0 ||
                    e.speed <= 0 || e.damagePerSecond <= 0 || e.reward < 0)
                    throw new ArgumentException("Invalid enemy: " + e.id);
            foreach (var wave in waves)
            {
                if (wave.duration <= 0 || wave.groups == null) throw new ArgumentException("Invalid wave.");
                foreach (var group in wave.groups)
                {
                    Enemy(group.enemyId);
                    if (group.count < 1 || group.interval <= 0 || group.startAt < 0 ||
                        group.startAt + (group.count - 1) * group.interval >= wave.duration)
                        throw new ArgumentException("Spawn must occur before dawn.");
                }
            }
            foreach (var c in blockedCells) ValidateCell(c);
            foreach (var c in spawnCells) ValidateCell(c);
        }

        private void ValidateCell(Cell c)
        {
            if (c.x < 0 || c.z < 0 || c.x >= width || c.z >= depth)
                throw new ArgumentException("Cell outside board.");
        }
    }
}

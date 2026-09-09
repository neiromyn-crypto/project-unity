using System;

namespace DawnGuard.Core
{
    [Serializable]
    public sealed class BuildingState
    {
        public int instanceId;
        public string definitionId;
        public Cell cell;
        public int level = 1, paidCredits, purchasedDay;
        public float health;
        public bool permanent;
        [NonSerialized] public float cooldown;
        [NonSerialized] public bool powered = true;
        public BuildingState Copy() { return (BuildingState)MemberwiseClone(); }
    }

    public sealed class EnemyState
    {
        public int instanceId;
        public string definitionId;
        public float x, z, health;
        public Cell cell, nextCell;
        public bool travelling;
        // Transient target selected by the damage simulation, never saved.
        [NonSerialized] public int attackTargetId;
    }

    [Serializable]
    public sealed class DaySnapshot
    {
        public int schemaVersion = 1;
        public int day, credits, tech, droneLevel;
        public float dayRemaining;
        public BuildingState[] buildings;
    }

    public struct ShotEvent
    {
        public float fromX, fromZ, toX, toZ;
        public bool drone, electric;
    }

    public sealed class Wallet
    {
        public int Credits { get; private set; }
        public Wallet(int credits)
        {
            if (credits < 0) throw new ArgumentOutOfRangeException("credits");
            Credits = credits;
        }
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (Credits < amount) return false;
            Credits -= amount;
            return true;
        }
        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            Credits = checked(Credits + amount);
        }
    }
}

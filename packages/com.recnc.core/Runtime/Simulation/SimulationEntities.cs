using System.Collections.Generic;
using ReCnc.Core.Data;
using ReCnc.Core.Map;

namespace ReCnc.Core.Simulation
{
    public enum OrderKind
    {
        Idle = 0,
        Move = 1,
        AttackMove = 2,
        AttackTarget = 3,
        Harvest = 4,
        ReturnOre = 5
    }

    public enum SimEventKind
    {
        WeaponFired,
        ProjectileImpact,
        EntityDestroyed,
        BuildingPlaced,
        UnitProduced,
        OreUnloaded,
        EvaAnnouncement
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int FactionId;
        public int SourceEntityId;
        public SimVec2 Position;
        public string Tag;   // Weapon sound name, EVA key, or ActorId
        public int Value;
    }

    public class ProductionQueueState
    {
        public string Category;
        public string ActiveActorId;
        public int TotalCost;
        public int PaidCost;
        public bool IsReadyToPlace;

        public float ProgressNormalized
        {
            get { return TotalCost <= 0 ? 0f : (float)PaidCost / TotalCost; }
        }
    }

    public class FactionState
    {
        public int FactionId;
        public string Name;
        public string Side; // "Allied" or "Soviet"
        public bool IsAiControlled;
        public int Credits = 10000;
        public int PowerProduced;
        public int PowerConsumed;
        public readonly Dictionary<string, ProductionQueueState> Queues = new Dictionary<string, ProductionQueueState>();

        public int NetPower
        {
            get { return PowerProduced - PowerConsumed; }
        }

        public bool IsLowPower
        {
            get { return PowerConsumed > PowerProduced; }
        }

        public FactionState(int id, string name, string side, bool isAi)
        {
            FactionId = id;
            Name = name;
            Side = side;
            IsAiControlled = isAi;
            Queues["MainBuilding"] = new ProductionQueueState { Category = "MainBuilding" };
            Queues["DefenseBuilding"] = new ProductionQueueState { Category = "DefenseBuilding" };
            Queues["Infantry"] = new ProductionQueueState { Category = "Infantry" };
            Queues["Vehicle"] = new ProductionQueueState { Category = "Vehicle" };
        }
    }

    public class EntityState
    {
        public int Id;
        public int FactionId;
        public ActorDefData Def;
        public int Hp;
        public SimVec2 Position;
        public CellCoord TopLeftCell; // For buildings
        public CellCoord RallyPoint;  // For production buildings
        public float BodyYawDeg;
        public float TurretYawDeg;
        public OrderKind CurrentOrder = OrderKind.Idle;
        public int TargetEntityId;
        public CellCoord TargetCell;
        public readonly List<CellCoord> Path = new List<CellCoord>();
        public float WeaponCooldown;

        // Harvester fields
        public int CarriedOreUnits;
        public int CarriedGemUnits;
        public float HarvestTimer;
        public const int MaxHarvesterCapacity = 20;

        public bool IsAlive
        {
            get { return Hp > 0; }
        }

        public int TotalCarriedLoad
        {
            get { return CarriedOreUnits + CarriedGemUnits; }
        }
    }

    public class ProjectileState
    {
        public int Id;
        public int FactionId;
        public int SourceEntityId;
        public int TargetEntityId;
        public WeaponDefData Weapon;
        public SimVec2 StartPos;
        public SimVec2 CurrentPos;
        public SimVec2 TargetPos;
        public float Progress;
        public float Duration;
    }
}

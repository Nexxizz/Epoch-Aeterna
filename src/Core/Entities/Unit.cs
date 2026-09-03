using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Entities;

/// <summary>What a unit is currently doing. Systems read it, commands write it.</summary>
public enum UnitOrder
{
    Idle,
    Move,
    Gather,
    Build,
    Attack,

    /// <summary>Walks to the target and attacks anything that comes into range on the way.</summary>
    AttackMove,
}

/// <summary>Stage of the gathering cycle.</summary>
public enum GatherPhase
{
    ToNode,
    Harvesting,
    ToDropOff,
}

/// <summary>How independently a unit reacts to enemies.</summary>
public enum Stance
{
    /// <summary>Verfolgt Feinde in Sichtweite.</summary>
    Aggressive,

    /// <summary>Fights back but holds its ground.</summary>
    Defensive,

    /// <summary>Only attacks what is already in range, and does not move a step.</summary>
    HoldPosition,
}

/// <summary>A mobile unit.</summary>
public sealed class Unit : Entity
{
    public UnitOrder Order { get; set; } = UnitOrder.Idle;
    public Stance Stance { get; set; } = Stance.Aggressive;

    /// <summary>Final destination of the current movement.</summary>
    public Vector2 MoveTarget { get; private set; }

    /// <summary>Smoothed waypoints from the pathfinder. Empty while the search is running.</summary>
    public List<Vector2> Path { get; } = new();

    public int PathIndex { get; set; }

    /// <summary>A path has been requested but not computed yet.</summary>
    public bool NeedsPath { get; set; }

    /// <summary>Follow-up targets queued with Shift. Worked through in order after arrival.</summary>
    public Queue<Vector2> QueuedTargets { get; } = new();

    public float MoveSpeed { get; set; } = 3f;
    public float TurnSpeedRadians { get; set; } = Mathf.DegToRad(540f);
    public float Radius { get; set; } = 0.4f;
    public int PopulationCost { get; set; } = 1;

    // --- Combat ----------------------------------------------------------

    public float AttackDamage { get; set; }
    public float AttackRange { get; set; } = 1f;
    public float AttackCooldownSeconds { get; set; } = 1.5f;
    public float AttackCooldownLeft { get; set; }
    public DamageType DamageType { get; set; } = DamageType.Blunt;
    public ArmorClass ArmorClass { get; set; } = ArmorClass.Infantry;
    public bool UsesProjectile { get; set; }
    public float ProjectileSpeed { get; set; } = 22f;
    public bool AutoEngages { get; set; } = true;
    public float Armor { get; set; }

    /// <summary>Current attack target. Invalid as soon as the target dies.</summary>
    public EntityId AttackTarget { get; set; } = EntityId.None;

    /// <summary>Where the unit returns to after a fight ends (Defensive stance).</summary>
    public Vector2 GuardPosition { get; set; }

    // --- Work ------------------------------------------------------------

    public bool CanGather { get; set; }
    public bool CanBuild { get; set; }
    public float GatherRatePerSecond { get; set; } = 0.5f;
    public int CarryCapacity { get; set; } = 10;

    public GatherPhase GatherPhase { get; set; }
    public EntityId GatherTarget { get; set; } = EntityId.None;
    public EntityId DropOffTarget { get; set; } = EntityId.None;

    /// <summary>The construction site the unit is working on.</summary>
    public EntityId BuildTarget { get; set; } = EntityId.None;

    public ResourceType CarriedResource { get; set; } = ResourceType.Wood;
    public float CarriedAmount { get; set; }

    /// <summary>Fraction of a resource unit not yet extracted as a whole number.</summary>
    public float HarvestProgress { get; set; }

    /// <summary>Clearance needed in tiles. Every MVP unit fits into one.</summary>
    public int Clearance => Mathf.Max(1, Mathf.CeilToInt(Radius * 2f / NavGrid.CellSize));

    public bool HasPath => PathIndex < Path.Count;

    public Vector2 CurrentWaypoint => Path[PathIndex];

    public bool IsCarryingFull => CarriedAmount >= CarryCapacity;

    public void ApplyDefinition(UnitDefinition definition)
    {
        base.ApplyDefinition(definition);
        MoveSpeed = definition.MoveSpeed;
        TurnSpeedRadians = Mathf.DegToRad(definition.TurnSpeedDegrees);
        Radius = definition.Radius;
        PopulationCost = definition.PopulationCost;

        AttackDamage = definition.AttackDamage;
        AttackRange = definition.AttackRange;
        AttackCooldownSeconds = definition.AttackCooldownSeconds;
        DamageType = definition.DamageType;
        ArmorClass = definition.ArmorClass;
        UsesProjectile = definition.UsesProjectile;
        ProjectileSpeed = definition.ProjectileSpeed;
        AutoEngages = definition.AutoEngages;
        Armor = definition.Armor;

        CanGather = definition.CanGather;
        CanBuild = definition.CanBuild;
        GatherRatePerSecond = definition.GatherRatePerSecond;
        CarryCapacity = definition.CarryCapacity;

        GuardPosition = Position;
    }

    // --- Orders ----------------------------------------------------------

    /// <summary>A new movement order. Discards path, queue and every assignment.</summary>
    public void OrderMoveTo(Vector2 target)
    {
        QueuedTargets.Clear();
        ClearAssignments();
        StartMoveTo(target);
        GuardPosition = target;
    }

    public void OrderAttackMoveTo(Vector2 target)
    {
        QueuedTargets.Clear();
        ClearAssignments();
        StartMoveTo(target);
        Order = UnitOrder.AttackMove;
        GuardPosition = target;
    }

    public void OrderAttack(EntityId target)
    {
        ClearAssignments();
        AttackTarget = target;
        Order = UnitOrder.Attack;
    }

    public void OrderGather(EntityId node)
    {
        ClearAssignments();
        GatherTarget = node;
        GatherPhase = GatherPhase.ToNode;
        Order = UnitOrder.Gather;
    }

    public void OrderBuild(EntityId site)
    {
        ClearAssignments();
        BuildTarget = site;
        Order = UnitOrder.Build;
    }

    /// <summary>Appends a follow-up target instead of replacing the current order (Shift-click).</summary>
    public void QueueMoveTo(Vector2 target)
    {
        if (Order == UnitOrder.Idle) OrderMoveTo(target);
        else QueuedTargets.Enqueue(target);
    }

    /// <summary>Takes the next target off the queue. false when none is left.</summary>
    public bool AdvanceToQueuedTarget()
    {
        if (QueuedTargets.Count == 0) return false;
        StartMoveTo(QueuedTargets.Dequeue());
        return true;
    }

    /// <summary>Sets only the movement target, without discarding existing assignments.</summary>
    public void StartMoveTo(Vector2 target)
    {
        MoveTarget = target;
        if (Order is UnitOrder.Idle or UnitOrder.Move) Order = UnitOrder.Move;
        Path.Clear();
        PathIndex = 0;
        NeedsPath = true;
    }

    /// <summary>Takes on a search result.</summary>
    public void SetPath(IReadOnlyList<Vector2> waypoints)
    {
        Path.Clear();
        Path.AddRange(waypoints);
        PathIndex = 0;
        NeedsPath = false;
    }

    public void ClearPath()
    {
        Path.Clear();
        PathIndex = 0;
        NeedsPath = false;
    }

    public void Stop()
    {
        Order = UnitOrder.Idle;
        ClearPath();
        ClearAssignments();
        QueuedTargets.Clear();
        GuardPosition = Position;
    }

    private void ClearAssignments()
    {
        GatherTarget = EntityId.None;
        DropOffTarget = EntityId.None;
        BuildTarget = EntityId.None;
        AttackTarget = EntityId.None;
    }
}

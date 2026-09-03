using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Entities;

/// <summary>Was eine Einheit gerade tut. Systeme lesen das, Befehle schreiben es.</summary>
public enum UnitOrder
{
    Idle,
    Move,
    Gather,
    Build,
    Attack,

    /// <summary>Laeuft zum Ziel und greift alles an, was unterwegs in Reichweite kommt.</summary>
    AttackMove,
}

/// <summary>Abschnitt des Sammelkreislaufs.</summary>
public enum GatherPhase
{
    ToNode,
    Harvesting,
    ToDropOff,
}

/// <summary>Wie selbstaendig eine Einheit auf Feinde reagiert.</summary>
public enum Stance
{
    /// <summary>Verfolgt Feinde in Sichtweite.</summary>
    Aggressive,

    /// <summary>Wehrt sich, bleibt aber am Platz.</summary>
    Defensive,

    /// <summary>Greift nur an, was in Reichweite steht, und weicht keinen Schritt.</summary>
    HoldPosition,
}

/// <summary>Bewegliche Einheit.</summary>
public sealed class Unit : Entity
{
    public UnitOrder Order { get; set; } = UnitOrder.Idle;
    public Stance Stance { get; set; } = Stance.Aggressive;

    /// <summary>Endziel der aktuellen Bewegung.</summary>
    public Vector2 MoveTarget { get; private set; }

    /// <summary>Geglaettete Wegpunkte vom Pathfinder. Leer, solange die Suche laeuft.</summary>
    public List<Vector2> Path { get; } = new();

    public int PathIndex { get; set; }

    /// <summary>Der Pfad ist angefordert, aber noch nicht berechnet.</summary>
    public bool NeedsPath { get; set; }

    /// <summary>Mit Shift angehaengte Folgeziele. Werden nach Ankunft der Reihe nach abgearbeitet.</summary>
    public Queue<Vector2> QueuedTargets { get; } = new();

    public float MoveSpeed { get; set; } = 3f;
    public float TurnSpeedRadians { get; set; } = Mathf.DegToRad(540f);
    public float Radius { get; set; } = 0.4f;
    public int PopulationCost { get; set; } = 1;

    // --- Kampf -----------------------------------------------------------

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

    /// <summary>Aktuelles Angriffsziel. Ungueltig, sobald das Ziel stirbt.</summary>
    public EntityId AttackTarget { get; set; } = EntityId.None;

    /// <summary>Wohin die Einheit nach einem beendeten Gefecht zurueckkehrt (Defensive).</summary>
    public Vector2 GuardPosition { get; set; }

    // --- Arbeit ----------------------------------------------------------

    public bool CanGather { get; set; }
    public bool CanBuild { get; set; }
    public float GatherRatePerSecond { get; set; } = 0.5f;
    public int CarryCapacity { get; set; } = 10;

    public GatherPhase GatherPhase { get; set; }
    public EntityId GatherTarget { get; set; } = EntityId.None;
    public EntityId DropOffTarget { get; set; } = EntityId.None;

    /// <summary>Baustelle, an der die Einheit arbeitet.</summary>
    public EntityId BuildTarget { get; set; } = EntityId.None;

    public ResourceType CarriedResource { get; set; } = ResourceType.Wood;
    public float CarriedAmount { get; set; }

    /// <summary>Bruchteil einer Einheit Ressource, der noch nicht als ganze Zahl abgebaut wurde.</summary>
    public float HarvestProgress { get; set; }

    /// <summary>Benoetigter Freiraum in Kacheln. Alle MVP-Einheiten passen in eine.</summary>
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

    // --- Befehle ---------------------------------------------------------

    /// <summary>Neuer Bewegungsbefehl. Verwirft Pfad, Warteschlange und alle Auftraege.</summary>
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

    /// <summary>Haengt ein Folgeziel an, statt den aktuellen Befehl zu ersetzen (Shift-Klick).</summary>
    public void QueueMoveTo(Vector2 target)
    {
        if (Order == UnitOrder.Idle) OrderMoveTo(target);
        else QueuedTargets.Enqueue(target);
    }

    /// <summary>Nimmt das naechste Ziel aus der Warteschlange. false, wenn keins mehr da ist.</summary>
    public bool AdvanceToQueuedTarget()
    {
        if (QueuedTargets.Count == 0) return false;
        StartMoveTo(QueuedTargets.Dequeue());
        return true;
    }

    /// <summary>Setzt nur das Bewegungsziel, ohne bestehende Auftraege zu verwerfen.</summary>
    public void StartMoveTo(Vector2 target)
    {
        MoveTarget = target;
        if (Order is UnitOrder.Idle or UnitOrder.Move) Order = UnitOrder.Move;
        Path.Clear();
        PathIndex = 0;
        NeedsPath = true;
    }

    /// <summary>Uebernimmt ein Suchergebnis.</summary>
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

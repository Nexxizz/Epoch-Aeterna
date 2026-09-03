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
}

/// <summary>Bewegliche Einheit.</summary>
public sealed class Unit : Entity
{
    public UnitOrder Order { get; set; } = UnitOrder.Idle;

    /// <summary>Endziel des aktuellen Befehls.</summary>
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

    /// <summary>Benoetigter Freiraum in Kacheln. Alle MVP-Einheiten passen in eine.</summary>
    public int Clearance => Mathf.Max(1, Mathf.CeilToInt(Radius * 2f / NavGrid.CellSize));

    public bool HasPath => PathIndex < Path.Count;

    public Vector2 CurrentWaypoint => Path[PathIndex];

    public void ApplyDefinition(UnitDefinition definition)
    {
        base.ApplyDefinition(definition);
        MoveSpeed = definition.MoveSpeed;
        TurnSpeedRadians = Mathf.DegToRad(definition.TurnSpeedDegrees);
        Radius = definition.Radius;
        PopulationCost = definition.PopulationCost;
    }

    /// <summary>Neuer Bewegungsbefehl. Verwirft Pfad und Warteschlange.</summary>
    public void OrderMoveTo(Vector2 target)
    {
        QueuedTargets.Clear();
        StartMoveTo(target);
    }

    /// <summary>Haengt ein Folgeziel an, statt den aktuellen Befehl zu ersetzen (Shift-Klick).</summary>
    public void QueueMoveTo(Vector2 target)
    {
        if (Order == UnitOrder.Idle) StartMoveTo(target);
        else QueuedTargets.Enqueue(target);
    }

    /// <summary>Nimmt das naechste Ziel aus der Warteschlange. false, wenn keins mehr da ist.</summary>
    public bool AdvanceToQueuedTarget()
    {
        if (QueuedTargets.Count == 0) return false;
        StartMoveTo(QueuedTargets.Dequeue());
        return true;
    }

    private void StartMoveTo(Vector2 target)
    {
        MoveTarget = target;
        Order = UnitOrder.Move;
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

    public void Stop()
    {
        Order = UnitOrder.Idle;
        Path.Clear();
        PathIndex = 0;
        NeedsPath = false;
        QueuedTargets.Clear();
    }
}

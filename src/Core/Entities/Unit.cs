using Godot;
using EpochAeterna.Core.Data;

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

    /// <summary>Zielpunkt der aktuellen Bewegung. Nur gueltig, wenn <see cref="Order"/> = Move.</summary>
    public Vector2 MoveTarget { get; set; }

    public float MoveSpeed { get; set; } = 3f;
    public float TurnSpeedRadians { get; set; } = Mathf.DegToRad(540f);
    public float Radius { get; set; } = 0.4f;
    public int PopulationCost { get; set; } = 1;

    public void ApplyDefinition(UnitDefinition definition)
    {
        base.ApplyDefinition(definition);
        MoveSpeed = definition.MoveSpeed;
        TurnSpeedRadians = Mathf.DegToRad(definition.TurnSpeedDegrees);
        Radius = definition.Radius;
        PopulationCost = definition.PopulationCost;
    }

    public void OrderMoveTo(Vector2 target)
    {
        MoveTarget = target;
        Order = UnitOrder.Move;
    }

    public void Stop() => Order = UnitOrder.Idle;
}

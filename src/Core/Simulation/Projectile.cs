using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Ein fliegendes Geschoss — Pfeil oder Schleuderstein.
/// </summary>
/// <remarks>
/// Bewusst keine Entity: Geschosse leben ein bis zwei Sekunden und wuerden die
/// Registry und den View-Manager mit staendigem An- und Abmelden belasten. Sie liegen
/// stattdessen in einer einfachen Liste, die die Darstellung jeden Frame ausliest.
/// </remarks>
public sealed class Projectile
{
    public required int OwnerPlayerId { get; init; }
    public required EntityId TargetId { get; init; }
    public required Vector2 Origin { get; init; }
    public required float Damage { get; init; }
    public required DamageType DamageType { get; init; }
    public required float Speed { get; init; }

    public Vector2 Position { get; set; }

    /// <summary>Letzte bekannte Zielposition — das Geschoss fliegt auch weiter, wenn das Ziel stirbt.</summary>
    public Vector2 TargetPosition { get; set; }

    public bool HasLanded { get; set; }

    /// <summary>0 bis 1 entlang der Flugbahn — die Darstellung berechnet daraus den Bogen.</summary>
    public float FlightProgress { get; set; }

    /// <summary>Zurueckgelegte Gesamtstrecke, damit der Bogen unabhaengig vom Ziel bleibt.</summary>
    public float TotalDistance { get; set; }
}

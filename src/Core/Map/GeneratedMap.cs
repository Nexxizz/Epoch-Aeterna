using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>Kleines Beiwerk ohne Spielwirkung — Grasbueschel, lose Steine.</summary>
public enum DecorationType
{
    GrassTuft,
    Pebble,
}

/// <summary>Ein Streuobjekt. Blockiert nichts und laesst sich nicht abbauen.</summary>
public readonly struct Decoration
{
    public required DecorationType Type { get; init; }
    public required Vector2 Position { get; init; }
    public required float Rotation { get; init; }
    public required float Scale { get; init; }
}

/// <summary>Ein Platz, an dem beim Matchaufbau ein Ressourcenvorkommen entsteht.</summary>
public readonly struct ResourceSpot
{
    public required string DefinitionId { get; init; }
    public required Vector2 Position { get; init; }
    public required float Rotation { get; init; }
    public required float Scale { get; init; }
}

/// <summary>Ergebnis der Kartenerzeugung: Gitter, Vorkommen, Beiwerk und Startplaetze.</summary>
public sealed class GeneratedMap
{
    public required NavGrid Grid { get; init; }
    public required List<ResourceSpot> ResourceSpots { get; init; }
    public required List<Decoration> Decorations { get; init; }
    public required List<Vector2> StartPositions { get; init; }
}

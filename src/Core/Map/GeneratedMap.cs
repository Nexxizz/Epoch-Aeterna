using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>Small clutter with no effect on play — grass tufts, loose stones.</summary>
public enum DecorationType
{
    GrassTuft,
    Pebble,
}

/// <summary>A scattered object. Blocks nothing and cannot be harvested.</summary>
public readonly struct Decoration
{
    public required DecorationType Type { get; init; }
    public required Vector2 Position { get; init; }
    public required float Rotation { get; init; }
    public required float Scale { get; init; }
}

/// <summary>A spot where a resource deposit is created during match setup.</summary>
public readonly struct ResourceSpot
{
    public required string DefinitionId { get; init; }
    public required Vector2 Position { get; init; }
    public required float Rotation { get; init; }
    public required float Scale { get; init; }
}

/// <summary>Result of map generation: grid, deposits, clutter and starting positions.</summary>
public sealed class GeneratedMap
{
    public required NavGrid Grid { get; init; }
    public required List<ResourceSpot> ResourceSpots { get; init; }
    public required List<Decoration> Decorations { get; init; }
    public required List<Vector2> StartPositions { get; init; }
}

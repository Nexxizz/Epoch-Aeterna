using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

public enum DecorationType
{
    Tree,
    Rock,
    Bush,
}

/// <summary>Ein Streuobjekt auf der Karte. Rein statisch — Ressourcenknoten folgen in Phase 3.1.</summary>
public readonly struct Decoration
{
    public required DecorationType Type { get; init; }
    public required Vector2 Position { get; init; }
    public required float Rotation { get; init; }
    public required float Scale { get; init; }
}

/// <summary>Ergebnis der Kartenerzeugung: Gitter, Streuobjekte und Startplaetze.</summary>
public sealed class GeneratedMap
{
    public required NavGrid Grid { get; init; }
    public required List<Decoration> Decorations { get; init; }
    public required List<Vector2> StartPositions { get; init; }
}

using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>
/// Erzeugt die Testkarte "Ebene der Anfaenge" aus einem Seed.
/// </summary>
/// <remarks>
/// Alles ist aus dem Seed abgeleitet: Gleiche Zahl, gleiche Karte. Das haelt
/// Selbsttests reproduzierbar und ist die Voraussetzung dafuer, dass spaeter im
/// Multiplayer alle Teilnehmer dieselbe Karte erzeugen, statt sie zu uebertragen.
/// </remarks>
public static class MapGenerator
{
    /// <summary>Ab diesem Hoehenunterschied innerhalb einer Kachel gilt sie als unbegehbar.</summary>
    private const float MaxWalkableSlope = 2.2f;

    /// <summary>Halber Abstand, den eine Startbasis frei von Hindernissen bekommt, in Metern.</summary>
    private const float StartClearRadius = 16f;

    public static GeneratedMap Generate(ulong seed, int width = 128, int height = 128)
    {
        var grid = new NavGrid(width, height);

        BuildHeights(grid, seed);
        MarkSteepTerrain(grid);

        List<Vector2> startPositions = ChooseStartPositions(grid);
        List<Decoration> decorations = ScatterDecorations(grid, seed, startPositions);

        return new GeneratedMap
        {
            Grid = grid,
            Decorations = decorations,
            StartPositions = startPositions,
        };
    }

    /// <summary>
    /// Sanfte Huegellandschaft aus zwei ueberlagerten Rauschfeldern: eine grosse Welle
    /// fuer die Grundform, eine feine fuer Unebenheiten.
    /// </summary>
    private static void BuildHeights(NavGrid grid, ulong seed)
    {
        var broad = new FastNoiseLite
        {
            Seed = (int)seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.008f,
        };

        var detail = new FastNoiseLite
        {
            Seed = (int)seed + 977,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.045f,
        };

        for (int y = 0; y <= grid.Height; y++)
        {
            for (int x = 0; x <= grid.Width; x++)
            {
                float wx = x * NavGrid.CellSize;
                float wy = y * NavGrid.CellSize;

                float value = broad.GetNoise2D(wx, wy) * 6.5f + detail.GetNoise2D(wx, wy) * 1.1f;

                // Raender leicht anheben, damit die Karte wie eine Senke wirkt und
                // der Blick nicht ins Leere laeuft.
                value += EdgeFalloff(grid, x, y) * 5f;

                grid.SetCornerHeight(x, y, value);
            }
        }
    }

    /// <summary>0 in der Kartenmitte, 1 am Rand — quadratisch ansteigend.</summary>
    private static float EdgeFalloff(NavGrid grid, int x, int y)
    {
        float nx = Mathf.Abs(x / (float)grid.Width * 2f - 1f);
        float ny = Mathf.Abs(y / (float)grid.Height * 2f - 1f);
        float edge = Mathf.Max(nx, ny);
        return edge < 0.75f ? 0f : Mathf.Pow((edge - 0.75f) / 0.25f, 2f);
    }

    private static void MarkSteepTerrain(NavGrid grid)
    {
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.CellSlope(x, y) > MaxWalkableSlope) grid.Block(x, y, BlockFlags.Terrain);
            }
        }
    }

    /// <summary>
    /// Zwei gegenueberliegende Startplaetze auf der flachsten Stelle ihrer Kartenhaelfte.
    /// </summary>
    private static List<Vector2> ChooseStartPositions(NavGrid grid)
    {
        var positions = new List<Vector2>
        {
            FindFlattestSpot(grid, new Vector2(-grid.WorldWidth * 0.32f, 0f)),
            FindFlattestSpot(grid, new Vector2(grid.WorldWidth * 0.32f, 0f)),
        };

        foreach (Vector2 position in positions) ClearArea(grid, position, StartClearRadius);
        return positions;
    }

    private static Vector2 FindFlattestSpot(NavGrid grid, Vector2 around)
    {
        Vector2I centre = grid.ClampCell(grid.WorldToCell(around));
        const int searchRadius = 14;

        Vector2I best = centre;
        float bestSlope = float.MaxValue;

        for (int dy = -searchRadius; dy <= searchRadius; dy++)
        {
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                int x = centre.X + dx;
                int y = centre.Y + dy;
                if (!grid.InBounds(x, y)) continue;

                // Steilheit der naeheren Umgebung, nicht nur der einzelnen Kachel —
                // ein Rathaus braucht eine flache Flaeche, keinen flachen Punkt.
                float slope = 0f;
                for (int oy = -2; oy <= 2; oy++)
                {
                    for (int ox = -2; ox <= 2; ox++) slope += grid.CellSlope(x + ox, y + oy);
                }

                if (slope >= bestSlope) continue;
                bestSlope = slope;
                best = new Vector2I(x, y);
            }
        }

        return grid.CellToWorld(best.X, best.Y);
    }

    private static void ClearArea(NavGrid grid, Vector2 centre, float radius)
    {
        Vector2I cell = grid.WorldToCell(centre);
        int cells = Mathf.CeilToInt(radius / NavGrid.CellSize);

        for (int dy = -cells; dy <= cells; dy++)
        {
            for (int dx = -cells; dx <= cells; dx++)
            {
                if (dx * dx + dy * dy > cells * cells) continue;
                grid.Unblock(cell.X + dx, cell.Y + dy, BlockFlags.Terrain | BlockFlags.Decoration);
            }
        }
    }

    /// <summary>
    /// Streut Waldstuecke, Felsen und Buesche. Baeume und Felsen sperren ihre Kachel —
    /// erst dadurch hat die Wegfindung ueberhaupt etwas zu umgehen.
    /// </summary>
    private static List<Decoration> ScatterDecorations(NavGrid grid, ulong seed, List<Vector2> startPositions)
    {
        var decorations = new List<Decoration>();
        var random = new RandomNumberGenerator { Seed = seed + 4242 };

        var forest = new FastNoiseLite
        {
            Seed = (int)seed + 313,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.02f,
        };

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.IsWalkable(x, y)) continue;

                Vector2 world = grid.CellToWorld(x, y);
                if (IsNearStart(world, startPositions)) continue;

                float density = forest.GetNoise2D(x * NavGrid.CellSize, y * NavGrid.CellSize);
                float slope = grid.CellSlope(x, y);

                DecorationType? type = null;

                if (density > 0.28f && random.Randf() < 0.55f) type = DecorationType.Tree;
                else if (slope > 1.4f && random.Randf() < 0.16f) type = DecorationType.Rock;
                else if (density is > 0.05f and < 0.22f && random.Randf() < 0.05f) type = DecorationType.Bush;

                if (type is null) continue;

                // Innerhalb der Kachel leicht versetzen, damit kein Rastermuster entsteht.
                var jitter = new Vector2(
                    random.RandfRange(-0.6f, 0.6f),
                    random.RandfRange(-0.6f, 0.6f));

                decorations.Add(new Decoration
                {
                    Type = type.Value,
                    Position = world + jitter,
                    Rotation = random.RandfRange(0f, Mathf.Tau),
                    Scale = random.RandfRange(0.8f, 1.25f),
                });

                if (type != DecorationType.Bush) grid.Block(x, y, BlockFlags.Decoration);
            }
        }

        return decorations;
    }

    private static bool IsNearStart(Vector2 world, List<Vector2> startPositions)
    {
        foreach (Vector2 start in startPositions)
        {
            if (world.DistanceSquaredTo(start) < StartClearRadius * StartClearRadius) return true;
        }
        return false;
    }
}

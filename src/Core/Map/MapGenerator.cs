using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>
/// Generates the test map "Plain of Beginnings" from a seed.
/// </summary>
/// <remarks>
/// Everything derives from the seed: same number, same map. That keeps
/// self-tests reproducible and is the precondition for every participant in a
/// later multiplayer match generating the same map instead of transferring it.
///
/// The generator only fixes positions. The deposits themselves are created during
/// match setup from the definitions — the map need not know any game values.
/// </remarks>
public static class MapGenerator
{
    public const string TreeId = "res_tree";
    public const string StoneId = "res_stone";
    public const string GoldId = "res_gold";
    public const string BerryId = "res_berries";

    /// <summary>Above this height difference within a tile, it counts as impassable.</summary>
    private const float MaxWalkableSlope = 2.2f;

    /// <summary>Radius around a starting base kept free of obstacles, in metres.</summary>
    private const float StartClearRadius = 18f;

    public static GeneratedMap Generate(ulong seed, int width = 128, int height = 128)
    {
        var grid = new NavGrid(width, height);

        BuildHeights(grid, seed);
        MarkSteepTerrain(grid);

        List<Vector2> startPositions = ChooseStartPositions(grid);
        var random = new RandomNumberGenerator { Seed = seed + 4242 };

        List<ResourceSpot> spots = ScatterResources(grid, seed, random, startPositions);
        List<Decoration> decorations = ScatterDecorations(grid, random);

        return new GeneratedMap
        {
            Grid = grid,
            ResourceSpots = spots,
            Decorations = decorations,
            StartPositions = startPositions,
        };
    }

    /// <summary>
    /// Gentle hills from two layered noise fields: one broad wave for the overall
    /// shape, one fine one for unevenness.
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

                // Raise the edges slightly so the map reads as a basin and
                // the view does not run off into nothing.
                value += EdgeFalloff(grid, x, y) * 5f;

                grid.SetCornerHeight(x, y, value);
            }
        }
    }

    /// <summary>0 at the map centre, 1 at the edge — rising quadratically.</summary>
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

    /// <summary>Two opposing starting positions on the flattest spot of their map half.</summary>
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

                // Steepness of the surroundings, not of the single tile —
                // a town centre needs a flat area, not a flat point.
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
    /// Scatters forest, quarries, gold veins and berry bushes.
    /// </summary>
    /// <remarks>
    /// Every base is guaranteed wood, food, stone and gold within reach.
    /// Without that guarantee chance decides the match before it starts.
    /// </remarks>
    private static List<ResourceSpot> ScatterResources(NavGrid grid, ulong seed,
        RandomNumberGenerator random, List<Vector2> startPositions)
    {
        var spots = new List<ResourceSpot>();

        var forest = new FastNoiseLite
        {
            Seed = (int)seed + 313,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.022f,
        };

        // Guaranteed supply near the start: a copse, berries, stone and gold per base.
        foreach (Vector2 start in startPositions)
        {
            AddCluster(spots, grid, random, start, TreeId, count: 14, minRadius: 12f, maxRadius: 20f);
            AddCluster(spots, grid, random, start, BerryId, count: 6, minRadius: 10f, maxRadius: 16f);
            AddCluster(spots, grid, random, start, StoneId, count: 5, minRadius: 14f, maxRadius: 22f);
            AddCluster(spots, grid, random, start, GoldId, count: 4, minRadius: 16f, maxRadius: 24f);
        }

        // The rest of the map: forest patches from noise, rock on steep ground.
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.IsWalkable(x, y)) continue;

                Vector2 world = grid.CellToWorld(x, y);
                if (IsNearStart(world, startPositions, StartClearRadius + 12f)) continue;

                float density = forest.GetNoise2D(x * NavGrid.CellSize, y * NavGrid.CellSize);
                float slope = grid.CellSlope(x, y);

                string? id = null;

                if (density > 0.34f && random.Randf() < 0.42f) id = TreeId;
                else if (slope > 1.5f && random.Randf() < 0.10f) id = StoneId;
                else if (density is > 0.05f and < 0.14f && random.Randf() < 0.02f) id = BerryId;
                else if (density < -0.42f && random.Randf() < 0.02f) id = GoldId;

                if (id is null) continue;

                Place(spots, grid, random, world, id);
            }
        }

        return spots;
    }

    /// <summary>Places a cluster of one deposit type in a ring around a centre point.</summary>
    private static void AddCluster(List<ResourceSpot> spots, NavGrid grid, RandomNumberGenerator random,
        Vector2 centre, string id, int count, float minRadius, float maxRadius)
    {
        // A random starting angle, so the clusters do not sit identically at every base.
        float baseAngle = random.RandfRange(0f, Mathf.Tau);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + Mathf.Tau * i / count + random.RandfRange(-0.25f, 0.25f);
            float radius = random.RandfRange(minRadius, maxRadius);

            Vector2 position = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2I cell = grid.WorldToCell(position);

            if (!grid.IsWalkable(cell.X, cell.Y)) continue;

            Place(spots, grid, random, grid.CellToWorld(cell.X, cell.Y), id);
        }
    }

    private static void Place(List<ResourceSpot> spots, NavGrid grid, RandomNumberGenerator random,
        Vector2 world, string id)
    {
        // Offset slightly within the tile so no grid pattern emerges.
        var jitter = new Vector2(random.RandfRange(-0.5f, 0.5f), random.RandfRange(-0.5f, 0.5f));

        spots.Add(new ResourceSpot
        {
            DefinitionId = id,
            Position = world + jitter,
            Rotation = random.RandfRange(0f, Mathf.Tau),
            Scale = random.RandfRange(0.85f, 1.2f),
        });

        // Claim the tile so it is not populated twice. Whether the deposit really
        // blocks the tile is decided later by its definition.
        Vector2I cell = grid.WorldToCell(world);
        grid.Block(cell.X, cell.Y, BlockFlags.Decoration);
    }

    /// <summary>Grass tufts and loose stones — purely visual, with no blocking effect.</summary>
    private static List<Decoration> ScatterDecorations(NavGrid grid, RandomNumberGenerator random)
    {
        var decorations = new List<Decoration>();

        for (int y = 0; y < grid.Height; y += 2)
        {
            for (int x = 0; x < grid.Width; x += 2)
            {
                if (!grid.IsWalkable(x, y) || random.Randf() > 0.16f) continue;

                Vector2 world = grid.CellToWorld(x, y);

                decorations.Add(new Decoration
                {
                    Type = random.Randf() < 0.7f ? DecorationType.GrassTuft : DecorationType.Pebble,
                    Position = world + new Vector2(random.RandfRange(-1f, 1f), random.RandfRange(-1f, 1f)),
                    Rotation = random.RandfRange(0f, Mathf.Tau),
                    Scale = random.RandfRange(0.7f, 1.3f),
                });
            }
        }

        return decorations;
    }

    private static bool IsNearStart(Vector2 world, List<Vector2> startPositions, float radius)
    {
        foreach (Vector2 start in startPositions)
        {
            if (world.DistanceSquaredTo(start) < radius * radius) return true;
        }
        return false;
    }
}

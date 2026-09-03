using Godot;

namespace EpochAeterna.Core.Pathfinding;

/// <summary>Why a tile is blocked. A bitmask, so sources can be set independently.</summary>
[System.Flags]
public enum BlockFlags : byte
{
    None = 0,

    /// <summary>Terrain too steep — does not change during the match.</summary>
    Terrain = 1 << 0,

    /// <summary>Tree, rock, bush.</summary>
    Decoration = 1 << 1,

    /// <summary>A building. Set when built, cleared when demolished.</summary>
    Building = 1 << 2,
}

/// <summary>
/// The navigation grid: heights, walkability and occupancy of the map.
/// </summary>
/// <remarks>
/// Deliberately one shared structure for simulation and display — the terrain mesh
/// is built from the same heights the pathfinding computes on. That way looks and
/// walkability cannot drift apart.
///
/// Tile coordinates run from (0,0) to (Width-1, Height-1); world coordinates are
/// centred on the origin, so the map is symmetric around (0,0).
/// </remarks>
public sealed class NavGrid
{
    public const float CellSize = 2f;

    /// <summary>Upper bound of the clearance computation. No unit needs a larger value.</summary>
    private const byte MaxClearance = 8;

    public int Width { get; }
    public int Height { get; }

    private readonly BlockFlags[] _blocked;
    private readonly byte[] _clearance;

    /// <summary>Heights at the tile corners, hence (Width+1) x (Height+1) values.</summary>
    private readonly float[] _cornerHeights;

    private bool _clearanceDirty = true;

    public float WorldWidth => Width * CellSize;
    public float WorldHeight => Height * CellSize;

    public NavGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _blocked = new BlockFlags[width * height];
        _clearance = new byte[width * height];
        _cornerHeights = new float[(width + 1) * (height + 1)];
    }

    // --- Coordinates -----------------------------------------------------

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public Vector2 CellToWorld(int x, int y) => new(
        (x + 0.5f) * CellSize - WorldWidth * 0.5f,
        (y + 0.5f) * CellSize - WorldHeight * 0.5f);

    public Vector2I WorldToCell(Vector2 world) => new(
        Mathf.FloorToInt((world.X + WorldWidth * 0.5f) / CellSize),
        Mathf.FloorToInt((world.Y + WorldHeight * 0.5f) / CellSize));

    public Vector2I ClampCell(Vector2I cell) => new(
        Mathf.Clamp(cell.X, 0, Width - 1),
        Mathf.Clamp(cell.Y, 0, Height - 1));

    // --- Heights ---------------------------------------------------------

    public float GetCornerHeight(int x, int y) =>
        _cornerHeights[Mathf.Clamp(y, 0, Height) * (Width + 1) + Mathf.Clamp(x, 0, Width)];

    public void SetCornerHeight(int x, int y, float value)
    {
        if (x < 0 || y < 0 || x > Width || y > Height) return;
        _cornerHeights[y * (Width + 1) + x] = value;
    }

    /// <summary>Bilinearly interpolated terrain height at any world point.</summary>
    public float SampleHeight(Vector2 world)
    {
        float gx = (world.X + WorldWidth * 0.5f) / CellSize;
        float gy = (world.Y + WorldHeight * 0.5f) / CellSize;

        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, Width);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, Height);
        int x1 = Mathf.Min(x0 + 1, Width);
        int y1 = Mathf.Min(y0 + 1, Height);

        float tx = Mathf.Clamp(gx - x0, 0f, 1f);
        float ty = Mathf.Clamp(gy - y0, 0f, 1f);

        float top = Mathf.Lerp(GetCornerHeight(x0, y0), GetCornerHeight(x1, y0), tx);
        float bottom = Mathf.Lerp(GetCornerHeight(x0, y1), GetCornerHeight(x1, y1), tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    /// <summary>Largest height difference within a tile — the measure of steepness.</summary>
    public float CellSlope(int x, int y)
    {
        float a = GetCornerHeight(x, y);
        float b = GetCornerHeight(x + 1, y);
        float c = GetCornerHeight(x, y + 1);
        float d = GetCornerHeight(x + 1, y + 1);

        float min = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
        float max = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
        return max - min;
    }

    // --- Walkability -----------------------------------------------------

    public BlockFlags GetFlags(int x, int y) => InBounds(x, y) ? _blocked[y * Width + x] : BlockFlags.Terrain;

    public bool IsWalkable(int x, int y) => InBounds(x, y) && _blocked[y * Width + x] == BlockFlags.None;

    public void Block(int x, int y, BlockFlags flag)
    {
        if (!InBounds(x, y)) return;
        _blocked[y * Width + x] |= flag;
        _clearanceDirty = true;
    }

    public void Unblock(int x, int y, BlockFlags flag)
    {
        if (!InBounds(x, y)) return;
        _blocked[y * Width + x] &= ~flag;
        _clearanceDirty = true;
    }

    /// <summary>Sets or clears a building's footprint, centred on its position.</summary>
    public void ApplyFootprint(Vector2 center, Vector2I footprint, bool blocked)
    {
        Vector2I origin = WorldToCell(center);
        int halfX = footprint.X / 2;
        int halfY = footprint.Y / 2;

        for (int y = origin.Y - halfY; y < origin.Y - halfY + footprint.Y; y++)
        {
            for (int x = origin.X - halfX; x < origin.X - halfX + footprint.X; x++)
            {
                if (blocked) Block(x, y, BlockFlags.Building);
                else Unblock(x, y, BlockFlags.Building);
            }
        }
    }

    // --- Clearance -------------------------------------------------------

    /// <summary>
    /// Distance to the nearest blocked tile, in tiles. A unit that is <c>n</c> tiles
    /// wide may only walk over tiles whose clearance is >= n.
    /// </summary>
    public byte GetClearance(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        if (_clearanceDirty) RebuildClearance();
        return _clearance[y * Width + x];
    }

    public bool IsPassable(int x, int y, int requiredClearance) =>
        IsWalkable(x, y) && GetClearance(x, y) >= requiredClearance;

    /// <summary>
    /// Multi-source breadth-first search from every blocked tile. Costs one pass over
    /// the map and only runs when something actually changed.
    /// </summary>
    private void RebuildClearance()
    {
        _clearanceDirty = false;

        var queue = new System.Collections.Generic.Queue<int>();

        for (int i = 0; i < _clearance.Length; i++)
        {
            if (_blocked[i] != BlockFlags.None)
            {
                _clearance[i] = 0;
                queue.Enqueue(i);
            }
            else
            {
                _clearance[i] = MaxClearance;
            }
        }

        // The map edge counts as blocked, otherwise units walk into the border.
        for (int x = 0; x < Width; x++)
        {
            EnqueueBorder(queue, x, 0);
            EnqueueBorder(queue, x, Height - 1);
        }
        for (int y = 0; y < Height; y++)
        {
            EnqueueBorder(queue, 0, y);
            EnqueueBorder(queue, Width - 1, y);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % Width;
            int y = index / Width;
            byte next = (byte)(_clearance[index] + 1);
            if (next >= MaxClearance) continue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!InBounds(nx, ny)) continue;

                    int neighbour = ny * Width + nx;
                    if (_clearance[neighbour] <= next) continue;

                    _clearance[neighbour] = next;
                    queue.Enqueue(neighbour);
                }
            }
        }
    }

    private void EnqueueBorder(System.Collections.Generic.Queue<int> queue, int x, int y)
    {
        int index = y * Width + x;
        if (_clearance[index] == 0) return;
        _clearance[index] = 0;
        queue.Enqueue(index);
    }

    /// <summary>Nearest walkable tile around a target — for clicks that land on obstacles.</summary>
    public Vector2I FindNearestPassable(Vector2I start, int requiredClearance, int maxRadius = 24)
    {
        if (IsPassable(start.X, start.Y, requiredClearance)) return start;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Only check the ring at this radius, not the whole area.
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

                    int x = start.X + dx;
                    int y = start.Y + dy;
                    if (IsPassable(x, y, requiredClearance)) return new Vector2I(x, y);
                }
            }
        }
        return start;
    }
}

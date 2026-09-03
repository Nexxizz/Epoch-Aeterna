using Godot;

namespace EpochAeterna.Core.Pathfinding;

/// <summary>Warum eine Kachel gesperrt ist. Als Bitmaske, damit Quellen unabhaengig gesetzt werden.</summary>
[System.Flags]
public enum BlockFlags : byte
{
    None = 0,

    /// <summary>Zu steiles Gelaende — aendert sich waehrend der Partie nicht.</summary>
    Terrain = 1 << 0,

    /// <summary>Baum, Fels, Busch.</summary>
    Decoration = 1 << 1,

    /// <summary>Gebaeude. Wird beim Bau gesetzt und beim Abriss geloescht.</summary>
    Building = 1 << 2,
}

/// <summary>
/// Das Navigationsgitter: Hoehen, Begehbarkeit und Belegung der Karte.
/// </summary>
/// <remarks>
/// Bewusst eine gemeinsame Struktur fuer Simulation und Darstellung — der Terrain-Mesh
/// wird aus denselben Hoehen gebaut, aus denen die Wegfindung rechnet. Damit koennen
/// Optik und Begehbarkeit nicht auseinanderlaufen.
///
/// Kachelkoordinaten laufen von (0,0) bis (Width-1, Height-1); die Weltkoordinaten sind
/// um den Ursprung zentriert, damit die Karte symmetrisch um (0,0) liegt.
/// </remarks>
public sealed class NavGrid
{
    public const float CellSize = 2f;

    /// <summary>Obergrenze der Freiraumberechnung. Groessere Werte braucht keine Einheit.</summary>
    private const byte MaxClearance = 8;

    public int Width { get; }
    public int Height { get; }

    private readonly BlockFlags[] _blocked;
    private readonly byte[] _clearance;

    /// <summary>Hoehen an den Kachel-Ecken, daher (Width+1) x (Height+1) Werte.</summary>
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

    // --- Koordinaten -----------------------------------------------------

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

    // --- Hoehen ----------------------------------------------------------

    public float GetCornerHeight(int x, int y) =>
        _cornerHeights[Mathf.Clamp(y, 0, Height) * (Width + 1) + Mathf.Clamp(x, 0, Width)];

    public void SetCornerHeight(int x, int y, float value)
    {
        if (x < 0 || y < 0 || x > Width || y > Height) return;
        _cornerHeights[y * (Width + 1) + x] = value;
    }

    /// <summary>Bilinear interpolierte Gelaendehoehe an einem beliebigen Weltpunkt.</summary>
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

    /// <summary>Groesster Hoehenunterschied innerhalb einer Kachel — Mass fuer die Steilheit.</summary>
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

    // --- Begehbarkeit ----------------------------------------------------

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

    /// <summary>Setzt oder loescht die Grundflaeche eines Gebaeudes, zentriert auf dessen Position.</summary>
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

    // --- Freiraum --------------------------------------------------------

    /// <summary>
    /// Abstand zur naechsten gesperrten Kachel, in Kacheln. Eine Einheit, die
    /// <c>n</c> Kacheln breit ist, darf nur ueber Kacheln mit Clearance >= n laufen.
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
    /// Mehrquellen-Breitensuche von allen gesperrten Kacheln aus. Kostet einen
    /// Durchlauf ueber die Karte und laeuft nur, wenn sich wirklich etwas geaendert hat.
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

        // Kartenrand zaehlt als gesperrt, sonst laufen Einheiten in die Kante.
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

    /// <summary>Naechstgelegene begehbare Kachel um ein Ziel herum — fuer Klicks auf Hindernisse.</summary>
    public Vector2I FindNearestPassable(Vector2I start, int requiredClearance, int maxRadius = 24)
    {
        if (IsPassable(start.X, start.Y, requiredClearance)) return start;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Nur den Ring dieses Radius pruefen, nicht die Flaeche.
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

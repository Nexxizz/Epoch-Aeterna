using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>What a player knows about a tile.</summary>
public enum Visibility : byte
{
    /// <summary>Never seen — completely black.</summary>
    Unexplored = 0,

    /// <summary>Seen at some point, but nobody nearby right now — dimmed.</summary>
    Explored = 1,

    /// <summary>Currently in sight of one of your own entities.</summary>
    Visible = 2,
}

/// <summary>
/// The fog of war for exactly one player.
/// </summary>
/// <remarks>
/// Two separate layers: <see cref="Visibility.Explored"/> is never reset —
/// what has been seen stays on the map — while visibility starts from scratch on
/// every rebuild. The expected behaviour follows from that on its own: abandoned
/// enemy buildings stay standing as a memory.
/// </remarks>
public sealed class VisionGrid
{
    private readonly Visibility[] _cells;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Increments whenever something changed — otherwise the view would upload needlessly.</summary>
    public int Revision { get; private set; }

    public VisionGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new Visibility[width * height];
    }

    public Visibility Get(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height ? _cells[y * Width + x] : Visibility.Unexplored;

    public bool IsVisible(int x, int y) => Get(x, y) == Visibility.Visible;

    public bool IsExplored(int x, int y) => Get(x, y) != Visibility.Unexplored;

    public bool IsVisible(NavGrid grid, Vector2 world)
    {
        Vector2I cell = grid.WorldToCell(world);
        return IsVisible(cell.X, cell.Y);
    }

    /// <summary>Demotes every visible tile back to "explored", before the new build-up.</summary>
    public void BeginRebuild()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == Visibility.Visible) _cells[i] = Visibility.Explored;
        }
    }

    /// <summary>Reveals a circle around an entity.</summary>
    public void Reveal(NavGrid grid, Vector2 world, float radiusMeters)
    {
        Vector2I centre = grid.WorldToCell(world);
        int radius = Mathf.CeilToInt(radiusMeters / NavGrid.CellSize);
        int radiusSquared = radius * radius;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int y = centre.Y + dy;
            if (y < 0 || y >= Height) continue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radiusSquared) continue;

                int x = centre.X + dx;
                if (x < 0 || x >= Width) continue;

                _cells[y * Width + x] = Visibility.Visible;
            }
        }
    }

    public void EndRebuild() => Revision++;

    /// <summary>Reveals the whole map — for debugging and later for spectators.</summary>
    public void RevealAll()
    {
        for (int i = 0; i < _cells.Length; i++) _cells[i] = Visibility.Visible;
        Revision++;
    }
}

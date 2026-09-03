using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Map;

/// <summary>Was ein Spieler von einer Kachel weiss.</summary>
public enum Visibility : byte
{
    /// <summary>Nie gesehen — komplett schwarz.</summary>
    Unexplored = 0,

    /// <summary>Schon einmal gesehen, aktuell aber niemand in der Naehe — abgedunkelt.</summary>
    Explored = 1,

    /// <summary>Gerade im Blickfeld einer eigenen Entity.</summary>
    Visible = 2,
}

/// <summary>
/// Der Nebel des Krieges fuer genau einen Spieler.
/// </summary>
/// <remarks>
/// Zwei getrennte Ebenen: <see cref="Visibility.Explored"/> wird nie zurueckgesetzt —
/// einmal Gesehenes bleibt auf der Karte —, waehrend die Sichtbarkeit bei jeder
/// Neuberechnung von vorn beginnt. Daraus ergibt sich von selbst das erwartete
/// Verhalten, dass verlassene Gegnergebaeude als Erinnerung stehen bleiben.
/// </remarks>
public sealed class VisionGrid
{
    private readonly Visibility[] _cells;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Zaehlt hoch, sobald sich etwas geaendert hat — die Darstellung spart sich sonst das Hochladen.</summary>
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

    /// <summary>Stuft alle sichtbaren Kacheln auf "erkundet" zurueck, vor dem neuen Aufbau.</summary>
    public void BeginRebuild()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == Visibility.Visible) _cells[i] = Visibility.Explored;
        }
    }

    /// <summary>Deckt einen Kreis um eine Entity auf.</summary>
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

    /// <summary>Deckt die ganze Karte auf — fuer Debug und spaeter fuer Zuschauer.</summary>
    public void RevealAll()
    {
        for (int i = 0; i < _cells.Length; i++) _cells[i] = Visibility.Visible;
        Revision++;
    }
}

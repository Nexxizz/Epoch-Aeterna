using System.Collections.Generic;
using Godot;

namespace EpochAeterna.Core.Pathfinding;

/// <summary>
/// A* auf dem Navigationsgitter, mit anschliessender Pfadglaettung.
/// </summary>
/// <remarks>
/// Die Arbeitsfelder werden einmal angelegt und pro Suche nur ueber einen
/// Generationszaehler entwertet, statt sie jedes Mal zu leeren. Bei 128x128 Kacheln
/// spart das den groessten Einzelposten der Suche.
///
/// Nicht threadsicher — pro Simulation genuegt eine Instanz, weil die Suche
/// ohnehin im Tick laeuft.
/// </remarks>
public sealed class AStarPathfinder
{
    private const float StraightCost = 1f;
    private const float DiagonalCost = 1.41421356f;

    private readonly NavGrid _grid;

    private readonly float[] _gScore;
    private readonly int[] _cameFrom;
    private readonly int[] _visitedGeneration;
    private readonly bool[] _closed;
    private readonly BinaryHeap _open;

    private int _generation;

    /// <summary>Obergrenze expandierter Knoten je Suche — verhindert Ausreisser bei unloesbaren Zielen.</summary>
    public int MaxExpansions { get; set; } = 6000;

    /// <summary>Wie viele Knoten die letzte Suche geoeffnet hat, fuers Profiling.</summary>
    public int LastExpansions { get; private set; }

    public AStarPathfinder(NavGrid grid)
    {
        _grid = grid;
        int size = grid.Width * grid.Height;
        _gScore = new float[size];
        _cameFrom = new int[size];
        _visitedGeneration = new int[size];
        _closed = new bool[size];
        _open = new BinaryHeap(size);
    }

    /// <summary>
    /// Sucht einen Weg und schreibt geglaettete Wegpunkte in <paramref name="result"/>.
    /// </summary>
    /// <returns>false, wenn kein Weg existiert; <paramref name="result"/> ist dann leer.</returns>
    public bool TryFindPath(Vector2 fromWorld, Vector2 toWorld, int clearance, List<Vector2> result)
    {
        result.Clear();

        Vector2I start = _grid.ClampCell(_grid.WorldToCell(fromWorld));
        Vector2I goal = _grid.ClampCell(_grid.WorldToCell(toWorld));

        // Auf ein Hindernis geklickt: den naechsten freien Platz daneben nehmen,
        // statt den Befehl wortlos zu verwerfen.
        if (!_grid.IsPassable(goal.X, goal.Y, clearance))
        {
            goal = _grid.FindNearestPassable(goal, clearance);
            if (!_grid.IsPassable(goal.X, goal.Y, clearance)) return false;
        }

        if (start == goal)
        {
            result.Add(toWorld);
            return true;
        }

        int startIndex = start.Y * _grid.Width + start.X;
        int goalIndex = goal.Y * _grid.Width + goal.X;

        _generation++;
        _open.Clear();
        LastExpansions = 0;

        Touch(startIndex);
        _gScore[startIndex] = 0f;
        _cameFrom[startIndex] = -1;
        _open.Push(startIndex, Heuristic(start, goal));

        bool found = false;

        while (_open.Count > 0)
        {
            int current = _open.Pop();
            if (_closed[current]) continue;
            _closed[current] = true;

            if (current == goalIndex)
            {
                found = true;
                break;
            }

            if (++LastExpansions > MaxExpansions) break;

            int cx = current % _grid.Width;
            int cy = current / _grid.Width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!_grid.IsPassable(nx, ny, clearance)) continue;

                    // Diagonalen nur, wenn beide angrenzenden Geraden frei sind —
                    // sonst schneiden Einheiten durch Hausecken.
                    if (dx != 0 && dy != 0 &&
                        (!_grid.IsPassable(cx + dx, cy, clearance) ||
                         !_grid.IsPassable(cx, cy + dy, clearance)))
                    {
                        continue;
                    }

                    int neighbour = ny * _grid.Width + nx;
                    if (_closed[neighbour] && _visitedGeneration[neighbour] == _generation) continue;

                    float step = dx != 0 && dy != 0 ? DiagonalCost : StraightCost;
                    float tentative = _gScore[current] + step + SlopePenalty(cx, cy, nx, ny);

                    Touch(neighbour);
                    if (tentative >= _gScore[neighbour]) continue;

                    _gScore[neighbour] = tentative;
                    _cameFrom[neighbour] = current;
                    _open.Push(neighbour, tentative + Heuristic(new Vector2I(nx, ny), goal));
                }
            }
        }

        if (!found) return false;

        BuildPath(goalIndex, result);
        Smooth(result, clearance);
        return result.Count > 0;
    }

    /// <summary>Setzt einen Knoten auf Startwerte, sofern er in dieser Suche noch nicht besucht wurde.</summary>
    private void Touch(int index)
    {
        if (_visitedGeneration[index] == _generation) return;
        _visitedGeneration[index] = _generation;
        _gScore[index] = float.MaxValue;
        _cameFrom[index] = -1;
        _closed[index] = false;
    }

    private static float Heuristic(Vector2I a, Vector2I b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        // Oktil-Distanz: exakt fuer 8-Richtungs-Bewegung, daher zulaessig und praezise.
        return StraightCost * (dx + dy) + (DiagonalCost - 2f * StraightCost) * Mathf.Min(dx, dy);
    }

    /// <summary>Steigungen verteuern, damit Einheiten flache Umwege bevorzugen.</summary>
    private float SlopePenalty(int fromX, int fromY, int toX, int toY)
    {
        float delta = Mathf.Abs(_grid.CellSlope(toX, toY) - _grid.CellSlope(fromX, fromY));
        return delta * 0.5f;
    }

    private void BuildPath(int goalIndex, List<Vector2> result)
    {
        for (int index = goalIndex; index != -1; index = _cameFrom[index])
        {
            result.Add(_grid.CellToWorld(index % _grid.Width, index / _grid.Width));
        }
        result.Reverse();
    }

    /// <summary>
    /// String-Pulling: Zwischenpunkte entfallen, solange die direkte Sicht frei bleibt.
    /// Aus dem Treppenmuster des Gitters werden dadurch lange gerade Strecken.
    /// </summary>
    private void Smooth(List<Vector2> path, int clearance)
    {
        if (path.Count <= 2) return;

        var smoothed = new List<Vector2>(path.Count) { path[0] };
        int anchor = 0;

        for (int probe = 2; probe < path.Count; probe++)
        {
            if (HasLineOfSight(path[anchor], path[probe], clearance)) continue;

            smoothed.Add(path[probe - 1]);
            anchor = probe - 1;
        }

        smoothed.Add(path[^1]);

        path.Clear();
        path.AddRange(smoothed);
    }

    /// <summary>Prueft alle beruehrten Kacheln entlang der Strecke (Supercover-Linie).</summary>
    private bool HasLineOfSight(Vector2 from, Vector2 to, int clearance)
    {
        Vector2I a = _grid.WorldToCell(from);
        Vector2I b = _grid.WorldToCell(to);

        int dx = Mathf.Abs(b.X - a.X);
        int dy = Mathf.Abs(b.Y - a.Y);
        int stepX = a.X < b.X ? 1 : -1;
        int stepY = a.Y < b.Y ? 1 : -1;

        int x = a.X;
        int y = a.Y;
        int error = dx - dy;

        while (true)
        {
            if (!_grid.IsPassable(x, y, clearance)) return false;
            if (x == b.X && y == b.Y) return true;

            int doubled = error * 2;
            if (doubled > -dy)
            {
                error -= dy;
                x += stepX;
            }
            if (doubled < dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    /// <summary>Minimaler Min-Heap ueber Knotenindizes. Vermeidet die Zuweisungen einer PriorityQueue.</summary>
    private sealed class BinaryHeap
    {
        private readonly int[] _items;
        private readonly float[] _priorities;

        public int Count { get; private set; }

        public BinaryHeap(int capacity)
        {
            // Ein Knoten kann mehrfach mit besserer Bewertung eingereiht werden.
            _items = new int[capacity * 4];
            _priorities = new float[capacity * 4];
        }

        public void Clear() => Count = 0;

        public void Push(int item, float priority)
        {
            if (Count >= _items.Length) return;

            int index = Count++;
            _items[index] = item;
            _priorities[index] = priority;

            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_priorities[parent] <= _priorities[index]) break;
                Swap(parent, index);
                index = parent;
            }
        }

        public int Pop()
        {
            int result = _items[0];
            Count--;

            if (Count > 0)
            {
                _items[0] = _items[Count];
                _priorities[0] = _priorities[Count];

                int index = 0;
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int smallest = index;

                    if (left < Count && _priorities[left] < _priorities[smallest]) smallest = left;
                    if (right < Count && _priorities[right] < _priorities[smallest]) smallest = right;
                    if (smallest == index) break;

                    Swap(smallest, index);
                    index = smallest;
                }
            }

            return result;
        }

        private void Swap(int a, int b)
        {
            (_items[a], _items[b]) = (_items[b], _items[a]);
            (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
        }
    }
}

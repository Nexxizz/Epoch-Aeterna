using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Pushes overlapping units apart.
/// </summary>
/// <remarks>
/// Deliberately not full RVO: at MVP army sizes it is enough to resolve overlaps
/// after movement. That costs almost nothing but prevents the ugliest case —
/// units standing exactly on top of one another.
///
/// Runs after the <see cref="MovementSystem"/> and therefore corrects its result
/// rather than interfering with pathfinding.
/// </remarks>
public sealed class AvoidanceSystem : ISimulationSystem
{
    /// <summary>Share of the overlap resolved per tick. 1 would oscillate.</summary>
    private const float Relaxation = 0.5f;

    /// <summary>Edge length of a hash cell in metres. Roughly twice a unit's diameter.</summary>
    private const float BucketSize = 2f;

    private readonly NavGrid _grid;
    private readonly Dictionary<long, List<Unit>> _buckets = new();

    public string Name => "Avoidance";

    public AvoidanceSystem(NavGrid grid) => _grid = grid;

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        IReadOnlyList<Unit> units = world.Entities.Units;
        if (units.Count < 2) return;

        FillBuckets(units);

        foreach (Unit unit in units)
        {
            Vector2 push = Vector2.Zero;

            foreach (Unit other in Neighbours(unit))
            {
                if (ReferenceEquals(other, unit)) continue;

                Vector2 delta = unit.Position - other.Position;
                float minDistance = unit.Radius + other.Radius;
                float distanceSquared = delta.LengthSquared();

                if (distanceSquared >= minDistance * minDistance) continue;

                // Exactly coincident: push apart deterministically, so the result does not
                // differ between runs.
                if (distanceSquared < 0.0001f)
                {
                    int sign = unit.Id.Value > other.Id.Value ? 1 : -1;
                    push += new Vector2(sign * minDistance * 0.5f, 0f);
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                push += delta / distance * (minDistance - distance) * 0.5f;
            }

            if (push == Vector2.Zero) continue;

            Vector2 target = unit.Position + push * Relaxation;
            Vector2I cell = _grid.WorldToCell(target);

            // Never push into a blocked tile — better to let them overlap.
            if (_grid.IsPassable(cell.X, cell.Y, unit.Clearance)) unit.Position = target;
        }
    }

    private void FillBuckets(IReadOnlyList<Unit> units)
    {
        foreach (List<Unit> bucket in _buckets.Values) bucket.Clear();

        foreach (Unit unit in units)
        {
            long key = BucketKey(unit.Position);
            if (!_buckets.TryGetValue(key, out List<Unit>? bucket))
            {
                bucket = new List<Unit>();
                _buckets[key] = bucket;
            }
            bucket.Add(unit);
        }
    }

    private IEnumerable<Unit> Neighbours(Unit unit)
    {
        int bx = Mathf.FloorToInt(unit.Position.X / BucketSize);
        int by = Mathf.FloorToInt(unit.Position.Y / BucketSize);

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_buckets.TryGetValue(Key(bx + dx, by + dy), out List<Unit>? bucket)) continue;
                foreach (Unit other in bucket) yield return other;
            }
        }
    }

    private static long BucketKey(Vector2 position) =>
        Key(Mathf.FloorToInt(position.X / BucketSize), Mathf.FloorToInt(position.Y / BucketSize));

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}

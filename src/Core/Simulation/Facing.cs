using Godot;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Converts between a direction on the map and an entity's rotation.
/// </summary>
/// <remarks>
/// One place for a convention that is easy to get subtly wrong and hard to spot:
/// a Godot node's forward is -Z, so rotating it by <c>theta</c> around Y gives a
/// forward of <c>(-sin, -cos)</c>. Deriving the angle therefore means negating
/// both components. Getting only one sign right mirrors the entity, which looks
/// correct along one axis and makes it walk backwards along the other.
/// </remarks>
public static class Facing
{
    /// <summary>Rotation that makes an entity face along <paramref name="direction"/>.</summary>
    public static float ToRotation(Vector2 direction) => Mathf.Atan2(-direction.X, -direction.Y);

    /// <summary>The direction an entity with this rotation is facing.</summary>
    public static Vector2 ToDirection(float rotation) =>
        new(-Mathf.Sin(rotation), -Mathf.Cos(rotation));

    /// <summary>Rotation that makes an entity at <paramref name="from"/> look at <paramref name="target"/>.</summary>
    public static float LookAt(Vector2 from, Vector2 target)
    {
        Vector2 delta = target - from;
        return delta.LengthSquared() < 0.0001f ? 0f : ToRotation(delta);
    }
}

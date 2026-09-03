using System;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Stabiler Bezeichner einer Entity. Bewusst ein Wertetyp statt einer Objektreferenz:
/// Views, Befehle und Savegames halten IDs, keine Zeiger — eine gestorbene Entity
/// laesst so keine haengenden Referenzen zurueck.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public static readonly EntityId None = new(0);

    public int Value { get; }

    public EntityId(int value) => Value = value;

    public bool IsValid => Value != 0;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => IsValid ? $"#{Value}" : "#none";

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;

    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
}

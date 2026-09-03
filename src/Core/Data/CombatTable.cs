using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// The counter matrix: damage multiplier per combination of damage type and armour class.
/// </summary>
/// <remarks>
/// A Resource rather than constants in code — an RTS gets rebalanced a hundred times
/// during development, and that should not force anyone to recompile.
///
/// The values sit flat in <see cref="Multipliers"/>, row by row per damage type:
/// Index = (int)damage * 4 + (int)armor.
///
/// The class lives in its own file because Godot can only instantiate a C# script
/// when the file name matches the class name exactly.
/// </remarks>
[GlobalClass]
public partial class CombatTable : Resource
{
    public const int ArmorClassCount = 4;
    public const int DamageTypeCount = 4;

    /// <summary>
    /// 16 values, row by row: Blunt, Pierce, Ranged, Siege — each against
    /// Civilian, Infantry, Ranged, Building.
    /// </summary>
    [Export]
    public float[] Multipliers { get; set; } = FallbackMultipliers();

    public float Get(DamageType damage, ArmorClass armor)
    {
        int index = (int)damage * ArmorClassCount + (int)armor;
        return index >= 0 && index < Multipliers.Length ? Multipliers[index] : 1f;
    }

    /// <summary>
    /// Stopgap for when no .tres was loaded. Deliberately *not* identical to the real
    /// balance values: that way the self-test notices a missing file instead of the
    /// fallback quietly covering up the failure.
    /// </summary>
    public static float[] FallbackMultipliers() => new[]
    {
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
    };
}

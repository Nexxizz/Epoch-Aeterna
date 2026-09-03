using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// Die Konter-Matrix: Schadensmultiplikator je Kombination aus Schadensart und Rüstungsklasse.
/// </summary>
/// <remarks>
/// Bewusst als Resource und nicht als Konstanten im Code — die Balance eines RTS wird
/// im Betrieb hundertfach nachjustiert, und das soll niemanden zwingen, neu zu bauen.
///
/// Die Werte liegen flach in <see cref="Multipliers"/>, zeilenweise nach Schadensart:
/// Index = (int)damage * 4 + (int)armor.
///
/// Die Klasse steht in einer eigenen Datei, weil Godot C#-Skripte nur instanziieren
/// kann, wenn der Dateiname exakt dem Klassennamen entspricht.
/// </remarks>
[GlobalClass]
public partial class CombatTable : Resource
{
    public const int ArmorClassCount = 4;
    public const int DamageTypeCount = 4;

    /// <summary>
    /// 16 Werte, zeilenweise: Blunt, Pierce, Ranged, Siege — je gegen
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
    /// Notbehelf, falls keine .tres geladen wurde. Bewusst *nicht* identisch mit den
    /// echten Balancewerten: So faellt im Selbsttest auf, wenn die Datei fehlt,
    /// statt dass der Fallback den Ausfall verdeckt.
    /// </summary>
    public static float[] FallbackMultipliers() => new[]
    {
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
    };
}

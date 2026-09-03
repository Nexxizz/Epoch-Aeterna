using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Bauplan eines Ressourcenvorkommens: Baum, Steinbruch, Goldader, Beerenbusch.</summary>
[GlobalClass]
public partial class ResourceNodeDefinition : EntityDefinition
{
    [Export] public ResourceType Resource { get; set; } = ResourceType.Wood;

    /// <summary>Wie viel insgesamt drin steckt, bevor der Knoten verschwindet.</summary>
    [Export] public int TotalAmount { get; set; } = 120;

    /// <summary>Wie viele Siedler gleichzeitig hier arbeiten koennen.</summary>
    [Export] public int MaxGatherers { get; set; } = 4;

    /// <summary>Faktor auf die Sammelrate des Siedlers — Gold gibt sich zaeher als Holz.</summary>
    [Export] public float GatherRateFactor { get; set; } = 1f;

    /// <summary>Sperrt die Kachel fuer die Wegfindung. Beeren und Farmen tun das nicht.</summary>
    [Export] public bool BlocksMovement { get; set; } = true;
}

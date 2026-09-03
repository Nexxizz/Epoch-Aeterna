using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>Was ein Spieler im Lauf der Partie zustande gebracht hat — fuer den Endbildschirm.</summary>
public sealed class MatchStats
{
    private readonly int[] _gathered = new int[ResourceTypes.Count];

    public int UnitsTrained { get; set; }
    public int BuildingsCompleted { get; set; }
    public int EnemiesKilled { get; set; }
    public int UnitsLost { get; set; }
    public int BuildingsLost { get; set; }

    public int GetGathered(ResourceType type) => _gathered[(int)type];

    public void AddGathered(ResourceType type, int amount) => _gathered[(int)type] += amount;

    public int TotalGathered
    {
        get
        {
            int total = 0;
            foreach (ResourceType type in ResourceTypes.All) total += _gathered[(int)type];
            return total;
        }
    }
}

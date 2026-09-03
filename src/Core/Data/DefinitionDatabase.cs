using System.Collections.Generic;
using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// Laedt alle Definitions-Resources aus <c>res://data/</c> und macht sie ueber ihre
/// String-ID zugaenglich.
/// </summary>
/// <remarks>
/// Kernstueck des data-driven Ansatzes: eine neue Einheit oder ein neues Gebaeude
/// braucht nur eine .tres in diesem Ordner — kein C#-Code, keine Registrierung.
/// </remarks>
public sealed class DefinitionDatabase
{
    public const string DataRoot = "res://data";

    private readonly Dictionary<string, UnitDefinition> _units = new();
    private readonly Dictionary<string, BuildingDefinition> _buildings = new();
    private readonly Dictionary<string, AgeDefinition> _ages = new();
    private readonly Dictionary<string, ResourceNodeDefinition> _resourceNodes = new();
    private readonly List<AgeDefinition> _agesByIndex = new();

    public IReadOnlyDictionary<string, UnitDefinition> Units => _units;
    public IReadOnlyDictionary<string, BuildingDefinition> Buildings => _buildings;
    public IReadOnlyList<AgeDefinition> Ages => _agesByIndex;
    public IReadOnlyDictionary<string, ResourceNodeDefinition> ResourceNodes => _resourceNodes;

    /// <summary>Konter-Matrix. Bleibt beim Standard, wenn keine .tres gefunden wurde.</summary>
    public CombatTable Combat { get; private set; } = new();

    public int HighestAgeIndex => _agesByIndex.Count - 1;

    public void LoadAll()
    {
        _units.Clear();
        _buildings.Clear();
        _ages.Clear();
        _agesByIndex.Clear();
        _resourceNodes.Clear();

        ScanDirectory(DataRoot);

        _agesByIndex.AddRange(_ages.Values);
        _agesByIndex.Sort(static (a, b) => a.Index.CompareTo(b.Index));

        GD.Print($"[Definitions] {_units.Count} Einheiten, {_buildings.Count} Gebaeude, " +
                 $"{_resourceNodes.Count} Vorkommen, {_agesByIndex.Count} Zeitalter geladen.");
    }

    private void ScanDirectory(string path)
    {
        using DirAccess? dir = DirAccess.Open(path);
        if (dir is null)
        {
            GD.PushWarning($"[Definitions] Ordner nicht lesbar: {path}");
            return;
        }

        dir.ListDirBegin();
        for (string name = dir.GetNext(); name != string.Empty; name = dir.GetNext())
        {
            string full = path.PathJoin(name);

            if (dir.CurrentIsDir())
            {
                if (!name.StartsWith('.')) ScanDirectory(full);
                continue;
            }

            // Im exportierten Build heissen Resources "<name>.tres.remap".
            if (name.EndsWith(".remap")) full = full[..^".remap".Length];
            else if (!name.EndsWith(".tres")) continue;

            Register(full);
        }
        dir.ListDirEnd();
    }

    private void Register(string resPath)
    {
        var resource = ResourceLoader.Load<Resource>(resPath);
        switch (resource)
        {
            case UnitDefinition unit:
                if (!Validate(unit.Id, resPath, _units.ContainsKey(unit.Id))) return;
                _units[unit.Id] = unit;
                break;

            case BuildingDefinition building:
                if (!Validate(building.Id, resPath, _buildings.ContainsKey(building.Id))) return;
                _buildings[building.Id] = building;
                break;

            case ResourceNodeDefinition node:
                if (!Validate(node.Id, resPath, _resourceNodes.ContainsKey(node.Id))) return;
                _resourceNodes[node.Id] = node;
                break;

            case AgeDefinition age:
                if (!Validate(age.Id, resPath, _ages.ContainsKey(age.Id))) return;
                _ages[age.Id] = age;
                break;

            case CombatTable table:
                Combat = table;
                break;
        }
    }

    private static bool Validate(string id, string resPath, bool duplicate)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            GD.PushError($"[Definitions] Definition ohne Id uebersprungen: {resPath}");
            return false;
        }
        if (duplicate)
        {
            GD.PushError($"[Definitions] Doppelte Id '{id}' — {resPath} wird ignoriert.");
            return false;
        }
        return true;
    }

    public UnitDefinition? GetUnit(string id) => _units.GetValueOrDefault(id);

    public BuildingDefinition? GetBuilding(string id) => _buildings.GetValueOrDefault(id);

    public ResourceNodeDefinition? GetResourceNode(string id) => _resourceNodes.GetValueOrDefault(id);

    public AgeDefinition? GetAge(int index) =>
        index >= 0 && index < _agesByIndex.Count ? _agesByIndex[index] : null;

    /// <summary>Definition beliebigen Typs — fuer Code, der Einheit und Gebaeude gleich behandelt.</summary>
    public EntityDefinition? GetEntity(string id)
    {
        if (_units.TryGetValue(id, out UnitDefinition? unit)) return unit;
        if (_buildings.TryGetValue(id, out BuildingDefinition? building)) return building;
        if (_resourceNodes.TryGetValue(id, out ResourceNodeDefinition? node)) return node;
        return null;
    }
}

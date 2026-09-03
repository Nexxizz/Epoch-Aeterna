using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Zeichnet Grasbueschel und lose Steine als <see cref="MultiMeshInstance3D"/>.
/// </summary>
/// <remarks>
/// Beiwerk ohne Spielwirkung — deshalb keine Entities. Eine Karte traegt einige
/// tausend davon; als einzelne Nodes waere das ebenso viele Draw-Calls, als MultiMesh
/// ist es einer pro Typ.
/// </remarks>
public sealed partial class DecorationRenderer : Node3D
{
    private static readonly string ShaderPath = "res://assets/shaders/decoration.gdshader";

    private readonly System.Collections.Generic.List<ShaderMaterial> _materials = new();

    /// <summary>Alle Beiwerk-Materialien. Der Nebel des Krieges haengt seine Textur hier ein.</summary>
    public System.Collections.Generic.IReadOnlyList<ShaderMaterial> Materials => _materials;

    public void Build(IReadOnlyList<Decoration> decorations, NavGrid grid)
    {
        var byType = new Dictionary<DecorationType, List<Decoration>>();

        foreach (Decoration decoration in decorations)
        {
            if (!byType.TryGetValue(decoration.Type, out List<Decoration>? list))
            {
                list = new List<Decoration>();
                byType[decoration.Type] = list;
            }
            list.Add(decoration);
        }

        foreach ((DecorationType type, List<Decoration> list) in byType) AddBatch(type, list, grid);
    }

    private void AddBatch(DecorationType type, List<Decoration> items, NavGrid grid)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = BuildMesh(type),
            InstanceCount = items.Count,
        };

        for (int i = 0; i < items.Count; i++)
        {
            Decoration item = items[i];
            float height = grid.SampleHeight(item.Position);

            var basis = Basis.FromEuler(new Vector3(0f, item.Rotation, 0f)).Scaled(Vector3.One * item.Scale);

            // Godots Grundkoerper sitzen mittig auf ihrem Ursprung, das Gelaende ist
            // aber die Standflaeche — daher um die halbe Hoehe anheben.
            float lift = GroundOffset(type) * item.Scale;

            multiMesh.SetInstanceTransform(i, new Transform3D(basis,
                new Vector3(item.Position.X, height + lift, item.Position.Y)));
        }

        ShaderMaterial material = BuildMaterial(type, grid);
        _materials.Add(material);

        AddChild(new MultiMeshInstance3D
        {
            Name = $"Decoration_{type}",
            Multimesh = multiMesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private static float GroundOffset(DecorationType type) => type switch
    {
        DecorationType.GrassTuft => 0.18f,
        _ => 0.08f,
    };

    /// <summary>Platzhaltergeometrie bis Phase 5.</summary>
    private static Mesh BuildMesh(DecorationType type) => type switch
    {
        DecorationType.GrassTuft => new PrismMesh { Size = new Vector3(0.5f, 0.45f, 0.06f) },
        _ => new SphereMesh { Radius = 0.22f, Height = 0.3f, RadialSegments = 5, Rings = 2 },
    };

    private static ShaderMaterial BuildMaterial(DecorationType type, NavGrid grid)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };

        material.SetShaderParameter("albedo_color", type == DecorationType.GrassTuft
            ? new Color(0.26f, 0.36f, 0.17f)
            : new Color(0.45f, 0.44f, 0.41f));

        material.SetShaderParameter("map_size", new Vector2(grid.WorldWidth, grid.WorldHeight));
        return material;
    }
}

using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Zeichnet Baeume, Felsen und Buesche als <see cref="MultiMeshInstance3D"/>.
/// </summary>
/// <remarks>
/// Eine Karte traegt schnell einige tausend Streuobjekte. Als einzelne Nodes waere das
/// ebenso viele Draw-Calls; als MultiMesh ist es einer pro Typ. Die Platzhalter-Meshes
/// hier werden in Phase 5 durch die Blender-Modelle ersetzt — der Aufbau bleibt gleich.
/// </remarks>
public sealed partial class DecorationRenderer : Node3D
{
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

        AddChild(new MultiMeshInstance3D
        {
            Name = $"Decoration_{type}",
            Multimesh = multiMesh,
            MaterialOverride = BuildMaterial(type),
        });
    }

    /// <summary>Anhebung ueber den Boden, damit das Objekt aufsitzt statt halb zu versinken.</summary>
    private static float GroundOffset(DecorationType type) => type switch
    {
        DecorationType.Tree => 3f,
        DecorationType.Rock => 0.5f,
        _ => 0.35f,
    };

    /// <summary>Platzhaltergeometrie bis Phase 5 — grob die Silhouette, damit die Karte lesbar ist.</summary>
    private static Mesh BuildMesh(DecorationType type) => type switch
    {
        DecorationType.Tree => new CylinderMesh
        {
            TopRadius = 0.05f,
            BottomRadius = 1.5f,
            Height = 6f,
            RadialSegments = 6,
            Rings = 1,
        },
        DecorationType.Rock => new SphereMesh
        {
            Radius = 1.1f,
            Height = 1.6f,
            RadialSegments = 6,
            Rings = 3,
        },
        _ => new SphereMesh
        {
            Radius = 0.55f,
            Height = 0.9f,
            RadialSegments = 5,
            Rings = 2,
        },
    };

    private static StandardMaterial3D BuildMaterial(DecorationType type) => new()
    {
        AlbedoColor = type switch
        {
            DecorationType.Tree => new Color(0.16f, 0.31f, 0.15f),
            DecorationType.Rock => new Color(0.44f, 0.43f, 0.41f),
            _ => new Color(0.27f, 0.38f, 0.19f),
        },
        Roughness = 0.9f,
    };
}

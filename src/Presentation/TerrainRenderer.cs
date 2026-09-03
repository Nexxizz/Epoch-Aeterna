using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Builds the visible terrain mesh from the heights of the <see cref="NavGrid"/>.
/// </summary>
/// <remarks>
/// Looks and walkability come from the same source — a slope that looks steep in the
/// image is steep for pathfinding too.
///
/// The mesh is split into chunks of 16x16 cells, so Godot can discard whole blocks
/// outside the view instead of drawing one enormous surface.
/// </remarks>
public sealed partial class TerrainRenderer : Node3D
{
    private const int ChunkCells = 16;

    private static readonly string ShaderPath = "res://assets/shaders/terrain.gdshader";

    /// <summary>The terrain material. The fog of war hooks its texture in here.</summary>
    public ShaderMaterial? Material { get; private set; }

    public void Build(NavGrid grid)
    {
        ShaderMaterial material = CreateMaterial(grid);
        Material = material;

        int chunksX = Mathf.CeilToInt(grid.Width / (float)ChunkCells);
        int chunksY = Mathf.CeilToInt(grid.Height / (float)ChunkCells);

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                var chunk = new MeshInstance3D
                {
                    Name = $"Chunk_{cx}_{cy}",
                    Mesh = BuildChunkMesh(grid, cx * ChunkCells, cy * ChunkCells),
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(chunk);
            }
        }
    }

    private static ArrayMesh BuildChunkMesh(NavGrid grid, int originX, int originY)
    {
        int cellsX = Mathf.Min(ChunkCells, grid.Width - originX);
        int cellsY = Mathf.Min(ChunkCells, grid.Height - originY);

        int verticesX = cellsX + 1;
        int verticesY = cellsY + 1;

        var vertices = new Vector3[verticesX * verticesY];
        var normals = new Vector3[verticesX * verticesY];
        var uvs = new Vector2[verticesX * verticesY];

        float halfWidth = grid.WorldWidth * 0.5f;
        float halfHeight = grid.WorldHeight * 0.5f;

        for (int y = 0; y < verticesY; y++)
        {
            for (int x = 0; x < verticesX; x++)
            {
                int gx = originX + x;
                int gy = originY + y;
                int index = y * verticesX + x;

                vertices[index] = new Vector3(
                    gx * NavGrid.CellSize - halfWidth,
                    grid.GetCornerHeight(gx, gy),
                    gy * NavGrid.CellSize - halfHeight);

                normals[index] = CornerNormal(grid, gx, gy);

                // UV across the whole map, so the splat map lines up across all chunks.
                uvs[index] = new Vector2(gx / (float)grid.Width, gy / (float)grid.Height);
            }
        }

        var indices = new int[cellsX * cellsY * 6];
        int write = 0;

        for (int y = 0; y < cellsY; y++)
        {
            for (int x = 0; x < cellsX; x++)
            {
                int topLeft = y * verticesX + x;
                int topRight = topLeft + 1;
                int bottomLeft = topLeft + verticesX;
                int bottomRight = bottomLeft + 1;

                // Godot expects the front face wound clockwise. With the reverse order the
                // terrain disappears entirely, because it gets discarded as a back
                // face.
                indices[write++] = topLeft;
                indices[write++] = topRight;
                indices[write++] = bottomLeft;

                indices[write++] = topRight;
                indices[write++] = bottomRight;
                indices[write++] = bottomLeft;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Normal from the height gradient of the neighbouring corners (central difference).</summary>
    private static Vector3 CornerNormal(NavGrid grid, int x, int y)
    {
        float left = grid.GetCornerHeight(x - 1, y);
        float right = grid.GetCornerHeight(x + 1, y);
        float back = grid.GetCornerHeight(x, y - 1);
        float front = grid.GetCornerHeight(x, y + 1);

        return new Vector3(
            left - right,
            2f * NavGrid.CellSize,
            back - front).Normalized();
    }

    private static ShaderMaterial CreateMaterial(NavGrid grid)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("splat_map", ImageTexture.CreateFromImage(BuildSplatMap(grid)));
        return material;
    }

    /// <summary>
    /// Builds the weight texture: height and steepness decide which ground shows
    /// through where. Flat and low becomes grass, steep becomes rock, dirt in between.
    /// </summary>
    private static Image BuildSplatMap(NavGrid grid)
    {
        var image = Image.CreateEmpty(grid.Width, grid.Height, false, Image.Format.Rgba8);

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                float slope = grid.CellSlope(x, y);
                float height = grid.GetCornerHeight(x, y);

                float rock = Mathf.Clamp((slope - 0.9f) / 1.6f, 0f, 1f);
                float dirt = Mathf.Clamp((slope - 0.45f) / 1.2f, 0f, 1f) * (1f - rock);

                // Sand only in the deepest hollows. The heights vary around zero, so the
                // threshold has to sit well below the mean — otherwise half the map would
                // be sand.
                float sand = Mathf.Clamp((-7f - height) / 2f, 0f, 1f) * (1f - rock - dirt);

                float grass = Mathf.Max(0f, 1f - rock - dirt - sand);

                image.SetPixel(x, y, new Color(grass, dirt, rock, sand));
            }
        }

        return image;
    }
}

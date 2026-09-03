using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Baut den sichtbaren Gelaende-Mesh aus den Hoehen des <see cref="NavGrid"/>.
/// </summary>
/// <remarks>
/// Optik und Begehbarkeit stammen aus derselben Quelle — ein Hang, der im Bild steil
/// aussieht, ist auch fuer die Wegfindung steil.
///
/// Der Mesh wird in Kacheln von 16x16 Zellen zerlegt, damit Godot ausserhalb des
/// Sichtfelds ganze Bloecke verwerfen kann, statt eine einzige riesige Flaeche zu zeichnen.
/// </remarks>
public sealed partial class TerrainRenderer : Node3D
{
    private const int ChunkCells = 16;

    private static readonly string ShaderPath = "res://assets/shaders/terrain.gdshader";

    public void Build(NavGrid grid)
    {
        ShaderMaterial material = CreateMaterial(grid);

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

                // UV ueber die gesamte Karte, damit die Splat-Map ueber alle Bloecke passt.
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

                // Godot erwartet die Vorderseite im Uhrzeigersinn. Bei umgekehrter
                // Reihenfolge verschwindet das Gelaende komplett, weil es als
                // Rueckseite weggeworfen wird.
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

    /// <summary>Normale aus dem Hoehengefaelle der Nachbarecken (zentrale Differenz).</summary>
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
    /// Erzeugt die Gewichtstextur: Hoehe und Steilheit entscheiden, welcher Untergrund
    /// wo durchkommt. Flach und tief wird Gras, steil wird Fels, dazwischen Erde.
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

                // Sand nur in den tiefsten Senken. Die Hoehen schwanken um null,
                // daher muss die Schwelle deutlich unter dem Mittel liegen — sonst
                // waere die halbe Karte Sandflaeche.
                float sand = Mathf.Clamp((-7f - height) / 2f, 0f, 1f) * (1f - rock - dirt);

                float grass = Mathf.Max(0f, 1f - rock - dirt - sand);

                image.SetPixel(x, y, new Color(grass, dirt, rock, sand));
            }
        }

        return image;
    }
}

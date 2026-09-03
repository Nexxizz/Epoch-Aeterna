using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Der Nebel des Krieges: dunkelt unerkundetes Gelaende ab und blendet aus, was der
/// Spieler gerade nicht sieht.
/// </summary>
/// <remarks>
/// Die Verdunkelung passiert im Gelaende-Shader, nicht ueber eine eigene Ebene.
/// Eine schwebende Flaeche muesste in der Hoehe ueber jedem Huegel liegen, brauchte
/// abgeschalteten Tiefentest und verdeckt dann alles andere gleich mit — ein
/// Texturzugriff im ohnehin vorhandenen Shader kostet dagegen nichts.
///
/// Gegnerische *Gebaeude* bleiben stehen, sobald sie einmal gesehen wurden: Man
/// erinnert sich, wo die Basis lag, sieht aber nicht, was dort gerade passiert.
/// </remarks>
public sealed partial class FogOfWarRenderer : Node
{
    /// <summary>Wie oft die Nebeltextur hochgeladen wird — deutlich seltener als die Bildrate reicht.</summary>
    private const float UploadInterval = 0.2f;

    private ImageTexture? _texture;
    private Image? _image;

    private Player? _viewer;
    private NavGrid? _grid;
    private ViewManager? _views;

    private int _lastRevision = -1;
    private float _timeSinceUpload;

    public void Attach(Player viewer, NavGrid grid, ViewManager views,
        TerrainRenderer terrain, DecorationRenderer decorations)
    {
        _viewer = viewer;
        _grid = grid;
        _views = views;

        _image = Image.CreateEmpty(grid.Width, grid.Height, false, Image.Format.Rgb8);
        _texture = ImageTexture.CreateFromImage(_image);

        terrain.Material?.SetShaderParameter("fog_map", _texture);

        foreach (ShaderMaterial material in decorations.Materials)
        {
            material.SetShaderParameter("fog_map", _texture);
        }
        Upload();
    }

    public override void _Process(double delta)
    {
        if (_viewer?.Vision is null) return;

        HideUnseenEntities();

        _timeSinceUpload += (float)delta;
        if (_timeSinceUpload < UploadInterval) return;
        _timeSinceUpload = 0f;

        if (_viewer.Vision.Revision == _lastRevision) return;
        Upload();
    }

    private void Upload()
    {
        if (_image is null || _texture is null || _viewer?.Vision is null) return;

        VisionGrid vision = _viewer.Vision;

        for (int y = 0; y < vision.Height; y++)
        {
            for (int x = 0; x < vision.Width; x++)
            {
                float value = vision.Get(x, y) switch
                {
                    Visibility.Visible => 1f,
                    Visibility.Explored => 0.45f,
                    _ => 0f,
                };
                _image.SetPixel(x, y, new Color(value, value, value));
            }
        }

        _texture.Update(_image);
        _lastRevision = vision.Revision;
    }

    /// <summary>
    /// Blendet aus, was der Spieler gerade nicht sieht. Eigene Entities bleiben immer
    /// sichtbar, gegnerische Gebaeude und Vorkommen bleiben als Erinnerung stehen.
    /// </summary>
    private void HideUnseenEntities()
    {
        if (_views is null || _grid is null || _viewer?.Vision is null) return;

        foreach ((Entity entity, EntityView view) in _views.Views)
        {
            if (entity.OwnerId == _viewer.Id)
            {
                view.Visible = true;
                continue;
            }

            Vector2I cell = _grid.WorldToCell(entity.Position);
            bool visible = _viewer.Vision.IsVisible(cell.X, cell.Y);

            view.Visible = entity switch
            {
                Building or ResourceNode => visible || _viewer.Vision.IsExplored(cell.X, cell.Y),
                _ => visible,
            };
        }
    }
}

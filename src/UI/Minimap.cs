using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.UI;

/// <summary>
/// The minimap in the bottom-left corner: terrain, fog of war, entities, camera frame.
/// </summary>
/// <remarks>
/// The map is drawn into a texture of exactly one pixel per navigation cell and then
/// scaled up. Anything else would mean drawing several hundred separate markers every
/// frame; this way a redraw is one pass over a byte array.
///
/// The terrain colour is baked once — it never changes. Only the fog and the entities
/// are written on top of it, ten times a second, which is far below the eye's
/// threshold for a strategic overview but a sixth of the rendering cost.
/// </remarks>
public sealed partial class Minimap : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly MinimapView _view = new();

    public override void _Ready()
    {
        Layer = 3;

        _panel.Name = "Minimap";
        HudTheme.AnchorBottom(_panel, HudTheme.Margin, HudTheme.Margin + HudTheme.MinimapSize,
            HudTheme.MinimapSize);
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;

        // Less padding than the other panels — every pixel here is map.
        StyleBoxFlat style = HudTheme.PanelStyle();
        style.SetContentMarginAll(4f);
        _panel.AddThemeStyleboxOverride("panel", style);

        AddChild(_panel);
        _panel.AddChild(_view);
    }

    public void Attach(SimulationWorld world, NavGrid grid, RtsCamera camera, int localPlayerId) =>
        _view.Attach(world, grid, camera, localPlayerId);

    /// <summary>Marks a spot that needs attention — an attack, for instance.</summary>
    public void Ping(Vector2 world, Color color) => _view.Ping(world, color);
}

/// <summary>The drawing surface of the minimap. Separate, because it has to be a Control.</summary>
public sealed partial class MinimapView : Control
{
    /// <summary>Seconds between two texture updates.</summary>
    private const float RefreshInterval = 0.1f;

    private const float PingSeconds = 3f;

    private static readonly Color Grass = new(0.29f, 0.40f, 0.20f);
    private static readonly Color Dirt = new(0.36f, 0.29f, 0.20f);
    private static readonly Color Rock = new(0.42f, 0.41f, 0.39f);
    private static readonly Color Sand = new(0.61f, 0.56f, 0.40f);

    private static readonly Color[] ResourceColors =
    {
        new(0.78f, 0.32f, 0.36f), // Beeren
        new(0.20f, 0.52f, 0.24f), // Baum
        new(0.66f, 0.66f, 0.70f), // Stein
        new(0.92f, 0.76f, 0.28f), // Gold
    };

    private readonly System.Collections.Generic.List<(Vector2 World, Color Color, float Left)> _pings = new();

    private SimulationWorld? _world;
    private NavGrid? _grid;
    private RtsCamera? _camera;
    private Player? _player;

    private ImageTexture? _texture;
    private Image? _image;
    private byte[] _terrain = System.Array.Empty<byte>();
    private byte[] _pixels = System.Array.Empty<byte>();

    private float _sinceRefresh = RefreshInterval;
    private bool _dragging;

    public override void _Ready()
    {
        // One cell is one texel — smoothing would only blur the dots.
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Klick oder Ziehen — Kamera dorthin bewegen";
    }

    public void Attach(SimulationWorld world, NavGrid grid, RtsCamera camera, int localPlayerId)
    {
        _world = world;
        _grid = grid;
        _camera = camera;
        _player = world.GetPlayer(localPlayerId);

        BakeTerrain(grid);

        _image = Image.CreateFromData(grid.Width, grid.Height, false, Image.Format.Rgba8, _pixels);
        _texture = ImageTexture.CreateFromImage(_image);
        _sinceRefresh = RefreshInterval;
    }

    public void Ping(Vector2 world, Color color) => _pings.Add((world, color, PingSeconds));

    public override void _Process(double delta)
    {
        if (_world is null) return;

        for (int i = _pings.Count - 1; i >= 0; i--)
        {
            (Vector2 world, Color color, float left) = _pings[i];
            left -= (float)delta;
            if (left <= 0f) _pings.RemoveAt(i);
            else _pings[i] = (world, color, left);
        }

        _sinceRefresh += (float)delta;
        if (_sinceRefresh >= RefreshInterval)
        {
            _sinceRefresh = 0f;
            RedrawTexture();
        }

        // The camera frame follows every frame, so panning does not stutter.
        QueueRedraw();
    }

    // --- Terrain ---------------------------------------------------------

    /// <summary>
    /// Ground colour per cell, from the same slope and height rule the terrain shader
    /// splats with — so the minimap and the world agree on where rock begins.
    /// </summary>
    private void BakeTerrain(NavGrid grid)
    {
        _terrain = new byte[grid.Width * grid.Height * 4];
        _pixels = new byte[_terrain.Length];

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                float slope = grid.CellSlope(x, y);
                float height = grid.GetCornerHeight(x, y);

                float rock = Mathf.Clamp((slope - 0.9f) / 1.6f, 0f, 1f);
                float dirt = Mathf.Clamp((slope - 0.45f) / 1.2f, 0f, 1f) * (1f - rock);
                float sand = Mathf.Clamp((-7f - height) / 2f, 0f, 1f) * (1f - rock - dirt);
                float grass = Mathf.Max(0f, 1f - rock - dirt - sand);

                Color color = Grass * grass + Dirt * dirt + Rock * rock + Sand * sand;

                // Lift the whole range: on a 188 pixel map the shaded world colours
                // would read as a uniform dark smear.
                float shade = 1.35f + 0.35f * Mathf.Clamp((height + 8f) / 16f, 0f, 1f);
                Write(_terrain, (y * grid.Width + x) * 4, color * shade);
            }
        }
    }

    // --- Texture ---------------------------------------------------------

    private void RedrawTexture()
    {
        if (_world is null || _grid is null || _image is null || _texture is null) return;

        ApplyFog();
        DrawEntities();

        _image.SetData(_grid.Width, _grid.Height, false, Image.Format.Rgba8, _pixels);
        _texture.Update(_image);
    }

    /// <summary>Unexplored stays black, remembered ground is dimmed, visible ground is full.</summary>
    private void ApplyFog()
    {
        VisionGrid? vision = _player?.Vision;

        if (vision is null)
        {
            System.Array.Copy(_terrain, _pixels, _terrain.Length);
            return;
        }

        for (int y = 0; y < _grid!.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                int index = (y * _grid.Width + x) * 4;
                float factor = vision.Get(x, y) switch
                {
                    Visibility.Visible => 1f,
                    Visibility.Explored => 0.45f,
                    _ => 0f,
                };

                _pixels[index] = (byte)(_terrain[index] * factor);
                _pixels[index + 1] = (byte)(_terrain[index + 1] * factor);
                _pixels[index + 2] = (byte)(_terrain[index + 2] * factor);
                _pixels[index + 3] = 255;
            }
        }
    }

    private void DrawEntities()
    {
        if (_world is null) return;

        foreach (ResourceNode node in _world.Entities.ResourceNodes)
        {
            if (!IsExplored(node.Position)) continue;
            Plot(node.Position, ResourceColors[(int)node.Resource], 1);
        }

        foreach (Building building in _world.Entities.Buildings)
        {
            // Buildings are remembered: an enemy base stays on the map once seen.
            if (!IsOwn(building) && !IsExplored(building.Position)) continue;
            Plot(building.Position, OwnerColor(building), 3);
        }

        foreach (Unit unit in _world.Entities.Units)
        {
            if (!IsOwn(unit) && !IsVisible(unit.Position)) continue;
            Plot(unit.Position, OwnerColor(unit), 2);
        }
    }

    private bool IsOwn(Entity entity) => _player is not null && entity.OwnerId == _player.Id;

    private bool IsExplored(Vector2 world)
    {
        if (_player?.Vision is null || _grid is null) return true;
        Vector2I cell = _grid.WorldToCell(world);
        return _player.Vision.IsExplored(cell.X, cell.Y);
    }

    private bool IsVisible(Vector2 world)
    {
        if (_player?.Vision is null || _grid is null) return true;
        return _player.Vision.IsVisible(_grid, world);
    }

    private Color OwnerColor(Entity entity)
    {
        Color color = _world?.GetPlayer(entity.OwnerId)?.Color ?? new Color(0.7f, 0.7f, 0.7f);
        // Your own units are the ones you look for — give them the brighter dot.
        return IsOwn(entity) ? color.Lightened(0.35f) : color;
    }

    /// <summary>Writes a square of <paramref name="size"/> texels centred on a world position.</summary>
    private void Plot(Vector2 world, Color color, int size)
    {
        if (_grid is null) return;

        Vector2I centre = _grid.WorldToCell(world);
        int half = size / 2;

        for (int dy = -half; dy <= half; dy++)
        {
            int y = centre.Y + dy;
            if (y < 0 || y >= _grid.Height) continue;

            for (int dx = -half; dx <= half; dx++)
            {
                int x = centre.X + dx;
                if (x < 0 || x >= _grid.Width) continue;

                Write(_pixels, (y * _grid.Width + x) * 4, color);
            }
        }
    }

    private static void Write(byte[] target, int index, Color color)
    {
        target[index] = (byte)(Mathf.Clamp(color.R, 0f, 1f) * 255f);
        target[index + 1] = (byte)(Mathf.Clamp(color.G, 0f, 1f) * 255f);
        target[index + 2] = (byte)(Mathf.Clamp(color.B, 0f, 1f) * 255f);
        target[index + 3] = 255;
    }

    // --- Overlay ---------------------------------------------------------

    public override void _Draw()
    {
        if (_texture is null || _grid is null) return;

        DrawTextureRect(_texture, new Rect2(Vector2.Zero, Size), false);

        foreach ((Vector2 world, Color color, float left) in _pings)
        {
            // Shrinks as it fades — reads as a beacon rather than a static dot.
            float phase = left / PingSeconds;
            DrawArc(WorldToLocal(world), 3f + 9f * (1f - phase), 0f, Mathf.Tau, 20,
                new Color(color, phase), 1.5f);
        }

        DrawCameraFrame();
    }

    /// <summary>The trapezoid the camera actually sees, projected onto the map.</summary>
    private void DrawCameraFrame()
    {
        if (_camera is null || _grid is null) return;

        Vector2 screen = GetViewport().GetVisibleRect().Size;
        Vector2[] corners =
        {
            GroundAt(new Vector2(0f, 0f)),
            GroundAt(new Vector2(screen.X, 0f)),
            GroundAt(screen),
            GroundAt(new Vector2(0f, screen.Y)),
            GroundAt(new Vector2(0f, 0f)),
        };

        DrawPolyline(corners, new Color(1f, 1f, 1f, 0.8f), 1.5f);
    }

    /// <summary>
    /// Where a screen corner meets the ground, in local minimap pixels.
    /// </summary>
    /// <remarks>
    /// At a shallow camera pitch the upper corners can point past the horizon, where
    /// there is no intersection at all. In that case the ray is followed a fixed
    /// distance and clamped to the map edge — the frame then ends at the border,
    /// which is exactly what the player sees.
    /// </remarks>
    private Vector2 GroundAt(Vector2 screenPosition)
    {
        if (_camera is null || _grid is null) return Vector2.Zero;

        if (!GroundPicker.TryPick(_grid, _camera.Camera, screenPosition, out Vector2 world))
        {
            Vector3 origin = _camera.Camera.ProjectRayOrigin(screenPosition);
            Vector3 direction = _camera.Camera.ProjectRayNormal(screenPosition);
            Vector3 far = origin + direction * 400f;
            world = new Vector2(far.X, far.Z);
        }

        return WorldToLocal(world);
    }

    private Vector2 WorldToLocal(Vector2 world)
    {
        if (_grid is null) return Vector2.Zero;

        float u = Mathf.Clamp((world.X + _grid.WorldWidth * 0.5f) / _grid.WorldWidth, 0f, 1f);
        float v = Mathf.Clamp((world.Y + _grid.WorldHeight * 0.5f) / _grid.WorldHeight, 0f, 1f);
        return new Vector2(u * Size.X, v * Size.Y);
    }

    private Vector2 LocalToWorld(Vector2 local)
    {
        if (_grid is null) return Vector2.Zero;

        float u = Size.X > 0f ? Mathf.Clamp(local.X / Size.X, 0f, 1f) : 0f;
        float v = Size.Y > 0f ? Mathf.Clamp(local.Y / Size.Y, 0f, 1f) : 0f;
        return new Vector2(
            u * _grid.WorldWidth - _grid.WorldWidth * 0.5f,
            v * _grid.WorldHeight - _grid.WorldHeight * 0.5f);
    }

    // --- Input -----------------------------------------------------------

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                if (button.Pressed) _camera?.JumpTo(LocalToWorld(button.Position));
                AcceptEvent();
                break;

            // Dragging keeps the camera under the cursor — the usual way to sweep
            // across the map quickly.
            case InputEventMouseMotion motion when _dragging:
                _camera?.JumpTo(LocalToWorld(motion.Position));
                AcceptEvent();
                break;
        }
    }
}

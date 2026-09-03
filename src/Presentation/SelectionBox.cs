using Godot;

namespace EpochAeterna.Presentation;

/// <summary>The selection rectangle you see while dragging with the left mouse button.</summary>
public sealed partial class SelectionBox : CanvasLayer
{
    private static readonly Color FillColor = new(0.35f, 0.75f, 1f, 0.14f);
    private static readonly Color BorderColor = new(0.55f, 0.88f, 1f, 0.85f);

    private readonly Rectangle _rectangle = new();

    public override void _Ready()
    {
        Layer = 1;
        AddChild(_rectangle);
        _rectangle.Visible = false;
    }

    public void UpdateRect(Vector2 from, Vector2 to)
    {
        _rectangle.Area = new Rect2(from, to - from).Abs();
        _rectangle.Visible = true;
        _rectangle.QueueRedraw();
    }

    public new void Hide() => _rectangle.Visible = false;

    /// <summary>Draws fill and border. Its own node, so that <c>_Draw</c> is available.</summary>
    private sealed partial class Rectangle : Control
    {
        public Rect2 Area { get; set; }

        public override void _Draw()
        {
            DrawRect(Area, FillColor);
            DrawRect(Area, BorderColor, filled: false, width: 1.5f);
        }
    }
}

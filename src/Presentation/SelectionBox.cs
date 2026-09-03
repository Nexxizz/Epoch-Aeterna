using Godot;

namespace EpochAeterna.Presentation;

/// <summary>Der Auswahlrahmen, den man beim Ziehen mit der linken Maustaste sieht.</summary>
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

    /// <summary>Zeichnet Fuellung und Rahmen. Eigener Node, damit <c>_Draw</c> zur Verfuegung steht.</summary>
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

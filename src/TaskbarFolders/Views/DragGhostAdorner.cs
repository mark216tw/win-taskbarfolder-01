using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Mark.TaskbarFolders.Views;

internal sealed class DragGhostAdorner : Adorner
{
    private const double Offset = 12;
    private const double GhostOpacity = 0.85;

    private readonly VisualBrush _brush;
    private readonly Size _size;
    private Point _position;

    public DragGhostAdorner(UIElement adornedElement, UIElement draggedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;

        _size = draggedElement.RenderSize;
        _brush = new VisualBrush(draggedElement)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
    }

    public void UpdatePosition(Point position)
    {
        _position = position;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(
            _position.X + Offset,
            _position.Y + Offset,
            _size.Width,
            _size.Height);

        drawingContext.PushOpacity(GhostOpacity);
        drawingContext.DrawRectangle(_brush, null, bounds);
        drawingContext.Pop();
    }
}

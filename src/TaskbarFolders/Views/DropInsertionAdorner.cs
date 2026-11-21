using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mark.TaskbarFolders.Views;

internal sealed class DropInsertionAdorner : Adorner
{
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(120);

    private readonly Pen _pen;
    private bool _isVisible;
    private Point _start;
    private Point _end;

    public DropInsertionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;

        var brush = Application.Current?.TryFindResource("AccentBrush") as Brush
            ?? Brushes.DodgerBlue;
        _pen = new Pen(brush, 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        _pen.Freeze();
        Opacity = 0;
    }

    public void ShowAt(double y, double left, double right) =>
        ShowLine(new Point(left, y), new Point(Math.Max(left, right), y));

    public void ShowVerticalAt(double x, double top, double bottom) =>
        ShowLine(new Point(x, top), new Point(x, Math.Max(top, bottom)));

    private void ShowLine(Point start, Point end)
    {
        _start = start;
        _end = end;
        InvalidateVisual();

        if (_isVisible)
        {
            return;
        }

        _isVisible = true;
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, FadeInDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    public void Hide()
    {
        if (!_isVisible)
        {
            return;
        }

        _isVisible = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!_isVisible)
        {
            return;
        }

        drawingContext.DrawLine(_pen, _start, _end);
        drawingContext.DrawEllipse(_pen.Brush, null, _start, 2.5, 2.5);
        drawingContext.DrawEllipse(_pen.Brush, null, _end, 2.5, 2.5);
    }
}

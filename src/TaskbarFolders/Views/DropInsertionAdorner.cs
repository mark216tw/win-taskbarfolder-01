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
    private double _y;
    private double _left;
    private double _right;

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

    public void ShowAt(double y, double left, double right)
    {
        _y = y;
        _left = left;
        _right = Math.Max(left, right);
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

        var start = new Point(_left, _y);
        var end = new Point(_right, _y);
        drawingContext.DrawLine(_pen, start, end);
        drawingContext.DrawEllipse(_pen.Brush, null, start, 2.5, 2.5);
        drawingContext.DrawEllipse(_pen.Brush, null, end, 2.5, 2.5);
    }
}

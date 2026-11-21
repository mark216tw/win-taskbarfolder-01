using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.ViewModels;
using Mark.TaskbarFolders.Services;

namespace Mark.TaskbarFolders.Views;

public partial class FlyoutWindow : Window
{
    private const double ItemRowHeight = 88;
    private const double HeaderHeight = 80;
    private const double MinFlyoutHeight = 150;
    private const int Columns = 5;

    private readonly FlyoutViewModel _viewModel;
    private readonly TaskbarGeometryService _geometry;
    private bool _positioned;
    private bool _suppressDeactivate;
    private bool _renameInProgress;
    private long _shownAt;
    private Point _dragStart;
    private ItemViewModel? _dragCandidate;
    private ItemViewModel? _draggedItem;
    private ListBoxItem? _draggedContainer;
    private AdornerLayer? _adornerLayer;
    private DragGhostAdorner? _dragGhost;
    private DropInsertionAdorner? _insertionAdorner;
    private double _draggedOpacity = 1;
    private bool _dropCompleted;
    private bool _isDragging;

    public FlyoutWindow(FlyoutViewModel viewModel, TaskbarGeometryService geometry)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _geometry = geometry;
        DataContext = viewModel;

        Width = 500;
        Height = CalculateHeight(viewModel.Items.Count);

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.LaunchFailed += OnLaunchFailed;

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closed += OnClosed;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnLaunchFailed(object? sender, string message)
    {
        _suppressDeactivate = true;
        try
        {
            MessageBox.Show(
                this,
                message,
                Mark.TaskbarFolders.Resources.Strings.Get("ErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _suppressDeactivate = false;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.LaunchFailed -= OnLaunchFailed;
        DataContext = null;
    }

    private double CalculateHeight(int itemCount)
    {
        var rows = Math.Max(1, (int)Math.Ceiling(itemCount / (double)Columns));
        return Math.Max(MinFlyoutHeight, HeaderHeight + (rows * ItemRowHeight));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_positioned)
        {
            return;
        }

        _positioned = true;
        var handle = new WindowInteropHelper(this).Handle;
        WindowThemeHelper.ApplyRoundedCorners(this);
        var placement = _geometry.GetPlacement(handle, Width, Height);

        Left = placement.X / placement.Scale;
        Top = placement.Y / placement.Scale;
        Width = placement.Width / placement.Scale;
        Height = placement.Height / placement.Scale;

        _geometry.ApplyPlacement(handle, placement);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        _shownAt = Environment.TickCount64;
        EnsureForeground();
        ItemList.Focus();
        Keyboard.Focus(ItemList);
        if (ItemList.Items.Count > 0)
        {
            ItemList.SelectedIndex = 0;
        }
    }

    private void EnsureForeground()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || NativeMethods.GetForegroundWindow() == handle)
        {
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            NativeMethods.SetForegroundWindow(handle);
            Activate();
        }
        finally
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnItemContextMenuOpened(object sender, RoutedEventArgs e) =>
        _suppressDeactivate = true;

    private void OnItemContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_renameInProgress)
        {
            return;
        }

        _suppressDeactivate = false;

        if (!IsActive)
        {
            Close();
        }
    }

    private async void OnRenameItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        var contextMenu = menuItem.Parent as ContextMenu;
        var item = (contextMenu?.PlacementTarget as FrameworkElement)?.DataContext as ItemViewModel
            ?? menuItem.DataContext as ItemViewModel;

        if (item is null)
        {
            return;
        }

        _renameInProgress = true;
        _suppressDeactivate = true;

        try
        {
            var dialog = new InputDialog(
                Strings.Get("EditDisplayNameTitle"),
                Strings.Get("EditDisplayNameLabel"),
                item.Name)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var name = dialog.Value.Trim();
            if (name.Length == 0 || string.Equals(name, item.Name, StringComparison.Ordinal))
            {
                return;
            }

            if (!await _viewModel.RenameItemAsync(item, name))
            {
                MessageBox.Show(
                    this,
                    Strings.Get("ErrorEditDisplayName"),
                    Strings.Get("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            _renameInProgress = false;
            _suppressDeactivate = false;
            Activate();
            ItemList.Focus();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suppressDeactivate)
        {
            return;
        }

        if (Environment.TickCount64 - _shownAt < 250)
        {
            return;
        }

        if (IsVisible)
        {
            Close();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                LaunchSelected();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(Columns);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-Columns);
                e.Handled = true;
                break;
            case Key.Left:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveSelection(1);
                e.Handled = true;
                break;
        }
    }

    private void OnListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ItemList);
        _dragCandidate = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext
            as ItemViewModel;
        _isDragging = false;
    }

    private void OnListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _dragCandidate is null ||
            _renameInProgress ||
            _viewModel.Items.Count <= 1)
        {
            return;
        }

        var position = e.GetPosition(ItemList);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragCandidate;
        _dragCandidate = null;
        _dropCompleted = false;
        _isDragging = true;
        _suppressDeactivate = true;

        var container = ItemList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        BeginDragVisuals(item, container, position);

        try
        {
            DragDrop.DoDragDrop(ItemList, new DataObject(typeof(ItemViewModel), item), DragDropEffects.Move);
        }
        finally
        {
            var dropCompleted = _dropCompleted;
            EndDragVisuals();
            _isDragging = false;
            if (!_renameInProgress)
            {
                _suppressDeactivate = false;
            }

            if (dropCompleted)
            {
                ItemList.SelectedItem = item;
                AnimateItemDrop(item);
            }
        }
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            _insertionAdorner?.Hide();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        _dragGhost?.UpdatePosition(e.GetPosition(ItemList));
        UpdateInsertionIndicator(e);
        e.Handled = true;
    }

    private void OnListDragLeave(object sender, DragEventArgs e) =>
        _insertionAdorner?.Hide();

    private async void OnListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ItemViewModel)) is not ItemViewModel dragged)
        {
            return;
        }

        var oldIndex = _viewModel.Items.IndexOf(dragged);
        if (oldIndex < 0)
        {
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        int destination;
        if (targetItem?.DataContext is ItemViewModel target)
        {
            if (ReferenceEquals(target, dragged))
            {
                return;
            }

            var targetIndex = _viewModel.Items.IndexOf(target);
            if (targetIndex < 0)
            {
                return;
            }

            destination = e.GetPosition(targetItem).X > targetItem.ActualWidth / 2
                ? targetIndex + 1
                : targetIndex;

            if (oldIndex < targetIndex)
            {
                destination--;
            }
        }
        else
        {
            destination = _viewModel.Items.Count - 1;
        }

        _insertionAdorner?.Hide();

        var clampedDestination = Math.Clamp(destination, 0, _viewModel.Items.Count - 1);
        _dropCompleted = clampedDestination != oldIndex;
        e.Handled = true;

        if (!_dropCompleted)
        {
            return;
        }

        if (!await _viewModel.MoveItemAsync(dragged, clampedDestination) && IsLoaded)
        {
            _suppressDeactivate = true;
            try
            {
                MessageBox.Show(
                    this,
                    Strings.Get("ErrorReorderItems"),
                    Strings.Get("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _suppressDeactivate = false;
                Activate();
                ItemList.Focus();
            }
        }
    }

    private void BeginDragVisuals(ItemViewModel item, ListBoxItem? container, Point position)
    {
        _draggedItem = item;
        _draggedContainer = container;
        _adornerLayer = AdornerLayer.GetAdornerLayer(ItemList);

        if (_adornerLayer is null)
        {
            return;
        }

        if (container is not null)
        {
            _draggedOpacity = container.Opacity;
            container.Opacity = 0.35;

            _dragGhost = new DragGhostAdorner(ItemList, container);
            _dragGhost.UpdatePosition(position);
            _adornerLayer.Add(_dragGhost);
        }

        _insertionAdorner = new DropInsertionAdorner(ItemList);
        _adornerLayer.Add(_insertionAdorner);
    }

    private void EndDragVisuals()
    {
        if (_draggedContainer is not null)
        {
            _draggedContainer.Opacity = _draggedOpacity;
        }

        if (_adornerLayer is not null)
        {
            if (_dragGhost is not null)
            {
                _adornerLayer.Remove(_dragGhost);
            }

            if (_insertionAdorner is not null)
            {
                _adornerLayer.Remove(_insertionAdorner);
            }
        }

        _dragGhost = null;
        _insertionAdorner = null;
        _adornerLayer = null;
        _draggedItem = null;
        _draggedContainer = null;
        _draggedOpacity = 1;
    }

    private void UpdateInsertionIndicator(DragEventArgs e)
    {
        if (_insertionAdorner is null)
        {
            return;
        }

        if (_draggedItem is null)
        {
            _insertionAdorner.Hide();
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem?.DataContext is ItemViewModel target)
        {
            if (ReferenceEquals(target, _draggedItem))
            {
                _insertionAdorner.Hide();
                return;
            }

            var position = e.GetPosition(targetItem);
            var offsetX = position.X > targetItem.ActualWidth / 2
                ? targetItem.ActualWidth
                : 0;
            var top = targetItem.TranslatePoint(new Point(offsetX, 4), ItemList);
            var bottom = top.Y + Math.Max(0, targetItem.ActualHeight - 8);
            _insertionAdorner.ShowVerticalAt(top.X, top.Y, bottom);
            return;
        }

        var last = _viewModel.Items.LastOrDefault();
        var lastContainer = last is null
            ? null
            : ItemList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem;

        if (lastContainer is null || ReferenceEquals(last, _draggedItem))
        {
            _insertionAdorner.Hide();
            return;
        }

        var endTop = lastContainer.TranslatePoint(new Point(lastContainer.ActualWidth, 4), ItemList);
        var endBottom = endTop.Y + Math.Max(0, lastContainer.ActualHeight - 8);
        _insertionAdorner.ShowVerticalAt(endTop.X, endTop.Y, endBottom);
    }

    private void AnimateItemDrop(ItemViewModel item)
    {
        ItemList.UpdateLayout();
        if (ItemList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        container.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0.35, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            });

        var transform = new ScaleTransform(1, 1);
        container.RenderTransformOrigin = new Point(0.5, 0.5);
        container.RenderTransform = transform;

        transform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.92, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            });
        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.92, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            });
    }

    private void OnListMouseUp(object sender, MouseButtonEventArgs e)
    {
        var candidate = _dragCandidate;
        _dragCandidate = null;
        if (_isDragging || candidate is null)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is ItemViewModel viewModel && ReferenceEquals(viewModel, candidate))
        {
            _viewModel.Launch(viewModel);
        }
    }

    private void LaunchSelected()
    {
        var item = ItemList.SelectedItem as ItemViewModel
            ?? ItemList.Items.OfType<ItemViewModel>().FirstOrDefault();
        _viewModel.Launch(item);
    }

    private void MoveSelection(int delta)
    {
        if (ItemList.Items.Count == 0)
        {
            return;
        }

        var index = ItemList.SelectedIndex;
        if (index < 0)
        {
            index = delta >= 0 ? 0 : ItemList.Items.Count - 1;
        }
        else
        {
            index = Math.Clamp(index + delta, 0, ItemList.Items.Count - 1);
        }

        ItemList.SelectedIndex = index;
        ItemList.ScrollIntoView(ItemList.SelectedItem);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Services;
using Mark.TaskbarFolders.ViewModels;

namespace Mark.TaskbarFolders.Views;

public partial class EditorWindow : Window
{
    private static readonly TimeSpan MinimizedTrimDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BackgroundTrimDelay = TimeSpan.FromSeconds(30);

    private readonly EditorViewModel _viewModel;
    private readonly ThemeService _theme;
    private readonly WindowPlacementService _placements;
    private readonly MemoryTrimService? _memoryTrim;
    private Point _dragStart;
    private GroupViewModel? _dragCandidate;
    private GroupViewModel? _draggedGroup;
    private ListBoxItem? _draggedContainer;
    private AdornerLayer? _adornerLayer;
    private DragGhostAdorner? _dragGhost;
    private DropInsertionAdorner? _insertionAdorner;
    private double _draggedOpacity = 1;
    private bool _dropCompleted;

    public EditorWindow(
        EditorViewModel viewModel,
        ThemeService theme,
        WindowPlacementService placements,
        WindowPlacement? initialPlacement = null,
        MemoryTrimService? memoryTrim = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _theme = theme;
        _placements = placements;
        _memoryTrim = memoryTrim;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        Closing += OnClosing;
        ApplySavedPlacement(initialPlacement);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            _memoryTrim?.ScheduleTrim(MinimizedTrimDelay);
        }
        else
        {
            _memoryTrim?.CancelPendingTrim();
        }
    }

    private void OnActivated(object? sender, EventArgs e) =>
        _memoryTrim?.CancelPendingTrim();

    private void OnDeactivated(object? sender, EventArgs e) =>
        _memoryTrim?.ScheduleTrim(BackgroundTrimDelay);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowThemeHelper.Apply(this, _theme.IsDark);
        ClampToMonitorWorkArea();
    }

    private void ClampToMonitorWorkArea()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new NativeMethods.MonitorInfo
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo) ||
            !NativeMethods.GetWindowRect(handle, out var windowRect) ||
            windowRect.Width <= 0 ||
            windowRect.Height <= 0)
        {
            return;
        }

        var clamped = WindowPlacementService.ClampToWorkArea(
            windowRect.Left,
            windowRect.Top,
            windowRect.Width,
            windowRect.Height,
            monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Top,
            monitorInfo.rcWork.Right,
            monitorInfo.rcWork.Bottom);

        if (clamped.Left == windowRect.Left &&
            clamped.Top == windowRect.Top &&
            clamped.Width == windowRect.Width &&
            clamped.Height == windowRect.Height)
        {
            return;
        }

        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var location = transform.Transform(new Point(clamped.Left, clamped.Top));
        var size = transform.Transform(new Point(clamped.Width, clamped.Height));

        Left = location.X;
        Top = location.Y;

        if (clamped.Width != windowRect.Width)
        {
            Width = size.X;
        }

        if (clamped.Height != windowRect.Height)
        {
            Height = size.Y;
        }
    }

    private void ApplySavedPlacement(WindowPlacement? preloaded)
    {
        var placement = preloaded ?? _placements.Load();
        if (placement is null)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = Math.Max(MinWidth, placement.Width);
        Height = Math.Max(MinHeight, placement.Height);
        Left = placement.Left;
        Top = placement.Top;

        if (placement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _placements.Save(new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized
        });
    }

    private void OnGroupListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(GroupList);
        _dragCandidate = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext
            as GroupViewModel;
    }

    private void OnGroupListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _dragCandidate = null;

    private void OnGroupListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null)
        {
            return;
        }

        var position = e.GetPosition(GroupList);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var group = _dragCandidate;
        _dragCandidate = null;
        _dropCompleted = false;

        var container = GroupList.ItemContainerGenerator.ContainerFromItem(group) as ListBoxItem;
        BeginDragVisuals(group, container, position);

        try
        {
            DragDrop.DoDragDrop(GroupList, new DataObject(typeof(GroupViewModel), group), DragDropEffects.Move);
        }
        finally
        {
            var dropCompleted = _dropCompleted;
            EndDragVisuals();

            if (dropCompleted)
            {
                AnimateGroupDrop(group);
            }
        }
    }

    private void OnGroupListDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(GroupViewModel)))
        {
            e.Effects = DragDropEffects.None;
            _insertionAdorner?.Hide();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        _dragGhost?.UpdatePosition(e.GetPosition(GroupList));
        UpdateInsertionIndicator(e);
        e.Handled = true;
    }

    private void OnGroupListDragLeave(object sender, DragEventArgs e) =>
        _insertionAdorner?.Hide();

    private void OnGroupListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(GroupViewModel)) is not GroupViewModel dragged)
        {
            return;
        }

        var oldIndex = _viewModel.Groups.IndexOf(dragged);
        if (oldIndex < 0)
        {
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        int destination;
        if (targetItem?.DataContext is GroupViewModel target)
        {
            if (ReferenceEquals(target, dragged))
            {
                return;
            }

            var targetIndex = _viewModel.Groups.IndexOf(target);
            if (targetIndex < 0)
            {
                return;
            }

            destination = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2
                ? targetIndex + 1
                : targetIndex;

            if (oldIndex < targetIndex)
            {
                destination--;
            }
        }
        else
        {
            destination = _viewModel.Groups.Count - 1;
        }

        _insertionAdorner?.Hide();

        var clampedDestination = Math.Clamp(destination, 0, _viewModel.Groups.Count - 1);
        _dropCompleted = clampedDestination != oldIndex;
        _viewModel.MoveGroupTo(dragged, destination);
        e.Handled = true;
    }

    private void BeginDragVisuals(GroupViewModel group, ListBoxItem? container, Point position)
    {
        _draggedGroup = group;
        _draggedContainer = container;
        _adornerLayer = AdornerLayer.GetAdornerLayer(GroupList);

        if (_adornerLayer is null)
        {
            return;
        }

        if (container is not null)
        {
            _draggedOpacity = container.Opacity;
            container.Opacity = 0.35;

            _dragGhost = new DragGhostAdorner(GroupList, container);
            _dragGhost.UpdatePosition(position);
            _adornerLayer.Add(_dragGhost);
        }

        _insertionAdorner = new DropInsertionAdorner(GroupList);
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
        _draggedGroup = null;
        _draggedContainer = null;
        _draggedOpacity = 1;
    }

    private void UpdateInsertionIndicator(DragEventArgs e)
    {
        if (_insertionAdorner is null)
        {
            return;
        }

        if (_draggedGroup is null)
        {
            _insertionAdorner.Hide();
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem?.DataContext is GroupViewModel target)
        {
            if (ReferenceEquals(target, _draggedGroup))
            {
                _insertionAdorner.Hide();
                return;
            }

            var position = e.GetPosition(targetItem);
            var offset = position.Y > targetItem.ActualHeight / 2
                ? targetItem.ActualHeight
                : 0;
            var point = targetItem.TranslatePoint(new Point(0, offset), GroupList);
            _insertionAdorner.ShowAt(point.Y, 4, GroupList.ActualWidth - 4);
            return;
        }

        var last = _viewModel.Groups.LastOrDefault();
        var lastContainer = last is null
            ? null
            : GroupList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem;
        var y = lastContainer is null
            ? GroupList.ActualHeight - 4
            : lastContainer.TranslatePoint(new Point(0, lastContainer.ActualHeight), GroupList).Y;
        _insertionAdorner.ShowAt(y, 4, GroupList.ActualWidth - 4);
    }

    private void AnimateGroupDrop(GroupViewModel group)
    {
        GroupList.UpdateLayout();
        if (GroupList.ItemContainerGenerator.ContainerFromItem(group) is not ListBoxItem container)
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
            new DoubleAnimation(0.96, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            });
        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.96, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            });
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

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_viewModel, _theme)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                this,
                Strings.Get("ConfirmDiscardChanges"),
                Strings.Get("ConfirmDiscardTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        SavePlacement();
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Mark.TaskbarFolders.Helpers;
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
    private long _shownAt;

    public FlyoutWindow(FlyoutViewModel viewModel, TaskbarGeometryService geometry)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _geometry = geometry;
        DataContext = viewModel;

        Width = 500;
        Height = CalculateHeight(viewModel.Items.Count);

        _viewModel.CloseRequested += (_, _) => Close();
        _viewModel.LaunchFailed += (_, message) =>
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
        };

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
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

    private void OnListMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is ItemViewModel viewModel)
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

using System.Windows;
using System.Windows.Controls;
using Mark.TaskbarFolders.Models;

namespace Mark.TaskbarFolders.Views;

public partial class AppPickerWindow : Window
{
    private readonly List<DiscoveredApp> _all;

    public AppPickerWindow(IReadOnlyList<DiscoveredApp> apps)
    {
        InitializeComponent();
        _all = apps.ToList();
        ApplyFilter(string.Empty);
    }

    public IReadOnlyList<DiscoveredApp> SelectedApps { get; private set; } = [];

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyFilter(SearchBox.Text);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => AppList.SelectAll();

    private void OnClearSelection(object sender, RoutedEventArgs e) => AppList.UnselectAll();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SelectedApps = AppList.SelectedItems
            .OfType<DiscoveredApp>()
            .ToList();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ApplyFilter(string query)
    {
        var trimmed = query.Trim();
        var filtered = trimmed.Length == 0
            ? _all
            : _all.Where(app =>
                app.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                app.Category.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                .ToList();

        AppList.ItemsSource = filtered;
    }
}

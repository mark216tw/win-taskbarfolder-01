using System.Windows;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Services;
using Mark.TaskbarFolders.ViewModels;

namespace Mark.TaskbarFolders.Views;

public partial class SettingsWindow : Window
{
    private readonly EditorViewModel _viewModel;
    private bool _saving;

    public SettingsWindow(EditorViewModel viewModel, ThemeService theme)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this, theme.IsDark);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        try
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
            if (!_viewModel.HasUnsavedChanges)
            {
                Close();
            }
        }
        finally
        {
            _saving = false;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

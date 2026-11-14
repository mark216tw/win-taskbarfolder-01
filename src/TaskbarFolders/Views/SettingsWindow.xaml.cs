using System.ComponentModel;
using System.Windows;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Services;
using Mark.TaskbarFolders.ViewModels;

namespace Mark.TaskbarFolders.Views;

public partial class SettingsWindow : Window
{
    private readonly EditorViewModel _viewModel;
    private readonly ThemeService _theme;
    private bool _saving;

    public SettingsWindow(EditorViewModel viewModel, ThemeService theme)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _theme = theme;
        DataContext = viewModel;
        ApplyAccentTheme();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this, theme.IsDark);
        Closing += OnClosing;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.ThemeMode))
        {
            ApplyAccentTheme();
        }
    }

    private void ApplyAccentTheme() =>
        _theme.ApplyAccentTheme(this, _viewModel.SelectedGroup?.ThemeColor);

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        try
        {
            if (_viewModel.HasUnsavedSettingsChanges)
            {
                await _viewModel.SaveSettingsCommand.ExecuteAsync(null);
                if (_viewModel.HasUnsavedSettingsChanges)
                {
                    return;
                }
            }

            Close();
        }
        finally
        {
            _saving = false;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.RevertThemeChanges();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

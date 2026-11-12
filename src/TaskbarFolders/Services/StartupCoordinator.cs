using System.Windows;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.ViewModels;
using Mark.TaskbarFolders.Views;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class StartupCoordinator
{
    private readonly ConfigService _config;
    private readonly DiscoveryService _discovery;
    private readonly IconService _icon;
    private readonly ShortcutService _shortcut;
    private readonly IDialogService _dialogs;
    private readonly ThemeService _theme;
    private readonly WindowPlacementService _placements;
    private readonly ILoggerFactory _loggerFactory;

    public StartupCoordinator(
        ConfigService config,
        DiscoveryService discovery,
        IconService icon,
        ShortcutService shortcut,
        IDialogService dialogs,
        ThemeService theme,
        WindowPlacementService placements,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _discovery = discovery;
        _icon = icon;
        _shortcut = shortcut;
        _dialogs = dialogs;
        _theme = theme;
        _placements = placements;
        _loggerFactory = loggerFactory;
    }

    public async Task StartEditorAsync()
    {
        var settings = await _config.LoadAsync();
        await ShowEditorAsync(settings);
    }

    private async Task ShowEditorAsync(AppSettings settings)
    {
        var viewModel = new EditorViewModel(
            _config,
            _discovery,
            _icon,
            _shortcut,
            _dialogs,
            _theme,
            _loggerFactory.CreateLogger<EditorViewModel>());

        await viewModel.InitializeAsync(settings);

        var window = new EditorWindow(viewModel, _theme, _placements);
        window.Show();
        Application.Current.MainWindow = window;

        _ = viewModel.LoadAllIconsAsync();
    }
}

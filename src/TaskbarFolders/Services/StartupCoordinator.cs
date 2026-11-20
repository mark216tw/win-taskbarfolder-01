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
    private readonly GroupTransferService _transfer;
    private readonly IDialogService _dialogs;
    private readonly ThemeService _theme;
    private readonly WindowPlacementService _placements;
    private readonly MemoryTrimService _memoryTrim;
    private readonly ILoggerFactory _loggerFactory;

    public StartupCoordinator(
        ConfigService config,
        DiscoveryService discovery,
        IconService icon,
        ShortcutService shortcut,
        GroupTransferService transfer,
        IDialogService dialogs,
        ThemeService theme,
        WindowPlacementService placements,
        MemoryTrimService memoryTrim,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _discovery = discovery;
        _icon = icon;
        _shortcut = shortcut;
        _transfer = transfer;
        _dialogs = dialogs;
        _theme = theme;
        _placements = placements;
        _memoryTrim = memoryTrim;
        _loggerFactory = loggerFactory;
    }

    public async Task StartEditorAsync()
    {
        var settingsTask = _config.LoadAsync();
        var placementTask = Task.Run(() => _placements.Load());
        await Task.WhenAll(settingsTask, placementTask);

        await ShowEditorAsync(await settingsTask, await placementTask);
    }

    private async Task ShowEditorAsync(AppSettings settings, WindowPlacement? placement)
    {
        var viewModel = new EditorViewModel(
            _config,
            _discovery,
            _icon,
            _shortcut,
            _transfer,
            _dialogs,
            _theme,
            _loggerFactory.CreateLogger<EditorViewModel>());

        await viewModel.InitializeAsync(settings);

        _memoryTrim.RegisterCallback(() =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.BeginInvoke(viewModel.ReleaseInactiveGroupIcons);
            }
        });

        var window = new EditorWindow(viewModel, _theme, _placements, placement, _memoryTrim);
        window.Show();
        Application.Current.MainWindow = window;

        _ = viewModel.LoadSelectedGroupIconsAsync();
    }
}

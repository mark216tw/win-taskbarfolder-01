using System.Windows;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.ViewModels;
using Mark.TaskbarFolders.Views;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class FlyoutLauncher : IDisposable
{
    private readonly StaExecutor _sta;
    private readonly LoggerFactory _loggerFactory;
    private readonly ConfigService _config;
    private readonly IconService _icon;
    private readonly LaunchService _launch;
    private readonly ThemeService _theme;
    private readonly TaskbarGeometryService _geometry;

    public FlyoutLauncher()
    {
        _sta = new StaExecutor();
        _loggerFactory = new LoggerFactory();
        _loggerFactory.AddProvider(new FileLoggerProvider(AppPaths.LogFile));

        _config = new ConfigService(logger: _loggerFactory.CreateLogger<ConfigService>());
        _icon = new IconService(_sta, _loggerFactory.CreateLogger<IconService>());
        _launch = new LaunchService(_loggerFactory.CreateLogger<LaunchService>());
        _theme = new ThemeService();
        _geometry = new TaskbarGeometryService(_loggerFactory.CreateLogger<TaskbarGeometryService>());
    }

    public async Task<bool> ShowAsync(string groupId)
    {
        var settings = await _config.LoadAsync();
        var group = settings.Groups.FirstOrDefault(
            candidate => candidate.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            MessageBox.Show(
                Strings.Format("ErrorGroupMissing", groupId),
                Strings.Get("ErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        _theme.Apply(settings.Theme);

        var viewModel = new FlyoutViewModel(
            group,
            _launch,
            _icon,
            _loggerFactory.CreateLogger<FlyoutViewModel>());

        var window = new FlyoutWindow(viewModel, _geometry);
        _theme.ApplyGroupTheme(window, group.ThemeColor);
        _ = viewModel.LoadIconsAsync();
        window.Show();
        return true;
    }

    public void Dispose()
    {
        _sta.Dispose();
        _loggerFactory.Dispose();
    }
}

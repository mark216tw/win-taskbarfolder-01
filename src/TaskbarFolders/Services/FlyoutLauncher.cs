using System.Windows;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.ViewModels;
using Mark.TaskbarFolders.Views;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class FlyoutLauncher : IDisposable
{
    private const int DebounceMilliseconds = 500;

    private readonly StaExecutor? _ownedSta;
    private readonly LoggerFactory? _ownedLoggerFactory;
    private readonly ConfigService _config;
    private readonly IconService _icon;
    private readonly LaunchService _launch;
    private readonly ThemeService _theme;
    private readonly TaskbarGeometryService _geometry;
    private readonly ILoggerFactory _loggerFactory;

    private Window? _currentWindow;
    private string? _currentGroupId;
    private long _shownAt;
    private bool _disposed;

    public FlyoutLauncher()
    {
        var sta = new StaExecutor();
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(new FileLoggerProvider(AppPaths.LogFile));

        _ownedSta = sta;
        _ownedLoggerFactory = loggerFactory;
        _loggerFactory = loggerFactory;
        _config = new ConfigService(logger: loggerFactory.CreateLogger<ConfigService>());
        _icon = new IconService(sta, loggerFactory.CreateLogger<IconService>());
        _launch = new LaunchService(loggerFactory.CreateLogger<LaunchService>());
        _theme = new ThemeService();
        _geometry = new TaskbarGeometryService(loggerFactory.CreateLogger<TaskbarGeometryService>());
    }

    private FlyoutLauncher(
        ConfigService config,
        IconService icon,
        LaunchService launch,
        ThemeService theme,
        TaskbarGeometryService geometry,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _icon = icon;
        _launch = launch;
        _theme = theme;
        _geometry = geometry;
        _loggerFactory = loggerFactory;
    }

    public static FlyoutLauncher CreateForHost(
        ConfigService config,
        IconService icon,
        LaunchService launch,
        ThemeService theme,
        TaskbarGeometryService geometry,
        ILoggerFactory loggerFactory) =>
        new(config, icon, launch, theme, geometry, loggerFactory);

    public bool Show(string groupId, bool showErrorDialog = true, bool applyTheme = true)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Show(groupId, showErrorDialog, applyTheme));
        }

        var settings = _config.Load();
        var group = settings.Groups.FirstOrDefault(
            candidate => candidate.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            if (showErrorDialog)
            {
                MessageBox.Show(
                    Strings.Format("ErrorGroupMissing", groupId),
                    Strings.Get("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return false;
        }

        if (_currentWindow is not null)
        {
            var sameGroup = string.Equals(_currentGroupId, group.Id, StringComparison.OrdinalIgnoreCase);
            if (sameGroup && Environment.TickCount64 - _shownAt < DebounceMilliseconds)
            {
                _currentWindow.Activate();
                return true;
            }

            CloseCurrent();
        }

        if (applyTheme)
        {
            _theme.Apply(settings.Theme);
        }

        var viewModel = new FlyoutViewModel(
            group,
            _config,
            _launch,
            _icon,
            _loggerFactory.CreateLogger<FlyoutViewModel>());

        var window = new FlyoutWindow(viewModel, _geometry);
        _theme.ApplyGroupTheme(window, group.ThemeColor);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_currentWindow, window))
            {
                _currentWindow = null;
                _currentGroupId = null;
            }
        };

        _currentWindow = window;
        _currentGroupId = group.Id;
        window.Show();
        _shownAt = Environment.TickCount64;
        _ = viewModel.LoadIconsAsync();
        return true;
    }

    private void CloseCurrent()
    {
        var window = _currentWindow;
        _currentWindow = null;
        _currentGroupId = null;
        if (window is not null)
        {
            window.Close();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseCurrent();
        _ownedSta?.Dispose();
        _ownedLoggerFactory?.Dispose();
    }
}

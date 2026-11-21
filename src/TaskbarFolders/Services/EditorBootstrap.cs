using System.Windows;
using System.Windows.Interop;
using Mark.TaskbarFolders.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class EditorBootstrap : IDisposable
{
    private ServiceProvider? _services;
    private Mutex? _editorMutex;
    private EventWaitHandle? _editorActivateEvent;
    private RegisteredWaitHandle? _activationWaitHandle;
    private FlyoutIpcServer? _flyoutServer;

    public bool EnsureSingleInstance()
    {
        try
        {
            _editorMutex = new Mutex(initiallyOwned: true, InstanceNames.EditorMutex, out var createdNew);
            _editorActivateEvent = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                InstanceNames.EditorActivateEvent);

            if (createdNew)
            {
                _activationWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                    _editorActivateEvent,
                    OnActivationSignaled,
                    state: null,
                    millisecondsTimeOutInterval: Timeout.Infinite,
                    executeOnlyOnce: false);
                return true;
            }

            _editorActivateEvent.Set();
            _editorActivateEvent.Dispose();
            _editorActivateEvent = null;
            _editorMutex.Dispose();
            _editorMutex = null;
            return false;
        }
        catch (Exception ex)
        {
            App.LogFatal(ex, "SingleInstance");
            return true;
        }
    }

    public async Task StartAsync()
    {
        _services = BuildServices();

        var coordinator = _services.GetRequiredService<StartupCoordinator>();
        await coordinator.StartEditorAsync();

        StartFlyoutServer();
    }

    private void StartFlyoutServer()
    {
        if (_services is null)
        {
            return;
        }

        var launcher = _services.GetRequiredService<FlyoutLauncher>();
        _flyoutServer = new FlyoutIpcServer(
            groupId => HandleFlyoutRequestAsync(launcher, groupId),
            _services.GetRequiredService<ILogger<FlyoutIpcServer>>());
        _flyoutServer.Start();
    }

    private static Task HandleFlyoutRequestAsync(FlyoutLauncher launcher, string groupId)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() => launcher.Show(groupId, showErrorDialog: false, applyTheme: false)).Task;
    }

    public void Dispose()
    {
        _flyoutServer?.Dispose();
        _flyoutServer = null;

        if (_services is not null)
        {
            _services.Dispose();
            _services = null;
        }

        _activationWaitHandle?.Unregister(null);
        _activationWaitHandle = null;

        _editorActivateEvent?.Dispose();
        _editorActivateEvent = null;

        if (_editorMutex is not null)
        {
            try
            {
                _editorMutex.ReleaseMutex();
            }
            catch
            {
                // The mutex may not be owned if startup failed early.
            }

            _editorMutex.Dispose();
            _editorMutex = null;
        }
    }

    private static void OnActivationSignaled(object? state, bool timedOut)
    {
        if (timedOut)
        {
            return;
        }

        try
        {
            Application.Current?.Dispatcher.BeginInvoke(ActivateEditorWindow);
        }
        catch (Exception)
        {
            // Application may be shutting down.
        }
    }

    private static void ActivateEditorWindow()
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(handle);
        }
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new FileLoggerProvider(AppPaths.LogFile));
        });

        services.AddSingleton<StaExecutor>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<AppFileInfoService>();
        services.AddSingleton<IconService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<GroupTransferService>();
        services.AddSingleton<TaskbarGeometryService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<WindowPlacementService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton(sp =>
        {
            var trim = new MemoryTrimService();
            var icons = sp.GetRequiredService<IconService>();
            var discovery = sp.GetRequiredService<DiscoveryService>();
            trim.RegisterCallback(icons.ClearMemoryCache);
            trim.RegisterCallback(discovery.Invalidate);
            trim.RegisterCallback(BuiltInIconCatalog.ClearCaches);
            return trim;
        });
        services.AddSingleton<StartupCoordinator>();
        services.AddSingleton(sp => FlyoutLauncher.CreateForHost(
            sp.GetRequiredService<ConfigService>(),
            sp.GetRequiredService<IconService>(),
            sp.GetRequiredService<LaunchService>(),
            sp.GetRequiredService<ThemeService>(),
            sp.GetRequiredService<TaskbarGeometryService>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<MemoryTrimService>()));

        return services.BuildServiceProvider();
    }
}

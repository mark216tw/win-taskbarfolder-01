using System.Windows;
using System.Windows.Interop;
using Mark.TaskbarFolders.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class EditorBootstrap : IDisposable
{
    private const string EditorMutexName = @"Local\TaskbarFolders.Editor";
    private const string EditorActivateEventName = @"Local\TaskbarFolders.Editor.Activate";

    private ServiceProvider? _services;
    private Mutex? _editorMutex;
    private EventWaitHandle? _editorActivateEvent;
    private Thread? _activationThread;

    public bool EnsureSingleInstance()
    {
        try
        {
            _editorMutex = new Mutex(initiallyOwned: true, EditorMutexName, out var createdNew);
            _editorActivateEvent = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                EditorActivateEventName);

            if (createdNew)
            {
                _activationThread = new Thread(WaitForActivationRequests)
                {
                    IsBackground = true,
                    Name = "TaskbarFolders.EditorActivation"
                };
                _activationThread.Start();
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
    }

    public void Dispose()
    {
        if (_services is not null)
        {
            _services.Dispose();
            _services = null;
        }

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

    private void WaitForActivationRequests()
    {
        var handle = _editorActivateEvent;
        if (handle is null)
        {
            return;
        }

        while (true)
        {
            try
            {
                if (!handle.WaitOne())
                {
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                Application.Current?.Dispatcher.BeginInvoke(() => ActivateEditorWindow());
            }
            catch (Exception)
            {
                return;
            }
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
        services.AddSingleton<IconService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<GroupTransferService>();
        services.AddSingleton<TaskbarGeometryService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<WindowPlacementService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<StartupCoordinator>();

        return services.BuildServiceProvider();
    }
}

using System.IO;
using System.Windows;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Services;

namespace Mark.TaskbarFolders;

public partial class App : Application
{
    private EditorBootstrap? _editor;
    private FlyoutLauncher? _flyout;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        try
        {
            var groupId = ParseGroupId(e.Args);

            var aumid = groupId is null
                ? "Mark.TaskbarFolders.Editor"
                : $"Mark.TaskbarFolders.Group.{groupId}";
            NativeMethods.SetCurrentProcessExplicitAppUserModelID(aumid);

            if (groupId is null)
            {
                var editor = new EditorBootstrap();
                if (!editor.EnsureSingleInstance())
                {
                    editor.Dispose();
                    Shutdown();
                    return;
                }

                _editor = editor;
                await editor.StartAsync();
                _ = Task.Run(IconService.PruneDefaultCache);
            }
            else
            {
                _flyout = new FlyoutLauncher();
                if (!await _flyout.ShowAsync(groupId))
                {
                    Shutdown();
                }
            }
        }
        catch (Exception ex)
        {
            LogFatal(ex, "Startup");
            MessageBox.Show(
                ex.Message,
                Mark.TaskbarFolders.Resources.Strings.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _flyout?.Dispose();
        _flyout = null;

        _editor?.Dispose();
        _editor = null;

        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogFatal(args.Exception, "Dispatcher");
            MessageBox.Show(
                args.Exception.Message,
                Mark.TaskbarFolders.Resources.Strings.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                LogFatal(exception, "AppDomain");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogFatal(args.Exception, "TaskScheduler");
            args.SetObserved();
        };
    }

    internal static void LogFatal(Exception exception, string source)
    {
        try
        {
            File.AppendAllText(
                AppPaths.LogFile,
                $"{DateTimeOffset.Now:O} [Fatal:{source}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take down the application.
        }
    }

    private static string? ParseGroupId(IEnumerable<string> arguments)
    {
        string? result = null;
        var args = arguments.ToList();
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.Equals("--group", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                result = args[index + 1];
            }
            else if (argument.StartsWith("--group=", StringComparison.OrdinalIgnoreCase))
            {
                result = argument["--group=".Length..];
            }
        }

        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }
}

using System.IO;
using System.Runtime;
using System.Windows;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Services;

namespace Mark.TaskbarFolders;

public static class Program
{
    private static readonly TimeSpan FlyoutIpcTimeout = TimeSpan.FromMilliseconds(1200);

    [STAThread]
    public static int Main(string[] args)
    {
        var groupId = CommandLineParser.ParseGroupId(args);
        if (groupId is not null)
        {
            if (FlyoutIpc.IsEditorRunning() && FlyoutIpc.TrySend(groupId, FlyoutIpcTimeout))
            {
                return 0;
            }

            if (!FlyoutInstanceGuard.TryAcquire(groupId))
            {
                return 0;
            }
        }

        EnableMulticoreJit(groupId is null ? "editor.profile" : "flyout.profile");

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static void EnableMulticoreJit(string profileName)
    {
        try
        {
            var directory = AppPaths.ProfileDirectory;
            Directory.CreateDirectory(directory);
            ProfileOptimization.SetProfileRoot(directory);
            ProfileOptimization.StartProfile(profileName);
        }
        catch
        {
            // Multicore JIT profiling is best effort.
        }
    }
}

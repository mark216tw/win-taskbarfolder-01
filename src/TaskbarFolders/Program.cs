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

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}

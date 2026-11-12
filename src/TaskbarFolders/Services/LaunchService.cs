using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Services;

public sealed class LaunchService
{
    private readonly ILogger<LaunchService> _logger;

    public LaunchService(ILogger<LaunchService> logger)
    {
        _logger = logger;
    }

    public void Launch(AppItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            if (item.Kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(item.Aumid))
            {
                LaunchPackagedApp(item.Aumid);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = item.Target,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(item.Arguments))
            {
                startInfo.Arguments = item.Arguments;
            }

            if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && Directory.Exists(item.WorkingDirectory))
            {
                startInfo.WorkingDirectory = item.WorkingDirectory;
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch {Name} ({Target})", item.Name, item.Target);
            throw;
        }
    }

    private void LaunchPackagedApp(string aumid)
    {
        var type = Type.GetTypeFromCLSID(ClsidApplicationActivationManager);
        if (type is null)
        {
            throw new InvalidOperationException("ApplicationActivationManager is not registered.");
        }

        var manager = (IApplicationActivationManager)Activator.CreateInstance(type)!;
        try
        {
            var result = manager.ActivateApplication(aumid, null, 0, out _);
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(manager);
        }
    }
}

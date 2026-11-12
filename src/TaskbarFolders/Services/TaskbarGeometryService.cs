using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Services;

public sealed record FlyoutPlacement(int X, int Y, int Width, int Height, double Scale);

public sealed class TaskbarGeometryService
{
    private const string PrimaryTaskbarClass = "Shell_TrayWnd";
    private const string SecondaryTaskbarClass = "Shell_SecondaryTrayWnd";
    private const int MarginDip = 8;

    private readonly ILogger<TaskbarGeometryService> _logger;

    public TaskbarGeometryService(ILogger<TaskbarGeometryService> logger)
    {
        _logger = logger;
    }

    public FlyoutPlacement GetPlacement(IntPtr hwnd, double widthDip, double heightDip)
    {
        var monitor = GetTargetMonitor();
        var monitorInfo = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            monitorInfo = new MonitorInfo
            {
                cbSize = (uint)Marshal.SizeOf<MonitorInfo>(),
                rcMonitor = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
                rcWork = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 }
            };
        }

        var dpi = hwnd != IntPtr.Zero ? GetDpiForWindow(hwnd) : 96u;
        if (dpi == 0)
        {
            dpi = 96;
        }

        var scale = dpi / 96.0;
        var width = (int)Math.Round(widthDip * scale);
        var height = (int)Math.Round(heightDip * scale);
        var margin = (int)Math.Round(MarginDip * scale);

        var taskbar = FindTaskbar(monitorInfo.rcMonitor);
        var taskbarRect = taskbar.GetValueOrDefault();
        var hasTaskbar = taskbar is not null;
        var isHorizontalTaskbar = hasTaskbar && taskbarRect.Width >= taskbarRect.Height;
        var isBottomTaskbar = isHorizontalTaskbar &&
            taskbarRect.Top >= monitorInfo.rcMonitor.Top + (monitorInfo.rcMonitor.Height / 2);
        var isLeftTaskbar = hasTaskbar &&
            !isHorizontalTaskbar &&
            taskbarRect.Left <= monitorInfo.rcMonitor.Left + (monitorInfo.rcMonitor.Width / 2);

        var maxHeight = hasTaskbar && isHorizontalTaskbar
            ? isBottomTaskbar
                ? taskbarRect.Top - monitorInfo.rcMonitor.Top - (2 * margin)
                : monitorInfo.rcMonitor.Bottom - taskbarRect.Bottom - (2 * margin)
            : monitorInfo.rcMonitor.Height - (2 * margin);

        height = Math.Clamp(height, 1, Math.Max(1, maxHeight));

        int x;
        int y;
        if (!hasTaskbar)
        {
            x = monitorInfo.rcMonitor.Left + ((monitorInfo.rcMonitor.Width - width) / 2);
            y = monitorInfo.rcMonitor.Bottom - height - margin;
        }
        else if (isHorizontalTaskbar)
        {
            x = taskbarRect.Left + ((taskbarRect.Width - width) / 2);
            y = isBottomTaskbar
                ? taskbarRect.Top - height - margin
                : taskbarRect.Bottom + margin;
        }
        else
        {
            x = isLeftTaskbar
                ? taskbarRect.Right + margin
                : taskbarRect.Left - width - margin;
            y = taskbarRect.Top + ((taskbarRect.Height - height) / 2);
        }

        x = Math.Clamp(x, monitorInfo.rcMonitor.Left + margin, monitorInfo.rcMonitor.Right - width - margin);
        y = Math.Clamp(y, monitorInfo.rcMonitor.Top + margin, monitorInfo.rcMonitor.Bottom - height - margin);

        return new FlyoutPlacement(x, y, width, height, scale);
    }

    public void ApplyPlacement(IntPtr hwnd, FlyoutPlacement placement)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var applied = SetWindowPos(
            hwnd,
            new IntPtr(-1),
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height,
            SwpNoActivate);

        if (!applied)
        {
            _logger.LogWarning("Failed to position flyout window.");
        }
    }

    private static IntPtr GetTargetMonitor()
    {
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero)
        {
            GetWindowThreadProcessId(foreground, out var processId);
            if (processId != (uint)Environment.ProcessId)
            {
                return MonitorFromWindow(foreground, MonitorDefaultToNearest);
            }
        }

        if (!GetCursorPos(out var cursor))
        {
            return MonitorFromPoint(new Point { X = 0, Y = 0 }, MonitorDefaultToNearest);
        }

        return MonitorFromPoint(cursor, MonitorDefaultToNearest);
    }

    private static Rect? FindTaskbar(Rect monitor)
    {
        Rect? best = null;
        long bestArea = 0;

        foreach (var handle in EnumerateTaskbars())
        {
            if (!IsWindowVisible(handle) || !GetWindowRect(handle, out var rect))
            {
                continue;
            }

            var intersection = Intersect(rect, monitor);
            if (intersection is null)
            {
                continue;
            }

            var area = (long)intersection.Value.Width * intersection.Value.Height;
            if (area > bestArea)
            {
                bestArea = area;
                best = rect;
            }
        }

        return best;
    }

    private static IEnumerable<IntPtr> EnumerateTaskbars()
    {
        var primary = FindWindow(PrimaryTaskbarClass, null);
        if (primary != IntPtr.Zero)
        {
            yield return primary;
        }

        var previous = IntPtr.Zero;
        while (true)
        {
            var next = FindWindowEx(IntPtr.Zero, previous, SecondaryTaskbarClass, null);
            if (next == IntPtr.Zero)
            {
                yield break;
            }

            yield return next;
            previous = next;
        }
    }

    private static Rect? Intersect(Rect a, Rect b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        if (right <= left || bottom <= top)
        {
            return null;
        }

        return new Rect { Left = left, Top = top, Right = right, Bottom = bottom };
    }
}

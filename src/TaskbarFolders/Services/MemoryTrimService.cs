using System.Runtime;
using Mark.TaskbarFolders.Helpers;

namespace Mark.TaskbarFolders.Services;

public sealed class MemoryTrimService : IDisposable
{
    private readonly List<Action> _trimCallbacks = [];
    private readonly Action _collectAndTrimProcess;
    private readonly object _sync = new();
    private Timer? _timer;
    private bool _disposed;

    public MemoryTrimService(Action? collectAndTrimProcess = null)
    {
        _collectAndTrimProcess = collectAndTrimProcess ?? CollectAndTrimCurrentProcess;
    }

    public void RegisterCallback(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _trimCallbacks.Add(callback);
        }
    }

    public void ScheduleTrim(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer ??= new Timer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void CancelPendingTrim()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void TrimNow()
    {
        Action[] callbacks;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            callbacks = [.. _trimCallbacks];
        }

        foreach (var callback in callbacks)
        {
            try
            {
                callback();
            }
            catch
            {
                // Memory trimming callbacks are best effort.
            }
        }

        try
        {
            _collectAndTrimProcess();
        }
        catch
        {
            // Process working-set trimming is best effort.
        }
    }

    private void OnTimerElapsed(object? state) => TrimNow();

    private static void CollectAndTrimCurrentProcess()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        NativeMethods.SetProcessWorkingSetSizeEx(
            NativeMethods.GetCurrentProcess(),
            (nint)(-1),
            (nint)(-1),
            0);
    }

    public void Dispose()
    {
        Timer? timer;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timer = _timer;
            _timer = null;
            _trimCallbacks.Clear();
        }

        timer?.Dispose();
    }
}

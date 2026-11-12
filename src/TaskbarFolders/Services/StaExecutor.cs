using System.Collections.Concurrent;

namespace Mark.TaskbarFolders.Services;

public sealed class StaExecutor : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly object _sync = new();
    private Thread? _thread;
    private bool _disposed;

    public Task<T> InvokeAsync<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureThread();

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                completion.SetResult(callback());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }

    public Task InvokeAsync(Action callback) =>
        InvokeAsync(() =>
        {
            callback();
            return true;
        });

    private void EnsureThread()
    {
        if (_thread is not null)
        {
            return;
        }

        lock (_sync)
        {
            if (_thread is not null)
            {
                return;
            }

            var thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "TaskbarFolders.ShellSta"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _thread = thread;
        }
    }

    private void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch
            {
                // The caller observes failures through the returned task.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
    }
}

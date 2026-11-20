using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public static class FlyoutIpc
{
    internal const string Acknowledge = "OK";

    internal const string Reject = "ERR";

    private const int BufferSize = 1024;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(400);

    public static bool IsEditorRunning()
    {
        try
        {
            if (Mutex.TryOpenExisting(InstanceNames.EditorMutex, out var mutex))
            {
                mutex.Dispose();
                return true;
            }
        }
        catch
        {
            // Probing the editor mutex is best effort.
        }

        return false;
    }

    public static bool TrySend(string groupId, TimeSpan timeout, string? pipeName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        try
        {
            return TrySendAsync(groupId, timeout, pipeName ?? InstanceNames.FlyoutPipe)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TrySendAsync(string groupId, TimeSpan timeout, string pipeName)
    {
        using var connectSource = new CancellationTokenSource(ConnectTimeout);
        using var responseSource = new CancellationTokenSource(timeout);

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await client.ConnectAsync(connectSource.Token).ConfigureAwait(false);

        await using (var writer = new StreamWriter(client, new UTF8Encoding(false), BufferSize, leaveOpen: true)
        {
            AutoFlush = true
        })
        {
            await writer.WriteLineAsync(groupId.AsMemory(), responseSource.Token).ConfigureAwait(false);
        }

        using var reader = new StreamReader(client, Encoding.UTF8, false, BufferSize, leaveOpen: true);
        var response = await reader.ReadLineAsync(responseSource.Token).ConfigureAwait(false);
        return string.Equals(response, Acknowledge, StringComparison.Ordinal);
    }
}

public sealed class FlyoutIpcServer : IDisposable
{
    private const int MaxInstances = 2;

    private const int BufferSize = 1024;

    private readonly Func<string, Task> _handler;
    private readonly ILogger<FlyoutIpcServer> _logger;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listeners;
    private bool _disposed;

    public FlyoutIpcServer(
        Func<string, Task> handler,
        ILogger<FlyoutIpcServer> logger,
        string? pipeName = null)
    {
        _handler = handler;
        _logger = logger;
        _pipeName = pipeName ?? InstanceNames.FlyoutPipe;
    }

    public void Start()
    {
        _listeners ??= Task.WhenAll(
            Enumerable.Range(0, MaxInstances).Select(_ => ListenAsync(_cts.Token)));
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    MaxInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Flyout IPC listener failed.");

                try
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        string groupId;
        using (var reader = new StreamReader(server, Encoding.UTF8, false, BufferSize, leaveOpen: true))
        {
            groupId = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        }

        var response = FlyoutIpc.Reject;
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            try
            {
                await _handler(groupId.Trim()).ConfigureAwait(false);
                response = FlyoutIpc.Acknowledge;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Flyout request failed for group {Group}", groupId);
            }
        }

        using (var writer = new StreamWriter(server, new UTF8Encoding(false), BufferSize, leaveOpen: true)
        {
            AutoFlush = true
        })
        {
            await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        try
        {
            server.WaitForPipeDrain();
        }
        catch (IOException)
        {
            // The client may already have gone away.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}

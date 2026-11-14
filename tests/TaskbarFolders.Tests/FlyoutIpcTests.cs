using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TaskbarFolders.Tests;

public sealed class FlyoutIpcTests
{
    [Fact]
    public async Task Send_DeliversGroupIdAndAcknowledges()
    {
        var pipeName = "TaskbarFolders.Tests." + Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new FlyoutIpcServer(
            groupId =>
            {
                received.TrySetResult(groupId);
                return Task.CompletedTask;
            },
            NullLogger<FlyoutIpcServer>.Instance,
            pipeName);
        server.Start();

        var sent = await Task.Run(
            () => FlyoutIpc.TrySend("group-1", TimeSpan.FromSeconds(5), pipeName));

        Assert.True(sent);
        Assert.Equal("group-1", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Send_WhenServerRejects_ReturnsFalse()
    {
        var pipeName = "TaskbarFolders.Tests." + Guid.NewGuid().ToString("N");

        using var server = new FlyoutIpcServer(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<FlyoutIpcServer>.Instance,
            pipeName);
        server.Start();

        var sent = await Task.Run(
            () => FlyoutIpc.TrySend("group-1", TimeSpan.FromSeconds(5), pipeName));

        Assert.False(sent);
    }

    [Fact]
    public void Send_WhenNoServer_ReturnsFalse()
    {
        var pipeName = "TaskbarFolders.Tests." + Guid.NewGuid().ToString("N");

        var sent = FlyoutIpc.TrySend("group-1", TimeSpan.FromMilliseconds(150), pipeName);

        Assert.False(sent);
    }
}

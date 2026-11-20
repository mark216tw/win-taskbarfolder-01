using Mark.TaskbarFolders.Services;

namespace TaskbarFolders.Tests;

public sealed class MemoryTrimServiceTests
{
    [Fact]
    public void TrimNow_InvokesRegisteredCallbacksAndProcessTrim()
    {
        var callbackCount = 0;
        var processTrimCount = 0;
        using var service = new MemoryTrimService(() => processTrimCount++);
        service.RegisterCallback(() => callbackCount++);
        service.RegisterCallback(() => callbackCount++);

        service.TrimNow();

        Assert.Equal(2, callbackCount);
        Assert.Equal(1, processTrimCount);
    }

    [Fact]
    public async Task ScheduleTrim_InvokesAfterDelay()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new MemoryTrimService(() => signal.TrySetResult());

        service.ScheduleTrim(TimeSpan.FromMilliseconds(40));

        await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CancelPendingTrim_PreventsScheduledExecution()
    {
        var count = 0;
        using var service = new MemoryTrimService(() => Interlocked.Increment(ref count));

        service.ScheduleTrim(TimeSpan.FromMilliseconds(120));
        service.CancelPendingTrim();

        await Task.Delay(250);

        Assert.Equal(0, Volatile.Read(ref count));
    }
}

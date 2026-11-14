using Mark.TaskbarFolders.Services;

namespace TaskbarFolders.Tests;

public sealed class FlyoutInstanceGuardTests
{
    [Fact]
    public void BuildName_IsStableAndDistinct()
    {
        var first = FlyoutInstanceGuard.BuildName("gaming");
        var second = FlyoutInstanceGuard.BuildName("gaming");
        var other = FlyoutInstanceGuard.BuildName("work");

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.StartsWith(@"Local\TaskbarFolders.Flyout.", first);
    }

    [Fact]
    public void TryAcquire_ThenRelease_AllowsReacquire()
    {
        var groupId = Guid.NewGuid().ToString("N");

        Assert.True(FlyoutInstanceGuard.TryAcquire(groupId));
        FlyoutInstanceGuard.Release();
        Assert.True(FlyoutInstanceGuard.TryAcquire(groupId));
        FlyoutInstanceGuard.Release();
    }

    [Fact]
    public void TryAcquire_SameProcessIsIdempotent()
    {
        var groupId = Guid.NewGuid().ToString("N");

        Assert.True(FlyoutInstanceGuard.TryAcquire(groupId));
        Assert.True(FlyoutInstanceGuard.TryAcquire(groupId));
        FlyoutInstanceGuard.Release();
    }
}

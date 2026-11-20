using Mark.TaskbarFolders.Services;

namespace TaskbarFolders.Tests;

public sealed class IconServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "TaskbarFoldersTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void PruneCache_RemovesOnlyExpiredEntries()
    {
        Directory.CreateDirectory(_root);
        var stale = Path.Combine(_root, "stale.png");
        var fresh = Path.Combine(_root, "fresh.png");
        File.WriteAllBytes(stale, [0]);
        File.WriteAllBytes(fresh, [0]);
        File.SetLastWriteTimeUtc(stale, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var removed = IconService.PruneCache(_root, DateTime.UtcNow.AddDays(-30));

        Assert.Equal(1, removed);
        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void PruneDefaultCache_RunsAtMostOncePerDay()
    {
        var now = DateTime.UtcNow;
        Directory.CreateDirectory(_root);
        var first = Path.Combine(_root, "first.png");
        File.WriteAllBytes(first, [0]);
        File.SetLastWriteTimeUtc(first, now.AddDays(-40));

        Assert.Equal(1, IconService.PruneDefaultCache(_root, now));
        Assert.False(File.Exists(first));

        var second = Path.Combine(_root, "second.png");
        File.WriteAllBytes(second, [0]);
        File.SetLastWriteTimeUtc(second, now.AddDays(-40));

        Assert.Equal(0, IconService.PruneDefaultCache(_root, now.AddHours(1)));
        Assert.True(File.Exists(second));

        Assert.Equal(1, IconService.PruneDefaultCache(_root, now.AddHours(25)));
        Assert.False(File.Exists(second));
    }

    [Theory]
    [InlineData(16, 48)]
    [InlineData(32, 64)]
    [InlineData(40, 80)]
    [InlineData(128, 96)]
    public void GetMaxPixelSize_ClampsToExpectedRange(int requestedSize, int expected)
    {
        Assert.Equal(expected, IconService.GetMaxPixelSize(requestedSize));
    }
}

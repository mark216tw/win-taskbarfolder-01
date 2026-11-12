using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging;

namespace TaskbarFolders.Tests;

public sealed class FileLoggerProviderTests : IDisposable
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
    public void Constructor_KeepsSmallLogFile()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "taskbarfolders.log");
        File.WriteAllText(path, "existing");

        using var provider = new FileLoggerProvider(path);

        Assert.False(File.Exists(path + ".1"));
        Assert.Equal("existing", File.ReadAllText(path));
    }

    [Fact]
    public void Constructor_RotatesOversizedLogFile()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "taskbarfolders.log");
        File.WriteAllText(path, new string('x', (1024 * 1024) + 100));

        using var provider = new FileLoggerProvider(path);
        var logger = provider.CreateLogger("test");
        logger.LogInformation("hello");

        Assert.True(File.Exists(path + ".1"));
        Assert.True(File.Exists(path));
        Assert.Contains("hello", File.ReadAllText(path));
    }
}

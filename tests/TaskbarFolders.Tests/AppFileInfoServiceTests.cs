using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging.Abstractions;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace TaskbarFolders.Tests;

public sealed class AppFileInfoServiceTests
{
    [Fact]
    public void ReadFileDescription_ReturnsDescriptionForExecutable()
    {
        using var sta = new StaExecutor();
        var service = CreateService(sta);

        var description = service.ReadFileDescription(GetNotepadPath());

        Assert.False(string.IsNullOrWhiteSpace(description));
    }

    [Fact]
    public void ReadFileDescription_ReturnsNullForNonExecutableContent()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var path = Path.Combine(directory, "fake.exe");
            File.WriteAllText(path, "not a PE file");
            var service = CreateService(sta);

            var description = service.ReadFileDescription(path);

            Assert.Null(description);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ReadFileDescription_ReturnsNullForMissingFile()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var service = CreateService(sta);

            var description = service.ReadFileDescription(Path.Combine(directory, "missing.exe"));

            Assert.Null(description);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_ReturnsNullForNonExecutableExtension()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var path = Path.Combine(directory, "sample.txt");
            File.WriteAllText(path, "hello");
            var service = CreateService(sta);

            var name = await service.ResolveDisplayNameAsync(path);

            Assert.Null(name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_ReturnsDescriptionForExecutable()
    {
        using var sta = new StaExecutor();
        var service = CreateService(sta);

        var name = await service.ResolveDisplayNameAsync(GetNotepadPath());

        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_ReadsShortcutTargetDescription()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var path = await CreateShortcutAsync(sta, directory, Path.Combine(directory, "sample.lnk"), GetNotepadPath());
            var service = CreateService(sta);

            var name = await service.ResolveDisplayNameAsync(path);

            Assert.False(string.IsNullOrWhiteSpace(name));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_IgnoresShortcutWithAumid()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var shortcuts = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup { Id = "sample", Name = "Sample" };
            var path = await shortcuts.CreateShortcutAsync(group);
            var service = CreateService(sta);

            var name = await service.ResolveDisplayNameAsync(path);

            Assert.Null(name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static AppFileInfoService CreateService(StaExecutor sta) =>
        new(sta, NullLogger<AppFileInfoService>.Instance);

    private static string GetNotepadPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");

    private static Task<string> CreateShortcutAsync(
        StaExecutor sta,
        string directory,
        string shortcutPath,
        string target) =>
        sta.InvokeAsync(() =>
        {
            Directory.CreateDirectory(directory);
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(target);
                link.SetWorkingDirectory(Path.GetDirectoryName(target) ?? directory);
                ((IPersistFile)link).Save(shortcutPath, true);
                return shortcutPath;
            }
            finally
            {
                ShellPropertyHelpers.ReleaseComObject(link);
            }
        });

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TaskbarFoldersTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

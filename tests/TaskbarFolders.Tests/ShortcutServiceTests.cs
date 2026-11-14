using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TaskbarFolders.Tests;

public sealed class ShortcutServiceTests
{
    [Fact]
    public async Task CreateShortcut_WritesAppUserModelId()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "TaskbarFoldersTests",
            Guid.NewGuid().ToString("N"));

        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup { Id = "games", Name = "遊戲" };

            var path = await service.CreateShortcutAsync(group);

            Assert.True(File.Exists(path));
            Assert.Equal("遊戲.lnk", Path.GetFileName(path));
            Assert.Equal(group.Aumid, service.ReadShortcutAumid(path));
            Assert.True(File.Exists(Path.Combine(directory, "games.auto.ico")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void GetShortcutPath_RemovesInvalidFileNameCharacters()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup { Id = "tools", Name = "工具/常用:精選" };

            var path = service.GetShortcutPath(group);

            Assert.Equal("工具_常用_精選.lnk", Path.GetFileName(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void GetShortcutPath_UsesFallbackForEmptyName()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup { Id = "empty", Name = "   " };

            var path = service.GetShortcutPath(group);

            Assert.Equal("分類.lnk", Path.GetFileName(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task CreateShortcut_SkipsRewriteWhenSignatureUnchanged()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup
            {
                Id = "tools",
                Name = "工具",
                IconPath = BuiltInIconCatalog.DefaultIconReference
            };

            var path = await service.CreateShortcutAsync(group);

            Assert.True(File.Exists(Path.Combine(directory, "tools.work-folder.builtin.ico")));

            var oldTimestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, oldTimestamp);

            var secondPath = await service.CreateShortcutAsync(group);

            Assert.Equal(path, secondPath);
            Assert.Equal(oldTimestamp, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task SyncAll_ResolvesDuplicateGroupNames()
    {
        var directory = CreateDirectory();
        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var groups = new List<AppGroup>
            {
                new() { Id = "first", Name = "工具", Order = 0 },
                new() { Id = "second", Name = "工具", Order = 1 }
            };

            var paths = await service.SyncAllAsync(groups);

            Assert.Equal(2, paths.Count);
            Assert.Contains(paths, path => Path.GetFileName(path) == "工具.lnk");
            Assert.Contains(paths, path => Path.GetFileName(path) == "工具 (2).lnk");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateDirectory() =>
        Path.Combine(Path.GetTempPath(), "TaskbarFoldersTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

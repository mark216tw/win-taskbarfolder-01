using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TaskbarFolders.Tests;

public sealed class BuiltInIconCatalogTests
{
    [Fact]
    public void DefaultIconReference_ResolvesToFolder()
    {
        var definition = BuiltInIconCatalog.Find(BuiltInIconCatalog.DefaultIconReference);

        Assert.NotNull(definition);
        Assert.Equal("folder", definition!.Id);
    }

    [Fact]
    public void All_HaveUniqueIds()
    {
        var distinct = BuiltInIconCatalog.All
            .Select(definition => definition.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(BuiltInIconCatalog.All.Count, distinct);
    }

    [Fact]
    public async Task RendersAllDefinitions()
    {
        using var sta = new StaExecutor();
        await sta.InvokeAsync(() =>
        {
            foreach (var definition in BuiltInIconCatalog.All)
            {
                var image = BuiltInIconCatalog.CreateImage(definition.Id);

                Assert.NotNull(image);
                Assert.Equal(256, image!.PixelWidth);
            }
        });
    }

    [Fact]
    public async Task Shortcut_UsesBuiltInIconAndGroupName()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "TaskbarFoldersTests",
            Guid.NewGuid().ToString("N"));

        using var sta = new StaExecutor();
        try
        {
            var service = new ShortcutService(sta, NullLogger<ShortcutService>.Instance, directory);
            var group = new AppGroup
            {
                Id = "games",
                Name = "遊戲",
                IconPath = BuiltInIconCatalog.CreateReference("games")
            };

            var path = await service.CreateShortcutAsync(group);

            Assert.Equal("遊戲.lnk", Path.GetFileName(path));
            Assert.True(File.Exists(Path.Combine(directory, "games.games.builtin.ico")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

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
        Assert.Equal("work-folder", definition!.Id);
    }

    [Fact]
    public void All_HasSixtyIcons()
    {
        Assert.Equal(60, BuiltInIconCatalog.All.Count);
    }

    [Fact]
    public void Themes_HasSixThemes()
    {
        Assert.Equal(6, BuiltInIconCatalog.Themes.Count);
    }

    [Fact]
    public void EachTheme_HasTenIcons()
    {
        foreach (var theme in BuiltInIconCatalog.Themes)
        {
            var count = BuiltInIconCatalog.All.Count(definition => string.Equals(
                definition.ThemeId,
                theme.Id,
                StringComparison.OrdinalIgnoreCase));

            Assert.Equal(10, count);
        }
    }

    [Fact]
    public void EveryDefinition_HasKnownTheme()
    {
        var themeIds = BuiltInIconCatalog.Themes
            .Select(theme => theme.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(BuiltInIconCatalog.All, definition => Assert.Contains(definition.ThemeId, themeIds));
    }

    [Fact]
    public void Colors_AreVariedWithinEveryTheme()
    {
        Assert.True(BuiltInIconCatalog.All.Select(definition => definition.Color).Distinct().Count() >= 8);

        foreach (var theme in BuiltInIconCatalog.Themes)
        {
            var colors = BuiltInIconCatalog.All
                .Where(definition => string.Equals(
                    definition.ThemeId,
                    theme.Id,
                    StringComparison.OrdinalIgnoreCase))
                .Select(definition => definition.Color)
                .Distinct()
                .Count();

            Assert.True(colors > 1, $"{theme.Id} icons should not share a single fixed color.");
        }
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
    public void RetiredIcon_NormalizesToDefault()
    {
        Assert.Null(BuiltInIconCatalog.Find("builtin:code"));
        Assert.False(BuiltInIconCatalog.IsKnown("builtin:code"));
        Assert.Equal(BuiltInIconCatalog.DefaultIconReference, BuiltInIconCatalog.NormalizeReference("builtin:code"));
        Assert.Equal(BuiltInIconCatalog.DefaultIconReference, BuiltInIconCatalog.NormalizeReference("builtin:fruit-apple"));
        Assert.Equal(BuiltInIconCatalog.DefaultIconReference, BuiltInIconCatalog.NormalizeReference("builtin:animal-cat"));
    }

    [Fact]
    public void UnknownReference_NormalizesToDefault()
    {
        Assert.Equal(BuiltInIconCatalog.DefaultIconReference, BuiltInIconCatalog.NormalizeReference(null));
        Assert.Equal(BuiltInIconCatalog.DefaultIconReference, BuiltInIconCatalog.NormalizeReference("C:\\icons\\custom.ico"));
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
                Assert.True(HasWhiteGlyph(image), $"{definition.Id} did not render a visible glyph.");
            }
        });
    }

    private static bool HasWhiteGlyph(System.Windows.Media.Imaging.BitmapSource image)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index] > 240 && pixels[index + 1] > 240 && pixels[index + 2] > 240)
            {
                return true;
            }
        }

        return false;
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
                IconPath = BuiltInIconCatalog.CreateReference("games-gamepad")
            };

            var path = await service.CreateShortcutAsync(group);

            Assert.Equal("遊戲.lnk", Path.GetFileName(path));
            Assert.True(File.Exists(Path.Combine(directory, "games.games-gamepad.builtin.ico")));
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

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
    public async Task CreateImage_UsesDefaultBackgroundWhenColorOmitted()
    {
        using var sta = new StaExecutor();

        var image = await sta.InvokeAsync(() => BuiltInIconCatalog.CreateImage("work-folder"));

        Assert.NotNull(image);
        AssertBackgroundColor(image!, BuiltInIconCatalog.DefaultBackground);
    }

    [Fact]
    public async Task CreateImage_UsesRequestedBackground()
    {
        using var sta = new StaExecutor();
        var theme = GroupThemeCatalog.Find("Blue");
        Assert.NotNull(theme);

        var image = await sta.InvokeAsync(
            () => BuiltInIconCatalog.CreateImage("work-folder", theme!.Swatch));

        Assert.NotNull(image);
        AssertBackgroundColor(image!, theme!.Swatch);
    }

    [Fact]
    public async Task CreateImage_CachesPerBackground()
    {
        using var sta = new StaExecutor();
        var red = System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0x00);

        var defaultImage = await sta.InvokeAsync(() => BuiltInIconCatalog.CreateImage("work-folder"));
        var redImage = await sta.InvokeAsync(() => BuiltInIconCatalog.CreateImage("work-folder", red));

        Assert.NotNull(defaultImage);
        Assert.NotNull(redImage);
        Assert.NotSame(defaultImage, redImage);
        AssertBackgroundColor(defaultImage!, BuiltInIconCatalog.DefaultBackground);
        AssertBackgroundColor(redImage!, red);
    }

    [Fact]
    public async Task CreatePreviewImage_RendersCompactBitmap()
    {
        using var sta = new StaExecutor();

        var preview = await sta.InvokeAsync(() => BuiltInIconCatalog.CreatePreviewImage("work-folder"));

        Assert.NotNull(preview);
        Assert.Equal(BuiltInIconCatalog.PreviewBitmapSize, preview!.PixelWidth);
        Assert.Equal(BuiltInIconCatalog.PreviewBitmapSize, preview.PixelHeight);
        Assert.True(HasWhiteGlyph(preview));
    }

    [Fact]
    public async Task ClearCaches_AllowsFreshRender()
    {
        using var sta = new StaExecutor();

        var first = await sta.InvokeAsync(() => BuiltInIconCatalog.CreatePreviewImage("work-folder"));
        BuiltInIconCatalog.ClearCaches();
        var second = await sta.InvokeAsync(() => BuiltInIconCatalog.CreatePreviewImage("work-folder"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
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

    private static void AssertBackgroundColor(
        System.Windows.Media.Imaging.BitmapSource image,
        System.Windows.Media.Color expected)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        var offset = (8 * stride) + (128 * 4);

        Assert.Equal(expected.B, pixels[offset]);
        Assert.Equal(expected.G, pixels[offset + 1]);
        Assert.Equal(expected.R, pixels[offset + 2]);
        Assert.Equal(255, pixels[offset + 3]);
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
            Assert.True(File.Exists(Path.Combine(directory, "games.games-gamepad.None.builtin.ico")));
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

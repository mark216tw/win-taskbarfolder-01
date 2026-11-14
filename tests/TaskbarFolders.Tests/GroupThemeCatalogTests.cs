using Mark.TaskbarFolders.Helpers;

namespace TaskbarFolders.Tests;

public sealed class GroupThemeCatalogTests
{
    [Fact]
    public void All_HasSeventeenPresets()
    {
        Assert.Equal(17, GroupThemeCatalog.All.Count);
    }

    [Fact]
    public void All_HaveUniqueIds()
    {
        var distinct = GroupThemeCatalog.All
            .Select(definition => definition.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(GroupThemeCatalog.All.Count, distinct);
    }

    [Fact]
    public void Find_ReturnsNullForNoneAndUnknown()
    {
        Assert.Null(GroupThemeCatalog.Find("None"));
        Assert.Null(GroupThemeCatalog.Find(null));
        Assert.Null(GroupThemeCatalog.Find("missing"));
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var definition = GroupThemeCatalog.Find("yellow");

        Assert.NotNull(definition);
        Assert.Equal("Yellow", definition!.Id);
    }

    [Fact]
    public void Palettes_HaveContrastingSurfaceAndText()
    {
        foreach (var definition in GroupThemeCatalog.All)
        {
            Assert.NotEqual(definition.Light.Surface, definition.Light.Text);
            Assert.NotEqual(definition.Dark.Surface, definition.Dark.Text);
        }
    }
}

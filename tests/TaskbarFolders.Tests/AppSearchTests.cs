using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;

namespace TaskbarFolders.Tests;

public sealed class AppSearchTests
{
    private static readonly AppItem[] Items =
    [
        new() { Name = "Visual Studio Code", Target = @"C:\Apps\Code.exe" },
        new() { Name = "Steam", Target = @"C:\Games\Steam.exe" },
        new() { Name = "記事本", Target = @"C:\Windows\notepad.exe" }
    ];

    [Fact]
    public void Filter_EmptyQuery_ReturnsAll()
    {
        var result = AppSearch.Filter(Items, "  ");

        Assert.Equal(Items.Length, result.Count);
    }

    [Fact]
    public void Filter_MatchesNameCaseInsensitively()
    {
        var result = AppSearch.Filter(Items, "steam");

        Assert.Equal("Steam", Assert.Single(result).Name);
    }

    [Fact]
    public void Filter_MatchesTarget()
    {
        var result = AppSearch.Filter(Items, "notepad");

        Assert.Equal("記事本", Assert.Single(result).Name);
    }
}

using Mark.TaskbarFolders.Helpers;

namespace TaskbarFolders.Tests;

public sealed class CommandLineParserTests
{
    [Fact]
    public void ParseGroupId_ReadsSeparatedArgument()
    {
        var groupId = CommandLineParser.ParseGroupId(["--group", "gaming"]);

        Assert.Equal("gaming", groupId);
    }

    [Fact]
    public void ParseGroupId_ReadsInlineArgument()
    {
        var groupId = CommandLineParser.ParseGroupId(["--group=gaming"]);

        Assert.Equal("gaming", groupId);
    }

    [Fact]
    public void ParseGroupId_TrimsWhitespace()
    {
        var groupId = CommandLineParser.ParseGroupId(["--group", "  gaming  "]);

        Assert.Equal("gaming", groupId);
    }

    [Fact]
    public void ParseGroupId_ReturnsNullWithoutArgument()
    {
        Assert.Null(CommandLineParser.ParseGroupId([]));
        Assert.Null(CommandLineParser.ParseGroupId(["--other", "value"]));
    }

    [Fact]
    public void ParseGroupId_ReturnsNullWithEmptyValue()
    {
        Assert.Null(CommandLineParser.ParseGroupId(["--group", "   "]));
        Assert.Null(CommandLineParser.ParseGroupId(["--group="]));
    }
}

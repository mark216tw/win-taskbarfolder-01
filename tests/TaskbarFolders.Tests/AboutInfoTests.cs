using System.Reflection;
using Mark.TaskbarFolders.Helpers;

namespace TaskbarFolders.Tests;

public sealed class AboutInfoTests
{
    [Fact]
    public void Version_ComesFromAssemblyInformationalVersion()
    {
        var expected = typeof(AboutInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+')[0];

        Assert.Equal(expected, AboutInfo.Version);
    }

    [Fact]
    public void FileVersion_ComesFromAssemblyFileVersion()
    {
        var expected = typeof(AboutInfo).Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()!
            .Version;

        Assert.Equal(expected, AboutInfo.FileVersion);
    }

    [Fact]
    public void Copyright_ComesFromAssemblyCopyright()
    {
        var expected = typeof(AboutInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()!
            .Copyright;

        Assert.Equal(expected, AboutInfo.Copyright);
    }
}

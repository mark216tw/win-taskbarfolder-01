using System.Reflection;

namespace Mark.TaskbarFolders.Helpers;

public static class AboutInfo
{
    public static string Version { get; } = ReadInformationalVersion();

    public static string FileVersion { get; } = ReadFileVersion();

    public static string Copyright { get; } = ReadCopyright();

    private static string ReadInformationalVersion()
    {
        var assembly = typeof(AboutInfo).Assembly;
        var value = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(value))
        {
            var metadataIndex = value.IndexOf('+');
            return metadataIndex >= 0 ? value[..metadataIndex] : value;
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static string ReadFileVersion() =>
        typeof(AboutInfo).Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version
        ?? typeof(AboutInfo).Assembly.GetName().Version?.ToString()
        ?? "1.0.0.0";

    private static string ReadCopyright() =>
        typeof(AboutInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()
            ?.Copyright
        ?? string.Empty;
}

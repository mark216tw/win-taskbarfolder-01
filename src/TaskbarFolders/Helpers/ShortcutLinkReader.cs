using System.IO;
using System.Text;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Helpers;

internal readonly record struct ShortcutLinkInfo(
    string Target,
    string? Arguments,
    string? WorkingDirectory,
    string? Aumid,
    bool HasPropertyAumid);

internal static class ShortcutLinkReader
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    public static ShortcutLinkInfo? TryRead(string path, bool includePropertyAumid = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);

            var target = ReadString(link, static (shellLink, buffer) =>
                shellLink.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0));
            var arguments = ReadString(link, static (shellLink, buffer) =>
                shellLink.GetArguments(buffer, buffer.Capacity));
            var workingDirectory = ReadString(link, static (shellLink, buffer) =>
                shellLink.GetWorkingDirectory(buffer, buffer.Capacity));

            target = Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));
            arguments = arguments.Trim();
            workingDirectory = workingDirectory.Trim();

            var aumid = ExtractAumid(arguments);
            var hasPropertyAumid = includePropertyAumid &&
                aumid is null &&
                !string.IsNullOrWhiteSpace(ReadPropertyAumid(path));

            return new ShortcutLinkInfo(
                target,
                string.IsNullOrWhiteSpace(arguments) ? null : arguments,
                string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                aumid,
                hasPropertyAumid);
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(link);
        }
    }

    private static string? ExtractAumid(string arguments)
    {
        if (!arguments.Contains(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var index = arguments.IndexOf(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase);
        var aumid = arguments[(index + AppsFolderPrefix.Length)..].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(aumid) ? null : aumid;
    }

    private static string? ReadPropertyAumid(string path)
    {
        try
        {
            var iid = IidIPropertyStore;
            SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref iid, out var store);
            try
            {
                return ShellPropertyHelpers.GetStringProperty(store, PkeyAppUserModelId);
            }
            finally
            {
                ShellPropertyHelpers.ReleaseComObject(store);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(IShellLinkW link, Action<IShellLinkW, StringBuilder> read)
    {
        var buffer = new StringBuilder(2048);
        read(link, buffer);
        return buffer.ToString();
    }
}

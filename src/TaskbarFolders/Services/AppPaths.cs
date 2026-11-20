using System.IO;
namespace Mark.TaskbarFolders.Services;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarFolders");

    public static string ConfigFile => Path.Combine(Root, "config.json");

    public static string WindowStateFile => Path.Combine(Root, "window.json");

    public static string ShortcutsDirectory => Path.Combine(Root, "Shortcuts");

    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarFolders", "Logs");

    public static string LogFile => Path.Combine(LogDirectory, "taskbarfolders.log");

    public static string IconCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarFolders", "IconCache");

    public static string ProfileDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarFolders", "Profiles");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ShortcutsDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(IconCacheDirectory);
    }
}

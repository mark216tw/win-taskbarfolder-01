using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Services;

public sealed class ShortcutService
{
    private readonly StaExecutor _sta;
    private readonly ILogger<ShortcutService> _logger;
    private readonly string _shortcutsDirectory;

    public ShortcutService(StaExecutor sta, ILogger<ShortcutService> logger, string? shortcutsDirectory = null)
    {
        _sta = sta;
        _logger = logger;
        _shortcutsDirectory = shortcutsDirectory ?? AppPaths.ShortcutsDirectory;
    }

    public string ShortcutsDirectory => _shortcutsDirectory;

    public string GetShortcutPath(AppGroup group, IReadOnlyList<AppGroup>? allGroups = null)
    {
        var fileName = BuildShortcutFileName(group, allGroups ?? [group]);
        return Path.Combine(_shortcutsDirectory, fileName + ".lnk");
    }

    private const int MaxShortcutNameLength = 80;

    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static string BuildShortcutFileName(AppGroup group, IReadOnlyList<AppGroup> allGroups)
    {
        var baseName = SanitizeFileName(group.Name);
        var ordered = allGroups
            .OrderBy(candidate => candidate.Order)
            .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = 0;
        var count = 0;
        foreach (var candidate in ordered)
        {
            if (!string.Equals(SanitizeFileName(candidate.Name), baseName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            count++;
            if (string.Equals(candidate.Id, group.Id, StringComparison.OrdinalIgnoreCase))
            {
                index = count;
                break;
            }
        }

        return index <= 1 ? baseName : $"{baseName} ({index})";
    }

    private static string SanitizeFileName(string? name)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var source = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        var builder = new System.Text.StringBuilder(source.Length);

        foreach (var character in source)
        {
            builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.', ' ');
        if (sanitized.Length > MaxShortcutNameLength)
        {
            sanitized = sanitized[..MaxShortcutNameLength].TrimEnd('.', ' ');
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "分類";
        }

        return ReservedFileNames.Contains(sanitized) ? "_" + sanitized : sanitized;
    }

    public Task<string> CreateShortcutAsync(AppGroup group, IReadOnlyList<AppGroup>? allGroups = null) =>
        _sta.InvokeAsync(() => CreateShortcutCore(group, GetShortcutPath(group, allGroups)));

    public Task<IReadOnlyList<string>> SyncAllAsync(IEnumerable<AppGroup> groups) =>
        _sta.InvokeAsync<IReadOnlyList<string>>(() =>
        {
            Directory.CreateDirectory(_shortcutsDirectory);
            var orderedGroups = groups.OrderBy(group => group.Order).ToList();
            var created = new List<string>();
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keepAuxiliary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in orderedGroups)
            {
                var path = GetShortcutPath(group, orderedGroups);
                try
                {
                    CreateShortcutCore(group, path);
                    created.Add(path);
                    keep.Add(path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create shortcut for group {Group}", group.Name);
                    if (File.Exists(path))
                    {
                        keep.Add(path);
                    }
                }
                finally
                {
                    TrackAuxiliaryFiles(group, keepAuxiliary);
                }
            }

            foreach (var existing in Directory.EnumerateFiles(_shortcutsDirectory))
            {
                var extension = Path.GetExtension(existing);
                var shouldKeep = true;
                if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    shouldKeep = keep.Contains(existing);
                }
                else if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".sig", StringComparison.OrdinalIgnoreCase))
                {
                    shouldKeep = keepAuxiliary.Contains(existing);
                }

                if (shouldKeep)
                {
                    continue;
                }

                try
                {
                    File.Delete(existing);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete stale shortcut artifact {Path}", existing);
                }
            }

            return created;
        });

    private void TrackAuxiliaryFiles(AppGroup group, HashSet<string> keep)
    {
        keep.Add(Path.Combine(_shortcutsDirectory, group.Id + ".sig"));
        keep.Add(GetIconPath(group));

        try
        {
            foreach (var file in Directory.EnumerateFiles(_shortcutsDirectory, group.Id + ".*"))
            {
                keep.Add(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate shortcut artifacts for group {Group}", group.Name);
        }
    }

    public string? ReadShortcutAumid(string shortcutPath)
    {
        var iid = IidIPropertyStore;
        SHGetPropertyStoreFromParsingName(shortcutPath, IntPtr.Zero, 0, ref iid, out var store);
        try
        {
            var key = PkeyAppUserModelId;
            return ShellPropertyHelpers.GetStringProperty(store, key);
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(store);
        }
    }

    public void OpenContainingFolder(string shortcutPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{shortcutPath}\"",
                UseShellExecute = true
            };
            Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open shortcut folder for {Path}", shortcutPath);
        }
    }

    private string CreateShortcutCore(AppGroup group, string shortcutPath)
    {
        Directory.CreateDirectory(_shortcutsDirectory);

        var executable = ResolveExecutable();
        var signaturePath = Path.Combine(_shortcutsDirectory, group.Id + ".sig");
        var signature = ComputeSignature(group, executable);

        if (File.Exists(shortcutPath) &&
            File.Exists(signaturePath) &&
            File.Exists(GetIconPath(group)) &&
            string.Equals(ReadSignature(signaturePath), signature, StringComparison.Ordinal))
        {
            return shortcutPath;
        }

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }

        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(executable);
            link.SetArguments($"--group \"{group.Id}\"");
            link.SetWorkingDirectory(Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory);
            link.SetDescription(group.Name);

            var icon = ResolveIconPath(group, out _);
            link.SetIconLocation(icon ?? executable, 0);

            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(link);
        }

        WriteAumid(shortcutPath, group.Aumid);
        WriteSignature(signaturePath, signature);
        return shortcutPath;
    }

    private static string ResolveExecutable() =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Unable to resolve the current executable path.");

    private static string ComputeSignature(AppGroup group, string executable)
    {
        var payload = string.Join(
            "\n",
            group.Id,
            group.Name,
            group.IconPath ?? string.Empty,
            GroupThemeCatalog.Normalize(group.ThemeColor),
            executable);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private string ReadSignature(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read shortcut signature {Path}", path);
            return string.Empty;
        }
    }

    private void WriteSignature(string path, string signature)
    {
        try
        {
            File.WriteAllText(path, signature);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write shortcut signature {Path}", path);
        }
    }

    private static void WriteAumid(string shortcutPath, string aumid)
    {
        var iid = IidIPropertyStore;
        SHGetPropertyStoreFromParsingName(shortcutPath, IntPtr.Zero, GpsReadWrite, ref iid, out var store);
        try
        {
            var key = PkeyAppUserModelId;
            ShellPropertyHelpers.SetStringProperty(store, key, aumid);
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(store);
        }
    }

    private string? ResolveIconPath(AppGroup group, out bool generated)
    {
        generated = false;
        var destinationPath = GetIconPath(group);
        var background = GroupThemeCatalog.ResolveIconBackground(group.ThemeColor);

        if (BuiltInIconCatalog.IsBuiltIn(group.IconPath))
        {
            var iconReference = BuiltInIconCatalog.NormalizeReference(group.IconPath);
            try
            {
                if (!File.Exists(destinationPath))
                {
                    BuiltInIconCatalog.SaveIco(iconReference, destinationPath, background);
                }

                CleanupStaleBuiltInIcons(group.Id, destinationPath);
                generated = true;
                return destinationPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render built-in icon for group {Group}", group.Name);
                return null;
            }
        }

        try
        {
            GenerateLetterIcon(group.Name, destinationPath, background);
            CleanupStaleLetterIcons(group.Id, destinationPath);
            generated = true;
            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate icon for group {Group}", group.Name);
            return null;
        }
    }

    private string GetIconPath(AppGroup group)
    {
        var themeToken = GetThemeToken(group.ThemeColor);

        if (!BuiltInIconCatalog.IsBuiltIn(group.IconPath))
        {
            return Path.Combine(_shortcutsDirectory, $"{group.Id}.{themeToken}.auto.ico");
        }

        var iconReference = BuiltInIconCatalog.NormalizeReference(group.IconPath);
        return Path.Combine(
            _shortcutsDirectory,
            $"{group.Id}.{GetIconId(iconReference)}.{themeToken}.builtin.ico");
    }

    private static string GetThemeToken(string? themeColor)
    {
        var normalized = GroupThemeCatalog.Normalize(themeColor);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Length == 0 ? "None" : builder.ToString();
    }

    private static string GetIconId(string? iconReference)
    {
        if (!BuiltInIconCatalog.IsBuiltIn(iconReference))
        {
            return "default";
        }

        var id = iconReference![BuiltInIconCatalog.Prefix.Length..];
        var builder = new StringBuilder(id.Length);
        foreach (var character in id)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return builder.Length == 0 ? "default" : builder.ToString();
    }

    private void CleanupStaleBuiltInIcons(string groupId, string keepPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_shortcutsDirectory, groupId + ".*builtin.ico"))
            {
                if (string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean stale built-in icon for group {Group}", groupId);
        }
    }

    private void CleanupStaleLetterIcons(string groupId, string keepPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_shortcutsDirectory, groupId + ".*auto.ico"))
            {
                if (string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean stale letter icon for group {Group}", groupId);
        }
    }

    private static void GenerateLetterIcon(string name, string destinationPath, Color background)
    {
        const int size = 256;
        var text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString();
        var brush = new SolidColorBrush(background);
        brush.Freeze();

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRoundedRectangle(brush, null, new System.Windows.Rect(0, 0, size, size), 56, 56);

            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    FontStyles.Normal,
                    FontWeights.SemiBold,
                    FontStretches.Normal),
                132,
                Brushes.White,
                1.0);

            context.DrawText(
                formatted,
                new System.Windows.Point((size - formatted.Width) / 2, (size - formatted.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        IcoWriter.Write(bitmap, destinationPath);
    }

}

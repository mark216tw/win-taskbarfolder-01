using System.IO;
using System.Runtime.InteropServices;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Services;

public sealed class DiscoveryService
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly StaExecutor _sta;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<DiscoveredApp>? _cached;
    private DateTimeOffset _cachedAt;

    public DiscoveryService(StaExecutor sta, ILogger<DiscoveryService> logger)
    {
        _sta = sta;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredApp>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cached;
        if (cached is not null && IsCacheFresh(_cachedAt, CacheDuration))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cached;
            if (cached is not null && IsCacheFresh(_cachedAt, CacheDuration))
            {
                return cached;
            }

            var discovered = await DiscoverCoreAsync(cancellationToken).ConfigureAwait(false);
            _cached = discovered;
            _cachedAt = DateTimeOffset.UtcNow;
            return discovered;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        _cached = null;
        _cachedAt = default;
    }

    internal static bool IsCacheFresh(DateTimeOffset cachedAt, TimeSpan duration) =>
        DateTimeOffset.UtcNow - cachedAt < duration;

    private async Task<IReadOnlyList<DiscoveredApp>> DiscoverCoreAsync(CancellationToken cancellationToken)
    {
        var classic = await _sta.InvokeAsync(DiscoverStartMenu).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DiscoveredApp> store;
        try
        {
            store = await _sta.InvokeAsync(DiscoverStoreApps).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Store app discovery failed.");
            store = [];
        }

        return Merge(classic, store);
    }

    private IReadOnlyList<DiscoveredApp> DiscoverStartMenu()
    {
        var results = new List<DiscoveredApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetStartMenuRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IReadOnlyList<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(IsShortcutFile)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate start menu root {Root}", root);
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    var discovered = CreateFromShortcut(file, root);
                    if (discovered is not null && seen.Add(discovered.DeduplicationKey))
                    {
                        results.Add(discovered);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping shortcut {File}", file);
                }
            }
        }

        return results
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<DiscoveredApp> DiscoverStoreApps()
    {
        var results = new List<DiscoveredApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var folderId = FolderIdAppsFolder;
        var iid = IidIShellItem2;
        SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref iid, out var folder);

        IEnumShellItems? enumerator = null;
        try
        {
            var bhid = BhidEnumItems;
            var enumIid = IidIEnumShellItems;
            folder.BindToHandler(IntPtr.Zero, ref bhid, ref enumIid, out var enumPointer);
            if (enumPointer == IntPtr.Zero)
            {
                return results;
            }

            try
            {
                enumerator = (IEnumShellItems)Marshal.GetObjectForIUnknown(enumPointer);
            }
            finally
            {
                Marshal.Release(enumPointer);
            }

            while (true)
            {
                var hr = enumerator.Next(1, out var item, out var fetched);
                if (hr != 0 || fetched == 0 || item is null)
                {
                    break;
                }

                try
                {
                    var discovered = CreateFromStoreItem(item);
                    if (discovered is not null && seen.Add(discovered.DeduplicationKey))
                    {
                        results.Add(discovered);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping store app item.");
                }
                finally
                {
                    ShellPropertyHelpers.ReleaseComObject(item);
                }
            }
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(enumerator);
            ShellPropertyHelpers.ReleaseComObject(folder);
        }

        return results
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static DiscoveredApp? CreateFromStoreItem(IShellItem item)
    {
        if (item is not IShellItem2 item2)
        {
            return null;
        }

        var name = ShellPropertyHelpers.GetDisplayName(item2);
        if (string.IsNullOrWhiteSpace(name) ||
            name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("@{", StringComparison.Ordinal))
        {
            return null;
        }

        var store = ShellPropertyHelpers.GetPropertyStore(item);
        if (store is null)
        {
            return null;
        }

        try
        {
            var aumid = ShellPropertyHelpers.GetStringProperty(store, PkeyAppUserModelId);
            if (string.IsNullOrWhiteSpace(aumid) || !aumid.Contains('!'))
            {
                return null;
            }

            return new DiscoveredApp(
                name.Trim(),
                AppsFolderPrefix + aumid,
                null,
                null,
                AppItemKind.Uwp,
                null,
                aumid,
                "Store");
        }
        finally
        {
            ShellPropertyHelpers.ReleaseComObject(store);
        }
    }

    private static DiscoveredApp? CreateFromShortcut(string path, string root)
    {
        var extension = Path.GetExtension(path);
        var category = Path.GetRelativePath(root, Path.GetDirectoryName(path) ?? root);
        if (category == ".")
        {
            category = "開始功能表";
        }

        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFromUrl(path, category);
        }

        if (extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase))
        {
            return new DiscoveredApp(
                Path.GetFileNameWithoutExtension(path),
                path,
                null,
                Path.GetDirectoryName(path),
                AppItemKind.Shortcut,
                path,
                null,
                category);
        }

        return CreateFromLink(path, category);
    }

    private static DiscoveredApp? CreateFromLink(string path, string category)
    {
        var info = ShortcutLinkReader.TryRead(path);
        if (info is null)
        {
            return null;
        }

        var link = info.Value;
        var name = Path.GetFileNameWithoutExtension(path);

        if (!string.IsNullOrWhiteSpace(link.Aumid))
        {
            return new DiscoveredApp(
                name,
                AppsFolderPrefix + link.Aumid,
                null,
                null,
                AppItemKind.Uwp,
                null,
                link.Aumid,
                category);
        }

        var target = string.IsNullOrWhiteSpace(link.Target) ? path : link.Target;
        var kind = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? AppItemKind.Executable
            : AppItemKind.Shortcut;

        return new DiscoveredApp(
            name,
            target,
            link.Arguments,
            link.WorkingDirectory ?? Path.GetDirectoryName(path),
            kind,
            path,
            null,
            category);
    }

    private static DiscoveredApp? CreateFromUrl(string path, string category)
    {
        string? url = null;
        string? iconFile = null;

        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Equals("URL", StringComparison.OrdinalIgnoreCase))
            {
                url = value;
            }
            else if (key.Equals("IconFile", StringComparison.OrdinalIgnoreCase))
            {
                iconFile = value;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new DiscoveredApp(
            Path.GetFileNameWithoutExtension(path),
            url,
            null,
            null,
            AppItemKind.Url,
            File.Exists(iconFile) ? iconFile : null,
            null,
            category);
    }

    private static bool IsShortcutFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".url", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetStartMenuRoots()
    {
        var common = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");
        var user = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");

        return new[] { common, user }.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DiscoveredApp> Merge(
        IReadOnlyList<DiscoveredApp> classic,
        IReadOnlyList<DiscoveredApp> store)
    {
        var merged = new List<DiscoveredApp>(classic.Count + store.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in classic)
        {
            if (seen.Add(app.DeduplicationKey))
            {
                merged.Add(app);
            }
        }

        foreach (var app in store)
        {
            if (seen.Add(app.DeduplicationKey))
            {
                merged.Add(app);
            }
        }

        return merged
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

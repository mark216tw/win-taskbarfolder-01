using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Services;

public sealed class IconService
{
    public const int CacheRetentionDays = 30;

    private const string PruneMarkerFileName = ".lastprune";

    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(24);

    private readonly StaExecutor _sta;
    private readonly ILogger<IconService> _logger;
    private readonly ConcurrentDictionary<string, BitmapSource?> _memoryCache = new();
    private readonly string _cacheDirectory;

    public IconService(StaExecutor sta, ILogger<IconService> logger, string? cacheDirectory = null)
    {
        _sta = sta;
        _logger = logger;
        _cacheDirectory = cacheDirectory ?? AppPaths.IconCacheDirectory;
    }

    public void ClearMemoryCache() => _memoryCache.Clear();

    public static int PruneDefaultCache() =>
        PruneDefaultCache(AppPaths.IconCacheDirectory, DateTime.UtcNow);

    public static int PruneDefaultCache(string directory, DateTime nowUtc)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var marker = Path.Combine(directory, PruneMarkerFileName);
            if (File.Exists(marker) &&
                nowUtc - File.GetLastWriteTimeUtc(marker) < PruneInterval)
            {
                return 0;
            }

            var removed = PruneCache(directory, nowUtc.AddDays(-CacheRetentionDays));
            File.WriteAllText(
                marker,
                nowUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            return removed;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<BitmapSource?> GetIconAsync(AppItem item, int size)
    {
        var memoryKey = BuildMemoryKey(item, size);
        if (_memoryCache.TryGetValue(memoryKey, out var cached))
        {
            return cached;
        }

        var fromDisk = await Task.Run(() =>
        {
            var cacheKey = BuildKey(item, size);
            return (Key: cacheKey, Icon: TryLoadFromDisk(GetCachePath(cacheKey), GetMaxPixelSize(size)));
        }).ConfigureAwait(false);

        if (fromDisk.Icon is not null)
        {
            _memoryCache[memoryKey] = fromDisk.Icon;
            return fromDisk.Icon;
        }

        return await _sta.InvokeAsync(() => LoadAndCache(item, size, fromDisk.Key, memoryKey)).ConfigureAwait(false);
    }

    public static int PruneCache(string directory, DateTime cutoffUtc)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.png"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch
                {
                    // A single unreadable cache entry must not stop the sweep.
                }
            }
        }
        catch
        {
            // Cache pruning is best effort.
        }

        return removed;
    }

    private BitmapSource? LoadAndCache(AppItem item, int size, string key, string memoryKey)
    {
        try
        {
            var icon = LoadIcon(item, size);
            if (icon is not null)
            {
                icon = DownscaleIfOversized(icon, GetMaxPixelSize(size));
                TrySaveToDisk(icon, GetCachePath(key));
            }

            _memoryCache[memoryKey] = icon;
            return icon;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load icon for {Target}", item.Target);
            return null;
        }
    }

    private static BitmapSource? LoadIcon(AppItem item, int size)
    {
        var shellPath = ResolveShellPath(item);
        if (!string.IsNullOrWhiteSpace(shellPath))
        {
            var fromShell = TryShellImage(shellPath, size);
            if (fromShell is not null)
            {
                return fromShell;
            }
        }

        return TryFileIcon(item);
    }

    private static string? ResolveShellPath(AppItem item)
    {
        if (item.Kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(item.Aumid))
        {
            return "shell:AppsFolder\\" + item.Aumid;
        }

        var path = string.IsNullOrWhiteSpace(item.IconPath) ? item.Target : item.IconPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var comma = path.LastIndexOf(',');
        if (comma > 2 && int.TryParse(path[(comma + 1)..], out _))
        {
            path = path[..comma];
        }

        return path.Trim().Trim('"');
    }

    private static BitmapSource? TryShellImage(string path, int size)
    {
        try
        {
            var iid = IidIShellItemImageFactory;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);

            try
            {
                var flags = SiigbfIconOnly | SiigbfBiggerSizeOk;
                var hResult = factory.GetImage(new NativeMethods.Size { Cx = size, Cy = size }, flags, out var bitmap);
                if (hResult < 0 || bitmap == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(bitmap);
                }
            }
            finally
            {
                ShellPropertyHelpers.ReleaseComObject(factory);
            }
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? TryFileIcon(AppItem item)
    {
        var path = ResolveShellPath(item);
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiLargeIcon);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static string BuildSource(AppItem item) =>
        item.Kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(item.Aumid)
            ? "uwp:" + item.Aumid
            : (string.IsNullOrWhiteSpace(item.IconPath) ? item.Target : item.IconPath);

    private static string BuildMemoryKey(AppItem item, int size) => $"{BuildSource(item)}|{size}";

    private static string BuildKey(AppItem item, int size)
    {
        var source = BuildSource(item);

        var stamp = string.Empty;
        if (!source.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = source;
            var comma = candidate.LastIndexOf(',');
            if (comma > 2 && int.TryParse(candidate[(comma + 1)..], out _))
            {
                candidate = candidate[..comma];
            }

            if (File.Exists(candidate))
            {
                stamp = File.GetLastWriteTimeUtc(candidate).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return $"{source}|{stamp}|{size}";
    }

    private string GetCachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDirectory, hash[..40] + ".png");
    }

    internal static int GetMaxPixelSize(int requestedSize) =>
        Math.Clamp(requestedSize * 2, 48, 96);

    private static BitmapSource DownscaleIfOversized(BitmapSource source, int maxPixelSize)
    {
        var largestDimension = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largestDimension <= maxPixelSize || largestDimension <= 0)
        {
            return source;
        }

        var scale = maxPixelSize / (double)largestDimension;
        var transform = new ScaleTransform(scale, scale);
        transform.Freeze();

        var scaled = new TransformedBitmap(source, transform);
        scaled.Freeze();
        return scaled;
    }

    private static BitmapSource? TryLoadFromDisk(string path, int maxPixelSize)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var decodeWidth = ShouldDownscalePng(stream, maxPixelSize) ? maxPixelSize : 0;
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0)
            {
                image.DecodePixelWidth = decodeWidth;
            }

            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldDownscalePng(Stream stream, int maxPixelSize)
    {
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) < 24)
        {
            return false;
        }

        // PNG signature + IHDR chunk starts with width/height at offsets 16 and 20 (big-endian).
        if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        return width > maxPixelSize || height > maxPixelSize;
    }

    private void TrySaveToDisk(BitmapSource source, string path)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        catch
        {
            // Icon caching is best effort.
        }
    }
}

using System.Diagnostics;
using System.IO;
using Mark.TaskbarFolders.Helpers;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class AppFileInfoService
{
    private readonly StaExecutor _sta;
    private readonly ILogger<AppFileInfoService> _logger;

    public AppFileInfoService(StaExecutor sta, ILogger<AppFileInfoService> logger)
    {
        _sta = sta;
        _logger = logger;
    }

    public Task<string?> ResolveDisplayNameAsync(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveShortcutDescriptionAsync(path);
        }

        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return GetFileDescriptionAsync(path);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<string?> GetFileDescriptionAsync(string path) =>
        Task.Run(() => ReadFileDescription(path));

    internal string? ReadFileDescription(string path)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read file description for {Path}", path);
            return null;
        }
    }

    private async Task<string?> ResolveShortcutDescriptionAsync(string path)
    {
        ShortcutLinkInfo? info;
        try
        {
            info = await _sta.InvokeAsync(() => ShortcutLinkReader.TryRead(path, includePropertyAumid: true))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read shortcut {Path}", path);
            return null;
        }

        if (info is null ||
            !string.IsNullOrWhiteSpace(info.Value.Aumid) ||
            info.Value.HasPropertyAumid)
        {
            return null;
        }

        var target = info.Value.Target;
        if (string.IsNullOrWhiteSpace(target) ||
            !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(target))
        {
            return null;
        }

        return await GetFileDescriptionAsync(target).ConfigureAwait(false);
    }
}

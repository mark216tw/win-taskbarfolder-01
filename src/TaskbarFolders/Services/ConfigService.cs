using System.IO;
using System.Text.Json;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class ConfigService
{
    private readonly string _configFile;
    private readonly ILogger<ConfigService>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ConfigService(string? rootDirectory = null, ILogger<ConfigService>? logger = null)
    {
        RootDirectory = rootDirectory ?? AppPaths.Root;
        _logger = logger;
        _configFile = Path.Combine(RootDirectory, "config.json");
    }

    public string RootDirectory { get; }

    public string ConfigFile => _configFile;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            return settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    public AppSettings Load()
    {
        _gate.Wait();
        try
        {
            return LoadCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RenameItemAsync(
        string groupId,
        string itemId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            var group = settings.Groups.FirstOrDefault(
                candidate => candidate.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));
            var item = group?.Items.FirstOrDefault(
                candidate => candidate.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                return false;
            }

            item.Name = trimmed;
            await SaveCoreAsync(settings, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootDirectory);

        if (File.Exists(_configFile))
        {
            var backup = _configFile + ".bak";
            File.Copy(_configFile, backup, overwrite: true);
        }

        var temporary = _configFile + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    AppSettingsJsonContext.Default.AppSettings,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporary, _configFile, overwrite: true);
    }

    private AppSettings LoadCore()
    {
        if (!File.Exists(_configFile))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(_configFile);
            return JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings)
                ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read configuration, attempting backup.");

            var backup = _configFile + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    using var fallback = File.OpenRead(backup);
                    return JsonSerializer.Deserialize(fallback, AppSettingsJsonContext.Default.AppSettings)
                        ?? new AppSettings();
                }
                catch (Exception backupException)
                {
                    _logger?.LogWarning(backupException, "Failed to read configuration backup.");
                }
            }

            return new AppSettings();
        }
    }

    private async Task<AppSettings> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configFile))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_configFile);
            var settings = await JsonSerializer
                .DeserializeAsync(stream, AppSettingsJsonContext.Default.AppSettings, cancellationToken)
                .ConfigureAwait(false);

            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to read configuration, attempting backup.");

            var backup = _configFile + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    await using var fallback = File.OpenRead(backup);
                    var settings = await JsonSerializer
                        .DeserializeAsync(fallback, AppSettingsJsonContext.Default.AppSettings, cancellationToken)
                        .ConfigureAwait(false);
                    return settings ?? new AppSettings();
                }
                catch (Exception backupException) when (backupException is not OperationCanceledException)
                {
                    _logger?.LogWarning(backupException, "Failed to read configuration backup.");
                }
            }

            return new AppSettings();
        }
    }

    public static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);

    public static AppSettings? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
}

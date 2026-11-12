using System.IO;
using System.Text.Json;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class GroupTransferService
{
    private readonly ILogger<GroupTransferService>? _logger;

    public GroupTransferService(ILogger<GroupTransferService>? logger = null)
    {
        _logger = logger;
    }

    public async Task ExportAsync(
        string path,
        IReadOnlyList<AppGroup> groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(groups);

        var document = new GroupExportDocument
        {
            Groups = groups.ToList()
        };

        var json = JsonSerializer.Serialize(document, GroupExportJsonContext.Default.GroupExportDocument);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            DeleteTemporary(temporary);
        }
    }

    public async Task<IReadOnlyList<AppGroup>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        GroupExportDocument? document;
        try
        {
            await using var stream = File.OpenRead(path);
            document = await JsonSerializer
                .DeserializeAsync(stream, GroupExportJsonContext.Default.GroupExportDocument, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("The group export file is not valid JSON.", ex);
        }

        if (document?.Groups is null || document.Groups.Count == 0)
        {
            throw new InvalidDataException("The group export file does not contain any groups.");
        }

        return NormalizeGroups(document.Groups);
    }

    private IReadOnlyList<AppGroup> NormalizeGroups(IEnumerable<AppGroup> groups)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AppGroup>();

        foreach (var group in groups)
        {
            if (group is null)
            {
                continue;
            }

            var id = group.Id;
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                id = Guid.NewGuid().ToString("N");
                ids.Add(id);
            }

            group.Id = id;

            if (group.Items is null)
            {
                group.Items = [];
            }

            result.Add(group);
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("The group export file does not contain any groups.");
        }

        return result;
    }

    private void DeleteTemporary(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete temporary export file {Path}", path);
        }
    }
}

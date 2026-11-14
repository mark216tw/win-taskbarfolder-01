using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;

namespace TaskbarFolders.Tests;

public sealed class GroupTransferServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "TaskbarFoldersTests",
        Guid.NewGuid().ToString("N"));

    public GroupTransferServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAndImport_RoundTripsGroups()
    {
        var service = new GroupTransferService();
        var path = Path.Combine(_root, "groups.json");
        var groups = new List<AppGroup>
        {
            new()
            {
                Id = "work",
                Name = "工作",
                IconPath = "builtin:work-folder",
                ThemeColor = "Blue",
                Order = 0,
                Items =
                [
                    new AppItem
                    {
                        Id = "editor",
                        Name = "Editor",
                        Target = @"C:\Apps\Editor.exe",
                        Kind = AppItemKind.Executable
                    }
                ]
            }
        };

        await service.ExportAsync(path, groups);
        var imported = await service.ImportAsync(path);

        var group = Assert.Single(imported);
        Assert.Equal("work", group.Id);
        Assert.Equal("工作", group.Name);
        Assert.Equal("builtin:work-folder", group.IconPath);
        Assert.Equal("Blue", group.ThemeColor);
        var item = Assert.Single(group.Items);
        Assert.Equal("Editor", item.Name);
        Assert.Equal(AppItemKind.Executable, item.Kind);
    }

    [Fact]
    public async Task Export_DoesNotIncludeTheme()
    {
        var service = new GroupTransferService();
        var path = Path.Combine(_root, "groups.json");

        await service.ExportAsync(path, [new AppGroup { Name = "分類" }]);

        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("\"theme\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("groups", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_InvalidJson_Throws()
    {
        var service = new GroupTransferService();
        var path = Path.Combine(_root, "groups.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path));
    }

    [Fact]
    public async Task Import_EmptyGroups_Throws()
    {
        var service = new GroupTransferService();
        var path = Path.Combine(_root, "groups.json");
        await File.WriteAllTextAsync(path, """{ "version": 1, "groups": [] }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path));
    }

    [Fact]
    public async Task Import_AssignsNewIdsForBlankAndDuplicateIds()
    {
        var service = new GroupTransferService();
        var path = Path.Combine(_root, "groups.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "version": 1,
              "groups": [
                { "id": "", "name": "A", "items": [] },
                { "id": "same", "name": "B", "items": [] },
                { "id": "same", "name": "C", "items": [] }
              ]
            }
            """);

        var imported = await service.ImportAsync(path);

        Assert.Equal(3, imported.Count);
        Assert.False(string.IsNullOrWhiteSpace(imported[0].Id));
        Assert.Equal("same", imported[1].Id);
        Assert.NotEqual(imported[1].Id, imported[2].Id);
        Assert.Equal(3, imported.Select(group => group.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

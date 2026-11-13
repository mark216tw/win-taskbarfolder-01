using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;

namespace TaskbarFolders.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "TaskbarFoldersTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        var service = new ConfigService(_root);
        var settings = new AppSettings
        {
            Theme = "Dark",
            Groups =
            [
                new AppGroup
                {
                    Id = "gaming",
                    Name = "遊戲",
                    ThemeColor = "Purple",
                    Order = 0,
                    Items =
                    [
                        new AppItem
                        {
                            Id = "steam",
                            Name = "Steam",
                            Target = @"C:\Games\Steam.exe",
                            Kind = AppItemKind.Executable
                        }
                    ]
                }
            ]
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Dark", loaded.Theme);
        var group = Assert.Single(loaded.Groups);
        Assert.Equal("gaming", group.Id);
        Assert.Equal("遊戲", group.Name);
        Assert.Equal("Purple", group.ThemeColor);
        var item = Assert.Single(group.Items);
        Assert.Equal("Steam", item.Name);
        Assert.Equal(AppItemKind.Executable, item.Kind);
    }

    [Fact]
    public async Task RenameItemAsync_UpdatesItemName()
    {
        var service = new ConfigService(_root);
        var settings = new AppSettings
        {
            Groups =
            [
                new AppGroup
                {
                    Id = "work",
                    Name = "工作",
                    Items =
                    [
                        new AppItem
                        {
                            Id = "editor",
                            Name = "記事本",
                            Target = @"C:\Windows\notepad.exe"
                        }
                    ]
                }
            ]
        };

        await service.SaveAsync(settings);

        var renamed = await service.RenameItemAsync("work", "editor", "  文字編輯器  ");

        Assert.True(renamed);
        var loaded = await service.LoadAsync();
        var item = Assert.Single(Assert.Single(loaded.Groups).Items);
        Assert.Equal("文字編輯器", item.Name);
    }

    [Fact]
    public async Task RenameItemAsync_ReturnsFalseWhenItemMissing()
    {
        var service = new ConfigService(_root);
        await service.SaveAsync(new AppSettings
        {
            Groups = [new AppGroup { Id = "work", Name = "工作" }]
        });

        Assert.False(await service.RenameItemAsync("work", "missing", "新名稱"));
        Assert.False(await service.RenameItemAsync("missing", "item", "新名稱"));

        var loaded = await service.LoadAsync();
        Assert.Empty(Assert.Single(loaded.Groups).Items);
    }

    [Fact]
    public async Task LoadMissingFile_ReturnsEmptySettings()
    {
        var service = new ConfigService(_root);

        var loaded = await service.LoadAsync();

        Assert.Empty(loaded.Groups);
        Assert.Equal("System", loaded.Theme);
    }

    [Fact]
    public async Task LoadCorruptFile_UsesBackup()
    {
        var service = new ConfigService(_root);
        var valid = new AppSettings { Theme = "Light", Groups = [new AppGroup { Name = "備份分類" }] };
        await service.SaveAsync(valid);
        await service.SaveAsync(valid);

        await File.WriteAllTextAsync(service.ConfigFile, "{ not valid json");

        var loaded = await service.LoadAsync();

        Assert.Equal("Light", loaded.Theme);
        Assert.Equal("備份分類", Assert.Single(loaded.Groups).Name);
    }

    [Fact]
    public async Task LoadCorruptFileAndBackup_ReturnsDefaults()
    {
        var service = new ConfigService(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(service.ConfigFile, "{ not valid json");
        await File.WriteAllTextAsync(service.ConfigFile + ".bak", "{ also invalid");

        var loaded = await service.LoadAsync();

        Assert.Empty(loaded.Groups);
        Assert.Equal("System", loaded.Theme);
    }
}

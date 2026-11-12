using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TaskbarFolders.Tests;

public sealed class WindowPlacementServiceTests : IDisposable
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
    public void Save_WritesPlacementFile()
    {
        var path = Path.Combine(_root, "window.json");
        var service = new WindowPlacementService(NullLogger<WindowPlacementService>.Instance, path);

        service.Save(new WindowPlacement
        {
            Left = 12,
            Top = 34,
            Width = 900,
            Height = 600,
            IsMaximized = true
        });

        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        Assert.Contains("\"width\": 900", json);
        Assert.Contains("\"isMaximized\": true", json);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var service = new WindowPlacementService(
            NullLogger<WindowPlacementService>.Instance,
            Path.Combine(_root, "missing.json"));

        Assert.Null(service.Load());
    }

    [Fact]
    public void Normalize_KeepsPlacementInsideVirtualScreen()
    {
        var placement = new WindowPlacement
        {
            Left = 100,
            Top = 120,
            Width = 900,
            Height = 600
        };

        var result = WindowPlacementService.Normalize(placement, 0, 0, 1920, 1080);

        Assert.Equal(100, result.Left);
        Assert.Equal(120, result.Top);
        Assert.Equal(900, result.Width);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public void Normalize_ClampsOffscreenPlacement()
    {
        var placement = new WindowPlacement
        {
            Left = -5000,
            Top = 5000,
            Width = 1000,
            Height = 700
        };

        var result = WindowPlacementService.Normalize(placement, 0, 0, 1920, 1080);

        Assert.Equal(0, result.Left);
        Assert.Equal(380, result.Top);
        Assert.Equal(1000, result.Width);
        Assert.Equal(700, result.Height);
    }

    [Fact]
    public void Normalize_UsesDefaultsForInvalidSize()
    {
        var placement = new WindowPlacement
        {
            Left = 100,
            Top = 100,
            Width = 0,
            Height = double.NaN
        };

        var result = WindowPlacementService.Normalize(placement, 0, 0, 1920, 1080);

        Assert.Equal(980, result.Width);
        Assert.Equal(680, result.Height);
    }

    [Fact]
    public void ClampToWorkArea_KeepsWindowInsideWorkArea()
    {
        var result = WindowPlacementService.ClampToWorkArea(-5000, 5000, 1000, 700, 0, 0, 1920, 1040);

        Assert.Equal(0, result.Left);
        Assert.Equal(340, result.Top);
        Assert.Equal(1000, result.Width);
        Assert.Equal(700, result.Height);
    }

    [Fact]
    public void ClampToWorkArea_ShrinksOversizedWindow()
    {
        var result = WindowPlacementService.ClampToWorkArea(0, 0, 3000, 2000, 0, 0, 1920, 1040);

        Assert.Equal(0, result.Left);
        Assert.Equal(0, result.Top);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1040, result.Height);
    }
}

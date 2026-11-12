using System.IO;
using System.Text.Json;
using System.Windows;
using Mark.TaskbarFolders.Models;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class WindowPlacementService
{
    private const double DefaultWidth = 980;
    private const double DefaultHeight = 680;

    private readonly ILogger<WindowPlacementService> _logger;
    private readonly string _filePath;

    public WindowPlacementService(ILogger<WindowPlacementService> logger)
        : this(logger, null)
    {
    }

    public WindowPlacementService(
        ILogger<WindowPlacementService> logger,
        string? filePath)
    {
        _logger = logger;
        _filePath = filePath ?? AppPaths.WindowStateFile;
    }

    public string FilePath => _filePath;

    public WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = File.ReadAllText(_filePath);
            var placement = JsonSerializer.Deserialize(json, WindowPlacementJsonContext.Default.WindowPlacement);
            if (placement is null)
            {
                return null;
            }

            return Normalize(
                placement,
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load window placement.");
            return null;
        }
    }

    public void Save(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(placement, WindowPlacementJsonContext.Default.WindowPlacement);
            var temporary = _filePath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save window placement.");
        }
    }

    public static WindowPlacement Normalize(
        WindowPlacement placement,
        double virtualLeft,
        double virtualTop,
        double virtualWidth,
        double virtualHeight)
    {
        var width = SanitizeSize(placement.Width, DefaultWidth);
        var height = SanitizeSize(placement.Height, DefaultHeight);
        var left = SanitizePosition(placement.Left, virtualLeft);
        var top = SanitizePosition(placement.Top, virtualTop);

        if (virtualWidth > 0)
        {
            width = Math.Min(width, virtualWidth);
        }

        if (virtualHeight > 0)
        {
            height = Math.Min(height, virtualHeight);
        }

        var maxLeft = virtualLeft + Math.Max(0, virtualWidth - width);
        var maxTop = virtualTop + Math.Max(0, virtualHeight - height);

        return new WindowPlacement
        {
            Left = Math.Clamp(left, virtualLeft, maxLeft),
            Top = Math.Clamp(top, virtualTop, maxTop),
            Width = width,
            Height = height,
            IsMaximized = placement.IsMaximized
        };
    }

    public static (int Left, int Top, int Width, int Height) ClampToWorkArea(
        int left,
        int top,
        int width,
        int height,
        int workLeft,
        int workTop,
        int workRight,
        int workBottom)
    {
        var availableWidth = Math.Max(1, workRight - workLeft);
        var availableHeight = Math.Max(1, workBottom - workTop);
        var clampedWidth = Math.Clamp(width, 1, availableWidth);
        var clampedHeight = Math.Clamp(height, 1, availableHeight);
        var maxLeft = Math.Max(workLeft, workRight - clampedWidth);
        var maxTop = Math.Max(workTop, workBottom - clampedHeight);

        return (
            Math.Clamp(left, workLeft, maxLeft),
            Math.Clamp(top, workTop, maxTop),
            clampedWidth,
            clampedHeight);
    }

    private static double SanitizeSize(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double SanitizePosition(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;
}

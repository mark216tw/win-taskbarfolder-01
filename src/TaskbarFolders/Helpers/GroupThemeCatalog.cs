using System.Windows.Media;

namespace Mark.TaskbarFolders.Helpers;

public static class GroupThemeCatalog
{
    public const string NoneId = "None";

    public static IReadOnlyList<GroupThemeDefinition> All { get; } =
    [
        new(
            "Yellow",
            "ThemeColorYellow",
            Color.FromRgb(0xC2, 0x8A, 0x00),
            new GroupThemePalette(
                Color.FromRgb(0xFF, 0xF8, 0xE1),
                Color.FromRgb(0xFD, 0xF1, 0xC8),
                Color.FromRgb(0xE6, 0xD9, 0xA8),
                Color.FromRgb(0x3A, 0x33, 0x20),
                Color.FromRgb(0x78, 0x6A, 0x3A),
                Color.FromRgb(0xC2, 0x8A, 0x00),
                Color.FromRgb(0xA8, 0x74, 0x00),
                Color.FromRgb(0x8A, 0x5F, 0x00),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xFB, 0xEF, 0xC9),
                Color.FromRgb(0xF5, 0xE3, 0xA8)),
            new GroupThemePalette(
                Color.FromRgb(0x2B, 0x26, 0x18),
                Color.FromRgb(0x35, 0x2F, 0x1D),
                Color.FromRgb(0x4A, 0x41, 0x28),
                Color.FromRgb(0xF3, 0xEC, 0xD8),
                Color.FromRgb(0xB8, 0xAC, 0x8A),
                Color.FromRgb(0xF0, 0xB4, 0x29),
                Color.FromRgb(0xF7, 0xC6, 0x4F),
                Color.FromRgb(0xD9, 0x9A, 0x12),
                Color.FromRgb(0x2B, 0x26, 0x18),
                Color.FromRgb(0x3A, 0x33, 0x20),
                Color.FromRgb(0x5C, 0x4A, 0x1E))),
        new(
            "Blue",
            "ThemeColorBlue",
            Color.FromRgb(0x25, 0x63, 0xEB),
            new GroupThemePalette(
                Color.FromRgb(0xEA, 0xF2, 0xFF),
                Color.FromRgb(0xDC, 0xE9, 0xFB),
                Color.FromRgb(0xBB, 0xD4, 0xF8),
                Color.FromRgb(0x16, 0x23, 0x3A),
                Color.FromRgb(0x5A, 0x6B, 0x85),
                Color.FromRgb(0x25, 0x63, 0xEB),
                Color.FromRgb(0x1D, 0x4E, 0xD8),
                Color.FromRgb(0x1E, 0x40, 0xAF),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xDC, 0xE9, 0xFF),
                Color.FromRgb(0xC7, 0xDB, 0xFB)),
            new GroupThemePalette(
                Color.FromRgb(0x16, 0x20, 0x2E),
                Color.FromRgb(0x1E, 0x2C, 0x3D),
                Color.FromRgb(0x2B, 0x3E, 0x56),
                Color.FromRgb(0xE8, 0xF0, 0xFA),
                Color.FromRgb(0x9F, 0xB2, 0xC8),
                Color.FromRgb(0x4C, 0xC2, 0xFF),
                Color.FromRgb(0x3D, 0xAE, 0xE6),
                Color.FromRgb(0x2C, 0x9A, 0xD1),
                Color.FromRgb(0x00, 0x2B, 0x3A),
                Color.FromRgb(0x1E, 0x2C, 0x3D),
                Color.FromRgb(0x26, 0x4F, 0x78))),
        new(
            "Green",
            "ThemeColorGreen",
            Color.FromRgb(0x0E, 0x9F, 0x6E),
            new GroupThemePalette(
                Color.FromRgb(0xEA, 0xF7, 0xEF),
                Color.FromRgb(0xDC, 0xF1, 0xE4),
                Color.FromRgb(0xBF, 0xE3, 0xCC),
                Color.FromRgb(0x15, 0x29, 0x1D),
                Color.FromRgb(0x5F, 0x7A, 0x68),
                Color.FromRgb(0x0E, 0x9F, 0x6E),
                Color.FromRgb(0x0B, 0x8A, 0x5E),
                Color.FromRgb(0x08, 0x70, 0x4C),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xDC, 0xF1, 0xE4),
                Color.FromRgb(0xC6, 0xE8, 0xD4)),
            new GroupThemePalette(
                Color.FromRgb(0x14, 0x23, 0x1A),
                Color.FromRgb(0x1B, 0x2F, 0x22),
                Color.FromRgb(0x2C, 0x46, 0x36),
                Color.FromRgb(0xE6, 0xF5, 0xEC),
                Color.FromRgb(0x9D, 0xB8, 0xA8),
                Color.FromRgb(0x4A, 0xDE, 0x9B),
                Color.FromRgb(0x35, 0xC8, 0x87),
                Color.FromRgb(0x25, 0xA9, 0x6F),
                Color.FromRgb(0x06, 0x28, 0x1A),
                Color.FromRgb(0x1B, 0x2F, 0x22),
                Color.FromRgb(0x22, 0x5A, 0x3E))),
        new(
            "Purple",
            "ThemeColorPurple",
            Color.FromRgb(0x7C, 0x3A, 0xED),
            new GroupThemePalette(
                Color.FromRgb(0xF2, 0xEC, 0xFF),
                Color.FromRgb(0xE9, 0xE0, 0xFF),
                Color.FromRgb(0xD8, 0xC8, 0xFA),
                Color.FromRgb(0x24, 0x1A, 0x38),
                Color.FromRgb(0x6B, 0x5C, 0x85),
                Color.FromRgb(0x7C, 0x3A, 0xED),
                Color.FromRgb(0x6D, 0x2C, 0xD6),
                Color.FromRgb(0x5B, 0x21, 0xB6),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xE9, 0xE0, 0xFF),
                Color.FromRgb(0xDC, 0xCD, 0xFB)),
            new GroupThemePalette(
                Color.FromRgb(0x1E, 0x17, 0x2B),
                Color.FromRgb(0x2A, 0x21, 0x40),
                Color.FromRgb(0x3E, 0x30, 0x59),
                Color.FromRgb(0xF2, 0xEC, 0xFA),
                Color.FromRgb(0xB4, 0xA6, 0xCC),
                Color.FromRgb(0xB7, 0x94, 0xFF),
                Color.FromRgb(0xA5, 0x7D, 0xFA),
                Color.FromRgb(0x8B, 0x5C, 0xF6),
                Color.FromRgb(0x1E, 0x12, 0x33),
                Color.FromRgb(0x2A, 0x21, 0x40),
                Color.FromRgb(0x44, 0x30, 0x6B))),
        new(
            "Orange",
            "ThemeColorOrange",
            Color.FromRgb(0xD9, 0x77, 0x06),
            new GroupThemePalette(
                Color.FromRgb(0xFF, 0xF2, 0xE6),
                Color.FromRgb(0xFF, 0xE8, 0xD1),
                Color.FromRgb(0xF3, 0xD2, 0xB0),
                Color.FromRgb(0x35, 0x21, 0x0F),
                Color.FromRgb(0x86, 0x69, 0x4F),
                Color.FromRgb(0xD9, 0x77, 0x06),
                Color.FromRgb(0xC2, 0x69, 0x05),
                Color.FromRgb(0xA6, 0x5A, 0x04),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xFF, 0xE8, 0xD1),
                Color.FromRgb(0xFA, 0xD9, 0xB8)),
            new GroupThemePalette(
                Color.FromRgb(0x2A, 0x1E, 0x12),
                Color.FromRgb(0x38, 0x26, 0x15),
                Color.FromRgb(0x4D, 0x3A, 0x22),
                Color.FromRgb(0xFA, 0xF0, 0xE6),
                Color.FromRgb(0xC4, 0xB0, 0x9A),
                Color.FromRgb(0xFB, 0xBF, 0x24),
                Color.FromRgb(0xF7, 0xC6, 0x4F),
                Color.FromRgb(0xD9, 0x9A, 0x12),
                Color.FromRgb(0x2A, 0x1E, 0x12),
                Color.FromRgb(0x38, 0x26, 0x15),
                Color.FromRgb(0x5C, 0x3D, 0x17)))
    ];

    private static readonly Dictionary<string, GroupThemeDefinition> Index =
        All.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

    public static GroupThemeDefinition? Find(string? themeColor)
    {
        if (string.IsNullOrWhiteSpace(themeColor) ||
            themeColor.Equals(NoneId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Index.TryGetValue(themeColor, out var definition) ? definition : null;
    }

    public static string Normalize(string? themeColor) => Find(themeColor)?.Id ?? NoneId;

    public static Brush? CreateSwatch(string? themeColor)
    {
        var definition = Find(themeColor);
        if (definition is null)
        {
            return null;
        }

        var brush = new SolidColorBrush(definition.Swatch);
        brush.Freeze();
        return brush;
    }
}

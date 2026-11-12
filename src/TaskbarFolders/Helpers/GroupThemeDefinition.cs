using System.Windows.Media;

namespace Mark.TaskbarFolders.Helpers;

public sealed record GroupThemeDefinition(
    string Id,
    string NameResourceKey,
    Color Swatch,
    GroupThemePalette Light,
    GroupThemePalette Dark);

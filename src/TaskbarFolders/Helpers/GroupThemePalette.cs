using System.Windows.Media;

namespace Mark.TaskbarFolders.Helpers;

public sealed record GroupThemePalette(
    Color Surface,
    Color SurfaceAlt,
    Color Border,
    Color Text,
    Color SubtleText,
    Color Accent,
    Color AccentHover,
    Color AccentPressed,
    Color AccentText,
    Color Hover,
    Color Selection);

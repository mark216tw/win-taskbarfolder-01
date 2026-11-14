using System.Windows.Media;

namespace Mark.TaskbarFolders.Helpers;

public sealed record BuiltInIconDefinition(
    string Id,
    string ThemeId,
    string NameResourceKey,
    Color Color,
    string PathData);

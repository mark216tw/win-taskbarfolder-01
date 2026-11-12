using System.Windows.Media;

namespace Mark.TaskbarFolders.Helpers;

public sealed record BuiltInIconDefinition(
    string Id,
    string NameResourceKey,
    Color Color,
    string PathData);

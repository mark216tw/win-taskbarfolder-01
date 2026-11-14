using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Resources;

namespace Mark.TaskbarFolders.ViewModels;

public sealed class BuiltInIconThemeGroup
{
    public BuiltInIconThemeGroup(BuiltInIconTheme theme, IReadOnlyList<BuiltInIconOption> icons)
    {
        Id = theme.Id;
        Name = Strings.Get(theme.NameResourceKey);
        Icons = icons;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<BuiltInIconOption> Icons { get; }
}

using System.Windows.Media;

namespace Mark.TaskbarFolders.ViewModels;

public sealed class GroupThemeOption
{
    public GroupThemeOption(string id, string name, Brush? previewBrush)
    {
        Id = id;
        Name = name;
        PreviewBrush = previewBrush;
    }

    public string Id { get; }

    public string Name { get; }

    public Brush? PreviewBrush { get; }
}

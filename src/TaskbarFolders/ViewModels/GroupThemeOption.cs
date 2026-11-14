using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mark.TaskbarFolders.ViewModels;

public partial class GroupThemeOption : ObservableObject
{
    public GroupThemeOption(string id, string name, Brush? previewBrush, bool isNone = false)
    {
        Id = id;
        Name = name;
        PreviewBrush = previewBrush;
        IsNone = isNone;
    }

    public string Id { get; }

    public string Name { get; }

    public Brush? PreviewBrush { get; }

    public bool IsNone { get; }

    [ObservableProperty]
    private bool isSelected;
}

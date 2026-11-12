using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mark.TaskbarFolders.Models;

namespace Mark.TaskbarFolders.ViewModels;

public partial class ItemViewModel : ObservableObject
{
    public ItemViewModel(AppItem item)
    {
        Id = item.Id;
        Name = item.Name;
        Target = item.Target;
        Arguments = item.Arguments;
        WorkingDirectory = item.WorkingDirectory;
        Kind = item.Kind;
        IconPath = item.IconPath;
        Aumid = item.Aumid;
    }

    public string Id { get; }

    [ObservableProperty]
    private string name = string.Empty;

    public string Target { get; }

    public string? Arguments { get; }

    public string? WorkingDirectory { get; }

    public AppItemKind Kind { get; }

    public string? IconPath { get; }

    public string? Aumid { get; }

    [ObservableProperty]
    private ImageSource? iconSource;

    public string ToolTip => Kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(Aumid)
        ? Aumid
        : Target;

    public AppItem ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Target = Target,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        Kind = Kind,
        IconPath = IconPath,
        Aumid = Aumid
    };
}

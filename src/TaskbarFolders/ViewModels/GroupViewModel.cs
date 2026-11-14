using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;

namespace Mark.TaskbarFolders.ViewModels;

public partial class GroupViewModel : ObservableObject
{
    public GroupViewModel(AppGroup group)
    {
        Id = group.Id;
        Name = group.Name;
        IconPath = group.IconPath;
        ThemeColor = GroupThemeCatalog.Normalize(group.ThemeColor);
        UpdateThemePreview();
        foreach (var item in group.Items)
        {
            Items.Add(new ItemViewModel(item));
        }
    }

    public string Id { get; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? iconPath;

    [ObservableProperty]
    private string themeColor = "None";

    [ObservableProperty]
    private ImageSource? iconSource;

    [ObservableProperty]
    private Brush? themePreviewBrush;

    [ObservableProperty]
    private bool isThemeNone;

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public string Aumid => $"Mark.TaskbarFolders.Group.{Id}";

    partial void OnThemeColorChanged(string value) => UpdateThemePreview();

    private void UpdateThemePreview()
    {
        ThemePreviewBrush = GroupThemeCatalog.CreateSwatch(ThemeColor);
        IsThemeNone = GroupThemeCatalog.Find(ThemeColor) is null;
    }

    public AppGroup ToModel(int order) => new()
    {
        Id = Id,
        Name = string.IsNullOrWhiteSpace(Name) ? Id : Name,
        IconPath = IconPath,
        ThemeColor = ThemeColor,
        Order = order,
        Items = Items.Select(item => item.ToModel()).ToList()
    };
}

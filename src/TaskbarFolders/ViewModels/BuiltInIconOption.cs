using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Resources;

namespace Mark.TaskbarFolders.ViewModels;

public partial class BuiltInIconOption : ObservableObject
{
    private ImageSource? _imageSource;

    public BuiltInIconOption(BuiltInIconDefinition definition)
    {
        Id = definition.Id;
        ThemeId = definition.ThemeId;
        Name = Strings.Get(definition.NameResourceKey);
    }

    public string Id { get; }

    public string ThemeId { get; }

    public string Name { get; }

    public ImageSource? ImageSource => _imageSource ??= BuiltInIconCatalog.CreatePreviewImage(Id);

    [ObservableProperty]
    private bool isSelected;
}

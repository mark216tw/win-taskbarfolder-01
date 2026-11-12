using System.Text.Json.Serialization;

namespace Mark.TaskbarFolders.Models;

public sealed class AppGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public string ThemeColor { get; set; } = "None";

    public int Order { get; set; }

    public List<AppItem> Items { get; set; } = [];

    [JsonIgnore]
    public string Aumid => $"Mark.TaskbarFolders.Group.{Id}";
}

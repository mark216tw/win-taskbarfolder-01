namespace Mark.TaskbarFolders.Models;

public sealed class AppItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public AppItemKind Kind { get; set; } = AppItemKind.Shortcut;

    public string? IconPath { get; set; }

    public string? Aumid { get; set; }
}

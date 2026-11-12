namespace Mark.TaskbarFolders.Models;

public sealed record DiscoveredApp(
    string Name,
    string Target,
    string? Arguments,
    string? WorkingDirectory,
    AppItemKind Kind,
    string? IconPath,
    string? Aumid,
    string Category)
{
    public AppItem ToAppItem() => new()
    {
        Name = Name,
        Target = Target,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        Kind = Kind,
        IconPath = IconPath,
        Aumid = Aumid
    };

    public string DeduplicationKey => Kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(Aumid)
        ? $"uwp:{Aumid}"
        : $"path:{Target.ToLowerInvariant()}|{Arguments}";
}

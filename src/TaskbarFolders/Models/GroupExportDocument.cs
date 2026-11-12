namespace Mark.TaskbarFolders.Models;

public sealed class GroupExportDocument
{
    public int Version { get; set; } = 1;

    public List<AppGroup> Groups { get; set; } = [];
}

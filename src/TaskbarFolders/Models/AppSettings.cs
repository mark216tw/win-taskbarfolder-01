namespace Mark.TaskbarFolders.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public string Theme { get; set; } = "System";

    public List<AppGroup> Groups { get; set; } = [];
}

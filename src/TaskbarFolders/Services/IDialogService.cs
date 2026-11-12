using Mark.TaskbarFolders.Models;

namespace Mark.TaskbarFolders.Services;

public interface IDialogService
{
    string? PickFile(string title, string filter);

    string? PickSaveFile(string title, string filter, string defaultFileName);

    string? Prompt(string title, string label, string? initialValue = null);

    IReadOnlyList<DiscoveredApp> PickApps(IReadOnlyList<DiscoveredApp> apps);

    void ShowPinGuide(string groupName, string shortcutPath);

    bool Confirm(string message);

    void ShowError(string message);

    void ShowMessage(string message);
}

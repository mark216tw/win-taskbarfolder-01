using System.Windows;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Views;
using Microsoft.Win32;

namespace Mark.TaskbarFolders.Services;

public sealed class DialogService : IDialogService
{
    private readonly ShortcutService _shortcut;
    private readonly ThemeService _theme;

    public DialogService(ShortcutService shortcut, ThemeService theme)
    {
        _shortcut = shortcut;
        _theme = theme;
    }

    public string? PickFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public string? Prompt(string title, string label, string? initialValue = null)
    {
        var dialog = new InputDialog(title, label, initialValue);
        Prepare(dialog);
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public IReadOnlyList<DiscoveredApp> PickApps(IReadOnlyList<DiscoveredApp> apps)
    {
        var dialog = new AppPickerWindow(apps);
        Prepare(dialog);
        return dialog.ShowDialog() == true ? dialog.SelectedApps : [];
    }

    public void ShowPinGuide(string groupName, string shortcutPath)
    {
        var dialog = new PinGuideWindow(groupName, shortcutPath, _shortcut);
        Prepare(dialog);
        dialog.ShowDialog();
    }

    public bool Confirm(string message) =>
        MessageBox.Show(
            GetOwner(),
            message,
            Strings.Get("ConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string message) =>
        MessageBox.Show(
            GetOwner(),
            message,
            Strings.Get("ErrorTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    public void ShowMessage(string message) =>
        MessageBox.Show(
            GetOwner(),
            message,
            Strings.Get("AppTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void Prepare(Window window)
    {
        window.Owner = GetOwner();
        window.SourceInitialized += (_, _) => WindowThemeHelper.Apply(window, _theme.IsDark);
    }

    private static Window? GetOwner() =>
        Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
        ?? Application.Current.MainWindow;
}

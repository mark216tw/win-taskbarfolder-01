using System.Windows;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Services;

namespace Mark.TaskbarFolders.Views;

public partial class PinGuideWindow : Window
{
    private readonly string _shortcutPath;
    private readonly ShortcutService _shortcut;

    public PinGuideWindow(string groupName, string shortcutPath, ShortcutService shortcut)
    {
        InitializeComponent();
        _shortcutPath = shortcutPath;
        _shortcut = shortcut;
        HeadingText.Text = Strings.Format("PinGuideHeading", groupName);
        ShortcutPathBox.Text = shortcutPath;
        InstructionsText.Text = Strings.Get("PinGuideInstructions");
        StatusText.Text = Strings.Get("PinAutoHint");
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) =>
        _shortcut.OpenContainingFolder(_shortcutPath);

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

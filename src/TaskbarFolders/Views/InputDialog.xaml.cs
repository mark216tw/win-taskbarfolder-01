using System.Windows;

namespace Mark.TaskbarFolders.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string label, string? initialValue)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        ValueBox.Text = initialValue ?? string.Empty;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.Windows;
using System.Windows.Media;
using Mark.TaskbarFolders.Helpers;
using Microsoft.Win32;

namespace Mark.TaskbarFolders.Services;

public sealed class ThemeService
{
    private ResourceDictionary? _currentTheme;

    public bool IsDark { get; private set; }

    public void Apply(string? mode)
    {
        var dark = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase) ||
                   (!string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase) && IsSystemDark());

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var source = new Uri(
            $"pack://application:,,,/TaskbarFolders;component/Resources/Themes/Colors.{(dark ? "Dark" : "Light")}.xaml",
            UriKind.Absolute);

        var dictionary = new ResourceDictionary { Source = source };
        if (_currentTheme is not null)
        {
            application.Resources.MergedDictionaries.Remove(_currentTheme);
        }

        application.Resources.MergedDictionaries.Insert(0, dictionary);
        _currentTheme = dictionary;
        IsDark = dark;
    }

    public void ApplyGroupTheme(Window window, string? themeColor)
    {
        var definition = GroupThemeCatalog.Find(themeColor);
        if (definition is null)
        {
            return;
        }

        var palette = IsDark ? definition.Dark : definition.Light;
        SetBrush(window, "SurfaceBrush", palette.Surface);
        SetBrush(window, "SurfaceAltBrush", palette.SurfaceAlt);
        SetBrush(window, "BorderBrush", palette.Border);
        SetBrush(window, "TextBrush", palette.Text);
        SetBrush(window, "SubtleTextBrush", palette.SubtleText);
        SetBrush(window, "AccentBrush", palette.Accent);
        SetBrush(window, "AccentHoverBrush", palette.AccentHover);
        SetBrush(window, "AccentPressedBrush", palette.AccentPressed);
        SetBrush(window, "AccentTextBrush", palette.AccentText);
        SetBrush(window, "HoverBrush", palette.Hover);
        SetBrush(window, "SelectionBrush", palette.Selection);
    }

    private static void SetBrush(Window window, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        window.Resources[key] = brush;
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }
}

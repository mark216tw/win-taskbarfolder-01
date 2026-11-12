using System.Globalization;
using System.Resources;

namespace Mark.TaskbarFolders.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("Mark.TaskbarFolders.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}

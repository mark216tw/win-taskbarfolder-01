using System.Globalization;
using Mark.TaskbarFolders.Models;

namespace Mark.TaskbarFolders.Helpers;

public static class AppSearch
{
    public static IReadOnlyList<AppItem> Filter(IEnumerable<AppItem> items, string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return items.ToList();
        }

        return items
            .Where(item => Matches(item, trimmed))
            .ToList();
    }

    public static bool Matches(AppItem item, string query) =>
        Contains(item.Name, query) ||
        Contains(item.Target, query) ||
        Contains(item.Aumid, query);

    private static bool Contains(string? value, string query) =>
        value is not null &&
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        value.Trim().ToUpper(CultureInfo.CurrentCulture);
}

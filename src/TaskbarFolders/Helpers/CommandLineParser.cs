namespace Mark.TaskbarFolders.Helpers;

public static class CommandLineParser
{
    public static string? ParseGroupId(IEnumerable<string> arguments)
    {
        string? result = null;
        var args = arguments as IList<string> ?? arguments.ToList();
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.Equals("--group", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                result = args[index + 1];
            }
            else if (argument.StartsWith("--group=", StringComparison.OrdinalIgnoreCase))
            {
                result = argument["--group=".Length..];
            }
        }

        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }
}

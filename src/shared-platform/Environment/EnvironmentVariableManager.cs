namespace JuliHyro.DotNetTool.Shared.Environment;

/// <summary>
/// Updates the current user's .NET environment variables without overwriting unrelated PATH entries.
/// </summary>
public static class EnvironmentVariableManager
{
    public static void RegisterUserRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        System.Environment.SetEnvironmentVariable("DOTNET_ROOT", fullRoot, EnvironmentVariableTarget.User);
        var current = System.Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var entries = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!entries.Any(entry => PathsEqual(entry, fullRoot)))
        {
            entries.Add(fullRoot);
            System.Environment.SetEnvironmentVariable("Path", string.Join(Path.PathSeparator, entries), EnvironmentVariableTarget.User);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}

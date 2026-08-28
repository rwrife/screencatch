namespace ScreenCatch.Core.Recording;

/// <summary>
/// Resolves media tools bundled beside a published application before falling
/// back to the operating system PATH.
/// </summary>
public static class MediaToolLocator
{
    public static string Resolve(string toolName, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("A media tool name is required.", nameof(toolName));
        }

        if (!string.Equals(Path.GetFileName(toolName), toolName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The media tool must be specified by name only.", nameof(toolName));
        }

        var executableName = OperatingSystem.IsWindows() && !toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? $"{toolName}.exe"
            : toolName;
        var applicationDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(baseDirectory);

        foreach (var candidate in new[]
        {
            Path.Combine(applicationDirectory, "tools", executableName),
            Path.Combine(applicationDirectory, executableName),
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }
}

namespace LegacyBridge.Generator;

/// <summary>
/// PATH first (Linux/macOS/containers), then Windows x64. Skips the x86 host
/// that shadows SDKs on some developer machines.
/// </summary>
public static class DotnetLocator
{
    public static string Find()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(root))
        {
            var exe = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(exe)) return exe;
        }

        foreach (var cand in PathHits())
        {
            if (IsX86Windows(cand)) continue;
            return cand;
        }

        if (OperatingSystem.IsWindows())
        {
            var x64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            if (File.Exists(x64)) return x64;
        }

        return "dotnet";
    }

    private static IEnumerable<string> PathHits()
    {
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var cand = Path.Combine(dir.Trim().Trim('"'), name);
            if (File.Exists(cand)) yield return cand;
        }
    }

    private static bool IsX86Windows(string path) =>
        OperatingSystem.IsWindows()
        && path.Contains("Program Files (x86)", StringComparison.OrdinalIgnoreCase);
}

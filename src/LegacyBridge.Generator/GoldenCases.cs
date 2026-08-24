using System.Globalization;
using System.Text.Json;

namespace LegacyBridge.Generator;

public sealed record GoldenCase(string Routine, IReadOnlyDictionary<string, decimal> Args, decimal Expected);

public static class GoldenCases
{
    public static IReadOnlyList<GoldenCase> Load()
    {
        var path = Find();
        if (path is null) return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("cases", out var arr)) return [];
        var list = new List<GoldenCase>();
        foreach (var c in arr.EnumerateArray())
        {
            var routine = c.GetProperty("routine").GetString() ?? "";
            var args = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in c.GetProperty("args").EnumerateObject())
                args[p.Name] = p.Value.GetDecimal();
            list.Add(new GoldenCase(routine, args, c.GetProperty("expected").GetDecimal()));
        }
        return list;
    }

    public static string? Find()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "evals", "golden-cases.json");
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    public static string Invariant(decimal d) => d.ToString(CultureInfo.InvariantCulture);
}

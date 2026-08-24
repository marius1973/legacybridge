namespace LegacyBridge.Generator.Spec;

public sealed record BusinessSpec(
    string Source,
    IReadOnlyList<SpecEntity> Entities,
    IReadOnlyList<SpecRule> Rules,
    IReadOnlyList<SpecFlow> Flows,
    IReadOnlyList<SpecQuery> Queries);

public sealed record SpecEntity(string Name, IReadOnlyList<string> Fields);
public sealed record SpecRule(string Id, string Description, string Routine);
public sealed record SpecFlow(string Name, string Kind, IReadOnlyList<string> Parameters);
public sealed record SpecQuery(string Routine, string Sql);

/// <summary>
/// Tiny reader for the Agent 1 YAML schema. Not a general YAML parser.
/// Understands inline lists <c>[a, b]</c> and nested <c>- item</c> lists
/// (what the TypeScript extractor actually emits).
/// </summary>
public static class SpecReader
{
    private static readonly HashSet<string> ListKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fields", "parameters", "steps"
    };

    public static BusinessSpec Load(string path) => Parse(File.ReadAllText(path));

    public static BusinessSpec Parse(string yaml)
    {
        string source = "";
        var entities = new List<SpecEntity>();
        var rules = new List<SpecRule>();
        var flows = new List<SpecFlow>();
        var queries = new List<SpecQuery>();

        string section = "";
        Dictionary<string, string>? item = null;
        string? listKey = null;

        void Flush()
        {
            if (item is null) return;
            switch (section)
            {
                case "entities":
                    entities.Add(new SpecEntity(
                        item.GetValueOrDefault("name") ?? "Entity",
                        SplitList(item.GetValueOrDefault("fields"))));
                    break;
                case "rules":
                    rules.Add(new SpecRule(
                        item.GetValueOrDefault("id") ?? "R0",
                        item.GetValueOrDefault("description") ?? "",
                        item.GetValueOrDefault("routine") ?? ""));
                    break;
                case "flows":
                    flows.Add(new SpecFlow(
                        item.GetValueOrDefault("name") ?? "",
                        item.GetValueOrDefault("kind") ?? "procedure",
                        SplitList(item.GetValueOrDefault("parameters"))));
                    break;
                case "queries":
                    queries.Add(new SpecQuery(
                        item.GetValueOrDefault("routine") ?? "",
                        item.GetValueOrDefault("sql") ?? ""));
                    break;
            }
            item = null;
            listKey = null;
        }

        foreach (var raw in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#')) continue;
            var trimmed = line.Trim();

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && trimmed.Contains(':'))
            {
                Flush();
                var (key, val) = SplitKey(trimmed);
                if (key == "source") { source = val; section = ""; continue; }
                if (key is "entities" or "rules" or "flows" or "queries")
                {
                    section = key;
                    continue;
                }
            }

            if (trimmed.StartsWith("- "))
            {
                var rest = trimmed[2..];
                if (item is not null && listKey is not null && !rest.Contains(':'))
                {
                    Append(item, listKey, rest);
                    continue;
                }
                Flush();
                item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (rest.Contains(':'))
                {
                    var (k, v) = SplitKey(rest);
                    item[k] = v;
                    listKey = ListKeyOrNull(k, v);
                }
                continue;
            }

            if (item is not null && trimmed.Contains(':'))
            {
                var (k, v) = SplitKey(trimmed);
                item[k] = v;
                listKey = ListKeyOrNull(k, v) ?? (k.Equals("sql", StringComparison.OrdinalIgnoreCase) ? "sql" : null);
                continue;
            }

            if (item is not null && listKey is not null)
                Append(item, listKey, trimmed);
        }
        Flush();
        return new BusinessSpec(source, entities, rules, flows, queries);
    }

    private static string? ListKeyOrNull(string key, string val) =>
        ListKeys.Contains(key) && val.Length == 0 ? key : null;

    private static void Append(Dictionary<string, string> item, string key, string value)
    {
        var prev = item.GetValueOrDefault(key) ?? "";
        var sep = key.Equals("sql", StringComparison.OrdinalIgnoreCase) ? " " : ", ";
        item[key] = string.IsNullOrWhiteSpace(prev) ? value : prev + sep + value;
    }

    private static (string Key, string Val) SplitKey(string line)
    {
        var i = line.IndexOf(':');
        var key = i < 0 ? line.Trim() : line[..i].Trim();
        var val = i < 0 ? "" : line[(i + 1)..].Trim().Trim('"');
        return (key, val);
    }

    private static IReadOnlyList<string> SplitList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var s = raw.Trim();
        if (s.StartsWith('[') && s.EndsWith(']'))
            s = s[1..^1];
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

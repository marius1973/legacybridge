using System.Text.RegularExpressions;
using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Generator;

public sealed record EntityModel(string Name, IReadOnlyList<string> Fields);

public static class SpecInfer
{
    public static IReadOnlyList<EntityModel> Entities(IReadOnlyList<IrProgram> programs)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in programs)
        foreach (var r in p.Routines)
            Walk(r.Body, tables, fields);

        return tables.Select(t => new EntityModel(EntityName(t), fields.OrderBy(f => f).ToList())).ToList();
    }

    private static string EntityName(string table)
    {
        var stem = table.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? table[..^1] : table;
        return Names.Pascal(stem);
    }

    private static void Walk(IReadOnlyList<IrStatement>? body, HashSet<string> tables, HashSet<string> fields)
    {
        foreach (var s in body ?? [])
        {
            var text = s.Expression?.RawText ?? "";
            foreach (Match m in Regex.Matches(text, @"\b(?:USE|FROM|INTO|UPDATE)\s+(\w+)", RegexOptions.IgnoreCase))
                tables.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"\bREPLACE\s+(\w+)", RegexOptions.IgnoreCase))
                fields.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"\b([A-Za-z_]\w*)\b"))
            {
                var w = m.Groups[1].Value;
                if (Kw.Contains(w)) continue;
                if (tables.Contains(w)) continue;
                if (w.Length >= 2 && (w[0] is 't' or 'T' or 'l' or 'L') && w[1] is 'n' or 'N' or 'c' or 'C')
                    continue;
                if (s.Kind is "scan" or "sql" or "expression")
                    fields.Add(w);
            }
            Walk(s.Then, tables, fields);
            Walk(s.Else, tables, fields);
            Walk(s.Body, tables, fields);
        }
    }

    private static readonly HashSet<string> Kw = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "GROUP", "BY", "ORDER",
        "SUM", "REPLACE", "WITH", "USE", "AND", "OR", "NOT", "DESC", "ASC", "FOR", "SCAN"
    };
}

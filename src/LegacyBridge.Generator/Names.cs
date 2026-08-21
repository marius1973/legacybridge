namespace LegacyBridge.Generator;

public static class Names
{
    public static string Pascal(string raw)
    {
        var parts = raw.Replace('.', '_').Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    public static string Ident(string raw) => raw.Replace('.', '_');

    public static string Property(string entity, string field)
    {
        var p = Pascal(field);
        return string.Equals(p, entity, StringComparison.OrdinalIgnoreCase) ? p + "Name" : p;
    }

    public static string ClrType(string field)
    {
        var f = field.ToLowerInvariant();
        if (f.Contains("name") || f is "product" or "code") return "string";
        return "decimal";
    }
}

using System.Text.RegularExpressions;
using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Parser.Parsing;

/// <summary>
/// PowerBuilder frontend: normalize scripts to the VFP subset, then reuse
/// <see cref="VfpParser"/>. DataWindows only extract the retrieve SQL.
/// </summary>
public static class PbParser
{
    public static IrProgram Parse(string source, string sourceName, bool strict = false)
        => VfpParser.Parse(PbNormalizer.ToVfp(source), sourceName, strict);

    public static IrProgram ParseDataWindow(string source, string sourceName)
    {
        var name = DataWindowName(source, sourceName);
        var sql = DataWindowRetrieve(source);
        if (sql is null)
            return new IrProgram(sourceName, []);
        IrStatement[] body = [new("sql", 1, Expression: new RawExpr(sql), SqlVerb: SqlVerbOf(sql))];
        return new IrProgram(sourceName, [new IrRoutine(name, "procedure", [], body)]);
    }

    private static string DataWindowName(string source, string sourceName)
    {
        var header = Regex.Match(source, @"\$PBExportHeader\$(\S+)");
        if (header.Success)
            return Path.GetFileNameWithoutExtension(header.Groups[1].Value);
        return Path.GetFileNameWithoutExtension(sourceName);
    }

    private static string? DataWindowRetrieve(string source)
    {
        var m = Regex.Match(source, @"retrieve\s*=\s*""((?:[^""]|"""")*)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value.Replace("\"\"", "\"", StringComparison.Ordinal) : null;
    }

    private static string? SqlVerbOf(string sql)
    {
        var i = 0;
        while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
        var end = i;
        while (end < sql.Length && char.IsLetter(sql[end])) end++;
        var verb = sql[i..end].ToLowerInvariant();
        return verb is "select" or "insert" or "update" or "delete" ? verb : null;
    }
}

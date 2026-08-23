using System.Text;
using System.Text.RegularExpressions;

namespace LegacyBridge.Parser.Parsing;

/// <summary>
/// Maps a PowerBuilder script subset onto the VFP surface the existing parser
/// already understands. Not a PB compiler — just enough so the same IR is
/// reachable from <c>.sru</c> / <c>.srf</c> / <c>.srw</c>.
/// </summary>
public static class PbNormalizer
{
    private static readonly Regex FunctionSig = new(
        @"^(?:(?:public|private|protected|global|static)\s+)*function\s+(\w+)\s+(\w+)\s*\(([^)]*)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubroutineSig = new(
        @"^(?:(?:public|private|protected|global|static)\s+)*subroutine\s+(\w+)\s*\(([^)]*)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TypedLocal = new(
        @"^(decimal|dec|integer|int|long|ulong|uint|string|boolean|blob|date|datetime|double|real|char|byte|unsignedlong|unsignedint)\s+(\w+)(?:\s*=\s*(.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ToVfp(string source)
    {
        var lines = StripBlockComments(source).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length;)
        {
            var trim = StripLineComment(lines[i]).Trim();
            if (trim.StartsWith('$') || IsGlobalInstance(trim))
            {
                i++;
                continue;
            }
            if (StartsBlock(trim, "forward prototypes"))
            {
                i = SkipUntil(lines, i, "end prototypes");
                continue;
            }
            if (StartsBlock(trim, "forward"))
            {
                i = SkipUntil(lines, i, "end forward");
                continue;
            }
            if (StartsBlock(trim, "global type") || StartsBlock(trim, "type"))
            {
                i = SkipUntil(lines, i, "end type");
                continue;
            }
            if (StartsBlock(trim, "on"))
            {
                i = SkipUntil(lines, i, "end on");
                continue;
            }
            if (StartsBlock(trim, "event"))
            {
                i = SkipUntil(lines, i, "end event");
                continue;
            }

            kept.Add(RewriteLine(lines[i]));
            i++;
        }
        return string.Join('\n', kept);
    }

    private static string RewriteLine(string raw)
    {
        var code = StripLineComment(raw).TrimEnd();
        if (code.EndsWith(';'))
            code = code[..^1].TrimEnd();
        var trim = code.Trim();
        if (trim.Length == 0)
            return "";

        if (IsEnd(trim, "if")) return "ENDIF";
        if (IsEnd(trim, "function")) return "ENDFUNC";
        if (IsEnd(trim, "subroutine")) return "ENDPROC";
        if (IsEnd(trim, "for")) return "ENDFOR";
        if (trim.Equals("loop", StringComparison.OrdinalIgnoreCase)) return "ENDDO";

        var fn = FunctionSig.Match(trim);
        if (fn.Success)
            return EmitRoutine("FUNCTION", fn.Groups[2].Value, fn.Groups[3].Value);

        var sub = SubroutineSig.Match(trim);
        if (sub.Success)
            return EmitRoutine("PROCEDURE", sub.Groups[1].Value, sub.Groups[2].Value);

        var local = TypedLocal.Match(trim);
        if (local.Success)
        {
            var name = local.Groups[2].Value;
            if (local.Groups[3].Success)
                return $"LOCAL {name}\n{name} = {local.Groups[3].Value.Trim()}";
            return $"LOCAL {name}";
        }

        return code;
    }

    private static string EmitRoutine(string kind, string name, string args)
    {
        var names = SplitArgs(args);
        return names.Count == 0
            ? $"{kind} {name}"
            : $"{kind} {name}\nLPARAMETERS {string.Join(", ", names)}";
    }

    private static List<string> SplitArgs(string args)
    {
        var names = new List<string>();
        foreach (var part in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !w.Equals("ref", StringComparison.OrdinalIgnoreCase)
                            && !w.Equals("readonly", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (words.Length == 0) continue;
            names.Add(words[^1].TrimEnd('[', ']'));
        }
        return names;
    }

    private static bool IsEnd(string trim, string word)
    {
        var parts = trim.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
               && parts[0].Equals("end", StringComparison.OrdinalIgnoreCase)
               && parts[1].Equals(word, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGlobalInstance(string trim)
    {
        var parts = trim.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 && parts[0].Equals("global", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsBlock(string trim, string keyword)
    {
        return trim.Equals(keyword, StringComparison.OrdinalIgnoreCase)
               || trim.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase)
               || trim.StartsWith(keyword + "\t", StringComparison.OrdinalIgnoreCase);
    }

    private static int SkipUntil(string[] lines, int start, string endKeyword)
    {
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (StartsBlock(StripLineComment(lines[i]).Trim(), endKeyword))
                return i + 1;
        }
        return lines.Length;
    }

    private static string StripLineComment(string line)
    {
        var cut = line.IndexOf("//", StringComparison.Ordinal);
        return cut < 0 ? line : line[..cut];
    }

    private static string StripBlockComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (i + 1 < s.Length && s[i] == '/' && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/'))
                {
                    if (s[i] is '\r' or '\n') sb.Append(s[i]);
                    i++;
                }
                if (i + 1 < s.Length) i++;
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}

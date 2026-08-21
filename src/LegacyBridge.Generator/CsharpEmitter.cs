using System.Text;
using System.Text.RegularExpressions;
using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Generator;

public static class CsharpEmitter
{
    public static string Expr(IrExpression? e, Func<string, string>? id = null) => e switch
    {
        null => "default",
        LiteralExpr { LiteralKind: "number" } n => n.Value.Contains('.')
            ? n.Value + "m"
            : n.Value + "m",
        LiteralExpr { LiteralKind: "string" } s =>
            "\"" + s.Value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        LiteralExpr { LiteralKind: "bool" } b =>
            b.Value.Equals(".T.", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
        IdentifierExpr i => (id ?? Names.Ident)(i.Name),
        BinaryExpr b => $"({Expr(b.Left, id)} {Op(b.Op)} {Expr(b.Right, id)})",
        UnaryExpr { Op: ".NOT." or "NOT" } u => $"!({Expr(u.Operand, id)})",
        UnaryExpr u => $"{(u.Op == "-" ? "-" : "+")}{Expr(u.Operand, id)}",
        CallExpr c when c.Name.Equals("ROUND", StringComparison.OrdinalIgnoreCase) =>
            $"Math.Round({Expr(c.Args[0], id)}, (int)({Expr(c.Args[1], id)}), MidpointRounding.AwayFromZero)",
        CallExpr c => $"{Names.Pascal(c.Name)}({string.Join(", ", c.Args.Select(a => Expr(a, id)))})",
        RawExpr r => "/* " + r.RawText.Replace("*/", "") + " */ 0m",
        _ => "0m"
    };

    public static string Op(string op) => op.ToUpperInvariant() switch
    {
        "*" => "*", "/" => "/", "+" => "+", "-" => "-",
        ">" => ">", "<" => "<", ">=" => ">=", "<=" => "<=",
        "=" or "==" => "==",
        "<>" or "!=" or "#" => "!=",
        ".AND." or "AND" => "&&",
        ".OR." or "OR" => "||",
        _ => op
    };

    public static string MethodBody(IrRoutine routine, string? entity, Func<string, string>? id = null)
    {
        var declared = new HashSet<string>(routine.Parameters, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        foreach (var line in Stmts(routine.Body, declared, entity, id, 2))
            sb.AppendLine(line);
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> Stmts(
        IReadOnlyList<IrStatement>? body, HashSet<string> declared, string? entity,
        Func<string, string>? id, int indent)
    {
        var pad = new string(' ', indent * 4);
        foreach (var s in body ?? [])
        {
            switch (s.Kind)
            {
                case "local":
                    break;
                case "assign":
                    var target = (id ?? Names.Ident)(s.Target ?? "x");
                    var rhs = Expr(s.Expression, id);
                    if (declared.Add(s.Target ?? "x") && id is null)
                        yield return $"{pad}var {target} = {rhs};";
                    else
                        yield return $"{pad}{target} = {rhs};";
                    break;
                case "if":
                    yield return $"{pad}if ({Expr(s.Expression, id)})";
                    yield return pad + "{";
                    foreach (var l in Stmts(s.Then, declared, entity, id, indent + 1)) yield return l;
                    yield return pad + "}";
                    if (s.Else is { Count: > 0 })
                    {
                        yield return pad + "else";
                        yield return pad + "{";
                        foreach (var l in Stmts(s.Else, declared, entity, id, indent + 1)) yield return l;
                        yield return pad + "}";
                    }
                    break;
                case "return":
                    yield return s.Expression is null
                        ? $"{pad}return;"
                        : $"{pad}return {Expr(s.Expression, id)};";
                    break;
                case "scan":
                    var item = "item";
                    Func<string, string> rowId = n => item + "." + Names.Property(entity ?? "Entity", n);
                    yield return $"{pad}foreach (var {item} in _repo.GetAll().Where({item} => {Expr(s.Expression, rowId)}))";
                    yield return pad + "{";
                    foreach (var l in Stmts(s.Body, declared, entity, rowId, indent + 1)) yield return l;
                    yield return pad + "}";
                    yield return $"{pad}_repo.Save();";
                    break;
                case "for":
                    var v = s.LoopVariable ?? "i";
                    var step = s.Step is null ? "1m" : Expr(s.Step, id);
                    yield return $"{pad}for (var {v} = {Expr(s.From, id)}; {v} <= {Expr(s.To, id)}; {v} += {step})";
                    yield return pad + "{";
                    foreach (var l in Stmts(s.Body, declared, entity, id, indent + 1)) yield return l;
                    yield return pad + "}";
                    break;
                case "doWhile":
                    yield return $"{pad}while ({Expr(s.Expression, id)})";
                    yield return pad + "{";
                    foreach (var l in Stmts(s.Body, declared, entity, id, indent + 1)) yield return l;
                    yield return pad + "}";
                    break;
                case "sql":
                    yield return $"{pad}// {raw(s)}";
                    if (entity is not null)
                        yield return $"{pad}return _repo.GetAll();";
                    break;
                default:
                    var t = raw(s);
                    if (TryReplace(t, entity, id, out var repl))
                    {
                        yield return pad + repl;
                        break;
                    }
                    if (t.StartsWith("USE", StringComparison.OrdinalIgnoreCase))
                        break;
                    if (!string.IsNullOrWhiteSpace(t))
                        yield return $"{pad}// {t}";
                    break;
            }
        }
    }

    private static string raw(IrStatement s) => s.Expression?.RawText ?? "";

    private static bool TryReplace(string text, string? entity, Func<string, string>? id, out string csharp)
    {
        var m = Regex.Match(text, @"REPLACE\s+(\w+)\s+WITH\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            csharp = "";
            return false;
        }
        var left = (id ?? Names.Ident)(m.Groups[1].Value);
        var right = Regex.Replace(m.Groups[2].Value.Trim(), @"\b[A-Za-z_]\w*\b", mm =>
        {
            var w = mm.Value;
            if (w.Equals("WITH", StringComparison.OrdinalIgnoreCase)) return w;
            return (id ?? Names.Ident)(w);
        });
        csharp = $"{left} = {right};";
        return true;
    }
}

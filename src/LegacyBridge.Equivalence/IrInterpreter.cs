using System.Globalization;
using System.Text.RegularExpressions;
using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;

namespace LegacyBridge.Equivalence;

public sealed class IrInterpreter
{
    public object? Run(IrRoutine routine, IReadOnlyDictionary<string, decimal> args, List<Dictionary<string, decimal>>? table = null)
    {
        var boxed = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in args) boxed[kv.Key] = kv.Value;
        return Run(routine, boxed, table);
    }

    public object? Run(IrRoutine routine, IReadOnlyDictionary<string, object?> args, List<Dictionary<string, decimal>>? table = null)
    {
        var env = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in routine.Parameters)
            env[p] = args.TryGetValue(p, out var v) ? v : 0m;
        Dictionary<string, decimal>? row = null;
        object? ret = null;
        var returned = false;
        Exec(routine.Body, env, table, ref row, ref ret, ref returned);
        return returned ? ret : table;
    }

    private void Exec(
        IReadOnlyList<IrStatement>? body,
        Dictionary<string, object?> env,
        List<Dictionary<string, decimal>>? table,
        ref Dictionary<string, decimal>? row,
        ref object? ret,
        ref bool returned)
    {
        foreach (var s in body ?? [])
        {
            if (returned) return;
            switch (s.Kind)
            {
                case "local":
                    break;
                case "assign":
                    var val = Eval(s.Expression, env, row);
                    if (row is not null && s.Target is not null && row.ContainsKey(s.Target))
                        row[s.Target] = Num(val);
                    else
                        env[s.Target ?? "x"] = val;
                    break;
                case "if":
                    if (Truth(Eval(s.Expression, env, row)))
                        Exec(s.Then, env, table, ref row, ref ret, ref returned);
                    else
                        Exec(s.Else, env, table, ref row, ref ret, ref returned);
                    break;
                case "return":
                    ret = s.Expression is null ? null : Eval(s.Expression, env, row);
                    returned = true;
                    return;
                case "scan":
                    if (table is null) break;
                    foreach (var r in table)
                    {
                        row = r;
                        if (Truth(Eval(s.Expression, env, row)))
                            Exec(s.Body, env, table, ref row, ref ret, ref returned);
                    }
                    row = null;
                    break;
                case "for":
                    var name = s.LoopVariable ?? "i";
                    var from = Num(Eval(s.From, env, row));
                    var to = Num(Eval(s.To, env, row));
                    var step = s.Step is null ? 1m : Num(Eval(s.Step, env, row));
                    for (var i = from; i <= to; i += step)
                    {
                        env[name] = i;
                        Exec(s.Body, env, table, ref row, ref ret, ref returned);
                        if (returned) return;
                    }
                    break;
                case "doWhile":
                    while (Truth(Eval(s.Expression, env, row)))
                    {
                        Exec(s.Body, env, table, ref row, ref ret, ref returned);
                        if (returned) return;
                    }
                    break;
                case "sql":
                    throw new InvalidOperationException("sql not interpreted");
                default:
                    var raw = s.Expression?.RawText ?? "";
                    if (TryReplace(raw, env, row)) break;
                    break;
            }
        }
    }

    public object? Eval(IrExpression? e, Dictionary<string, object?> env, Dictionary<string, decimal>? row) => e switch
    {
        null => 0m,
        LiteralExpr { LiteralKind: "number" } n =>
            decimal.Parse(n.Value, CultureInfo.InvariantCulture),
        LiteralExpr { LiteralKind: "bool" } b =>
            b.Value.Equals(".T.", StringComparison.OrdinalIgnoreCase),
        LiteralExpr { LiteralKind: "string" } s => s.Value,
        IdentifierExpr i => Lookup(i.Name, env, row),
        BinaryExpr b => Bin(b.Op, Eval(b.Left, env, row), Eval(b.Right, env, row)),
        UnaryExpr { Op: ".NOT." or "NOT" or "!" } u => !Truth(Eval(u.Operand, env, row)),
        UnaryExpr { Op: "-" } u => -Num(Eval(u.Operand, env, row)),
        UnaryExpr u => Num(Eval(u.Operand, env, row)),
        CallExpr c when c.Name.Equals("ROUND", StringComparison.OrdinalIgnoreCase) =>
            Math.Round(Num(Eval(c.Args[0], env, row)), (int)Num(Eval(c.Args[1], env, row)), MidpointRounding.AwayFromZero),
        CallExpr c => Call(c, env, row),
        RawExpr => 0m,
        _ => 0m
    };

    private static object? Lookup(string name, Dictionary<string, object?> env, Dictionary<string, decimal>? row)
    {
        if (row is not null && row.TryGetValue(name, out var rv)) return rv;
        return env.TryGetValue(name, out var v) ? v : 0m;
    }

    private object? Call(CallExpr c, Dictionary<string, object?> env, Dictionary<string, decimal>? row)
    {
        var name = c.Name.ToUpperInvariant();
        var args = c.Args.Select(a => Eval(a, env, row)).ToArray();
        return name switch
        {
            "ALLTRIM" => Str(args[0]).Trim(),
            "UPPER" => Str(args[0]).ToUpperInvariant(),
            "LOWER" => Str(args[0]).ToLowerInvariant(),
            "LEN" => (decimal)Str(args[0]).Length,
            "LEFT" => Left(Str(args[0]), (int)Num(args[1])),
            "RIGHT" => Right(Str(args[0]), (int)Num(args[1])),
            "SUBSTR" or "SUBSTRING" => Substr(Str(args[0]), (int)Num(args[1]), args.Length > 2 ? (int)Num(args[2]) : int.MaxValue),
            _ => throw new InvalidOperationException($"unknown function {c.Name}")
        };
    }

    private static string Left(string s, int n) => s[..Math.Clamp(n, 0, s.Length)];
    private static string Right(string s, int n)
    {
        n = Math.Clamp(n, 0, s.Length);
        return s[^n..];
    }
    private static string Substr(string s, int start, int len)
    {
        var i = Math.Clamp(start - 1, 0, s.Length); // VFP is 1-based
        len = Math.Clamp(len, 0, s.Length - i);
        return s.Substring(i, len);
    }

    private static object? Bin(string op, object? l, object? r) => op.ToUpperInvariant() switch
    {
        "*" => Num(l) * Num(r),
        "/" => Num(l) / Num(r),
        "+" => l is string || r is string ? Str(l) + Str(r) : Num(l) + Num(r),
        "-" => Num(l) - Num(r),
        ">" => Num(l) > Num(r),
        "<" => Num(l) < Num(r),
        ">=" => Num(l) >= Num(r),
        "<=" => Num(l) <= Num(r),
        "=" or "==" => Eq(l, r),
        "<>" or "!=" or "#" => !Eq(l, r),
        ".AND." or "AND" => Truth(l) && Truth(r),
        ".OR." or "OR" => Truth(l) || Truth(r),
        _ => throw new InvalidOperationException($"unknown op {op}")
    };

    private static bool Eq(object? l, object? r) =>
        l is string || r is string
            ? string.Equals(Str(l), Str(r), StringComparison.OrdinalIgnoreCase)
            : Num(l) == Num(r);

    private static string Str(object? v) => v switch
    {
        string s => s,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? ".T." : ".F.",
        null => "",
        _ => v.ToString() ?? ""
    };

    private static decimal Num(object? v) => v switch
    {
        decimal d => d,
        bool b => b ? 1m : 0m,
        int i => i,
        string s when decimal.TryParse(s, CultureInfo.InvariantCulture, out var d) => d,
        _ => 0m
    };

    private static bool Truth(object? v) => v switch
    {
        bool b => b,
        decimal d => d != 0m,
        string s => s.Length > 0,
        _ => v is not null
    };

    private bool TryReplace(string text, Dictionary<string, object?> env, Dictionary<string, decimal>? row)
    {
        var m = Regex.Match(text, @"REPLACE\s+(\w+)\s+WITH\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success || row is null) return false;
        var field = m.Groups[1].Value;
        var expr = ExpressionParser.Parse(m.Groups[2].Value.Trim());
        row[field] = Num(Eval(expr, env, row));
        return true;
    }
}

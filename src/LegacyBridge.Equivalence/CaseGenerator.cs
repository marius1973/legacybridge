using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Equivalence;

public sealed record EqCase(
    string Id,
    string Routine,
    IReadOnlyDictionary<string, decimal> Args,
    List<Dictionary<string, decimal>>? Table,
    bool Skip = false,
    string? SkipReason = null);

public static class CaseGenerator
{
    public static IReadOnlyList<EqCase> For(IrProgram program)
    {
        var cases = new List<EqCase>();
        foreach (var r in program.Routines)
        {
            if (HasKind(r, "sql"))
            {
                cases.Add(new EqCase($"{r.Name}/sql", r.Name, new Dictionary<string, decimal>(), null, true, "embedded SQL not in oracle"));
                continue;
            }
            if (HasKind(r, "scan"))
            {
                cases.Add(Scan(r.Name, "in-stock",
                    Row(("stock", 10m), ("unit_cost", 2.5m), ("total_value", 0m)),
                    Row(("stock", 0m), ("unit_cost", 9m), ("total_value", 99m))));
                cases.Add(Scan(r.Name, "empty-skip",
                    Row(("stock", 0m), ("unit_cost", 1m), ("total_value", 7m))));
                cases.Add(Scan(r.Name, "neg-qty",
                    Row(("stock", -3m), ("unit_cost", 4m), ("total_value", 1m)),
                    Row(("stock", 3m), ("unit_cost", 4m), ("total_value", 0m))));
                continue;
            }
            cases.AddRange(FromCfg(r));
        }
        return cases;
    }

    /// <summary>
    /// Values from IF thresholds (n, n±1, n±0.01) plus a small grid.
    /// Hits both sides of `> 10000` / `> 50` instead of a blind cartesian product.
    /// </summary>
    private static IEnumerable<EqCase> FromCfg(IrRoutine r)
    {
        var cuts = Thresholds(r.Body).Distinct().ToList();
        var n = r.Parameters.Count;
        if (n == 0)
        {
            yield return new EqCase($"{r.Name}/noparams", r.Name, new Dictionary<string, decimal>(), null);
            yield break;
        }

        var aVals = ValuesFor(cuts, extra: [0m, -1m, 1m, 10m, 50m, 50.01m, 100m, 10000m, 100000m]);
        if (n == 1)
        {
            foreach (var a in aVals)
                yield return One(r, a);
            yield break;
        }

        var bVals = ValuesFor(cuts, extra: [0m, 10m, 50m, 51m, 100m, 150m]);
        if (aVals.Count * bVals.Count > 96)
        {
            aVals = ValuesFor(cuts, extra: [0m, 10m, 50m, 100m]);
            bVals = ValuesFor(cuts, extra: [0m, 50m, 100m, 150m]);
        }
        foreach (var a in aVals)
        foreach (var b in bVals)
            yield return Two(r, a, b);
    }

    private static List<decimal> ValuesFor(IReadOnlyList<decimal> cuts, decimal[] extra)
    {
        var set = new SortedSet<decimal>(extra);
        foreach (var t in cuts)
        {
            set.Add(t);
            set.Add(t - 1);
            set.Add(t + 1);
            set.Add(t - 0.01m);
            set.Add(t + 0.01m);
        }
        return set.ToList();
    }

    private static IEnumerable<decimal> Thresholds(IReadOnlyList<IrStatement>? body)
    {
        foreach (var s in Walk(body))
        {
            if (s.Kind != "if") continue;
            foreach (var n in Literals(s.Expression))
                yield return n;
        }
    }

    private static IEnumerable<decimal> Literals(IrExpression? e)
    {
        switch (e)
        {
            case LiteralExpr { LiteralKind: "number" } n
                when decimal.TryParse(n.Value, System.Globalization.CultureInfo.InvariantCulture, out var d):
                yield return d;
                break;
            case BinaryExpr b:
                foreach (var x in Literals(b.Left)) yield return x;
                foreach (var x in Literals(b.Right)) yield return x;
                break;
            case UnaryExpr u:
                foreach (var x in Literals(u.Operand)) yield return x;
                break;
            case CallExpr c:
                foreach (var a in c.Args)
                foreach (var x in Literals(a))
                    yield return x;
                break;
        }
    }

    private static bool HasKind(IrRoutine r, string kind) =>
        Walk(r.Body).Any(s => s.Kind == kind);

    private static IEnumerable<IrStatement> Walk(IReadOnlyList<IrStatement>? body)
    {
        foreach (var s in body ?? [])
        {
            yield return s;
            foreach (var c in Walk(s.Then)) yield return c;
            foreach (var c in Walk(s.Else)) yield return c;
            foreach (var c in Walk(s.Body)) yield return c;
        }
    }

    private static EqCase Scan(string routine, string tag, params Dictionary<string, decimal>[] rows) =>
        new($"{routine}/{tag}", routine, new Dictionary<string, decimal>(), rows.Select(Clone).ToList());

    private static EqCase One(IrRoutine r, decimal a) =>
        new($"{r.Name}({a})", r.Name, new Dictionary<string, decimal> { [r.Parameters[0]] = a }, null);

    private static EqCase Two(IrRoutine r, decimal a, decimal b) =>
        new($"{r.Name}({a},{b})", r.Name,
            new Dictionary<string, decimal> { [r.Parameters[0]] = a, [r.Parameters[1]] = b }, null);

    private static Dictionary<string, decimal> Row(params (string k, decimal v)[] pairs)
    {
        var d = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    private static Dictionary<string, decimal> Clone(Dictionary<string, decimal> r) =>
        new(r, StringComparer.OrdinalIgnoreCase);
}

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
            cases.AddRange(Grid(r));
        }
        return cases;
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

    private static IEnumerable<EqCase> Grid(IrRoutine r)
    {
        decimal[] nums = [0m, -1m, 1m, 10m, 50m, 50.01m, 100m, 10000m, 100000m];
        var n = r.Parameters.Count;
        if (n == 0)
        {
            yield return new EqCase($"{r.Name}/noparams", r.Name, new Dictionary<string, decimal>(), null);
            yield break;
        }
        if (n == 1)
        {
            foreach (var a in nums)
                yield return One(r, a);
            yield break;
        }
        // ponytail: first two params only; extra params stay 0
        foreach (var a in nums)
        foreach (var b in (decimal[])[0m, 10m, 50m, 51m, 100m, 150m])
            yield return Two(r, a, b);
    }

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

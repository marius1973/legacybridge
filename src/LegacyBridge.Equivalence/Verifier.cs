using System.Globalization;
using System.Text;
using LegacyBridge.Parser.Ir;
using VfpInventory.Application;
using VfpInventory.Domain;

namespace LegacyBridge.Equivalence;

public sealed record EqRow(string Id, string Routine, string Args, string Oracle, string Migrated, string Result);

public sealed record EqReport(int Matched, int Mismatched, int Skipped, IReadOnlyList<EqRow> Rows)
{
    public double Rate => Matched + Mismatched == 0 ? 0 : (double)Matched / (Matched + Mismatched);
}

public static class Verifier
{
    public static EqReport Run(IrProgram program)
    {
        var interp = new IrInterpreter();
        var rows = new List<EqRow>();
        var match = 0;
        var miss = 0;
        var skip = 0;
        foreach (var c in CaseGenerator.For(program))
        {
            if (c.Skip)
            {
                skip++;
                rows.Add(new EqRow(c.Id, c.Routine, FormatArgs(c), "—", "—", "skip: " + c.SkipReason));
                continue;
            }
            var tableOracle = CloneTable(c.Table);
            var tableMig = CloneTable(c.Table);
            string oracle;
            string migrated;
            try
            {
                oracle = Fmt(interp.Run(program.Routines.First(r => r.Name == c.Routine), c.Args, tableOracle));
            }
            catch (Exception ex)
            {
                oracle = "ERR " + ex.Message;
            }
            try
            {
                migrated = Fmt(CallMigrated(c, tableMig));
            }
            catch (Exception ex)
            {
                migrated = "ERR " + ex.Message;
            }
            var ok = oracle == migrated;
            if (ok) match++; else miss++;
            rows.Add(new EqRow(c.Id, c.Routine, FormatArgs(c), oracle, migrated, ok ? "match" : "MISMATCH"));
        }
        return new EqReport(match, miss, skip, rows);
    }

    public static string ToMarkdown(EqReport r, string source)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Equivalence report — `{source}`");
        sb.AppendLine();
        sb.AppendLine($"**Match rate:** {(r.Rate * 100).ToString("0.0", CultureInfo.InvariantCulture)}% ({r.Matched}/{r.Matched + r.Mismatched}) · skipped {r.Skipped}");
        sb.AppendLine();
        sb.AppendLine("| Case | Routine | Args | Oracle | Migrated | Result |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var row in r.Rows)
            sb.AppendLine($"| `{Escape(row.Id)}` | {row.Routine} | {Escape(row.Args)} | {Escape(row.Oracle)} | {Escape(row.Migrated)} | {row.Result} |");
        return sb.ToString();
    }

    private static object? CallMigrated(EqCase c, List<Dictionary<string, decimal>>? table)
    {
        var products = (table ?? []).Select(ToProduct).ToList();
        var svc = new ProductService(new MemRepo(products));
        var mi = typeof(ProductService).GetMethod(c.Routine);
        if (mi is null) throw new InvalidOperationException($"no method {c.Routine}");
        var args = mi.GetParameters().Select(p => (object)(c.Args.TryGetValue(p.Name ?? "", out var v) ? v : 0m)).ToArray();
        var result = mi.Invoke(svc, args);
        return result ?? products;
    }

    private static Product ToProduct(Dictionary<string, decimal> row) => new()
    {
        Stock = Get(row, "stock"),
        UnitCost = Get(row, "unit_cost"),
        TotalValue = Get(row, "total_value"),
        Year = Get(row, "year"),
        ProductName = row.TryGetValue("product", out var _) ? "p" : ""
    };

    private static decimal Get(Dictionary<string, decimal> row, string k) =>
        row.TryGetValue(k, out var v) ? v : 0m;

    private static string Fmt(object? v)
    {
        if (v is decimal d) return d.ToString("0.##", CultureInfo.InvariantCulture);
        if (v is IEnumerable<Product> products)
            return string.Join(";", products.Select(p => p.TotalValue.ToString("0.##", CultureInfo.InvariantCulture)));
        if (v is IEnumerable<Dictionary<string, decimal>> dicts)
            return string.Join(";", dicts.Select(r => Get(r, "total_value").ToString("0.##", CultureInfo.InvariantCulture)));
        return v?.ToString() ?? "void";
    }

    private static string FormatArgs(EqCase c)
    {
        if (c.Table is not null)
            return "rows=" + c.Table.Count;
        return string.Join(", ", c.Args.Select(kv => $"{kv.Key}={kv.Value.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static List<Dictionary<string, decimal>>? CloneTable(List<Dictionary<string, decimal>>? t) =>
        t?.Select(r => new Dictionary<string, decimal>(r, StringComparer.OrdinalIgnoreCase)).ToList();

    private static string Escape(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private sealed class MemRepo(List<Product> items) : IProductRepository
    {
        public IReadOnlyList<Product> GetAll() => items;
        public void Save() { }
    }
}

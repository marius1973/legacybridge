using System.Text.Json;
using LegacyBridge.Equivalence;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Equivalence.Tests;

/// <summary>
/// Hand-computed expected values from evals/golden-cases.json.
/// These do not come from IrInterpreter or CsharpEmitter — they are the
/// independent check that the oracle itself is honest (H2).
/// </summary>
public class GoldenOracleTests
{
    [Fact]
    public void Hand_computed_goldens_match_the_interpreter()
    {
        var program = VfpParser.Parse(File.ReadAllText(Sample()), "inv_calc.prg");
        var interp = new IrInterpreter();
        foreach (var c in Load())
        {
            var r = program.Routines.First(x => x.Name == c.Routine);
            var got = interp.Run(r, c.Args);
            Assert.Equal(c.Expected, got);
        }
    }

    [Fact]
    public void Cfg_cases_include_threshold_neighbors()
    {
        var program = VfpParser.Parse(File.ReadAllText(Sample()), "inv_calc.prg");
        var ids = CaseGenerator.For(program).Select(c => c.Id).ToList();
        Assert.Contains(ids, id => id.Contains("10000", StringComparison.Ordinal) || id.Contains("10001", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.Contains("49.99", StringComparison.Ordinal) || id.Contains("50.01", StringComparison.Ordinal) || id.Contains("(50,", StringComparison.Ordinal));
    }

    private static IEnumerable<(string Routine, Dictionary<string, decimal> Args, decimal Expected)> Load()
    {
        var json = File.ReadAllText(GoldenPath());
        using var doc = JsonDocument.Parse(json);
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var args = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in c.GetProperty("args").EnumerateObject())
                args[p.Name] = p.Value.GetDecimal();
            yield return (c.GetProperty("routine").GetString()!, args, c.GetProperty("expected").GetDecimal());
        }
    }

    private static string GoldenPath()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "evals", "golden-cases.json");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("evals/golden-cases.json");
    }

    private static string Sample()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "samples", "vfp-inventory", "legacy", "inv_calc.prg");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("inv_calc.prg");
    }
}

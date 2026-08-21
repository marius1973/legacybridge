using LegacyBridge.Equivalence;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Equivalence.Tests;

public class InterpreterTests
{
    [Theory]
    [InlineData(1, 10, 15)]
    [InlineData(100, 150, 15300)]
    [InlineData(100, 100, 10005)]
    [InlineData(0, 0, 5)]
    public void CalcStockValue_matches_rules(decimal qty, decimal cost, decimal expected)
    {
        var src = File.ReadAllText(Sample());
        var program = VfpParser.Parse(src, "inv_calc.prg");
        var r = program.Routines.First(x => x.Name == "CalcStockValue");
        var got = new IrInterpreter().Run(r, new Dictionary<string, decimal>
        {
            ["tnQty"] = qty,
            ["tnUnitCost"] = cost
        });
        Assert.Equal(expected, got);
    }

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(100, 60, 50)]
    [InlineData(100, 0, 100)]
    public void ApplyDiscount_caps_at_50(decimal amount, decimal pct, decimal expected)
    {
        var program = VfpParser.Parse(File.ReadAllText(Sample()), "inv_calc.prg");
        var r = program.Routines.First(x => x.Name == "ApplyDiscount");
        var got = new IrInterpreter().Run(r, new Dictionary<string, decimal>
        {
            ["tnAmount"] = amount,
            ["tnPercent"] = pct
        });
        Assert.Equal(expected, got);
    }

    [Fact]
    public void Sample_match_rate_is_100_percent()
    {
        var program = VfpParser.Parse(File.ReadAllText(Sample()), "inv_calc.prg");
        var report = Verifier.Run(program);
        Assert.Equal(0, report.Mismatched);
        Assert.True(report.Matched > 0);
        Assert.Equal(1.0, report.Rate);
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

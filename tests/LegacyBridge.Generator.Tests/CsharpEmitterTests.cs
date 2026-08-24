using LegacyBridge.Generator;
using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Generator.Tests;

public class CsharpEmitterTests
{
    [Fact]
    public void Emits_decimal_arithmetic_and_round()
    {
        var e = ExpressionParser.Parse("ROUND(tnQty * tnUnitCost, 2)");
        var c = CsharpEmitter.Expr(e);
        Assert.Contains("Math.Round", c);
        Assert.Contains("*", c);
        Assert.Contains("tnQty", c);
    }

    [Fact]
    public void Maps_logical_ops()
    {
        Assert.Equal("&&", CsharpEmitter.Op(".AND."));
        Assert.Equal("!=", CsharpEmitter.Op("<>"));
    }

    [Fact]
    public void Pascal_unit_cost()
    {
        Assert.Equal("UnitCost", Names.Pascal("unit_cost"));
        Assert.Equal("ProductName", Names.Property("Product", "product"));
    }

    [Fact]
    public void CalcStockValue_compiles_as_csharp_shape()
    {
        const string src = """
            PROCEDURE CalcStockValue
                LPARAMETERS tnQty, tnUnitCost
                LOCAL lnValue
                lnValue = tnQty * tnUnitCost
                IF lnValue > 10000
                    lnValue = lnValue * 1.02
                ELSE
                    lnValue = lnValue + 5
                ENDIF
                RETURN ROUND(lnValue, 2)
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "t.prg");
        var body = CsharpEmitter.MethodBody(program.Routines[0], null);
        Assert.Contains("if (", body);
        Assert.Contains("return Math.Round", body);
        Assert.Contains("var lnValue", body);
    }

    [Fact]
    public void Maps_string_helpers()
    {
        var e = ExpressionParser.Parse("UPPER(ALLTRIM(tcName))");
        var c = CsharpEmitter.Expr(e);
        Assert.Contains("ToUpperInvariant", c);
        Assert.Contains("Trim()", c);
    }
}

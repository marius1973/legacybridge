using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Parser.Tests;

public class ParserTests
{
    private const string Sample = """
        * Calculates an invoice total
        PROCEDURE CalcTotal
            LPARAMETERS tnPrice, tnQty
            LOCAL lnTotal
            lnTotal = tnPrice * tnQty
            IF lnTotal > 1000
                lnTotal = lnTotal * 0.9
            ELSE
                lnTotal = lnTotal + 10
            ENDIF
            RETURN lnTotal
        ENDPROC
        """;

    [Fact]
    public void Parses_routine_with_parameters()
    {
        var program = VfpParser.Parse(Sample, "sample.prg");
        var routine = Assert.Single(program.Routines);
        Assert.Equal("CalcTotal", routine.Name);
        Assert.Equal("procedure", routine.Kind);
        Assert.Equal(new[] { "tnPrice", "tnQty" }, routine.Parameters);
    }

    [Fact]
    public void Parses_if_else_as_structured_statement()
    {
        var program = VfpParser.Parse(Sample, "sample.prg");
        var ifStmt = Assert.Single(program.Routines[0].Body.Where(s => s.Kind == "if"));
        Assert.NotNull(ifStmt.Then);
        Assert.NotNull(ifStmt.Else);
        Assert.Equal("lnTotal > 1000", ifStmt.Expression!.RawText);
        var cmp = Assert.IsType<BinaryExpr>(ifStmt.Expression);
        Assert.Equal(">", cmp.Op);
        Assert.Single(ifStmt.Then!);
        Assert.Single(ifStmt.Else!);
    }

    [Fact]
    public void Parses_for_loop_with_step()
    {
        const string src = """
            FUNCTION SumTo
                LPARAMETERS tnN
                lnSum = 0
                FOR i = 1 TO tnN STEP 2
                    lnSum = lnSum + i
                ENDFOR
                RETURN lnSum
            ENDFUNC
            """;
        var program = VfpParser.Parse(src, "loop.prg");
        var loop = Assert.Single(program.Routines[0].Body.Where(s => s.Kind == "for"));
        Assert.Equal("i", loop.LoopVariable);
        Assert.Equal("1", loop.From!.RawText);
        Assert.Equal("tnN", loop.To!.RawText);
        Assert.Equal("2", loop.Step!.RawText);
        Assert.Single(loop.Body!);
    }

    [Fact]
    public void Parses_scan_and_do_while()
    {
        const string src = """
            PROCEDURE Archive
                USE invoices
                SCAN FOR year < 2020
                    DELETE
                ENDSCAN
                DO WHILE .NOT. EOF()
                    SKIP
                ENDDO
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "archive.prg");
        var kinds = program.Routines[0].Body.Select(s => s.Kind).ToList();
        Assert.Contains("scan", kinds);
        Assert.Contains("doWhile", kinds);
    }

    [Fact]
    public void Captures_embedded_sql_as_raw_statement()
    {
        const string src = """
            PROCEDURE TopClients
                SELECT name, total FROM clients WHERE total > 500 ORDER BY total DESC
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "sql.prg");
        var sql = Assert.Single(program.Routines[0].Body.Where(s => s.Kind == "sql"));
        Assert.Contains("FROM clients", sql.Expression!.RawText);
    }

    [Fact]
    public void Ir_serializes_to_json()
    {
        var program = VfpParser.Parse(Sample, "sample.prg");
        var json = IrSerializer.ToJson(program);
        Assert.Contains("\"CalcTotal\"", json);
        Assert.Contains("\"tnPrice\"", json);
    }

    [Fact]
    public void Throws_on_missing_endproc()
    {
        Assert.Throws<ParserException>(() =>
            VfpParser.Parse("PROCEDURE Broken\n  x = 1", "broken.prg"));
    }

    [Fact]
    public void Parses_inv_calc_sample_with_typed_ast()
    {
        var src = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "inv_calc.prg"));
        var program = VfpParser.Parse(src, "inv_calc.prg");
        Assert.Equal(4, program.Routines.Count);

        var calc = program.Routines[0];
        var assign = Assert.Single(calc.Body.Where(s => s.Kind == "assign"));
        var mul = Assert.IsType<BinaryExpr>(assign.Expression);
        Assert.Equal("*", mul.Op);

        var ret = Assert.Single(calc.Body.Where(s => s.Kind == "return"));
        var round = Assert.IsType<CallExpr>(ret.Expression);
        Assert.Equal("ROUND", round.Name);
        Assert.Equal(2, round.Args.Count);

        var scan = Assert.Single(program.Routines[2].Body.Where(s => s.Kind == "scan"));
        var cond = Assert.IsType<BinaryExpr>(scan.Expression);
        Assert.Equal(">", cond.Op);
        Assert.Equal("stock", Assert.IsType<IdentifierExpr>(cond.Left).Name);
    }
}

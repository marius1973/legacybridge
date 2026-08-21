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
        Assert.Equal("lnTotal > 1000", ifStmt.Expression);
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
        Assert.Equal("1", loop.From);
        Assert.Equal("tnN", loop.To);
        Assert.Equal("2", loop.Step);
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
        Assert.Contains("FROM clients", sql.Expression);
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
}

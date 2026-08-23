using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Parser.Tests;

public class PbParserTests
{
    private static string SampleSru() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "n_billing.sru"));

    [Fact]
    public void Parses_sample_object_with_same_routine_names_as_vfp_twin()
    {
        var program = PbParser.Parse(SampleSru(), "n_billing.sru");
        Assert.Equal(new[] { "CalcStockValue", "ApplyDiscount", "MonthlyReport" },
            program.Routines.Select(r => r.Name));
        Assert.Equal(new[] { "tnQty", "tnUnitCost" }, program.Routines[0].Parameters);
        Assert.Equal("function", program.Routines[0].Kind);
        Assert.Equal("procedure", program.Routines[2].Kind);
    }

    [Fact]
    public void CalcStockValue_has_structured_if_and_round_call()
    {
        var program = PbParser.Parse(SampleSru(), "n_billing.sru");
        var body = program.Routines[0].Body;
        Assert.Contains(body, s => s.Kind == "local" && s.Target == "ld_value");
        var iff = Assert.Single(body, s => s.Kind == "if");
        Assert.Equal("ld_value > 10000", iff.Expression!.RawText);
        Assert.NotNull(iff.Then);
        Assert.NotNull(iff.Else);
        var ret = Assert.Single(body, s => s.Kind == "return");
        var call = Assert.IsType<CallExpr>(ret.Expression);
        Assert.Equal("round", call.Name, ignoreCase: true);
    }

    [Fact]
    public void MonthlyReport_is_raw_sql()
    {
        var sql = Assert.Single(PbParser.Parse(SampleSru(), "n_billing.sru").Routines[2].Body);
        Assert.Equal("sql", sql.Kind);
        Assert.Contains("products", sql.Expression!.RawText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Strips_export_header_prototypes_and_on_blocks()
    {
        var vfpish = PbNormalizer.ToVfp(SampleSru());
        Assert.DoesNotContain("PBExportHeader", vfpish, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forward prototypes", vfpish, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TriggerEvent", vfpish, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FUNCTION CalcStockValue", vfpish, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Do_while_loop_maps_to_doWhile()
    {
        const string src = """
            public subroutine Tick (integer li_n);
            integer li_i = 0
            do while li_i < li_n
                li_i = li_i + 1
            loop
            end subroutine
            """;
        var r = Assert.Single(PbParser.Parse(src, "n_tick.sru").Routines);
        Assert.Equal(new[] { "li_n" }, r.Parameters);
        Assert.Contains(r.Body, s => s.Kind == "local");
        var loop = Assert.Single(r.Body, s => s.Kind == "doWhile");
        Assert.Single(loop.Body!);
    }

    [Fact]
    public void Global_function_file_parses()
    {
        const string src = """
            global function decimal f_tax (decimal ad_amount);
            return ad_amount * 0.18
            end function
            """;
        var r = Assert.Single(PbParser.Parse(src, "f_tax.srf").Routines);
        Assert.Equal("f_tax", r.Name);
        Assert.Equal(new[] { "ad_amount" }, r.Parameters);
    }

    [Fact]
    public void DataWindow_extracts_retrieve_sql()
    {
        const string src = """
            $PBExportHeader$d_invoice_lines.srd
            table(retrieve="SELECT qty, unit_cost FROM invoice_lines WHERE billed = 1")
            """;
        var program = PbParser.ParseDataWindow(src, "d_invoice_lines.srd");
        var r = Assert.Single(program.Routines);
        Assert.Equal("d_invoice_lines", r.Name);
        Assert.Equal("sql", Assert.Single(r.Body).Kind);
        Assert.Contains("invoice_lines", r.Body[0].Expression!.RawText);
    }

    [Fact]
    public void DataWindow_without_retrieve_is_empty()
    {
        var program = PbParser.ParseDataWindow("release 12;", "empty.srd");
        Assert.Empty(program.Routines);
    }

    [Fact]
    public void SourceParser_dispatches_by_extension()
    {
        var pb = SourceParser.Parse("public function integer Foo (integer a);\nreturn a\nend function", "n.sru");
        Assert.Equal("Foo", Assert.Single(pb.Routines).Name);

        var dw = SourceParser.Parse("retrieve=\"SELECT 1 FROM dual\"", "d.srd");
        Assert.Equal("sql", Assert.Single(Assert.Single(dw.Routines).Body).Kind);

        var vfp = SourceParser.Parse("PROCEDURE Bar\nRETURN 1\nENDPROC", "x.prg");
        Assert.Equal("Bar", Assert.Single(vfp.Routines).Name);
        Assert.True(SourceParser.IsLegacySource("a.sru"));
        Assert.False(SourceParser.IsLegacySource("a.txt"));
    }

    [Fact]
    public void Block_comments_and_ref_args_are_stripped()
    {
        const string src = """
            public function integer Add (ref integer ai_a, integer ai_b);
            /* skip me */
            return ai_a + ai_b
            end function
            """;
        var r = Assert.Single(PbParser.Parse(src, "n.sru").Routines);
        Assert.Equal(new[] { "ai_a", "ai_b" }, r.Parameters);
        Assert.DoesNotContain(r.Body, s => s.Expression?.RawText.Contains("skip me") == true);
    }
}

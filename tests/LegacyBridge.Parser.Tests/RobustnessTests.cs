using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Parser.Tests;

public class RobustnessTests
{
    [Fact]
    public void Nested_parentheses()
    {
        var mul = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("((a + b) * (c - d))"));
        Assert.Equal("*", mul.Op);
        Assert.Equal("+", Assert.IsType<BinaryExpr>(mul.Left).Op);
        Assert.Equal("-", Assert.IsType<BinaryExpr>(mul.Right).Op);
    }

    [Fact]
    public void Negative_number_is_a_literal()
    {
        var n = Assert.IsType<LiteralExpr>(ExpressionParser.Parse("-5"));
        Assert.Equal("-5", n.Value);
        Assert.Equal("number", n.LiteralKind);
    }

    [Fact]
    public void Routine_without_lparameters()
    {
        const string src = """
            PROCEDURE Ping
                RETURN 1
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "ping.prg");
        var r = Assert.Single(program.Routines);
        Assert.Empty(r.Parameters);
        Assert.Equal("1", Assert.IsType<LiteralExpr>(r.Body[0].Expression).Value);
    }

    [Fact]
    public void Local_dotted_names()
    {
        const string src = """
            PROCEDURE Init
                LOCAL m.x, m.y
                m.x = 1
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "init.prg");
        var local = Assert.Single(program.Routines[0].Body, s => s.Kind == "local");
        Assert.Equal("m.x, m.y", local.Target);
        var assign = Assert.Single(program.Routines[0].Body, s => s.Kind == "assign");
        Assert.Equal("m.x", assign.Target);
    }

    [Fact]
    public void ElseIf_desugars_to_nested_if()
    {
        const string src = """
            PROCEDURE Grade
                IF n > 8
                    x = 1
                ELSEIF n > 5
                    x = 2
                ELSE
                    x = 3
                ENDIF
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "g.prg");
        var outer = Assert.Single(program.Routines[0].Body, s => s.Kind == "if");
        var nested = Assert.Single(outer.Else!, s => s.Kind == "if");
        Assert.NotNull(nested.Else);
    }

    [Fact]
    public void For_next_without_step()
    {
        const string src = """
            FUNCTION SumTo
                FOR i = 1 TO n
                    s = s + i
                NEXT
            ENDFUNC
            """;
        var program = VfpParser.Parse(src, "f.prg");
        var loop = Assert.Single(program.Routines[0].Body, s => s.Kind == "for");
        Assert.Null(loop.Step);
        Assert.Equal("n", loop.To!.RawText);
    }

    [Fact]
    public void Scan_while_and_empty_return()
    {
        const string src = """
            PROCEDURE Walk
                SCAN WHILE .NOT. EOF()
                ENDSCAN
                RETURN
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "w.prg");
        var scan = Assert.Single(program.Routines[0].Body, s => s.Kind == "scan");
        Assert.IsType<UnaryExpr>(scan.Expression);
        var ret = Assert.Single(program.Routines[0].Body, s => s.Kind == "return");
        Assert.Null(ret.Expression);
    }

    [Fact]
    public void Default_degrades_unknown_statements()
    {
        const string src = """
            PROCEDURE Open
                USE products
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "o.prg");
        var stmt = Assert.Single(program.Routines[0].Body);
        Assert.Equal("expression", stmt.Kind);
        Assert.IsType<RawExpr>(stmt.Expression);
    }

    [Fact]
    public void Strict_rejects_unknown_statements()
    {
        const string src = """
            PROCEDURE Open
                USE products
            ENDPROC
            """;
        var ex = Assert.Throws<ParserException>(() => VfpParser.Parse(src, "o.prg", strict: true));
        Assert.Contains("Unknown statement", ex.Message);
    }

    [Fact]
    public void Strict_still_parses_known_subset()
    {
        const string src = """
            PROCEDURE Calc
                LOCAL m.x
                m.x = -2 * (1 + 3)
                RETURN m.x
            ENDPROC
            """;
        var program = VfpParser.Parse(src, "c.prg", strict: true);
        Assert.Equal("m.x", Assert.Single(program.Routines[0].Body, s => s.Kind == "local").Target);
    }

    [Fact]
    public void Insert_sql_is_raw()
    {
        const string src = """
            PROCEDURE Save
                INSERT INTO t (a) VALUES (1)
            ENDPROC
            """;
        var sql = Assert.Single(VfpParser.Parse(src, "s.prg").Routines[0].Body);
        Assert.Equal("sql", sql.Kind);
        Assert.Contains("INSERT", sql.Expression!.RawText);
    }

    [Fact]
    public void Bang_is_not_and_functio_abbreviation()
    {
        const string src = """
            FUNCTIO Foo(a, b)
                IF a > 0 THEN
                    RETURN !EOF()
                ENDIF
            ENDFUNC
            """;
        var r = Assert.Single(VfpParser.Parse(src, "f.prg").Routines);
        Assert.Equal("Foo", r.Name);
        Assert.Equal(new[] { "a", "b" }, r.Parameters);
        var iff = Assert.Single(r.Body);
        Assert.Equal("if", iff.Kind);
        Assert.Equal("return", iff.Then![0].Kind);
        Assert.IsType<UnaryExpr>(iff.Then[0].Expression);
    }

    [Fact]
    public void Local_as_and_with_endwith()
    {
        const string src = """
            FUNCTION Pdf
                LOCAL loReport AS PreviewHelper OF App
                WITH loReport
                    .Run()
                ENDWITH
                RETURN 1
            ENDFUNC
            """;
        var body = VfpParser.Parse(src, "p.prg").Routines[0].Body;
        Assert.Contains(body, s => s.Kind == "local");
        Assert.Contains(body, s => s.Kind == "with");
    }

    [Fact]
    public void Word_and_without_dots()
    {
        var e = ExpressionParser.Parse("a > 1 AND b = 0");
        var and = Assert.IsType<BinaryExpr>(e);
        Assert.Equal("AND", and.Op, ignoreCase: true);
    }

    [Fact]
    public void Missing_endfunc_ends_at_next_function_or_eof()
    {
        const string src = """
            FUNCTION A
                RETURN 1
            FUNCTION B
                RETURN 2
            """;
        var program = VfpParser.Parse(src, "e.prg");
        Assert.Equal(2, program.Routines.Count);
        Assert.Equal("A", program.Routines[0].Name);
        Assert.Equal("B", program.Routines[1].Name);
    }
}

using LegacyBridge.Equivalence;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Equivalence.Tests;

public class StringOracleTests
{
    [Theory]
    [InlineData("  ada  ", "ADA")]
    [InlineData("Fox", "FOX")]
    public void Upper_alltrim_roundtrip(string input, string expected)
    {
        const string src = """
            FUNCTION Tag
                LPARAMETERS tcName
                RETURN UPPER(ALLTRIM(tcName))
            ENDFUNC
            """;
        var r = Assert.Single(VfpParser.Parse(src, "s.prg").Routines);
        var got = new IrInterpreter().Run(r, new Dictionary<string, object?> { ["tcName"] = input });
        Assert.Equal(expected, got);
    }

    [Fact]
    public void String_equality_is_case_insensitive()
    {
        const string src = """
            FUNCTION Same
                LPARAMETERS tcA, tcB
                IF tcA = tcB
                    RETURN 1
                ELSE
                    RETURN 0
                ENDIF
            ENDFUNC
            """;
        var r = Assert.Single(VfpParser.Parse(src, "s.prg").Routines);
        var got = new IrInterpreter().Run(r, new Dictionary<string, object?> { ["tcA"] = "Ab", ["tcB"] = "ab" });
        Assert.Equal(1m, got);
    }

    [Fact]
    public void Concat_and_len()
    {
        const string src = """
            FUNCTION Glue
                LPARAMETERS tcA, tcB
                RETURN LEN(ALLTRIM(tcA) + tcB)
            ENDFUNC
            """;
        var r = Assert.Single(VfpParser.Parse(src, "s.prg").Routines);
        var got = new IrInterpreter().Run(r, new Dictionary<string, object?> { ["tcA"] = " x ", ["tcB"] = "yz" });
        Assert.Equal(3m, got);
    }
}

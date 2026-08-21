using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Parser.Tests;

public class ExpressionParserTests
{
    [Fact]
    public void Multiplication_binds_tighter_than_addition()
    {
        var add = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("a + b * c"));
        Assert.Equal("+", add.Op);
        Assert.Equal("a", Assert.IsType<IdentifierExpr>(add.Left).Name);
        var mul = Assert.IsType<BinaryExpr>(add.Right);
        Assert.Equal("*", mul.Op);
    }

    [Fact]
    public void Parentheses_override_precedence()
    {
        var mul = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("(a + b) * c"));
        Assert.Equal("*", mul.Op);
        var add = Assert.IsType<BinaryExpr>(mul.Left);
        Assert.Equal("+", add.Op);
        Assert.Equal("c", Assert.IsType<IdentifierExpr>(mul.Right).Name);
    }

    [Fact]
    public void Nested_calls()
    {
        var round = Assert.IsType<CallExpr>(ExpressionParser.Parse("ROUND(SUM(x), 2)"));
        Assert.Equal("ROUND", round.Name);
        Assert.Equal(2, round.Args.Count);
        var sum = Assert.IsType<CallExpr>(round.Args[0]);
        Assert.Equal("SUM", sum.Name);
        Assert.Equal("2", Assert.IsType<LiteralExpr>(round.Args[1]).Value);
    }

    [Theory]
    [InlineData("<>")]
    [InlineData("!=")]
    [InlineData("#")]
    public void Not_equals_variants(string op)
    {
        var cmp = Assert.IsType<BinaryExpr>(ExpressionParser.Parse($"a {op} b"));
        Assert.Equal(op, cmp.Op);
    }

    [Fact]
    public void Or_binds_looser_than_and()
    {
        var or = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("a .AND. b .OR. c"));
        Assert.Equal(".OR.", or.Op, ignoreCase: true);
        var and = Assert.IsType<BinaryExpr>(or.Left);
        Assert.Equal(".AND.", and.Op, ignoreCase: true);
    }

    [Fact]
    public void Unary_minus_binds_tighter_than_mul()
    {
        var mul = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("-a * b"));
        Assert.Equal("*", mul.Op);
        var neg = Assert.IsType<UnaryExpr>(mul.Left);
        Assert.Equal("-", neg.Op);
    }

    [Fact]
    public void Not_binds_looser_than_comparison()
    {
        var not = Assert.IsType<UnaryExpr>(ExpressionParser.Parse(".NOT. a > 1"));
        var cmp = Assert.IsType<BinaryExpr>(not.Operand);
        Assert.Equal(">", cmp.Op);
    }

    [Fact]
    public void Comparison_binds_tighter_than_and()
    {
        var and = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("a > 1 .AND. b < 2"));
        Assert.Equal(".AND.", and.Op, ignoreCase: true);
        Assert.IsType<BinaryExpr>(and.Left);
        Assert.IsType<BinaryExpr>(and.Right);
    }

    [Fact]
    public void Boolean_literals()
    {
        var and = Assert.IsType<BinaryExpr>(ExpressionParser.Parse(".T. .AND. .F."));
        Assert.Equal("bool", Assert.IsType<LiteralExpr>(and.Left).LiteralKind);
        Assert.Equal("bool", Assert.IsType<LiteralExpr>(and.Right).LiteralKind);
    }

    [Fact]
    public void Dotted_identifier()
    {
        var id = Assert.IsType<IdentifierExpr>(ExpressionParser.Parse("m.x"));
        Assert.Equal("m.x", id.Name);
    }

    [Fact]
    public void String_literal()
    {
        var s = Assert.IsType<LiteralExpr>(ExpressionParser.Parse("'it''s'"));
        Assert.Equal("it's", s.Value);
        Assert.Equal("string", s.LiteralKind);
    }

    [Fact]
    public void Comparisons_and_unary_plus()
    {
        var eq = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("a == b"));
        Assert.Equal("==", eq.Op);
        var le = Assert.IsType<BinaryExpr>(ExpressionParser.Parse("a <= b"));
        Assert.Equal("<=", le.Op);
        var plus = Assert.IsType<UnaryExpr>(ExpressionParser.Parse("+a"));
        Assert.Equal("+", plus.Op);
    }

    [Fact]
    public void Throws_on_junk_and_missing_paren()
    {
        Assert.Throws<ParserException>(() => ExpressionParser.Parse("a b"));
        Assert.Throws<ParserException>(() => ExpressionParser.Parse("(a + b"));
        Assert.Throws<ParserException>(() => ExpressionParser.Parse("* 1"));
    }
}

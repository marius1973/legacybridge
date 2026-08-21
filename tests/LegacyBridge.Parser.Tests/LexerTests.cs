using LegacyBridge.Parser.Lexing;
using Xunit;

namespace LegacyBridge.Parser.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenizes_keywords_case_insensitively()
    {
        var tokens = new Lexer("procedure Hello").Tokenize();
        Assert.Equal(TokenKind.Procedure, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("Hello", tokens[1].Lexeme);
    }

    [Fact]
    public void Skips_star_and_ampersand_comments()
    {
        var tokens = new Lexer("* header comment\nx = 1 && inline").Tokenize();
        var significant = tokens.Where(t => t.Kind != TokenKind.NewLine && t.Kind != TokenKind.Eof).ToList();
        Assert.Equal(
            new[] { TokenKind.Identifier, TokenKind.Assign, TokenKind.Number },
            significant.Select(t => t.Kind).ToArray());
    }

    [Fact]
    public void Recognizes_dotted_logical_operators()
    {
        var tokens = new Lexer("a .AND. b .OR. .NOT. c").Tokenize();
        Assert.Contains(tokens, t => t.Kind == TokenKind.And);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Or);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Not);
    }

    [Fact]
    public void Lexes_strings_with_all_three_delimiters()
    {
        var tokens = new Lexer("'one' \"two\" [three]").Tokenize();
        var strings = tokens.Where(t => t.Kind == TokenKind.StringLiteral).Select(t => t.Lexeme).ToList();
        Assert.Equal(new[] { "one", "two", "three" }, strings);
    }

    [Fact]
    public void Recognizes_not_equals_variants()
    {
        foreach (var op in new[] { "<>", "!=", "#" })
        {
            var tokens = new Lexer($"a {op} b").Tokenize();
            Assert.Contains(tokens, t => t.Kind == TokenKind.NotEquals);
        }
    }

    [Fact]
    public void Semicolon_continuation_joins_lines()
    {
        var tokens = new Lexer("x = 1 + ;\n    2").Tokenize();
        var significant = tokens.Where(t => t.Kind != TokenKind.Eof).ToList();
        // No NewLine token between 1 and 2 thanks to the continuation.
        Assert.Equal(new[] {
            TokenKind.Identifier, TokenKind.Assign, TokenKind.Number,
            TokenKind.Plus, TokenKind.Number
        }, significant.Select(t => t.Kind).ToArray());
    }
}

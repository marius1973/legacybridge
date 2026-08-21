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
    public void Recognizes_dotted_boolean_literals()
    {
        var tokens = new Lexer(".T. .F.").Tokenize();
        Assert.Contains(tokens, t => t.Kind == TokenKind.True);
        Assert.Contains(tokens, t => t.Kind == TokenKind.False);
    }

    [Fact]
    public void Doubled_quotes_are_escapes()
    {
        var tokens = new Lexer("'it''s' \"say \"\"hi\"\"\"").Tokenize();
        var strings = tokens.Where(t => t.Kind == TokenKind.StringLiteral).Select(t => t.Lexeme).ToList();
        Assert.Equal(new[] { "it's", "say \"hi\"" }, strings);
    }

    [Fact]
    public void Lexes_strings_with_all_three_delimiters()
    {
        var tokens = new Lexer("'one' \"two\" [three]").Tokenize();
        var strings = tokens.Where(t => t.Kind == TokenKind.StringLiteral).Select(t => t.Lexeme).ToList();
        Assert.Equal(new[] { "one", "two", "three" }, strings);
    }

    [Fact]
    public void Note_at_line_start_is_a_comment()
    {
        var tokens = new Lexer("NOTE ignored\nx = 1").Tokenize();
        var significant = tokens.Where(t => t.Kind is not TokenKind.NewLine and not TokenKind.Eof).ToList();
        Assert.Equal(TokenKind.Identifier, significant[0].Kind);
        Assert.Equal("x", significant[0].Lexeme);
    }

    [Fact]
    public void Unknown_punctuation_is_skipped()
    {
        var tokens = new Lexer("x @ 1").Tokenize();
        var kinds = tokens.Where(t => t.Kind is not TokenKind.NewLine and not TokenKind.Eof).Select(t => t.Kind).ToArray();
        Assert.Equal(new[] { TokenKind.Identifier, TokenKind.Number }, kinds);
    }

    [Fact]
    public void Bang_lexes_as_not()
    {
        Assert.Equal(TokenKind.Not, new Lexer("!x").Tokenize()[0].Kind);
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

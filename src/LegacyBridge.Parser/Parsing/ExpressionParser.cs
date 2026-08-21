using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;

namespace LegacyBridge.Parser.Parsing;

/// <summary>
/// Pratt parser. Precedence (low → high):
/// OR &lt; AND &lt; NOT &lt; comparison &lt; add &lt; mul &lt; unary +/- &lt; call/atom.
/// </summary>
public static class ExpressionParser
{
    public static IrExpression Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        int pos = 0;
        return Parse(tokens, ref pos);
    }

    public static IrExpression Parse(List<Token> tokens, ref int pos, params TokenKind[] extraStops)
    {
        var expr = ParseBp(tokens, ref pos, 0, extraStops);
        if (!IsStop(tokens[pos].Kind, extraStops))
            throw Error(tokens[pos], "Unexpected token in expression");
        return expr;
    }

    private static IrExpression ParseBp(List<Token> tokens, ref int pos, int minBp, TokenKind[] extraStops)
    {
        var left = ParsePrefix(tokens, ref pos, extraStops);
        while (true)
        {
            var t = tokens[pos];
            if (IsStop(t.Kind, extraStops) || !TryInfixBp(t.Kind, out int lbp))
                break;
            if (lbp < minBp)
                break;
            Advance(tokens, ref pos);
            var right = ParseBp(tokens, ref pos, lbp + 1, extraStops);
            left = new BinaryExpr(t.Lexeme, left, right, $"{left.RawText} {t.Lexeme} {right.RawText}");
        }
        return left;
    }

    private static IrExpression ParsePrefix(List<Token> tokens, ref int pos, TokenKind[] extraStops)
    {
        int start = pos;
        var t = tokens[pos];

        switch (t.Kind)
        {
            case TokenKind.Number:
                Advance(tokens, ref pos);
                return new LiteralExpr(t.Lexeme, "number", t.Lexeme);

            case TokenKind.StringLiteral:
                Advance(tokens, ref pos);
                return new LiteralExpr(t.Lexeme, "string", t.Lexeme);

            case TokenKind.True:
            case TokenKind.False:
                Advance(tokens, ref pos);
                return new LiteralExpr(t.Lexeme, "bool", t.Lexeme);

            case TokenKind.Identifier:
                return ParseIdentOrCall(tokens, ref pos, start);

            case TokenKind.LeftParen:
                Advance(tokens, ref pos);
                var inner = ParseBp(tokens, ref pos, 0, extraStops);
                Expect(tokens, ref pos, TokenKind.RightParen, "Expected ')'");
                return inner;

            case TokenKind.Not:
            case TokenKind.Minus:
            case TokenKind.Plus:
                Advance(tokens, ref pos);
                int rbp = t.Kind == TokenKind.Not ? 25 : 60;
                var operand = ParseBp(tokens, ref pos, rbp, extraStops);
                if (t.Kind == TokenKind.Minus && operand is LiteralExpr { LiteralKind: "number" } lit)
                    return new LiteralExpr("-" + lit.Value, "number", "-" + lit.RawText);
                return new UnaryExpr(t.Lexeme, operand, Raw(tokens, start, pos));

            default:
                throw Error(t, "Expected expression");
        }
    }

    private static IrExpression ParseIdentOrCall(List<Token> tokens, ref int pos, int start)
    {
        var name = ConsumeIdent(tokens, ref pos);
        while (tokens[pos].Kind == TokenKind.Dot && PeekKind(tokens, pos, 1) == TokenKind.Identifier)
        {
            Advance(tokens, ref pos); // '.'
            name += "." + ConsumeIdent(tokens, ref pos);
        }

        if (tokens[pos].Kind != TokenKind.LeftParen)
            return new IdentifierExpr(name, name);

        Advance(tokens, ref pos); // '('
        var args = new List<IrExpression>();
        if (tokens[pos].Kind != TokenKind.RightParen)
        {
            while (true)
            {
                args.Add(ParseBp(tokens, ref pos, 0, []));
                if (tokens[pos].Kind != TokenKind.Comma)
                    break;
                Advance(tokens, ref pos);
            }
        }
        Expect(tokens, ref pos, TokenKind.RightParen, "Expected ')'");
        return new CallExpr(name, args, Raw(tokens, start, pos));
    }

    private static string ConsumeIdent(List<Token> tokens, ref int pos)
    {
        var t = tokens[pos];
        if (t.Kind != TokenKind.Identifier)
            throw Error(t, "Expected identifier");
        Advance(tokens, ref pos);
        return t.Lexeme;
    }

    private static bool TryInfixBp(TokenKind kind, out int bp)
    {
        bp = kind switch
        {
            TokenKind.Or => 10,
            TokenKind.And => 20,
            TokenKind.Assign or TokenKind.Equals or TokenKind.NotEquals
                or TokenKind.Less or TokenKind.LessOrEqual
                or TokenKind.Greater or TokenKind.GreaterOrEqual => 30,
            TokenKind.Plus or TokenKind.Minus => 40,
            TokenKind.Star or TokenKind.Slash => 50,
            _ => 0
        };
        return bp != 0;
    }

    private static bool IsStop(TokenKind kind, TokenKind[] extra)
    {
        if (kind is TokenKind.Eof or TokenKind.NewLine or TokenKind.RightParen or TokenKind.Comma)
            return true;
        foreach (var s in extra)
            if (s == kind) return true;
        return false;
    }

    private static void Advance(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count - 1) pos++;
    }

    private static TokenKind PeekKind(List<Token> tokens, int pos, int ahead) =>
        pos + ahead < tokens.Count ? tokens[pos + ahead].Kind : TokenKind.Eof;

    private static void Expect(List<Token> tokens, ref int pos, TokenKind kind, string message)
    {
        if (tokens[pos].Kind != kind)
            throw Error(tokens[pos], message);
        Advance(tokens, ref pos);
    }

    private static string Raw(List<Token> tokens, int start, int end)
    {
        var parts = new List<string>();
        for (int i = start; i < end && i < tokens.Count; i++)
        {
            var k = tokens[i].Kind;
            if (k is not TokenKind.NewLine and not TokenKind.Eof)
                parts.Add(tokens[i].Lexeme);
        }
        return string.Join(' ', parts);
    }

    private static ParserException Error(Token token, string message) =>
        new($"{message} — got {token}");
}

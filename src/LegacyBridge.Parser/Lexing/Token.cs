namespace LegacyBridge.Parser.Lexing;

public sealed record Token(TokenKind Kind, string Lexeme, int Line, int Column)
{
    public override string ToString() => $"{Kind} '{Lexeme}' @ {Line}:{Column}";
}

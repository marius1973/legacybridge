using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;

namespace LegacyBridge.Parser.Parsing;

/// <summary>
/// Recursive-descent parser for the VFP subset. Produces <see cref="IrProgram"/>.
/// Control flow is structured; expressions are captured as raw token text.
/// </summary>
public sealed class VfpParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public VfpParser(List<Token> tokens) => _tokens = tokens;

    public static IrProgram Parse(string source, string sourceName)
    {
        var tokens = new Lexer(source).Tokenize();
        return new VfpParser(tokens).ParseProgram(sourceName);
    }

    public IrProgram ParseProgram(string sourceName)
    {
        var routines = new List<IrRoutine>();
        SkipNewLines();
        while (!Check(TokenKind.Eof))
        {
            routines.Add(ParseRoutine());
            SkipNewLines();
        }
        return new IrProgram(sourceName, routines);
    }

    private IrRoutine ParseRoutine()
    {
        Token head = Current;
        string kind;
        if (Match(TokenKind.Procedure)) kind = "procedure";
        else if (Match(TokenKind.Function)) kind = "function";
        else throw Error(Current, "Expected PROCEDURE or FUNCTION");

        var name = Expect(TokenKind.Identifier, "Expected routine name").Lexeme;
        var parameters = new List<string>();

        SkipNewLines();
        if (Match(TokenKind.LParameters) || Match(TokenKind.Parameters))
        {
            do
            {
                parameters.Add(Expect(TokenKind.Identifier, "Expected parameter name").Lexeme);
            } while (Match(TokenKind.Comma));
        }

        var body = ParseBlock(TokenKind.EndProc, TokenKind.EndFunc);
        ExpectAny("Expected ENDPROC/ENDFUNC", TokenKind.EndProc, TokenKind.EndFunc);
        return new IrRoutine(name, kind, parameters, body);
    }

    /// <summary>Parses statements until one of the terminator kinds (not consumed).</summary>
    private List<IrStatement> ParseBlock(params TokenKind[] terminators)
    {
        var statements = new List<IrStatement>();
        SkipNewLines();
        while (!Check(TokenKind.Eof) && !terminators.Contains(Current.Kind)
               && Current.Kind != TokenKind.Else)
        {
            statements.Add(ParseStatement());
            SkipNewLines();
        }
        return statements;
    }

    private IrStatement ParseStatement()
    {
        var t = Current;

        switch (t.Kind)
        {
            case TokenKind.Identifier when PeekKind(1) == TokenKind.Assign:
                return ParseAssignment();
            case TokenKind.If:
                return ParseIf();
            case TokenKind.For:
                return ParseFor();
            case TokenKind.Scan:
                return ParseScan();
            case TokenKind.Do when PeekKind(1) == TokenKind.While:
                return ParseDoWhile();
            case TokenKind.Return:
                Advance();
                return new IrStatement("return", t.Line, Expression: CaptureExpression());
            case TokenKind.Select or TokenKind.Insert or TokenKind.Update or TokenKind.Delete:
                return new IrStatement("sql", t.Line, Expression: CaptureExpression());
            default:
                return new IrStatement("expression", t.Line, Expression: CaptureExpression());
        }
    }

    private IrStatement ParseAssignment()
    {
        var target = Expect(TokenKind.Identifier, "Expected assignment target");
        Expect(TokenKind.Assign, "Expected '='");
        return new IrStatement("assign", target.Line, Target: target.Lexeme,
            Expression: CaptureExpression());
    }

    private IrStatement ParseIf()
    {
        var ifToken = Expect(TokenKind.If, "Expected IF");
        var condition = CaptureExpression();
        var then = ParseBlock(TokenKind.EndIf, TokenKind.Else, TokenKind.ElseIf);
        List<IrStatement>? elseBranch = null;

        if (Match(TokenKind.Else))
        {
            elseBranch = ParseBlock(TokenKind.EndIf);
        }
        else if (Check(TokenKind.ElseIf))
        {
            // Desugar ELSEIF into a nested IF in the else branch.
            var nested = ParseElseIfAsIf();
            elseBranch = new List<IrStatement> { nested };
        }

        Expect(TokenKind.EndIf, "Expected ENDIF");
        return new IrStatement("if", ifToken.Line, Expression: condition,
            Then: then, Else: elseBranch);
    }

    private IrStatement ParseElseIfAsIf()
    {
        var elseIf = Expect(TokenKind.ElseIf, "Expected ELSEIF");
        var condition = CaptureExpression();
        var then = ParseBlock(TokenKind.EndIf, TokenKind.Else, TokenKind.ElseIf);
        List<IrStatement>? elseBranch = null;
        if (Match(TokenKind.Else))
            elseBranch = ParseBlock(TokenKind.EndIf);
        else if (Check(TokenKind.ElseIf))
            elseBranch = new List<IrStatement> { ParseElseIfAsIf() };
        return new IrStatement("if", elseIf.Line, Expression: condition,
            Then: then, Else: elseBranch);
    }

    private IrStatement ParseFor()
    {
        var forToken = Expect(TokenKind.For, "Expected FOR");
        var variable = Expect(TokenKind.Identifier, "Expected loop variable").Lexeme;
        Expect(TokenKind.Assign, "Expected '=' after loop variable");
        var from = CaptureUntil(TokenKind.To);
        Expect(TokenKind.To, "Expected TO");
        var to = CaptureUntil(TokenKind.Step, TokenKind.NewLine);
        string? step = null;
        if (Match(TokenKind.Step))
            step = CaptureExpression();

        var body = ParseBlock(TokenKind.EndFor, TokenKind.Next);
        ExpectAny("Expected ENDFOR/NEXT", TokenKind.EndFor, TokenKind.Next);
        return new IrStatement("for", forToken.Line, LoopVariable: variable,
            From: from, To: to, Step: step, Body: body);
    }

    private IrStatement ParseScan()
    {
        var scan = Expect(TokenKind.Scan, "Expected SCAN");
        var condition = CaptureExpression(); // optional FOR/WHILE clause, raw
        var body = ParseBlock(TokenKind.EndScan);
        Expect(TokenKind.EndScan, "Expected ENDSCAN");
        return new IrStatement("scan", scan.Line, Expression: condition, Body: body);
    }

    private IrStatement ParseDoWhile()
    {
        var doToken = Expect(TokenKind.Do, "Expected DO");
        Expect(TokenKind.While, "Expected WHILE");
        var condition = CaptureExpression();
        var body = ParseBlock(TokenKind.EndDo);
        Expect(TokenKind.EndDo, "Expected ENDDO");
        return new IrStatement("doWhile", doToken.Line, Expression: condition, Body: body);
    }

    // ---- token helpers ----

    /// <summary>Captures raw text of everything up to the end of the statement.</summary>
    private string CaptureExpression() => CaptureUntil(TokenKind.NewLine);

    private string CaptureUntil(params TokenKind[] stops)
    {
        var parts = new List<string>();
        int depth = 0;
        while (!Check(TokenKind.Eof))
        {
            if (depth == 0 && stops.Contains(Current.Kind))
                break;
            if (Current.Kind == TokenKind.LeftParen) depth++;
            if (Current.Kind == TokenKind.RightParen) depth--;
            parts.Add(Current.Lexeme);
            Advance();
        }
        return string.Join(' ', parts).Trim();
    }

    private void SkipNewLines()
    {
        while (Check(TokenKind.NewLine)) Advance();
    }

    private Token Current => _tokens[_pos];

    private TokenKind PeekKind(int ahead) =>
        _pos + ahead < _tokens.Count ? _tokens[_pos + ahead].Kind : TokenKind.Eof;

    private bool Check(TokenKind kind) => Current.Kind == kind;

    private bool Match(TokenKind kind)
    {
        if (!Check(kind)) return false;
        Advance();
        return true;
    }

    private void Advance()
    {
        if (_pos < _tokens.Count - 1) _pos++;
    }

    private Token Expect(TokenKind kind, string message)
    {
        SkipNewLinesInline();
        if (Current.Kind != kind) throw Error(Current, message);
        var t = Current;
        Advance();
        return t;
    }

    private Token ExpectAny(string message, params TokenKind[] kinds)
    {
        SkipNewLinesInline();
        foreach (var k in kinds)
            if (Current.Kind == k)
            {
                var t = Current;
                Advance();
                return t;
            }
        throw Error(Current, message);
    }

    private void SkipNewLinesInline()
    {
        while (Check(TokenKind.NewLine)) Advance();
    }

    private static ParserException Error(Token token, string message) =>
        new($"{message} — got {token}");
}

public sealed class ParserException(string message) : Exception(message);

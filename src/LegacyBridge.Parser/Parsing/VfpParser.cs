using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;

namespace LegacyBridge.Parser.Parsing;

/// <summary>
/// Recursive-descent parser for the VFP subset. Produces <see cref="IrProgram"/>.
/// Control flow is structured; expressions are a typed AST.
/// </summary>
public sealed class VfpParser
{
    private readonly List<Token> _tokens;
    private readonly bool _strict;
    private int _pos;

    public VfpParser(List<Token> tokens, bool strict = false)
    {
        _tokens = tokens;
        _strict = strict;
    }

    public static IrProgram Parse(string source, string sourceName, bool strict = false)
    {
        var tokens = new Lexer(source).Tokenize();
        return new VfpParser(tokens, strict).ParseProgram(sourceName);
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
                parameters.Add(ParseDottedName());
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
            case TokenKind.Identifier when LooksLikeAssignment():
                return ParseAssignment();
            case TokenKind.Local:
                return ParseLocal();
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
                return new IrStatement("return", t.Line, Expression: TryParseExpr());
            case TokenKind.Select or TokenKind.Insert or TokenKind.Update or TokenKind.Delete:
                return new IrStatement("sql", t.Line, Expression: new RawExpr(CaptureExpression()));
            default:
                if (_strict)
                    throw Error(t, $"Unknown statement '{t.Lexeme}'");
                return new IrStatement("expression", t.Line, Expression: new RawExpr(CaptureExpression()));
        }
    }

    private IrStatement ParseAssignment()
    {
        int line = Current.Line;
        var target = ParseDottedName();
        Expect(TokenKind.Assign, "Expected '='");
        return new IrStatement("assign", line, Target: target, Expression: ParseExpr());
    }

    private IrStatement ParseLocal()
    {
        var tok = Expect(TokenKind.Local, "Expected LOCAL");
        var names = new List<string> { ParseDottedName() };
        while (Match(TokenKind.Comma))
            names.Add(ParseDottedName());
        return new IrStatement("local", tok.Line, Target: string.Join(", ", names));
    }

    private IrStatement ParseIf()
    {
        var ifToken = Expect(TokenKind.If, "Expected IF");
        var condition = ParseExpr();
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
        var condition = ParseExpr();
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
        var from = ParseExpr(TokenKind.To);
        Expect(TokenKind.To, "Expected TO");
        var to = ParseExpr(TokenKind.Step, TokenKind.NewLine);
        IrExpression? step = null;
        if (Match(TokenKind.Step))
            step = ParseExpr();

        var body = ParseBlock(TokenKind.EndFor, TokenKind.Next);
        ExpectAny("Expected ENDFOR/NEXT", TokenKind.EndFor, TokenKind.Next);
        return new IrStatement("for", forToken.Line, LoopVariable: variable,
            From: from, To: to, Step: step, Body: body);
    }

    private IrStatement ParseScan()
    {
        var scan = Expect(TokenKind.Scan, "Expected SCAN");
        _ = Match(TokenKind.For) || Match(TokenKind.While);
        var condition = TryParseExpr();
        var body = ParseBlock(TokenKind.EndScan);
        Expect(TokenKind.EndScan, "Expected ENDSCAN");
        return new IrStatement("scan", scan.Line, Expression: condition, Body: body);
    }

    private IrStatement ParseDoWhile()
    {
        var doToken = Expect(TokenKind.Do, "Expected DO");
        Expect(TokenKind.While, "Expected WHILE");
        var condition = ParseExpr();
        var body = ParseBlock(TokenKind.EndDo);
        Expect(TokenKind.EndDo, "Expected ENDDO");
        return new IrStatement("doWhile", doToken.Line, Expression: condition, Body: body);
    }

    // ---- token helpers ----

    private bool LooksLikeAssignment()
    {
        int i = 0;
        if (PeekKind(i) != TokenKind.Identifier) return false;
        i++;
        while (PeekKind(i) == TokenKind.Dot && PeekKind(i + 1) == TokenKind.Identifier)
            i += 2;
        return PeekKind(i) == TokenKind.Assign;
    }

    private string ParseDottedName()
    {
        var name = Expect(TokenKind.Identifier, "Expected identifier").Lexeme;
        while (Match(TokenKind.Dot))
            name += "." + Expect(TokenKind.Identifier, "Expected identifier").Lexeme;
        return name;
    }

    private IrExpression ParseExpr(params TokenKind[] extraStops) =>
        ExpressionParser.Parse(_tokens, ref _pos, extraStops);

    private IrExpression? TryParseExpr()
    {
        if (Check(TokenKind.NewLine) || Check(TokenKind.Eof)
            || Check(TokenKind.EndProc) || Check(TokenKind.EndFunc)
            || Check(TokenKind.EndIf) || Check(TokenKind.Else) || Check(TokenKind.ElseIf)
            || Check(TokenKind.EndFor) || Check(TokenKind.Next)
            || Check(TokenKind.EndScan) || Check(TokenKind.EndDo))
            return null;
        return ParseExpr();
    }

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
        SkipNewLines();
        if (Current.Kind != kind) throw Error(Current, message);
        var t = Current;
        Advance();
        return t;
    }

    private Token ExpectAny(string message, params TokenKind[] kinds)
    {
        SkipNewLines();
        foreach (var k in kinds)
            if (Current.Kind == k)
            {
                var t = Current;
                Advance();
                return t;
            }
        throw Error(Current, message);
    }

    private static ParserException Error(Token token, string message) =>
        new($"{message} — got {token}");
}

public sealed class ParserException(string message) : Exception(message);

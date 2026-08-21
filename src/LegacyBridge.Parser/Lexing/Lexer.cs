using System.Text;

namespace LegacyBridge.Parser.Lexing;

/// <summary>
/// Hand-written lexer for the Visual FoxPro subset supported in v0.1.
/// Handles: keywords (case-insensitive), identifiers, numbers, strings
/// ('...' / "..." / [...]), &amp;&amp; comments, * and NOTE line comments,
/// ; line continuation, and the .AND./.OR./.NOT. logical operators.
/// </summary>
public sealed class Lexer
{
    private static readonly Dictionary<string, TokenKind> Keywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PROCEDURE"] = TokenKind.Procedure,
            ["FUNCTION"] = TokenKind.Function,
            ["ENDPROC"] = TokenKind.EndProc,
            ["ENDFUNC"] = TokenKind.EndFunc,
            ["LPARAMETERS"] = TokenKind.LParameters,
            ["PARAMETERS"] = TokenKind.Parameters,
            ["IF"] = TokenKind.If,
            ["ELSE"] = TokenKind.Else,
            ["ELSEIF"] = TokenKind.ElseIf,
            ["ENDIF"] = TokenKind.EndIf,
            ["FOR"] = TokenKind.For,
            ["TO"] = TokenKind.To,
            ["STEP"] = TokenKind.Step,
            ["ENDFOR"] = TokenKind.EndFor,
            ["NEXT"] = TokenKind.Next,
            ["SCAN"] = TokenKind.Scan,
            ["ENDSCAN"] = TokenKind.EndScan,
            ["DO"] = TokenKind.Do,
            ["WHILE"] = TokenKind.While,
            ["ENDDO"] = TokenKind.EndDo,
            ["RETURN"] = TokenKind.Return,
            ["LOCAL"] = TokenKind.Local,
            ["SELECT"] = TokenKind.Select,
            ["INSERT"] = TokenKind.Insert,
            ["UPDATE"] = TokenKind.Update,
            ["DELETE"] = TokenKind.Delete,
            ["USE"] = TokenKind.Use,
            ["REPLACE"] = TokenKind.Replace,
            ["WITH"] = TokenKind.With,
            ["ENDWITH"] = TokenKind.EndWith,
            ["THEN"] = TokenKind.Then,
            ["AND"] = TokenKind.And,
            ["OR"] = TokenKind.Or,
            ["NOT"] = TokenKind.Not,
        };

    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;
    private bool _atLineStart = true;

    public Lexer(string source) => _src = source;

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var t = NextToken();
            tokens.Add(t);
            if (t.Kind == TokenKind.Eof)
                return tokens;
        }
    }

    private Token NextToken()
    {
        while (_pos < _src.Length)
        {
            char c = _src[_pos];

            // Newlines are statement separators — significant in VFP.
            if (c is '\r' or '\n')
            {
                return EmitNewLine();
            }

            if (c is ' ' or '\t')
            {
                Advance();
                continue;
            }

            // ; at end of line = line continuation: consume through the newline.
            if (c == ';')
            {
                Advance();
                while (_pos < _src.Length && _src[_pos] is ' ' or '\t') Advance();
                if (_pos < _src.Length && _src[_pos] is '\r' or '\n')
                    ConsumeNewLine();
                continue;
            }

            // && inline comment — to end of line.
            if (c == '&' && Peek(1) == '&')
            {
                SkipToEndOfLine();
                continue;
            }

            // * comment or NOTE — only at statement start.
            if (_atLineStart && c == '*')
            {
                SkipToEndOfLine();
                continue;
            }

            // .AND. / .OR. / .NOT. and member access '.'
            if (c == '.')
            {
                return LexDot();
            }

            if (char.IsLetter(c) || c == '_')
            {
                return LexWord();
            }

            if (char.IsDigit(c))
            {
                return LexNumber();
            }

            if (c is '\'' or '"' or '[')
            {
                return LexString(c == '[' ? ']' : c);
            }

            return LexOperator(c);
        }

        return new Token(TokenKind.Eof, string.Empty, _line, _col);
    }

    private Token EmitNewLine()
    {
        int line = _line, col = _col;
        ConsumeNewLine();
        return new Token(TokenKind.NewLine, "\n", line, col);
    }

    private void ConsumeNewLine()
    {
        if (_pos < _src.Length && _src[_pos] == '\r') Advance();
        if (_pos < _src.Length && _src[_pos] == '\n') Advance();
        _line++;
        _col = 1;
        _atLineStart = true;
    }

    private void SkipToEndOfLine()
    {
        while (_pos < _src.Length && _src[_pos] is not ('\r' or '\n'))
            Advance();
    }

    private Token LexDot()
    {
        int line = _line, col = _col;
        var rest = PeekWordAfterDot();
        var kind = rest.ToUpperInvariant() switch
        {
            "AND." => TokenKind.And,
            "OR." => TokenKind.Or,
            "NOT." => TokenKind.Not,
            "T." => TokenKind.True,
            "F." => TokenKind.False,
            _ => (TokenKind?)null
        };
        if (kind is not null)
        {
            Advance(); // '.'
            for (int i = 0; i < rest.Length; i++) Advance();
            _atLineStart = false;
            return new Token(kind.Value, "." + rest, line, col);
        }
        Advance();
        _atLineStart = false;
        return new Token(TokenKind.Dot, ".", line, col);
    }

    private string PeekWordAfterDot()
    {
        var sb = new StringBuilder();
        int i = _pos + 1;
        while (i < _src.Length && (char.IsLetter(_src[i]) || _src[i] == '.'))
            sb.Append(_src[i++]);
        return sb.ToString();
    }

    private Token LexWord()
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
        {
            sb.Append(_src[_pos]);
            Advance();
        }
        var word = sb.ToString();

        // NOTE at statement start is a comment line.
        if (_atLineStart && word.Equals("NOTE", StringComparison.OrdinalIgnoreCase))
        {
            SkipToEndOfLine();
            return NextToken();
        }

        _atLineStart = false;
        if (Keywords.TryGetValue(word, out var kind))
            return new Token(kind, word, line, col);
        // VFP allows 4+ character abbreviations: FUNCTIO → FUNCTION, ENDI → ENDIF.
        if (word.Length >= 4)
        {
            var hits = Keywords.Where(kv => kv.Key.StartsWith(word, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 1)
                return new Token(hits[0].Value, word, line, col);
        }
        return new Token(TokenKind.Identifier, word, line, col);
    }

    private Token LexNumber()
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.'))
        {
            sb.Append(_src[_pos]);
            Advance();
        }
        _atLineStart = false;
        return new Token(TokenKind.Number, sb.ToString(), line, col);
    }

    private Token LexString(char terminator)
    {
        int line = _line, col = _col;
        Advance(); // opening quote
        var sb = new StringBuilder();
        while (_pos < _src.Length && _src[_pos] is not ('\r' or '\n'))
        {
            if (_src[_pos] == terminator)
            {
                if (Peek(1) == terminator)
                {
                    sb.Append(terminator);
                    Advance();
                    Advance();
                    continue;
                }
                Advance(); // closing
                break;
            }
            sb.Append(_src[_pos]);
            Advance();
        }
        _atLineStart = false;
        return new Token(TokenKind.StringLiteral, sb.ToString(), line, col);
    }

    private Token LexOperator(char c)
    {
        int line = _line, col = _col;
        TokenKind kind;
        string lexeme = c.ToString();

        switch (c)
        {
            case '(': kind = TokenKind.LeftParen; break;
            case ')': kind = TokenKind.RightParen; break;
            case ',': kind = TokenKind.Comma; break;
            case '+': kind = TokenKind.Plus; break;
            case '-': kind = TokenKind.Minus; break;
            case '*': kind = TokenKind.Star; break;
            case '/': kind = TokenKind.Slash; break;
            case '=':
                if (Peek(1) == '=') { Advance(); kind = TokenKind.Equals; lexeme = "=="; }
                else kind = TokenKind.Assign;
                break;
            case '<':
                if (Peek(1) == '=') { Advance(); kind = TokenKind.LessOrEqual; lexeme = "<="; }
                else if (Peek(1) == '>') { Advance(); kind = TokenKind.NotEquals; lexeme = "<>"; }
                else kind = TokenKind.Less;
                break;
            case '>':
                if (Peek(1) == '=') { Advance(); kind = TokenKind.GreaterOrEqual; lexeme = ">="; }
                else kind = TokenKind.Greater;
                break;
            case '!':
                if (Peek(1) == '=') { Advance(); kind = TokenKind.NotEquals; lexeme = "!="; }
                else kind = TokenKind.Not; // VFP: !EOF() is .NOT. EOF()
                break;
            case '#': kind = TokenKind.NotEquals; break;
            default:
                // ponytail: skip unknown punctuation (:, @, ?) so real .prg files still lex
                Advance();
                _atLineStart = false;
                return NextToken();
        }

        Advance();
        _atLineStart = false;
        return new Token(kind, lexeme, line, col);
    }

    private char Peek(int ahead) =>
        _pos + ahead < _src.Length ? _src[_pos + ahead] : '\0';

    private void Advance()
    {
        _pos++;
        _col++;
    }
}

public sealed class LexerException(string message) : Exception(message);

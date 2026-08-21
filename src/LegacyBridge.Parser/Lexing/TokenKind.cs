namespace LegacyBridge.Parser.Lexing;

/// <summary>Token categories for the Visual FoxPro subset (v0.1).</summary>
public enum TokenKind
{
    Identifier,
    Number,
    StringLiteral,

    // Routine structure
    Procedure,
    Function,
    EndProc,
    EndFunc,
    LParameters,
    Parameters,

    // Control flow
    If,
    Else,
    ElseIf,
    EndIf,
    For,
    To,
    Step,
    EndFor,
    Next,
    Scan,
    EndScan,
    Do,
    While,
    EndDo,
    Return,
    Local,

    // Data / SQL
    Select,
    Insert,
    Update,
    Delete,
    Use,
    Replace,
    With,

    Then,
    EndWith,

    // Logical operators (.AND. / .OR. / .NOT.) and literals (.T. / .F.)
    And,
    Or,
    Not,
    True,
    False,

    // Operators & punctuation
    Comma,
    Dot,
    LeftParen,
    RightParen,
    Assign,        // =
    Equals,        // ==
    NotEquals,     // <>  !=  #
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Plus,
    Minus,
    Star,
    Slash,

    NewLine,
    Eof
}

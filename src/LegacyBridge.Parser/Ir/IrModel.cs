using System.Text.Json.Serialization;

namespace LegacyBridge.Parser.Ir;

/// <summary>
/// Unified intermediate representation. This is the deterministic,
/// inspectable artifact the AI agents consume — never raw source text.
/// </summary>
public sealed record IrProgram(
    string SourceName,
    IReadOnlyList<IrRoutine> Routines);

public sealed record IrRoutine(
    string Name,
    string Kind,                        // "procedure" | "function"
    IReadOnlyList<string> Parameters,
    IReadOnlyList<IrStatement> Body);

public sealed record IrStatement(
    string Kind,                        // assign | if | for | scan | doWhile | sql | return | expression
    int Line,
    string? Target = null,              // assignment target
    IrExpression? Expression = null,
    string? LoopVariable = null,        // FOR variable
    IrExpression? From = null,          // FOR lower bound
    IrExpression? To = null,            // FOR upper bound
    IrExpression? Step = null,          // FOR step
    IReadOnlyList<IrStatement>? Then = null,
    IReadOnlyList<IrStatement>? Else = null,
    IReadOnlyList<IrStatement>? Body = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(LiteralExpr), "literal")]
[JsonDerivedType(typeof(IdentifierExpr), "identifier")]
[JsonDerivedType(typeof(BinaryExpr), "binary")]
[JsonDerivedType(typeof(UnaryExpr), "unary")]
[JsonDerivedType(typeof(CallExpr), "call")]
[JsonDerivedType(typeof(RawExpr), "raw")]
public abstract record IrExpression(string RawText);

public sealed record LiteralExpr(string Value, string LiteralKind, string RawText) : IrExpression(RawText);

public sealed record IdentifierExpr(string Name, string RawText) : IrExpression(RawText);

public sealed record BinaryExpr(string Op, IrExpression Left, IrExpression Right, string RawText) : IrExpression(RawText);

public sealed record UnaryExpr(string Op, IrExpression Operand, string RawText) : IrExpression(RawText);

public sealed record CallExpr(string Name, IReadOnlyList<IrExpression> Args, string RawText) : IrExpression(RawText);

/// <summary>
/// Unparsed text: embedded SQL and unknown statements.
/// ponytail: --strict (plan week 2) would reject these instead of degrading.
/// </summary>
public sealed record RawExpr(string RawText) : IrExpression(RawText);

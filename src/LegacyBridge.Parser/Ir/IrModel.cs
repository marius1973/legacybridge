namespace LegacyBridge.Parser.Ir;

/// <summary>
/// Unified intermediate representation. This is the deterministic,
/// inspectable artifact the AI agents consume — never raw source text.
/// Expressions are captured as raw text in v0.1 (expression AST: v0.2).
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
    string? Expression = null,          // raw expression text
    string? LoopVariable = null,        // FOR variable
    string? From = null,                // FOR lower bound
    string? To = null,                  // FOR upper bound
    string? Step = null,                // FOR step
    IReadOnlyList<IrStatement>? Then = null,
    IReadOnlyList<IrStatement>? Else = null,
    IReadOnlyList<IrStatement>? Body = null);

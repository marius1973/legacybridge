# Architecture

## Principle: LLMs propose, the IR disposes

LegacyBridge never feeds raw legacy source directly to a code-generation LLM.
Source code passes through a **deterministic parser** that produces a unified
intermediate representation (IR, JSON). All downstream agents — extraction,
generation, equivalence testing — consume the IR.

Why:

1. **Inspectability** — the IR is a diffable artifact you can code-review.
2. **Testability** — the parser is plain C# with unit tests; no model drift.
3. **Portability** — one IR serves both VFP and PowerBuilder frontends.

## Modules (v0.2 status)

| Module | Path | Status |
|---|---|---|
| VFP lexer | `src/LegacyBridge.Parser/Lexing/` | ✅ |
| VFP parser → IR | `src/LegacyBridge.Parser/Parsing/` | ✅ |
| Expression AST | `src/LegacyBridge.Parser/Parsing/ExpressionParser.cs` | ✅ |
| CLI `analyze` | `src/LegacyBridge.Cli/` | ✅ |
| Business Spec extractor (Agent 1) | `src/agents/` | v0.2.1 |
| .NET generator (Agent 2) | `src/agents/` | v0.3 |
| Equivalence tester (Agent 3) | `src/agents/` | v0.4 |
| MCP server | `src/agents/mcp-server/` | v0.5 |
| Dashboard | `src/dashboard/` | v0.6 |

## IR shape

```jsonc
{
  "SourceName": "inv_calc.prg",
  "Routines": [
    {
      "Name": "CalcStockValue",
      "Kind": "procedure",
      "Parameters": ["tnQty", "tnUnitCost"],
      "Body": [
        { "Kind": "assign", "Target": "lnValue",
          "Expression": { "Kind": "binary", "Op": "*",
            "Left": { "Kind": "identifier", "Name": "tnQty" },
            "Right": { "Kind": "identifier", "Name": "tnUnitCost" } } },
        { "Kind": "if",
          "Expression": { "Kind": "binary", "Op": ">", /* lnValue > 10000 */ },
          "Then": [ /* ... */ ], "Else": [ /* ... */ ] }
      ]
    }
  ]
}
```

Expressions are a typed AST (`literal` / `identifier` / `unary` / `binary` / `call`).
Each node keeps `RawText` for traceability. Unknown statements and embedded SQL
degrade to `raw`.

## Error strategy

The parser fails fast with line/column information (`ParserException`).
Unknown statements degrade to `expression` + `RawExpr` so partial migrations
still produce a complete IR. Pass `strict: true` (CLI `--strict`) to reject them
instead. Embedded SQL is always captured as `raw`, even in strict mode.

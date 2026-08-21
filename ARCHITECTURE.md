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

## Modules (v0.4 status)

| Module | Path | Status |
|---|---|---|
| VFP lexer | `src/LegacyBridge.Parser/Lexing/` | ✅ |
| VFP parser → IR | `src/LegacyBridge.Parser/Parsing/` | ✅ |
| Expression AST | `src/LegacyBridge.Parser/Parsing/ExpressionParser.cs` | ✅ |
| CLI `analyze` / `extract` / `generate` | `src/LegacyBridge.Cli/` | ✅ |
| Business Spec extractor (Agent 1) | `src/agents/` | ✅ |
| .NET generator (Agent 2) | `src/LegacyBridge.Generator/` | ✅ |
| Equivalence tester (Agent 3) | `src/LegacyBridge.Equivalence/` | ✅ |
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

## Agent 1: business spec

`legacybridge extract` parses source to IR, then `src/agents/extract.ts` maps IR → YAML
validated against `src/agents/schemas/business-spec.schema.json`.

The default path is **deterministic** (no API key) so CI can enforce recall ≥ 0.8
against `samples/vfp-inventory/business-spec.expected.yaml`. Pass `--llm` to overlay
the versioned prompt `src/agents/prompts/extractor.v1.md` via OpenAI, Anthropic, or
**Ollama** (`OLLAMA_HOST`, default `http://127.0.0.1:11434` — $0). Invalid LLM YAML
falls back to the IR mapping unless `LEGACYBRIDGE_LLM=required`.

## Agent 2: .NET generator

`legacybridge generate` maps the IR AST to a four-project .NET 8 solution
(`Domain` / `Application` / `Infrastructure` / `Api`) using C# string templates
(no Scriban — the structure is small and fixed).

Method bodies are **deterministic**: `CsharpEmitter` walks `IrExpression` /
`IrStatement`. `ROUND` → `Math.Round`, `.AND.` → `&&`, `SCAN FOR` → `foreach` +
`Where`, `REPLACE … WITH` → property assign. Embedded SQL stays a comment plus
`IReadOnlyList<T>` stub so the solution still compiles.

`--build` runs `dotnet build` up to 3 times and logs each attempt. That is the
compile-fix loop slot for an LLM repair pass later; on `inv_calc` the AST path
succeeds on attempt 1 (0 retries). Output: `samples/vfp-inventory/migrated/`.

## Agent 3: equivalence

`legacybridge verify` runs the same cases on two oracles:

1. **IR interpreter** — executes the AST (arithmetic, IF, SCAN/REPLACE, `ROUND`).
2. **Migrated `ProductService`** — the generated .NET 8 application.

Cases are a deterministic grid (zeros, negatives, cap-at-50, qty×cost around 10000) plus SCAN fixtures. Embedded SQL (`MonthlyReport`) is skipped, not failed. Threshold: `evals/thresholds.json` → `equivalence: 0.9`. Report: `samples/vfp-inventory/EQUIVALENCE-REPORT.md`.

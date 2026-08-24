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

## Modules (v0.7 status)

| Module | Path | Status |
|---|---|---|
| VFP lexer | `src/LegacyBridge.Parser/Lexing/` | ✅ |
| VFP parser → IR | `src/LegacyBridge.Parser/Parsing/` | ✅ |
| PowerBuilder frontend | `src/LegacyBridge.Parser/Parsing/PbParser.cs` | ✅ subset (normalize → VFP parser; `.srd` retrieve SQL) |
| Expression AST | `src/LegacyBridge.Parser/Parsing/ExpressionParser.cs` | ✅ |
| CLI `analyze` / `extract` / `generate` / `verify` | `src/LegacyBridge.Cli/` | ✅ |
| Business Spec extractor (Agent 1) | `src/agents/` | ✅ |
| .NET generator (Agent 2) | `src/LegacyBridge.Generator/` | ✅ |
| Equivalence tester (Agent 3) | `src/LegacyBridge.Equivalence/` | ✅ |
| MCP server | `src/agents/mcp-server/` | ✅ |
| Dashboard | `src/dashboard/` | ✅ |

## IR shape

```jsonc
{
  "SourceName": "inv_calc.prg",
  "IrVersion": 1,
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

`legacybridge generate [--spec spec.yaml]` maps the IR AST to a .NET 8 solution
(`Domain` / `Application` / `Infrastructure` / `Api` / `Tests`) using C# string
templates (no Scriban).

**Entities come from the Agent 1 spec when `--spec` is passed.** `SpecInfer`
(Hungarian-prefix heuristics on IR text) is only the fallback. Persistence is
EF Core **InMemory** so the demo has no database; Postgres is a provider swap
(`UseNpgsql`) later.

Method bodies are **deterministic**: `CsharpEmitter` walks `IrExpression` /
`IrStatement`. `ROUND` → `Math.Round`, `.AND.` → `&&`, `SCAN FOR` → `foreach` +
`Where`, `REPLACE … WITH` → property assign, `ALLTRIM`/`UPPER`/`LOWER`/`LEN` →
string methods. Embedded SQL stays a comment plus `IReadOnlyList<T>` stub.

`--build` runs `dotnet build` **once**. The compiler log is the slot for a
future LLM repair pass — retrying the same command is not a fix loop.
Generated `Tests/` encode hand-computed goldens from `evals/golden-cases.json`.

## Agent 3: equivalence

`legacybridge verify` runs the same cases on two oracles:

1. **IR interpreter** — executes the AST (arithmetic, strings, IF, SCAN/REPLACE, `ROUND`).
2. **Migrated `ProductService`** — the generated .NET 8 application.

Cases are driven by **control-flow thresholds** extracted from `IF` literals
(e.g. `> 10000` → 9999 / 10000 / 10001) plus a small grid. Extra parameters
beyond the first two stay 0. Embedded SQL (`MonthlyReport`) is skipped, not
failed. Threshold: `evals/thresholds.json` → `equivalence: 0.9`.

**Known limit:** interpreter and emitter share the IR. A parser bug can make
both wrong and still report 100%. Mitigation: `evals/golden-cases.json`
(hand-computed) vs the interpreter in CI.

## MCP server

`src/agents/mcp-server/` is a stdio MCP server (`@modelcontextprotocol/sdk`). Each tool shells out to `legacybridge` (the C# CLI) — no second pipeline. Config: repo-root `.mcp.json`. `--self-test` runs `analyze_legacy` on the bundled sample.

## Dashboard

`src/dashboard/` (Next.js) shows the committed equivalence table on load and can re-run the CLI pipeline over SSE (`POST /api/run`). `docker compose up --build` serves it at http://localhost:3000. No Postgres/Langfuse — the sample demo does not need them.

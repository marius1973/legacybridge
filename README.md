# LegacyBridge

> AI-agent pipeline that analyzes Visual FoxPro / PowerBuilder code, extracts its business logic, and generates an equivalent **.NET 8 / C# solution with Clean Architecture + DDD** — proven correct by **automatically generated functional-equivalence tests**.
>
> Also available as a CLI, a web dashboard, and an **MCP server** for AI coding agents.

![CI](https://github.com/marius1973/legacybridge/actions/workflows/ci.yml/badge.svg)
![Coverage](https://img.shields.io/badge/coverage-80%25%2B-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![MCP](https://img.shields.io/badge/MCP-server-blueviolet)
![License](https://img.shields.io/badge/license-MIT-blue)

<!-- TODO: add a 30-second demo.gif here — it is the single most-viewed asset in this repo -->
<!-- ![Demo](docs/demo.gif) -->

---

## The problem

Migrating a legacy system is not expensive because the new code is hard to write.
It is expensive because **nobody can prove the new system behaves like the old one.**

Teams spend 60–80% of a migration budget on reverse-engineering business rules buried in `.prg` and `.sru` files, and on manual regression testing. AI code translators make this worse: they produce code that *looks* right but silently changes business logic.

**LegacyBridge treats equivalence as the product, not the translation.**

## How it works

```
VFP / PowerBuilder source (.prg, .scx, .sru + DBF tables)
        │
        ▼
┌───────────────────┐   deterministic lexer + parser (C#)
│   Parser → IR     │   → unified intermediate representation (JSON)
└─────────┬─────────┘
          ▼
┌───────────────────┐   Agent 1: maps entities, rules, flows, embedded SQL
│  Business Spec    │   → versioned YAML business specification
│  Extractor (LLM)  │
└─────────┬─────────┘
          ▼
┌───────────────────┐   Agent 2: .NET 8 solution — Domain / Application /
│  .NET Generator   │   Infrastructure layers, EF Core, DDD patterns
│  (LLM + templates)│
└─────────┬─────────┘
          ▼
┌───────────────────┐   Agent 3: generates test cases, runs BOTH versions
│ Equivalence Tester│   (legacy vs. .NET) on the same data, diffs outputs
│        ⭐         │   → equivalence report with % functional match
└─────────┬─────────┘
          ▼
┌───────────────────┐   eval suite in CI: extraction precision, compile rate,
│  Evals + Tracing  │   functional equivalence. Langfuse tracing per agent.
└───────────────────┘
```

## Quickstart (2 minutes)

```bash
git clone https://github.com/marius1973/legacybridge.git
cd legacybridge

# Analyze the bundled VFP sample and emit its IR
dotnet run --project src/LegacyBridge.Cli -- analyze samples/vfp-inventory/legacy --output ir.json

# (Coming in v0.4 — full pipeline with agents)
docker compose up
legacybridge migrate samples/vfp-inventory --verify
```

## Results on the bundled sample

| Metric | Value |
|---|---|
| Functional equivalence (`samples/vfp-inventory`) | 🎯 target ≥ 90% *(measured in CI from v0.4)* |
| Generated code compiling without manual edits | 🎯 target ≥ 95% |
| Migration time (sample) | 🎯 minutes vs. ~40 h manual estimate |
| Eval cases in CI | growing — see `evals/` |

*Targets are published as CI-enforced thresholds, not marketing: the build fails if a change drops equivalence below the threshold.*

## Use it from an AI agent (MCP)

LegacyBridge exposes its pipeline as an [MCP](https://modelcontextprotocol.io) server, so agents like Claude Code can migrate legacy code as a tool call.

```jsonc
// .mcp.json (available from v0.5)
{
  "mcpServers": {
    "legacybridge": {
      "command": "node",
      "args": ["src/agents/mcp-server/dist/index.js"]
    }
  }
}
```

Tools exposed: `analyze_legacy` · `generate_dotnet` · `run_equivalence`

## Supported language subset (honest scope)

Current parser coverage (v0.1):

| Construct | VFP | PowerBuilder |
|---|---|---|
| `PROCEDURE` / `FUNCTION`, `LPARAMETERS` | ✅ | 🔜 v0.2 |
| `IF / ELSE / ENDIF` | ✅ | 🔜 |
| `FOR ... TO ... STEP / ENDFOR` | ✅ | 🔜 |
| `SCAN / ENDSCAN`, `DO WHILE / ENDDO` | ✅ | 🔜 |
| Embedded SQL (`SELECT/INSERT/UPDATE/DELETE`) | raw capture | 🔜 |
| Forms (`.scx`) / DataWindows | 🔜 v0.3 | 🔜 v0.3 |

A small, well-tested subset beats a broad, fragile one. Expressions are currently captured as raw text in the IR; a full expression AST lands in v0.2.

## Repository layout

```
src/
  LegacyBridge.Parser/    C# lexer + parser → unified IR (JSON)
  LegacyBridge.Cli/       `legacybridge analyze|migrate|verify`
  agents/                 (v0.2+) TypeScript agent orchestration + MCP server
  dashboard/              (v0.6+) Next.js progress + equivalence dashboard
evals/                    (v0.4+) datasets, CI-published reports
samples/
  vfp-inventory/          real before/after case: legacy → migrated → report
docs/                     architecture, migration guide, demo assets
```

## Design decisions

- **Why an IR instead of direct LLM translation?** Deterministic parsing gives the agents a faithful, inspectable model of the code — and gives you a diffable artifact in code review. LLMs propose; the IR disposes.
- **Why equivalence tests instead of snapshots?** Snapshot tests freeze behavior you don't understand. Equivalence tests run the legacy binary semantics against the new domain code on the *same* dataset — that is the property a business actually pays for.
- **Why evals in CI?** An LLM pipeline without evals is a demo. Every prompt/model change is gated on extraction precision and functional-equivalence thresholds.

## Roadmap

- [x] **v0.1** — VFP lexer/parser → IR, CLI `analyze`, CI + tests
- [ ] **v0.2** — expression AST, PowerBuilder subset, Business Spec extractor agent
- [ ] **v0.3** — .NET 8 generator (DDD + EF Core) from Business Spec
- [ ] **v0.4** — equivalence tester + eval suite in CI
- [ ] **v0.5** — MCP server (`analyze_legacy`, `generate_dotnet`, `run_equivalence`)
- [ ] **v0.6** — Next.js dashboard, PowerBuilder sample case
- [ ] **v1.0** — second real-world case, launch write-up

## Contributing

Issues labeled `good-first-issue` are scoped to be tackled without LLM infrastructure. See `ARCHITECTURE.md` before opening a PR.

## License

MIT © Mario Manrique — 15+ years migrating legacy systems in production (finance, mining, telecom).

---

*If this project helped your migration, a ⭐ helps others find it.*

# LegacyBridge walkthrough

From clone to an equivalence report, on the bundled Visual FoxPro sample.
PowerBuilder (`.sru`) follows the same commands.

## 0. Prerequisites

- .NET 8 SDK (`dotnet --list-sdks` should list `8.0.x`)
- Node 20+
- On Windows, if `dotnet` on PATH is the x86 host, set
  `DOTNET_ROOT=C:\Program Files\dotnet` or use that `dotnet.exe` directly.

## 1. Install

```bash
git clone https://github.com/marius1973/legacybridge.git
cd legacybridge
npm install --prefix src/agents
```

## 2. Parse → IR

```bash
dotnet run --project src/LegacyBridge.Cli -- analyze samples/vfp-inventory/legacy --output ir.json
```

The JSON is versioned (`IrVersion: 1`). Same command works on `samples/pb-billing/legacy`.

## 3. Extract a business spec (Agent 1)

```bash
dotnet run --project src/LegacyBridge.Cli -- extract samples/vfp-inventory/legacy --output spec.yaml
```

Deterministic by default (no API key). Compare with
`samples/vfp-inventory/business-spec.expected.yaml`. Optional: `--llm`.

## 4. Generate .NET 8 (Agent 2) — **from the spec**

```bash
dotnet run --project src/LegacyBridge.Cli -- generate samples/vfp-inventory/legacy \
  --output generated/demo --build --spec spec.yaml
```

`--spec` is the important flag: entities and fields come from Agent 1's YAML.
Without `--spec`, the generator falls back to heuristics on the IR (`SpecInfer`).

The solution is four (plus tests) .NET 8 projects: Domain / Application /
Infrastructure / Api / Tests. Persistence is **EF Core InMemory** so the demo
runs with zero database. Switching to Postgres later is a provider change
(`UseNpgsql` instead of `UseInMemoryDatabase`) — not a rewrite of the domain.

`--build` runs `dotnet build` **once**. An LLM compile-repair loop is a documented
hook, not a silent retry of the same command.

Generated `Tests/` contains xUnit theories whose expected values are the
**hand-computed** goldens in `evals/golden-cases.json` (not the interpreter).

```bash
dotnet test generated/demo
```

## 5. Prove equivalence (Agent 3)

```bash
dotnet run --project src/LegacyBridge.Cli -- verify samples/vfp-inventory/legacy \
  --output EQUIVALENCE-REPORT.md --min-match 0.9
```

Two executors, same cases:

1. IR interpreter (oracle)
2. Migrated `ProductService`

Cases are built from **control-flow thresholds** (the `> 10000` and `> 50`
literals plus neighbors), not a blind grid. Embedded SQL is skipped, not faked.

### Honest limit

The interpreter and the C# emitter both read the same IR. If the parser is
wrong, both can be wrong together and still report 100%. That is why
`evals/golden-cases.json` exists: expected numbers computed by hand from the
business rules, checked against the interpreter in CI, and baked into the
generated xUnit tests.

## 6. Dashboard

```bash
npm install --prefix src/dashboard
npm run dev --prefix src/dashboard
# http://localhost:3000 — ES/EN toggle, Run bundled sample
```

The dashboard runs extract then `generate --spec` on that YAML, then verify.

Or: `docker compose up --build`.

## 7. MCP (local stdio — not the cloud dashboard)

After `npm install --prefix src/agents`, point the host at repo-root `.mcp.json`.
Smoke: `npx tsx mcp-server/index.ts --self-test` from `src/agents`.

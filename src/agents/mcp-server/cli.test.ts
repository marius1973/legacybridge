import { test } from "node:test";
import assert from "node:assert/strict";
import { analyzeLegacy, summarizeIr } from "./cli.ts";

test("summarizeIr lists routines without dumping bodies", () => {
  const ir = JSON.stringify({
    SourceName: "inv_calc.prg",
    Routines: [
      { Name: "CalcStockValue", Kind: "procedure", Parameters: ["tnQty"], Body: [{}, {}] },
    ],
  });
  const s = summarizeIr(ir);
  assert.match(s, /CalcStockValue/);
  assert.match(s, /"statements": 2/);
  assert.doesNotMatch(s, /Body/);
});

test("analyze_legacy on bundled sample", async () => {
  const out = await analyzeLegacy("samples/vfp-inventory/legacy");
  assert.match(out, /CalcStockValue/);
  assert.match(out, /ApplyDiscount/);
});

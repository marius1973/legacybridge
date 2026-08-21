import { test } from "node:test";
import assert from "node:assert/strict";
import { fromIr, validateSpec } from "./extract.ts";

const ir = {
  SourceName: "inv_calc.prg",
  Routines: [
    {
      Name: "CalcStockValue",
      Kind: "procedure",
      Parameters: ["tnQty", "tnUnitCost"],
      Body: [
        {
          Kind: "if",
          Expression: { RawText: "lnValue > 10000" },
          Then: [{ Kind: "assign", Target: "lnValue", Expression: { RawText: "lnValue * 1.02" } }],
          Else: [{ Kind: "assign", Target: "lnValue", Expression: { RawText: "lnValue + 5" } }],
        },
      ],
    },
    {
      Name: "MonthlyReport",
      Kind: "procedure",
      Parameters: ["tnYear"],
      Body: [
        {
          Kind: "sql",
          Expression: { RawText: "SELECT product, SUM(total_value) FROM products WHERE year = tnYear" },
        },
      ],
    },
  ],
};

test("fromIr emits schema-valid spec with Product and IF rules", () => {
  const spec = validateSpec(fromIr(ir));
  assert.equal(spec.source, "inv_calc.prg");
  assert.equal(spec.entities[0]?.name, "Product");
  assert.ok(spec.entities[0].fields.includes("product"));
  assert.ok(spec.rules.some((r) => r.routine === "CalcStockValue" && r.description.includes("10000")));
  assert.ok(spec.rules.some((r) => r.description.includes("5")));
  assert.equal(spec.queries[0]?.routine, "MonthlyReport");
});

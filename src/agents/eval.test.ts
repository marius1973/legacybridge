import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { parse as parseYaml } from "yaml";
import { validateSpec } from "./extract.ts";
import { score } from "./eval.ts";

test("golden spec validates", () => {
  const gold = validateSpec(
    parseYaml(readFileSync("../../samples/vfp-inventory/business-spec.expected.yaml", "utf8")),
  );
  assert.ok(gold.rules.length >= 6);
});

test("score is 1 when pred equals gold", () => {
  const gold = validateSpec(
    parseYaml(readFileSync("../../samples/vfp-inventory/business-spec.expected.yaml", "utf8")),
  );
  const s = score(gold, gold);
  assert.equal(s.entities.recall, 1);
  assert.equal(s.rules.recall, 1);
});

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse as parseYaml } from "yaml";
import { validateSpec, type Spec } from "./extract.ts";

function tokens(s: string): Set<string> {
  return new Set(
    s
      .toLowerCase()
      .replace(/[^a-z0-9.]+/g, " ")
      .split(/\s+/)
      .filter((w) => w.length > 0 && (/^\d/.test(w) || (w.length > 1 && !["when", "then", "otherwise", "with", "from", "scan", "while"].includes(w)))),
  );
}

function entityKey(name: string): string {
  return name.toLowerCase().replace(/ies$/, "y").replace(/s$/, "");
}

function ruleMatch(gold: Spec["rules"][0], pred: Spec["rules"][0]): boolean {
  if (gold.routine !== pred.routine) return false;
  const g = tokens(gold.description);
  const p = tokens(pred.description);
  let hit = 0;
  for (const w of g) if (p.has(w)) hit++;
  return hit >= 2 || [...g].some((w) => /^\d/.test(w) && p.has(w));
}

export function score(pred: Spec, gold: Spec) {
  const goldEnt = new Set(gold.entities.map((e) => entityKey(e.name)));
  const predEnt = new Set(pred.entities.map((e) => entityKey(e.name)));
  let entHit = 0;
  for (const e of goldEnt) if (predEnt.has(e)) entHit++;
  const entRecall = goldEnt.size ? entHit / goldEnt.size : 1;
  const entPrec = predEnt.size ? entHit / predEnt.size : 1;

  const used = new Set<number>();
  let ruleHit = 0;
  for (const g of gold.rules) {
    const idx = pred.rules.findIndex((p, i) => !used.has(i) && ruleMatch(g, p));
    if (idx >= 0) {
      used.add(idx);
      ruleHit++;
    }
  }
  const ruleRecall = gold.rules.length ? ruleHit / gold.rules.length : 1;
  const rulePrec = pred.rules.length ? ruleHit / pred.rules.length : 1;

  return {
    entities: { precision: entPrec, recall: entRecall },
    rules: { precision: rulePrec, recall: ruleRecall },
  };
}

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(name);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1] ?? "")) {
  const gotPath = arg("--got");
  const goldPath = arg("--golden");
  const min = Number(arg("--min-recall") ?? "0.8");
  if (!gotPath || !goldPath) {
    console.error("usage: eval --got spec.yaml --golden expected.yaml [--min-recall 0.8]");
    process.exit(2);
  }
  const pred = validateSpec(parseYaml(readFileSync(gotPath, "utf8")));
  const gold = validateSpec(parseYaml(readFileSync(goldPath, "utf8")));
  const s = score(pred, gold);
  const fmt = (n: number) => n.toFixed(2);
  console.log(`entities  P=${fmt(s.entities.precision)} R=${fmt(s.entities.recall)}`);
  console.log(`rules     P=${fmt(s.rules.precision)} R=${fmt(s.rules.recall)}`);
  if (s.entities.recall < min || s.rules.recall < min) {
    console.error(`recall below ${min}`);
    process.exit(1);
  }
}

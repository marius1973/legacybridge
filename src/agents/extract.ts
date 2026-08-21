import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import Ajv from "ajv";
import { parse as parseYaml, stringify as toYaml } from "yaml";

const here = dirname(fileURLToPath(import.meta.url));

export type Spec = {
  source: string;
  entities: { name: string; fields: string[] }[];
  rules: { id: string; description: string; routine: string }[];
  flows: { name: string; kind: string; parameters?: string[]; steps?: string[] }[];
  queries: { routine: string; sql: string }[];
};

type Stmt = {
  Kind: string;
  Target?: string;
  Expression?: { RawText?: string; Kind?: string };
  Then?: Stmt[];
  Else?: Stmt[];
  Body?: Stmt[];
};

type Routine = { Name: string; Kind: string; Parameters: string[]; Body: Stmt[] };
type Program = { SourceName: string; Routines: Routine[] };

export function validateSpec(spec: unknown): Spec {
  const schema = JSON.parse(readFileSync(join(here, "schemas", "business-spec.schema.json"), "utf8"));
  const ajv = new Ajv({ allErrors: true, strict: false });
  if (!ajv.validate(schema, spec)) {
    throw new Error("invalid business spec: " + ajv.errorsText());
  }
  return spec as Spec;
}

function programs(ir: unknown): Program[] {
  return Array.isArray(ir) ? ir : [ir as Program];
}

function raw(expr?: Stmt["Expression"]): string {
  return (expr?.RawText ?? "").trim();
}

function summarizeAssigns(stmts: Stmt[] | undefined): string {
  return (stmts ?? [])
    .filter((s) => s.Kind === "assign")
    .map((s) => `${s.Target} = ${raw(s.Expression)}`)
    .join("; ");
}

function walk(stmts: Stmt[] | undefined, fn: (s: Stmt) => void): void {
  for (const s of stmts ?? []) {
    fn(s);
    walk(s.Then, fn);
    walk(s.Else, fn);
    walk(s.Body, fn);
  }
}

function entityName(table: string): string {
  const stem = table.replace(/s$/i, "");
  return stem.charAt(0).toUpperCase() + stem.slice(1).toLowerCase();
}

function tablesAndFields(text: string, tables: Set<string>, fields: Set<string>): void {
  const use = /\bUSE\s+(\w+)/gi;
  const from = /\bFROM\s+(\w+)/gi;
  const into = /\bINTO\s+(\w+)/gi;
  const upd = /\bUPDATE\s+(\w+)/gi;
  const repl = /\bREPLACE\s+(\w+)/gi;
  for (const re of [use, from, into, upd]) {
    for (const m of text.matchAll(re)) tables.add(m[1]);
  }
  for (const m of text.matchAll(repl)) fields.add(m[1]);
  const select = /\bSELECT\s+(.+?)\s+FROM\b/is.exec(text);
  if (select) {
    for (const col of select[1].split(",")) {
      const name = col.replace(/\bSUM\s*\(/i, "").replace(/[()]/g, "").trim().split(/\s+/).pop();
      if (name && /^[A-Za-z_]\w*$/.test(name)) fields.add(name);
    }
  }
  const kw = new Set(["SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "GROUP", "BY", "ORDER", "SUM", "REPLACE", "WITH", "USE", "AND", "OR", "NOT", "DESC", "ASC"]);
  for (const m of text.matchAll(/\b([A-Za-z_]\w*)\b/g)) {
    const w = m[1];
    if (kw.has(w.toUpperCase())) continue;
    if ([...tables].some((t) => t.toLowerCase() === w.toLowerCase())) continue;
    if (/^[tl][ncl]/i.test(w)) continue;
    fields.add(w);
  }
}

export function fromIr(ir: unknown): Spec {
  const progs = programs(ir);
  const source = progs[0]?.SourceName ?? "unknown";
  const tables = new Set<string>();
  const fields = new Set<string>();
  const rules: Spec["rules"] = [];
  const flows: Spec["flows"] = [];
  const queries: Spec["queries"] = [];
  let n = 1;
  const addRule = (routine: string, description: string) => {
    if (!description) return;
    rules.push({ id: `R${n++}`, description, routine });
  };

  for (const p of progs) {
    for (const r of p.Routines ?? []) {
      const top = r.Body ?? [];
      walk(top, (s) => {
        if (s.Kind === "if") {
          const cond = raw(s.Expression);
          const then = summarizeAssigns(s.Then);
          addRule(r.Name, then ? `When ${cond}, ${then}` : `When ${cond}`);
          const els = summarizeAssigns(s.Else);
          if (els) addRule(r.Name, `Otherwise ${els}`);
        }
        if (s.Kind === "scan") {
          const cond = raw(s.Expression);
          if (cond) addRule(r.Name, `Scan while ${cond}`);
          tablesAndFields(cond, tables, fields);
        }
        if (s.Kind === "assign") {
          const text = raw(s.Expression);
          if (/[*/]/.test(text) && (/\b100\b/.test(text) || /percent/i.test(text) || /result/i.test(s.Target ?? "")))
            addRule(r.Name, `${s.Target} = ${text}`);
        }
        const text = s.Kind === "sql" || s.Kind === "expression" ? raw(s.Expression) : "";
        if (s.Kind === "sql" || /^\s*(REPLACE|USE)\b/i.test(text)) {
          queries.push({ routine: r.Name, sql: text });
          tablesAndFields(text, tables, fields);
          if (/REPLACE/i.test(text)) addRule(r.Name, text);
        }
      });
      flows.push({
        name: r.Name,
        kind: r.Kind,
        parameters: r.Parameters ?? [],
        steps: top.map((s) => s.Kind),
      });
    }
  }

  const entities = [...tables].map((t) => ({
    name: entityName(t),
    fields: [...fields].sort(),
  }));

  return { source, entities, rules, flows, queries };
}

async function llmSpec(ir: unknown): Promise<Spec | null> {
  const prompt = readFileSync(join(here, "prompts", "extractor.v1.md"), "utf8");
  const user = `${prompt}\n\nIR:\n${JSON.stringify(ir, null, 2)}`;
  const text = await complete(user);
  if (!text) return null;
  const yaml = text.replace(/^```ya?ml\s*/i, "").replace(/```$/i, "").trim();
  return validateSpec(parseYaml(yaml));
}

async function complete(user: string): Promise<string | null> {
  const mode = (process.env.LEGACYBRIDGE_LLM ?? "").toLowerCase();
  if (!mode || mode === "off" || mode === "none") return null;
  const required = mode === "required" || mode === "auto" || mode === "1" || mode === "true";
  try {
    if (process.env.OPENAI_API_KEY && (mode === "openai" || required || mode === "auto")) {
      return await openai(user);
    }
    if (process.env.ANTHROPIC_API_KEY && (mode === "anthropic" || required || mode === "auto")) {
      return await anthropic(user);
    }
    if (mode === "ollama" || mode === "required" || mode === "auto") {
      return await ollama(user);
    }
  } catch (e) {
    if (mode === "required") throw e;
    console.error(`llm skipped: ${(e as Error).message}`);
  }
  return null;
}

async function openai(user: string): Promise<string> {
  const res = await fetch("https://api.openai.com/v1/chat/completions", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${process.env.OPENAI_API_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model: process.env.OPENAI_MODEL ?? "gpt-4o-mini",
      temperature: 0,
      messages: [{ role: "user", content: user }],
    }),
    signal: AbortSignal.timeout(60_000),
  });
  if (!res.ok) throw new Error(`openai ${res.status}`);
  const body = (await res.json()) as { choices: { message: { content: string } }[] };
  return body.choices[0].message.content;
}

async function anthropic(user: string): Promise<string> {
  const res = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "x-api-key": process.env.ANTHROPIC_API_KEY!,
      "anthropic-version": "2023-06-01",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model: process.env.ANTHROPIC_MODEL ?? "claude-3-haiku-20240307",
      max_tokens: 2048,
      temperature: 0,
      messages: [{ role: "user", content: user }],
    }),
    signal: AbortSignal.timeout(60_000),
  });
  if (!res.ok) throw new Error(`anthropic ${res.status}`);
  const body = (await res.json()) as { content: { text: string }[] };
  return body.content.map((c) => c.text).join("");
}

async function ollama(user: string): Promise<string> {
  const host = (process.env.OLLAMA_HOST ?? "http://127.0.0.1:11434").replace(/\/$/, "");
  const res = await fetch(`${host}/api/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: process.env.OLLAMA_MODEL ?? "llama3.2",
      stream: false,
      messages: [{ role: "user", content: user }],
    }),
    signal: AbortSignal.timeout(60_000),
  });
  if (!res.ok) throw new Error(`ollama ${res.status}`);
  const body = (await res.json()) as { message: { content: string } };
  return body.message.content;
}

export async function extract(ir: unknown, wantLlm: boolean): Promise<{ spec: Spec; via: "llm" | "ir" }> {
  if (wantLlm) {
    const spec = await llmSpec(ir);
    if (spec) return { spec, via: "llm" };
  }
  return { spec: validateSpec(fromIr(ir)), via: "ir" };
}

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(name);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1] ?? "")) {
  const irPath = arg("--ir");
  if (!irPath) {
    console.error("usage: extract --ir ir.json [--output spec.yaml] [--llm]");
    process.exit(2);
  }
  const ir = JSON.parse(readFileSync(irPath, "utf8"));
  const { spec, via } = await extract(ir, process.argv.includes("--llm") || !!process.env.LEGACYBRIDGE_LLM);
  const yaml = toYaml(spec);
  const out = arg("--output");
  if (out) {
    mkdirSync(dirname(out), { recursive: true });
    writeFileSync(out, yaml);
  }
  else process.stdout.write(yaml);
  console.error(`extractor: ${via}`);
}

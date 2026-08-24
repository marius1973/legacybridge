import { spawn } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export function repoRoot(start = dirname(fileURLToPath(import.meta.url))): string {
  let dir = start;
  while (true) {
    if (existsSync(join(dir, "LegacyBridge.sln"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) throw new Error("LegacyBridge.sln not found");
    dir = parent;
  }
}

export function dotnetBin(): string {
  const root = process.env.DOTNET_ROOT;
  if (root) {
    const exe = join(root, process.platform === "win32" ? "dotnet.exe" : "dotnet");
    if (existsSync(exe)) return exe;
  }
  if (process.platform === "win32") {
    const x64 = join(process.env["ProgramFiles"] || "C:\\Program Files", "dotnet", "dotnet.exe");
    if (existsSync(x64)) return x64;
  }
  return "dotnet";
}

export function runCli(args: string[], cwd = repoRoot()): Promise<{ code: number; stdout: string; stderr: string }> {
  const project = join(cwd, "src", "LegacyBridge.Cli", "LegacyBridge.Cli.csproj");
  return new Promise((ok, fail) => {
    const child = spawn(dotnetBin(), ["run", "--project", project, "--", ...args], {
      cwd,
      env: process.env,
    });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (d) => { stdout += d; });
    child.stderr.on("data", (d) => { stderr += d; });
    child.on("error", fail);
    child.on("close", (code) => ok({ code: code ?? 1, stdout, stderr }));
  });
}

export function summarizeIr(json: string): string {
  const parsed = JSON.parse(json) as unknown;
  const programs = Array.isArray(parsed) ? parsed : [parsed];
  const summary = programs.map((p: { SourceName?: string; Routines?: { Name: string; Kind: string; Parameters?: string[]; Body?: unknown[] }[] }) => ({
    source: p.SourceName,
    routines: (p.Routines ?? []).map((r) => ({
      name: r.Name,
      kind: r.Kind,
      parameters: r.Parameters ?? [],
      statements: r.Body?.length ?? 0,
    })),
  }));
  return JSON.stringify(summary, null, 2);
}

export async function analyzeLegacy(path: string, cwd = repoRoot()): Promise<string> {
  const dir = mkdtempSync(join(tmpdir(), "lb-analyze-"));
  const irPath = join(dir, "ir.json");
  const r = await runCli(["analyze", resolve(cwd, path), "--output", irPath], cwd);
  if (r.code !== 0) return fail(r);
  return `${r.stdout.trim()}\n\n${summarizeIr(readFileSync(irPath, "utf8"))}`;
}

export async function generateDotnet(path: string, output?: string, cwd = repoRoot()): Promise<string> {
  const out = resolve(cwd, output ?? join("generated", "mcp"));
  mkdirSync(out, { recursive: true });
  const r = await runCli(["generate", resolve(cwd, path), "--output", out, "--build"], cwd);
  if (r.code !== 0) return fail(r);
  const files = listGenerated(out);
  return `${r.stdout.trim()}\n\nfiles (${files.length}):\n${files.slice(0, 40).join("\n")}`;
}

export async function runEquivalence(path: string, cwd = repoRoot()): Promise<string> {
  const dir = mkdtempSync(join(tmpdir(), "lb-verify-"));
  const report = join(dir, "EQUIVALENCE-REPORT.md");
  const r = await runCli(["verify", resolve(cwd, path), "--output", report, "--min-match", "0.9"], cwd);
  const head = existsSync(report) ? readFileSync(report, "utf8").split(/\r?\n/).slice(0, 8).join("\n") : "";
  if (r.code !== 0) return fail(r, head);
  return `${r.stdout.trim()}\n\n${head}`;
}

function fail(r: { stdout: string; stderr: string }, extra = ""): string {
  return ["CLI failed", r.stdout.trim(), r.stderr.trim(), extra].filter(Boolean).join("\n");
}

function listGenerated(root: string, acc: string[] = [], rel = ""): string[] {
  const here = join(root, rel);
  for (const name of readdirSync(here)) {
    if (name === "bin" || name === "obj") continue;
    const next = rel ? `${rel}/${name}` : name;
    if (statSync(join(here, name)).isDirectory()) listGenerated(root, acc, next);
    else acc.push(next);
  }
  return acc;
}

import { spawn } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";

export function repoRoot(): string {
  if (process.env.REPO_ROOT) return process.env.REPO_ROOT;
  let dir = process.cwd();
  while (true) {
    if (existsSync(join(dir, "LegacyBridge.sln"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) throw new Error("LegacyBridge.sln not found");
    dir = parent;
  }
}

function dotnetBin(): string {
  const win = join("C:", "Program Files", "dotnet", "dotnet.exe");
  return existsSync(win) ? win : "dotnet";
}

export function runCli(args: string[], cwd = repoRoot()): Promise<{ code: number; stdout: string; stderr: string }> {
  const project = join(cwd, "src", "LegacyBridge.Cli", "LegacyBridge.Cli.csproj");
  return new Promise((ok, fail) => {
    const child = spawn(dotnetBin(), ["run", "--project", project, "--", ...args], { cwd, env: process.env });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (d) => { stdout += String(d); });
    child.stderr.on("data", (d) => { stderr += String(d); });
    child.on("error", fail);
    child.on("close", (code) => ok({ code: code ?? 1, stdout, stderr }));
  });
}

export type EqRow = { id: string; routine: string; args: string; oracle: string; migrated: string; result: string };

export function parseReport(md: string): { rate: string; skipped: string; rows: EqRow[] } {
  const rate = md.match(/\*\*Match rate:\*\*\s*([0-9.]+%)/)?.[1] ?? "?";
  const skipped = md.match(/skipped\s+(\d+)/i)?.[1] ?? "0";
  const rows: EqRow[] = [];
  for (const line of md.split(/\r?\n/)) {
    if (!line.startsWith("| `")) continue;
    const cols = line.split("|").map((c) => c.trim()).filter(Boolean);
    if (cols.length < 6) continue;
    rows.push({
      id: cols[0].replace(/`/g, ""),
      routine: cols[1],
      args: cols[2],
      oracle: cols[3],
      migrated: cols[4],
      result: cols[5],
    });
  }
  return { rate, skipped, rows };
}

export function sampleReport(cwd = repoRoot()): string {
  return readFileSync(join(cwd, "samples", "vfp-inventory", "EQUIVALENCE-REPORT.md"), "utf8");
}

export async function runPipeline(
  onStep: (step: string, status: "running" | "ok" | "fail", detail?: string) => void,
  sourcePath?: string,
): Promise<{ ok: boolean; report: string }> {
  const cwd = repoRoot();
  const path = sourcePath ?? join(cwd, "samples", "vfp-inventory", "legacy");
  const work = mkdtempSync(join(tmpdir(), "lb-dash-"));
  const steps: [string, string[]][] = [
    ["analyze", ["analyze", path, "--output", join(work, "ir.json")]],
    ["extract", ["extract", path, "--output", join(work, "spec.yaml")]],
    ["generate", ["generate", path, "--output", join(work, "migrated"), "--build"]],
    ["verify", ["verify", path, "--output", join(work, "EQUIVALENCE-REPORT.md"), "--min-match", "0.9"]],
  ];
  for (const [name, args] of steps) {
    onStep(name, "running");
    const r = await runCli(args, cwd);
    if (r.code !== 0) {
      onStep(name, "fail", (r.stderr || r.stdout).slice(-1500));
      return { ok: false, report: r.stderr || r.stdout };
    }
    onStep(name, "ok", r.stdout.trim().split(/\r?\n/).pop());
  }
  return { ok: true, report: readFileSync(join(work, "EQUIVALENCE-REPORT.md"), "utf8") };
}

export function saveUpload(name: string, bytes: Buffer): string {
  const dir = mkdtempSync(join(tmpdir(), "lb-up-"));
  const dest = join(dir, name.replace(/[^\w.-]/g, "_") || "upload.prg");
  writeFileSync(dest, bytes);
  return dest;
}

"use client";

import { useEffect, useState } from "react";

type Row = { id: string; routine: string; args: string; oracle: string; migrated: string; result: string };
type Sample = { rate: string; skipped: string; rows: Row[] };
type Step = { id: string; label: string; status: string; detail?: string };

const STEPS: { id: string; label: string }[] = [
  { id: "analyze", label: "Parser → IR" },
  { id: "extract", label: "Business spec" },
  { id: "generate", label: ".NET generate" },
  { id: "verify", label: "Equivalence" },
];

export default function Page() {
  const [sample, setSample] = useState<Sample | null>(null);
  const [steps, setSteps] = useState<Step[]>(STEPS.map((s) => ({ ...s, status: "idle" })));
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");
  const [file, setFile] = useState<File | null>(null);

  useEffect(() => {
    fetch("/api/sample")
      .then((r) => r.json())
      .then(setSample)
      .catch(() => setErr("could not load committed report"));
  }, []);

  async function run() {
    setBusy(true);
    setErr("");
    setSample(null);
    setSteps(STEPS.map((s) => ({ ...s, status: "idle" })));
    const body = new FormData();
    if (file) body.append("file", file);
    const res = await fetch("/api/run", { method: "POST", body });
    if (!res.body) { setBusy(false); setErr("no stream"); return; }
    const reader = res.body.getReader();
    const dec = new TextDecoder();
    let buf = "";
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buf += dec.decode(value, { stream: true });
      const parts = buf.split("\n\n");
      buf = parts.pop() ?? "";
      for (const block of parts) {
        const line = block.replace(/^data:\s*/, "");
        if (!line) continue;
        const ev = JSON.parse(line) as { step?: string; status?: string; detail?: string; report?: Sample; error?: string };
        if (ev.error) setErr(ev.error);
        if (ev.report) setSample(ev.report);
        if (ev.step && ev.status) {
          setSteps((prev) => prev.map((s) => (s.id === ev.step ? { ...s, status: ev.status!, detail: ev.detail } : s)));
        }
      }
    }
    setBusy(false);
  }

  return (
    <main>
      <h1>LegacyBridge</h1>
      <p className="sub">VFP → .NET 8. Same cases on the IR oracle and the generated code.</p>
      <div className="row">
        <button type="button" disabled={busy} onClick={run}>
          {busy ? "Running…" : file ? `Migrate ${file.name}` : "Run bundled sample"}
        </button>
        <label className="file">
          <input type="file" accept=".prg,.txt" hidden onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          {file ? file.name : "or upload .prg"}
        </label>
      </div>
      <div className="steps">
        {steps.map((s) => (
          <div className="step" key={s.id}>
            <b>{s.label}</b>
            <span className={s.status === "ok" ? "ok" : s.status === "fail" ? "fail" : ""}>
              {s.status === "idle" ? "—" : s.status}
            </span>
          </div>
        ))}
      </div>
      {sample && (
        <>
          <div className="rate ok">{sample.rate} match</div>
          <p className="sub">{sample.rows.length} cases · {sample.skipped} skipped</p>
          <div className="wrap">
            <table>
              <thead>
                <tr>
                  <th>Case</th>
                  <th>Routine</th>
                  <th>Oracle</th>
                  <th>Migrated</th>
                  <th>Result</th>
                </tr>
              </thead>
              <tbody>
                {sample.rows.map((r) => (
                  <tr key={r.id}>
                    <td>{r.id}</td>
                    <td>{r.routine}</td>
                    <td>{r.oracle}</td>
                    <td>{r.migrated}</td>
                    <td className={r.result === "match" ? "ok" : r.result.startsWith("skip") ? "skip" : "fail"}>
                      {r.result}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      {err && <pre>{err}</pre>}
    </main>
  );
}

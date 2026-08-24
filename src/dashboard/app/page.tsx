"use client";

import { useEffect, useState } from "react";
import { copy, formatResult, readLang, writeLang, type Lang } from "@/lib/i18n";

type Row = { id: string; routine: string; args: string; oracle: string; migrated: string; result: string };
type Sample = { rate: string; skipped: string; rows: Row[] };
type Step = { id: keyof typeof copy.es.steps; status: string; detail?: string };

const STEP_IDS: Step["id"][] = ["analyze", "extract", "generate", "verify"];

export default function Page() {
  const [lang, setLang] = useState<Lang>("es");
  const [sample, setSample] = useState<Sample | null>(null);
  const [steps, setSteps] = useState<Step[]>(STEP_IDS.map((id) => ({ id, status: "idle" })));
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const t = copy[lang];

  useEffect(() => {
    const next = readLang();
    setLang(next);
    document.documentElement.lang = next;
  }, []);

  useEffect(() => {
    fetch("/api/sample")
      .then((r) => r.json())
      .then(setSample)
      .catch(() => setErr("load"));
  }, []);

  function pickLang(next: Lang) {
    setLang(next);
    writeLang(next);
  }

  async function run() {
    setBusy(true);
    setErr("");
    setSample(null);
    setSteps(STEP_IDS.map((id) => ({ id, status: "idle" })));
    const body = new FormData();
    if (file) body.append("file", file);
    const res = await fetch("/api/run", { method: "POST", body });
    if (!res.body) { setBusy(false); setErr(t.noStream); return; }
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
      <div className="head">
        <div>
          <h1>LegacyBridge</h1>
          <p className="sub">{t.sub}</p>
        </div>
        <div className="lang" role="group" aria-label={t.lang}>
          <button type="button" className={lang === "es" ? "on" : ""} onClick={() => pickLang("es")}>ES</button>
          <button type="button" className={lang === "en" ? "on" : ""} onClick={() => pickLang("en")}>EN</button>
        </div>
      </div>
      <div className="row">
        <button type="button" disabled={busy} onClick={run}>
          {busy ? t.running : file ? `${t.migrate} ${file.name}` : t.run}
        </button>
        <label className="file">
          <input type="file" accept=".prg,.sru,.srd,.txt" hidden onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          {file ? file.name : t.upload}
        </label>
      </div>
      <div className="steps">
        {steps.map((s) => (
          <div className="step" key={s.id}>
            <b>{t.steps[s.id]}</b>
            <span className={s.status === "ok" ? "ok" : s.status === "fail" ? "fail" : ""}>
              {t.status[s.status as keyof typeof t.status] ?? s.status}
            </span>
          </div>
        ))}
      </div>
      {sample && (
        <>
          <div className="rate ok">{sample.rate} {t.match}</div>
          <p className="sub">{sample.rows.length} {t.cases} · {sample.skipped} {t.skipped}</p>
          <div className="wrap">
            <table>
              <thead>
                <tr>
                  <th>{t.case}</th>
                  <th>{t.routine}</th>
                  <th>{t.oracle}</th>
                  <th>{t.migrated}</th>
                  <th>{t.result}</th>
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
                      {formatResult(r.result, t)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      {err && <pre>{err === "load" ? t.loadErr : err}</pre>}
    </main>
  );
}

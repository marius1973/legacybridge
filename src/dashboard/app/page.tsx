"use client";

import { useEffect, useState } from "react";
import { copy, formatResult, readLang, writeLang, type Lang } from "@/lib/i18n";

type Row = { id: string; routine: string; args: string; oracle: string; migrated: string; result: string };
type Sample = { rate: string; skipped: string; rows: Row[] };
type Artifacts = { ir: string; spec: string; files: { path: string; content: string }[]; report: string };
type Tab = "code" | "ir" | "spec" | "report";
type Step = { id: keyof typeof copy.es.steps; status: string; detail?: string };

const STEP_IDS: Step["id"][] = ["analyze", "extract", "generate", "verify"];
const HERO = [
  { value: "100%", label: "heroMatch" },
  { value: "148/148", label: "heroCases" },
  { value: "99%", label: "heroCover" },
] as const;
const GH = "https://github.com/marius1973/legacybridge";

function codeLines(src: string) {
  return src.split("\n").map((line, i) => (
    <span key={i} className={/^\s*\*/.test(line) ? "cmt" : undefined}>
      {line}
      {"\n"}
    </span>
  ));
}

export default function Page() {
  const [lang, setLang] = useState<Lang>("es");
  const [sample, setSample] = useState<Sample | null>(null);
  const [source, setSource] = useState("");
  const [sourceName, setSourceName] = useState("inv_calc.prg");
  const [steps, setSteps] = useState<Step[]>(STEP_IDS.map((id) => ({ id, status: "idle" })));
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [artifacts, setArtifacts] = useState<Artifacts | null>(null);
  const [tab, setTab] = useState<Tab>("code");
  const [activeFile, setActiveFile] = useState(0);
  const [dl, setDl] = useState(false);
  const t = copy[lang];

  useEffect(() => {
    const next = readLang();
    setLang(next);
    document.documentElement.lang = next;
  }, []);

  useEffect(() => {
    fetch("/api/sample")
      .then((r) => {
        if (!r.ok) throw new Error(String(r.status));
        return r.json();
      })
      .then((j: Sample & { source?: string; sourceName?: string; artifacts?: Artifacts }) => {
        setSample({ rate: j.rate, skipped: j.skipped, rows: j.rows });
        if (j.source) setSource(j.source);
        if (j.sourceName) setSourceName(j.sourceName);
        if (j.artifacts) setArtifacts(j.artifacts);
      })
      .catch(() => setErr("load"))
      .finally(() => setLoading(false));
  }, []);

  function pickLang(next: Lang) {
    setLang(next);
    writeLang(next);
  }

  async function downloadNet() {
    if (!file) {
      window.location.assign("/api/download");
      return;
    }
    setDl(true);
    setErr("");
    try {
      const body = new FormData();
      body.append("file", file);
      const res = await fetch("/api/download", { method: "POST", body });
      if (!res.ok) {
        setErr((await res.text()).slice(-1500) || t.runErr);
        return;
      }
      const blob = await res.blob();
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = "VfpInventory.zip";
      a.click();
      URL.revokeObjectURL(a.href);
    } catch {
      setErr(t.networkErr);
    } finally {
      setDl(false);
    }
  }

  async function run() {
    setBusy(true);
    setErr("");
    setSteps(STEP_IDS.map((id) => ({ id, status: "idle" })));
    const body = new FormData();
    if (file) body.append("file", file);
    try {
      const res = await fetch("/api/run", { method: "POST", body });
      if (!res.ok) {
        setErr(`${t.runErr} HTTP ${res.status}`);
        return;
      }
      if (!res.body) {
        setErr(t.noStream);
        return;
      }
      const reader = res.body.getReader();
      const dec = new TextDecoder();
      let buf = "";
      let failDetail = "";
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buf += dec.decode(value, { stream: true });
        const parts = buf.split("\n\n");
        buf = parts.pop() ?? "";
        for (const block of parts) {
          const line = block.replace(/^data:\s*/, "");
          if (!line) continue;
          const ev = JSON.parse(line) as { step?: string; status?: string; detail?: string; report?: Sample; artifacts?: Artifacts; error?: string };
          if (ev.error) {
            failDetail = ev.error;
            setErr(ev.error);
          }
          if (ev.report) setSample(ev.report);
          if (ev.artifacts) {
            setArtifacts(ev.artifacts);
            setTab("code");
            setActiveFile(0);
          }
          if (ev.step && ev.status) {
            if (ev.status === "fail") failDetail = ev.detail || ev.step;
            setSteps((prev) => prev.map((s) => (s.id === ev.step ? { ...s, status: ev.status!, detail: ev.detail } : s)));
          }
        }
      }
      if (failDetail && !err) setErr(failDetail);
    } catch {
      setErr(t.networkErr);
    } finally {
      setBusy(false);
    }
  }

  return (
    <main>
      <div className="head">
        <div>
          <h1>LegacyBridge</h1>
          <p className="sub">{t.sub}</p>
        </div>
        <div className="tools">
          <div className="lang" role="group" aria-label={t.lang}>
            <button type="button" className={lang === "es" ? "on" : ""} onClick={() => pickLang("es")}>ES</button>
            <button type="button" className={lang === "en" ? "on" : ""} onClick={() => pickLang("en")}>EN</button>
          </div>
          <a className="gh" href={GH} target="_blank" rel="noreferrer" aria-label={t.github} title={t.github}>
            <svg viewBox="0 0 16 16" width="18" height="18" aria-hidden="true">
              <path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8" />
            </svg>
          </a>
        </div>
      </div>

      <div className="metrics" aria-label={t.heroMatch}>
        {HERO.map((m) => (
          <div className="metric" key={m.label}>
            <b>{m.value}</b>
            <span>{t[m.label]}</span>
          </div>
        ))}
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

      {source && (
        <details className="legacy">
          <summary>{t.legacyCode} ({sourceName})</summary>
          <pre className="code">{codeLines(source)}</pre>
        </details>
      )}

      <div className="steps" aria-live="polite">
        {steps.map((s) => (
          <div className="step" key={s.id}>
            <b>{t.steps[s.id]}</b>
            <span className={s.status === "ok" ? "ok" : s.status === "fail" ? "fail" : s.status === "running" ? "run" : ""}>
              {t.status[s.status as keyof typeof t.status] ?? s.status}
            </span>
            {s.detail && <small className="detail">{s.detail}</small>}
          </div>
        ))}
      </div>

      {loading && (
        <div className="skel" aria-hidden="true">
          <div /><div /><div />
        </div>
      )}

      {sample && (
        <>
          <div className="rate ok">{sample.rate} {t.match}</div>
          <p className="sub">{sample.rows.length} {t.cases} · {sample.skipped} {t.skipped}</p>
          <p className="note">{t.idsNote}</p>
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
                {sample.rows.map((r) => {
                  const kind = r.result === "match" ? "ok" : r.result.startsWith("skip") ? "skip" : "fail";
                  const cell = r.result === "match" ? "match-cell" : r.result === "MISMATCH" ? "miss-cell" : "";
                  return (
                    <tr key={r.id} className={kind === "ok" ? "row-ok" : ""}>
                      <td data-label={t.case}>{r.id}</td>
                      <td data-label={t.routine}>{r.routine}</td>
                      <td data-label={t.oracle} className={cell}>{r.oracle}</td>
                      <td data-label={t.migrated} className={cell}>{r.migrated}</td>
                      <td data-label={t.result} className={kind}>{formatResult(r.result, t)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
      {artifacts && (
        <section className="artifacts">
          <h2>{t.artifacts}</h2>
          <div className="tabs">
            {(["code", "ir", "spec", "report"] as Tab[]).map((id) => (
              <button key={id} type="button" className={tab === id ? "on" : ""} onClick={() => setTab(id)}>
                {id === "code" ? t.tabCode : id === "ir" ? t.tabIr : id === "spec" ? t.tabSpec : t.tabReport}
              </button>
            ))}
            <button type="button" className="zip" disabled={dl || busy} onClick={() => downloadNet()}>
              {dl ? t.downloading : t.downloadZip}
            </button>
          </div>
          {tab === "code" && (
            <div className="codeview">
              <aside>
                {artifacts.files.map((f, i) => (
                  <button key={f.path} type="button" className={i === activeFile ? "on" : ""} onClick={() => setActiveFile(i)}>
                    {f.path}
                  </button>
                ))}
              </aside>
              <pre className="code"><code>{artifacts.files[activeFile]?.content}</code></pre>
            </div>
          )}
          {tab === "ir" && <pre className="code">{artifacts.ir ? artifacts.ir.slice(0, 20000) : t.irPending}</pre>}
          {tab === "spec" && <pre className="code">{artifacts.spec}</pre>}
          {tab === "report" && <pre className="code">{artifacts.report}</pre>}
        </section>
      )}
      {err && <pre>{err === "load" ? t.loadErr : err}</pre>}
    </main>
  );
}

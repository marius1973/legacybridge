export type Lang = "es" | "en";

const KEY = "lb-lang";

export const copy = {
  es: {
    sub: "VFP / PowerBuilder → .NET 8. Los mismos casos en el oráculo IR y en el código generado.",
    run: "Ejecutar sample incluido",
    migrate: "Migrar",
    running: "Ejecutando…",
    upload: "o subir .prg / .sru",
    loadErr: "no se pudo cargar el reporte",
    noStream: "sin stream",
    match: "acierto",
    cases: "casos",
    skipped: "omitidos",
    case: "Caso",
    routine: "Rutina",
    oracle: "Oráculo",
    migrated: "Migrado",
    result: "Resultado",
    lang: "Idioma",
    steps: {
      analyze: "Parser → IR",
      extract: "Spec de negocio",
      generate: "Generar .NET",
      verify: "Equivalencia",
    },
    status: { idle: "—", running: "en curso", ok: "ok", fail: "error" },
    resultMatch: "acierto",
    resultMismatch: "DESAJUSTE",
    skip: "omitido",
  },
  en: {
    sub: "VFP / PowerBuilder → .NET 8. Same cases on the IR oracle and the generated code.",
    run: "Run bundled sample",
    migrate: "Migrate",
    running: "Running…",
    upload: "or upload .prg / .sru",
    loadErr: "could not load committed report",
    noStream: "no stream",
    match: "match",
    cases: "cases",
    skipped: "skipped",
    case: "Case",
    routine: "Routine",
    oracle: "Oracle",
    migrated: "Migrated",
    result: "Result",
    lang: "Language",
    steps: {
      analyze: "Parser → IR",
      extract: "Business spec",
      generate: ".NET generate",
      verify: "Equivalence",
    },
    status: { idle: "—", running: "running", ok: "ok", fail: "fail" },
    resultMatch: "match",
    resultMismatch: "MISMATCH",
    skip: "skip",
  },
} as const;

export type Copy = (typeof copy)[Lang];

export function readLang(): Lang {
  if (typeof window === "undefined") return "es";
  const saved = localStorage.getItem(KEY);
  if (saved === "es" || saved === "en") return saved;
  return navigator.language.toLowerCase().startsWith("es") ? "es" : "en";
}

export function writeLang(lang: Lang) {
  localStorage.setItem(KEY, lang);
  document.documentElement.lang = lang;
}

export function formatResult(raw: string, t: Copy): string {
  if (raw === "match") return t.resultMatch;
  if (raw === "MISMATCH") return t.resultMismatch;
  if (raw.startsWith("skip")) return t.skip + raw.slice(4);
  return raw;
}

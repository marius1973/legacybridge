export type Lang = "es" | "en";

const KEY = "lb-lang";

export const copy = {
  es: {
    sub: "VFP / PowerBuilder → .NET 8. Los mismos casos en el IR y en el código generado.",
    run: "Ejecutar sample incluido",
    migrate: "Migrar",
    running: "Ejecutando…",
    upload: "o subir .prg / .sru",
    loadErr: "no se pudo cargar el reporte",
    noStream: "sin stream — reintenta",
    runErr: "Falló la ejecución. Mira el paso en rojo o reintenta.",
    networkErr: "No hay conexión con el servidor. Reintenta.",
    match: "acierto",
    cases: "casos",
    skipped: "omitidos",
    case: "Caso",
    routine: "Rutina",
    oracle: "Valor esperado",
    migrated: "Migrado",
    result: "Resultado",
    lang: "Idioma",
    github: "Código en GitHub",
    heroMatch: "acierto",
    heroCases: "casos",
    heroCover: "cobertura",
    legacyCode: "Ver código legacy de ejemplo",
    idsNote: "Los IDs de casos y rutinas quedan en inglés: son código.",
    artifacts: "Artefactos generados",
    tabCode: "Código .NET",
    tabIr: "IR",
    tabSpec: "Spec",
    tabReport: "Reporte",
    downloadZip: "Descargar solución .NET",
    downloading: "Preparando .zip…",
    irPending: "Ejecuta el sample para ver el IR JSON del parser.",
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
    sub: "VFP / PowerBuilder → .NET 8. Same cases on the IR expected values and the generated code.",
    run: "Run bundled sample",
    migrate: "Migrate",
    running: "Running…",
    upload: "or upload .prg / .sru",
    loadErr: "could not load committed report",
    noStream: "no stream — retry",
    runErr: "Run failed. Check the red step or retry.",
    networkErr: "No connection to the server. Retry.",
    match: "match",
    cases: "cases",
    skipped: "skipped",
    case: "Case",
    routine: "Routine",
    oracle: "Expected",
    migrated: "Migrated",
    result: "Result",
    lang: "Language",
    github: "GitHub repo",
    heroMatch: "match",
    heroCases: "cases",
    heroCover: "coverage",
    legacyCode: "View sample legacy code",
    idsNote: "Case IDs and routine names stay in English — they are code.",
    artifacts: "Generated artifacts",
    tabCode: ".NET code",
    tabIr: "IR",
    tabSpec: "Spec",
    tabReport: "Report",
    downloadZip: "Download .NET solution",
    downloading: "Preparing .zip…",
    irPending: "Run the sample to see the parser IR JSON.",
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

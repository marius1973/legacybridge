# LegacyBridge — Runbook de Publicación v0.1

> Objetivo: publicar el repo en GitHub de forma que cause la mejor primera
> impresión posible a un reclutador que llegue desde mariomanrique.dev.
> Tiempo total estimado: **~45 minutos**.

---

## Pre-flight: correcciones antes del push (5 min)

**1. Corregir las URLs del README** (apuntan al usuario equivocado):

En `README.md`, reemplazar `mariomanrique` → `marius1973` en estas 2 líneas:

```diff
- ![CI](https://github.com/mariomanrique/legacybridge/actions/workflows/ci.yml/badge.svg)
+ ![CI](https://github.com/marius1973/LegacyBridge/actions/workflows/ci.yml/badge.svg)

- git clone https://github.com/mariomanrique/legacybridge.git
+ git clone https://github.com/marius1973/LegacyBridge.git
```

**2. Verificación local final** (en tu máquina Windows):

```powershell
cd D:\Proyectos\Github\Agent_LegacyBridge\legacybridge
dotnet build
dotnet test
# Debe mostrar: Passed! - Failed: 0, Passed: 13
```

---

## Paso 1 — Push inicial (5 min)

```powershell
cd D:\Proyectos\Github\Agent_LegacyBridge\legacybridge

# Si la carpeta aún no es repo git:
git init -b main
git remote add origin https://github.com/marius1973/LegacyBridge.git

git add .
git commit -m "v0.1: VFP lexer/parser → IR, CLI analyze, CI, 13 tests"
git push -u origin main
```

Si el repo remoto ya tiene contenido (un README autogenerado al crearlo):

```powershell
git pull origin main --rebase --allow-unrelated-histories
git push -u origin main
```

---

## Paso 2 — Verificar el CI (5 min)

1. Ir a `https://github.com/marius1973/LegacyBridge/actions`
2. El workflow **CI** debe aparecer corriendo automáticamente tras el push.
3. Esperar a que termine en verde ✅. Si falla, el log dirá exactamente qué paso
   (los más probables: versión del SDK o el smoke test — ambos ya probados localmente).
4. Una vez verde, el **badge del README se actualiza solo** en la próxima carga.

---

## Paso 3 — Configurar el repo para reclutadores (10 min)

En `https://github.com/marius1973/LegacyBridge` → ⚙️ (About, arriba a la derecha):

**Description:**
```
AI-agent pipeline that migrates Visual FoxPro / PowerBuilder to .NET 8 (DDD) —
with automatically generated functional-equivalence tests. CLI + MCP server.
```

**Website:** `https://www.mariomanrique.dev`

**Topics** (copiar tal cual — son los términos que buscan reclutadores y recruiters técnicos):
```
dotnet  csharp  legacy-migration  visual-foxpro  powerbuilder
ai-agents  llm  mcp-server  clean-architecture  ddd  code-migration
```

**Settings → General:**
- ✅ Activar **Issues** (para los `good-first-issue` del Sprint 6)
- ❌ Desactivar Wiki y Projects si no los usarás (menos ruido visual)
- Mantener el repo **público** (es tu pieza de marketing técnico)

---

## Paso 4 — Release v0.1 (5 min)

```powershell
git tag -a v0.1 -m "v0.1: VFP lexer/parser → IR, CLI analyze, CI + 13 tests"
git push origin v0.1
```

Luego en GitHub → **Releases → Draft a new release**:
- Tag: `v0.1`
- Title: `v0.1 — Parser foundation`
- Descripción (copiar):

```markdown
First public milestone: the deterministic foundation of the pipeline.

- Hand-written Visual FoxPro lexer + recursive-descent parser
- Unified IR (JSON) — the artifact all AI agents will consume
- CLI: `legacybridge analyze <path> --output ir.json`
- 13 unit tests, GitHub Actions CI with smoke test on a real VFP sample
- Sample: legacy inventory system (`samples/vfp-inventory`)

Next: v0.2 — typed expression AST + ≥80% coverage.
```

---

## Paso 5 — Conectar con tu presencia (10 min)

**En GitHub:**
- Ir a tu perfil → **Customize your pins** → pinea `LegacyBridge` en primer lugar

**En mariomanrique.dev:**
- En la sección Portafolio, agregar/actualizar la tarjeta de LegacyBridge:

```
LegacyBridge
Pipeline open source de agentes IA que migra VFP/PowerBuilder a .NET 8 (DDD),
con tests de equivalencia funcional generados automáticamente. CLI + MCP server.
.NET 8 · C# · TypeScript · LLM Agents · MCP
Ver en GitHub →
```

**En LinkedIn** (post corto, en inglés — copiar y ajustar):

```
After 15+ years migrating legacy systems in production (VFP, PowerBuilder,
finance & mining), I got tired of one thing: nobody could prove the migrated
system behaved like the old one.

So I'm building LegacyBridge in public: an AI-agent pipeline that parses
legacy code into a typed IR, generates .NET 8 / DDD code, and — the key part —
proves functional equivalence with automatically generated tests.

v0.1 is out: the VFP lexer/parser → IR, with CI and a real sample.
Building in public, one release per week.

🔗 https://github.com/marius1973/LegacyBridge
#dotnet #legacy #ai #opensource
```

---

## Calendario de publicación (alineado al plan de desarrollo)

| Cuándo | Acción |
|---|---|
| **Hoy** | Pasos 1–5 de este runbook → v0.1 publicada + post LinkedIn |
| Semana 2 (fin S1) | `git tag v0.2` + release notes — AST de expresiones, badge cobertura real |
| Semana 4 (fin S2) | `git tag v0.2.1` + comentario en LinkedIn sobre el eval de extracción |
| Semana 6 (fin S3) | `git tag v0.3` + **post grande**: antes/después VFP → .NET 8 compilando |
| Semana 8 (fin S4) | `git tag v0.4` + post del equivalence report (el diferenciador) |
| Semana 9 (S5) | `git tag v0.5` + GIF de Claude Code usando el MCP server |
| Semana 11 (S6) | `git tag v1.0.0` + Show HN / Dev.to + actualización de mariomanrique.dev |

**Regla de oro:** cada release tiene notas escritas en inglés con el formato
"qué se logró → métricas → qué sigue". Un historial de releases así vale más
que cualquier línea del CV.

---

## Checklist final de la v0.1

- [ ] URLs del README corregidas a `marius1973/LegacyBridge`
- [ ] `dotnet test` en verde local (13/13)
- [ ] Push a `main` completado
- [ ] Workflow CI verde en GitHub Actions
- [ ] Badge de CI visible en el README
- [ ] Description, website y 11 topics configurados
- [ ] Release v0.1 creada con notas
- [ ] Repo pineado en el perfil
- [ ] Tarjeta en mariomanrique.dev actualizada
- [ ] Post de LinkedIn publicado

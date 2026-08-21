# LegacyBridge — Plan de Desarrollo v0.2 → v1.0

> Punto de partida: **v0.1 completada** — lexer/parser VFP → IR (JSON), CLI `analyze`,
> 13 tests en verde, CI con GitHub Actions, sample `vfp-inventory`.
>
> Ritmo asumido: **8–10 h/semana** (compatible con trabajo/proyectos actuales).
> Duración total: **10 semanas** + buffer.
> Cada semana termina con algo **commiteable y demostrable** — nunca trabajo invisible.

---

## Resumen ejecutivo

| Sprint | Semanas | Entregable | Versión |
|---|---|---|---|
| S1 · Parser pro | 1–2 | AST de expresiones + cobertura ≥80% | v0.2 |
| S2 · Agente extractor | 3–4 | Business Spec YAML desde IR | v0.2.1 |
| S3 · Generador .NET | 5–6 | Solución .NET 8 DDD que compila | v0.3 |
| S4 · Equivalencia + evals | 7–8 | Reporte de equivalencia en CI | v0.4 |
| S5 · MCP server | 9 | Herramientas expuestas a agentes | v0.5 |
| S6 · Dashboard + lanzamiento | 10–11 | Dashboard, demo.gif, v1.0 + post | v1.0 |

---

## Sprint 1 — Parser a nivel profesional (Semanas 1–2) → `v0.2`

**Objetivo:** convertir el parser de "funciona" a "confiable". Es la base determinista
de todo el pipeline; cada bug aquí se multiplica en los agentes.

### Semana 1 — AST de expresiones

Actualmente las expresiones se capturan como texto crudo (`"Expression": "tnQty * tnUnitCost"`).
El generador .NET (Sprint 3) necesita un árbol tipado.

**Tareas:**
1. Definir `IrExpression` (sealed hierarchy):
   - `LiteralExpr(number|string|bool)`
   - `IdentifierExpr(name)`
   - `BinaryExpr(op, left, right)` — aritmética, comparación, `.AND./.OR.`
   - `UnaryExpr(.NOT., -)`
   - `CallExpr(name, args)` — `ROUND(x, 2)`, `EOF()`, `SUM(...)`
2. Implementar `ExpressionParser` (Pratt / precedence climbing) sobre la lista de tokens
   de cada expresión. Precedencia: `OR < AND < NOT < comparación < suma < multiplicación < unario < llamada/átomo`.
3. Integrar: `IrStatement.Expression` pasa de `string` a `IrExpression` (mantener
   `RawText` como campo para trazabilidad y debugging).
4. Migrar los 7 tests de parser existentes + agregar 8–10 tests de expresiones
   (precedencia, paréntesis, llamadas anidadas, operadores `<>`, `!=`, `#`).

**Criterio de aceptación:** `inv_calc.prg` parsea con AST completo; `git tag v0.2-preview`.
**Esfuerzo:** ~9 h. **Riesgo:** precedencia de `.AND./.OR.` en VFP — validar contra
documentación y contra comportamiento real si tienes acceso a un entorno VFP.

### Semana 2 — Cobertura y robustez

**Tareas:**
1. Subir cobertura del parser a **≥80%** (reporte con coverlet en CI, badge real en README).
2. Casos edge: expresiones entre paréntesis anidados, strings con escapes,
   números negativos, `LOCAL m.x, m.y` (sintaxis `m.`), rutinas sin LPARAMETERS.
3. Modo `--strict`: falla ante construcciones desconocidas vs. modo default que
   degrada a `expression` con texto crudo (comportamiento actual).
4. Publicar el reporte de cobertura como artifact en CI.

**Criterio de aceptación:** badge de cobertura real (no decorativo), CI verde, `git tag v0.2`.
**Esfuerzo:** ~8 h.

> **Momento de visibilidad nº 1:** commit/tag público. Un reclutador que mire el repo
> ve actividad reciente y un historial de releases, no un repo abandonado.

---

## Sprint 2 — Agente 1: Business Spec Extractor (Semanas 3–4) → `v0.2.1`

**Objetivo:** primer agente LLM del pipeline. Del IR JSON a una especificación de
negocio en YAML (entidades, reglas, flujos, queries).

### Semana 3 — Estructura del spec + scaffolding de agentes

**Tareas:**
1. Definir el schema `business-spec.yaml` (validado con JSON Schema):
   ```yaml
   source: inv_calc.prg
   entities:
     - name: Product
       fields: [stock, unit_cost, total_value]
   rules:
     - id: R1
       description: "Stock de alto valor (>10000) lleva recargo de seguro del 2%"
       routine: CalcStockValue
   flows: [...]
   queries: [...]
   ```
2. Crear `src/agents/` (TypeScript + Node 20): CLI del orquestador, config de
   proveedor LLM (OpenAI / Anthropic / **Ollama local** — documentar costo $0 con local).
3. Prompt del extractor **versionado en el repo** (`src/agents/prompts/extractor.v1.md`)
   con few-shot examples tomados del sample. Los prompts en el repo son señal de seriedad.

**Criterio de aceptación:** el agente corre sobre el IR del sample y produce YAML válido contra el schema.
**Esfuerzo:** ~9 h.

### Semana 4 — Calidad del extractor

**Tareas:**
1. Golden dataset manual: escribir a mano el `business-spec.expected.yaml` correcto
   para `inv_calc.prg` (tú conoces el dominio — 1–2 h bien invertidas).
2. Eval de extracción: script que compara output del agente vs. golden (reglas
   detectadas / reglas reales = **precisión y recall**). Primer eval del proyecto.
3. CLI `legacybridge extract` que encadena parser → agente → spec.
4. Documentar en README la tabla de precisión del extractor.

**Criterio de aceptación:** eval de extracción corre en CI con umbral (p. ej. recall ≥ 0.8), `git tag v0.2.1`.
**Esfuerzo:** ~9 h.

---

## Sprint 3 — Agente 2: Generador .NET (Semanas 5–6) → `v0.3`

**Objetivo:** del Business Spec a una solución .NET 8 que **compile sin intervención manual**.
Este es el sprint que más vende tu dominio de DDD/Clean Architecture.

### Semana 5 — Esqueleto generado (determinista)

**Tareas:**
1. Templates con **Scriban** (C#) para la estructura fija — no le des al LLM lo que
   un template hace mejor:
   ```
   Generated/
     Domain/          (entidades, value objects, interfaces de repo)
     Application/     (servicios, DTOs — aquí va la lógica del LLM)
     Infrastructure/  (EF Core, repositorios)
     Api/             (endpoints mínimos)
   ```
2. Generador determinista de: entidades (de `entities` del spec), DbContext,
   proyectos .csproj, solución.
3. `legacybridge generate` produce la solución y corre `dotnet build` — reporta
   el resultado. Métrica clave: **% que compila sin edición**.

**Criterio de aceptación:** solución generada con dominio completo compila en verde.
**Esfuerzo:** ~9 h.

### Semana 6 — Lógica de negocio con LLM

**Tareas:**
1. El Agente 2 recibe: spec YAML + AST de la rutina + template del método → genera
   el cuerpo de cada servicio de Application.
2. Loop de auto-corrección: si no compila, el error del compilador vuelve al agente
   (máx. 3 iteraciones, loggeado). Documentar este loop en el README — es una
   decisión de diseño que los hiring managers preguntan en entrevistas.
3. Métrica publicada: tasa de compilación y nº promedio de iteraciones.
4. Output en `samples/vfp-inventory/migrated/` **commiteado al repo** — el
   antes/después visible sin ejecutar nada.

**Criterio de aceptación:** `migrated/` compila, README actualizado con métricas, `git tag v0.3`.
**Esfuerzo:** ~10 h.

> **Momento de visibilidad nº 2:** con v0.3 ya tienes un demo convincente.
> Post corto en LinkedIn (ES+EN): "migré un sistema VFP a .NET 8 con agentes — repo abierto".

---

## Sprint 4 — Agente 3: Equivalencia + Evals (Semanas 7–8) → `v0.4`

**Objetivo:** la joya del proyecto. Demostrar que el código migrado **se comporta igual**
que el legacy. Es tu tesis central y lo que nadie más muestra.

### Semana 7 — Harness de equivalencia

**Tareas:**
1. **Oráculo legacy**: implementar en C# un mini-intérprete del IR (ejecuta el AST
   directamente) — esto simula la semántica VFP sin necesitar VFP instalado.
   Alcance: la aritmética, control de flujo y funciones del sample (`ROUND`, etc.).
2. Agente 3: genera casos de prueba (normales + borde + adversarios: qty=0,
   percent>50, valores negativos) a partir del spec.
3. Runner: ejecuta cada caso contra el oráculo IR y contra el código .NET migrado
   (vía tests xUnit generados + Testcontainers/Postgres para la capa de datos).
4. `legacybridge verify` → `EQUIVALENCE-REPORT.md`: casos, match/mismatch, % equivalencia.

**Criterio de aceptación:** reporte de equivalencia generado para `inv_calc` con % real.
**Esfuerzo:** ~10 h. **Riesgo:** el intérprete IR es lo más complejo del sprint — si se
complica, recorta a las rutinas sin SQL embebido primero.

### Semana 8 — Evals en CI + observabilidad

**Tareas:**
1. Suite de evals completa en `evals/`: extracción (S2), compilación (S3),
   equivalencia (S4) — cada uno con umbral que **rompe el build** si baja.
2. **Langfuse** self-hosted en docker-compose: tracing por agente (prompt in,
   tokens, latencia, costo). Screenshot del trace en el README.
3. `EQUIVALENCE-REPORT.md` commiteado en `samples/vfp-inventory/`.
4. README: tabla de resultados con valores reales (reemplaza los 🎯).

**Criterio de aceptación:** CI ejecuta los 3 evals con umbrales; README muestra números reales; `git tag v0.4`.
**Esfuerzo:** ~9 h.

> **Momento de visibilidad nº 3:** este es EL diferenciador. Si solo muestras una
> cosa en una entrevista, es el equivalence report.

---

## Sprint 5 — Servidor MCP (Semana 9) → `v0.5`

**Objetivo:** exponer el pipeline como herramientas MCP. Nicho caliente 2026 y
demuestra integración con el ecosistema de agentes real.

**Tareas:**
1. `src/agents/mcp-server/` (TypeScript, `@modelcontextprotocol/sdk`):
   - `analyze_legacy(path)` → IR resumido
   - `generate_dotnet(path)` → resumen + archivos generados
   - `run_equivalence(path)` → % equivalencia + reporte
2. Probar con Claude Code y documentar la configuración (snippet `.mcp.json`).
3. Video/GIF corto de Claude Code migrando el sample vía LegacyBridge.
4. README: sección MCP con el GIF.

**Criterio de aceptación:** migración end-to-end invocada desde Claude Code, `git tag v0.5`.
**Esfuerzo:** ~8 h.

---

## Sprint 6 — Dashboard + lanzamiento (Semanas 10–11) → `v1.0`

### Semana 10 — Dashboard Next.js

**Tareas:**
1. `src/dashboard/` (Next.js + Tailwind): subir `.prg` → progreso del pipeline por
   agente (polling o SSE) → reporte de equivalencia visual (tabla de casos, diff
   de outputs).
2. Docker-compose completo: dashboard + orchestrator + postgres + langfuse,
   `docker compose up` levanta todo (es el quickstart del README — debe funcionar).
3. Grabar **demo.gif de 30 s** y ponerlo como primer elemento del README.

**Criterio de aceptación:** quickstart del README ejecutable de punta a punta.
**Esfuerzo:** ~10 h.

### Semana 11 — Lanzamiento v1.0

**Tareas:**
1. Segundo caso de muestra: `pb-billing` (PowerBuilder) aunque sea con parser mínimo
   de datawindow/procedures — demuestra que el IR es multi-lenguaje. Si no alcanza
   el tiempo, déjalo como issue abierto bien descrita (también es señal positiva).
2. Auditoría final del README: quickstart cronometrado, badges reales, métricas reales.
3. **Post de lanzamiento en inglés** (Dev.to + LinkedIn + Hacker News "Show HN" si el
   demo.gif quedó bien). Estructura: problema → demo → métricas → decisiones de diseño.
4. Actualizar mariomanrique.dev: LegacyBridge como proyecto destacado nº 1, con
   las métricas del equivalence report.
5. Abrir 3–5 issues `good-first-issue` bien escritas — invitan contribución y
   muestran que sabes liderar trabajo técnico.

**Criterio de aceptación:** `git tag v1.0.0`, post publicado, web actualizada.
**Esfuerzo:** ~10 h.

---

## Semana 12 — Buffer

Reserva explícita para: desbordes del Sprint 4 (el más riesgoso), feedback del
lanzamiento, o adelantar `pb-billing`. Si todo salió bien, úsala para empezar la
versión en inglés de tu sitio y postular.

---

## Gestión del proyecto

**Cadencia semanal (30 min, contigo mismo):**
- ¿Qué se commiteó esta semana? (si la respuesta es "nada visible", ajustar scope)
- Actualizar el roadmap del README: marcar lo hecho
- Un commit por tarea, mensajes en inglés, convención `feat:/fix:/test:`

**Tablero:** GitHub Projects del propio repo — columnas Backlog / Week N / Done.
Que el tablero sea público: demuestra gestión, no solo código.

**Reglas de oro:**
1. **CI verde siempre** — nunca mergear con tests rotos.
2. **Métricas reales o ninguna** — jamás un número inventado en el README.
3. **Prompts y specs versionados** como código, con su changelog.
4. **Scope honesto** — si algo no se soporta, se dice. Es tu marca.

## Riesgos principales

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Intérprete IR (S4) más complejo de lo previsto | Media | Recortar a rutinas sin SQL; el reporte parcial ya demuestra el concepto |
| Costos de API LLM | Media | Soporte Ollama local desde S2; documentar costo por migración del sample |
| Output LLM no determinista rompe evals | Alta | Temperatura 0, semillas, umbrales con margen, reintentos acotados |
| Falta de tiempo (trabajos freelance) | Alta | El plan ya tiene buffer; cada sprint cierra con algo demostrable, así que un retraso no destruye el proyecto |
| Perfeccionismo en el dashboard | Media | El dashboard es lo MENOS importante para contratación — recórtalo antes que los evals |

## Definición de éxito (v1.0)

- [ ] `docker compose up` → migración + equivalencia del sample, de punta a punta
- [ ] Equivalencia ≥90% publicada con reporte en el repo
- [ ] 3 evals con umbrales en CI
- [ ] Servidor MCP funcionando con Claude Code (con GIF)
- [ ] Cobertura de tests ≥80% (badge real)
- [ ] README que un hiring manager entiende en 60 segundos
- [ ] Post de lanzamiento publicado en inglés
- [ ] mariomanrique.dev actualizado con LegacyBridge como proyecto insignia

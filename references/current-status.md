# Estado actual del plan de estudio

## Última actualización
2026-06-28 — Semana 2 cerrada formalmente. Listo para iniciar Semana 3 (DI + MediatR).

---

## Estado por semana

| Semana | Estado | Notas |
|--------|--------|-------|
| S1 — .NET 10 + ASP.NET Core | ✅ Completado | Skeleton con Minimal APIs, middleware custom, IOptions. Rama `week-01` |
| S2 — EF Core | ✅ Completado | Code First + Fluent API, CRUD real sobre SQLite, AsNoTracking, proyecciones, N+1. Rama `week-02` |
| S3 — DI + MediatR | ⬜ Pendiente | — |
| S4 — Clean Architecture | ⬜ Pendiente | — |
| S5 — Testing | ⬜ Pendiente | — |
| S6 — Seguridad JWT/OAuth | ⬜ Pendiente | — |
| S7 — Docker | ⬜ Pendiente | — |
| S8 — Observabilidad | ⬜ Pendiente | — |
| S9 — Microservicios | ⬜ Pendiente | — |
| S10 — Azure + CI/CD | ⬜ Pendiente | — |

**Leyenda**: ⬜ Pendiente · 🔄 En progreso · ✅ Completado · ⏸ Bloqueado

---

## Semana actual
**Semana 3 — DI + SOLID + MediatR (CQRS)** — Pendiente de iniciar. Las Semanas 1 y 2 están completas y documentadas en `weekly-docs/`.

**Deuda técnica heredada de S2 a resolver en el refactor:**
- Paginación duplicada entre `GetAll` y `GetTaskDetailed` → encuentra su lugar natural con MediatR (`PagedResult<T>` o pipeline behavior).
- Código muerto menor: borrar `TaskItemsListDummy()` y corregir comentario obsoleto "retorna lista harcodeada" en `GetAll`.

---

## Temas profundizados (más allá del plan base)

- **S2 — Relaciones, N+1 y proyecciones** (`weekly-docs/week-02-summary_Part2.1.md`): problema N+1 y su detección en logs de EF Core, por qué no activar lazy loading por defecto, `Include` (eager) vs `Select` (proyección) vs Split Queries vs explicit loading, explosión cartesiana y stitching en memoria, round-trips y latencia de red.
- **S2 — Distinción 400 vs 500**: `Enum.TryParse` para tratar dato inválido del cliente como `400 Bad Request` en lugar de `500`.
- **S2 — Operador null-forgiving (`!`)**: no cambia el comportamiento en runtime; solo defendible cuando el dominio garantiza el valor (FK obligatoria).

---

## Dudas o bloqueos registrados

- **Anotado para S8 (Observabilidad)**: error `transfer closed with outstanding read data` / "response has already started" al escribir respuestas de error desde el middleware. Solución senior: validar `context.Response.HasStarted` y usar `Results.Problem` / `ProblemDetails` (RFC 7807). No bloqueante.

---

## Estado del proyecto TaskManager API

- [✅] Semana 1: Skeleton con Minimal APIs
- [✅] Semana 2: Persistencia con EF Core
- [ ] Semana 3: Refactor con MediatR
- [ ] Semana 4: Restructuración en Clean Architecture
- [ ] Semana 5: Suite de tests
- [ ] Semana 6: Seguridad JWT
- [ ] Semana 7: Dockerización completa
- [ ] Semana 8: Logging y observabilidad
- [ ] Semana 9: Microservicio de notificaciones
- [ ] Semana 10: CI/CD y deploy a Azure

---

## Instrucciones para el mentor

Cuando el usuario vuelva en un nuevo chat:
1. Leer este archivo primero para saber en qué punto está
2. Preguntar: "¿Seguimos con la Semana X o tienes alguna duda de lo que ya revisaste?"
3. Si el usuario confirma que terminó una semana, actualizar su estado a ✅ en este archivo (mentalmente — no tienes permisos de escritura, pero toma nota para la sesión)
4. Si el usuario trae código real del proyecto, revisarlo en contexto del plan
5. Recordar que puede empezar a postularse desde la semana 5–6 sin esperar el plan completo

---

## Contexto adicional de la sesión inicial

- El plan fue creado con una hoja de ruta interactiva visual (widget HTML)
- Se incluyeron 4 secciones: Semanas, Tus conocimientos, Entrevistas técnicas, Ruta mínima rápida
- El usuario prefirió recibir la continuación como skill .md (no como prompt de sistema)
- Énfasis especial en: empleabilidad rápida, proyecto práctico incremental, nivel senior sin explicaciones básicas

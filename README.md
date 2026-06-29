# .NET Senior Bootcamp — 10 semanas

> Hoja de ruta de actualización profesional: de .NET Framework 4.7 a .NET moderno (10.x).  
> Proyecto incremental, documentación por semana y banco de preguntas para entrevistas Senior.

---

## Contexto y motivación

Tengo experiencia sólida en **ASP.NET MVC**, **Web APIs REST** y **SQL Server** sobre **.NET Framework 4.7**, arquitecturas N-tier y aplicaciones de consola. Este repositorio documenta mi proceso de actualización hacia el ecosistema .NET moderno de forma sistemática, con un proyecto práctico que evoluciona semana a semana y foco explícito en empleabilidad.

No es un repositorio de tutoriales — es un plan de trabajo real, con decisiones técnicas documentadas, trade-offs y preparación para entrevistas Senior.

---

## Tecnologías del plan

| Semana | Tecnología principal |
|--------|---------------------|
| S1 ✅ | .NET 10, ASP.NET Core, Minimal APIs, Middleware, `IOptions<T>` |
| S2 ✅ | Entity Framework Core 10 — Code First, Fluent API, Migrations, AsNoTracking, proyecciones |
| S3 | DI nativa, SOLID pragmático, MediatR (CQRS), Pipeline Behaviors |
| S4 | Clean Architecture, Vertical Slice Architecture, DDD táctico |
| S5 | xUnit, NSubstitute, FluentAssertions, WebApplicationFactory, TestContainers |
| S6 | JWT, OAuth 2.0, OpenID Connect, ASP.NET Core Identity |
| S7 | Docker, Dockerfile multi-stage, docker-compose |
| S8 | Serilog, Structured logging, OpenTelemetry, Correlation IDs |
| S9 | RabbitMQ, MassTransit, Saga Pattern, Outbox Pattern, YARP |
| S10 | Azure Container Apps, GitHub Actions CI/CD, deploy automatizado |

---

## Estructura del repositorio

```
dotnet-2026-bootcamp/
│
├── TaskManager/                  # Proyecto principal (evoluciona semana a semana)
│   └── TaskManager.Api/          # ASP.NET Core — .NET 10
│
├── references/                   # Documentación de referencia
│   ├── complete-plan.md          # Detalle completo del plan de 10 semanas
│   ├── current-status.md         # Estado actual y progreso por semana
│   └── interviews.md             # Banco de preguntas técnicas para entrevistas Senior
│
├── weekly-docs/                  # Notas técnicas por semana
│   ├── week-01.md                # Conceptos, decisiones y aprendizajes de S1
│   └── week-NN.md                # (se agrega una por semana)
│
└── .cowork                       # Metadata de sesiones (ver más abajo)
```

---

## Proyecto práctico: TaskManager API

Un solo proyecto que **evoluciona cada semana**, acumulando capas de complejidad real:

- **S1 ✅** — Skeleton: Minimal APIs, middleware de logging y manejo de excepciones, configuración tipada con `IOptions<T>`
- **S2 ✅** — Persistencia: EF Core Code First con Fluent API, entidades `TaskItem / Project / User`, migrations, CRUD real sobre SQLite, lecturas optimizadas (`AsNoTracking` + proyección `Select`)
- **S3** — Refactor: MediatR, Commands, Queries, Pipeline Behaviors transversales
- **S4** — Arquitectura: Clean Architecture (Domain / Application / Infrastructure / API)
- **S5** — Testing: handlers unitarios + integration tests end-to-end con BD real
- **S6** — Seguridad: JWT, refresh tokens, roles `Admin / User`
- **S7** — Contenedores: Dockerfile multi-stage, docker-compose con API + SQL Server + Seq
- **S8** — Observabilidad: Serilog, correlation IDs, health checks, OpenTelemetry
- **S9** — Mensajería: microservicio de notificaciones, RabbitMQ/MassTransit, Outbox Pattern
- **S10** — Cloud: CI/CD en GitHub Actions, deploy a Azure Container Apps

---

## Cómo navegar este repositorio

### Ramas por semana

Cada semana tiene su propia rama que congela el estado final del proyecto en ese punto:

```
main        → estado más reciente (semana actual en curso)
week-01     → TaskManager API al finalizar Semana 1
week-02     → TaskManager API al finalizar Semana 2
...
week-10     → versión final con CI/CD y deploy en Azure
```

**Para ver el proyecto en cualquier punto de su evolución**, cambiá a la rama correspondiente:

```bash
git checkout week-01   # Minimal APIs + middleware
git checkout week-04   # Con Clean Architecture aplicada
git checkout week-07   # Con Docker y docker-compose
```

La rama `main` siempre refleja el estado más avanzado. Las ramas `week-NN` son snapshots inmutables — no reciben cambios retroactivos.

### Documentación semanal

Cada semana tiene un documento en `weekly-docs/` con:
- Conceptos cubiertos y explicaciones técnicas
- Decisiones de diseño tomadas y alternativas descartadas
- Preguntas de entrevista relacionadas al tema
- Pendientes para la semana siguiente

---

## Cómo correr el proyecto (estado actual — Semana 2)

**Prerequisitos**: .NET 10 SDK y la herramienta `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

```bash
cd TaskManager/TaskManager.Api

# Crear/actualizar la base de datos SQLite a partir de las migrations
dotnet ef database update

dotnet run
```

La base de datos SQLite (`taskmanager.db`) se genera localmente a partir de las migrations — no se versiona en el repositorio.

Endpoints disponibles en `https://localhost:{puerto}/`:

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/tasks` | Lista paginada (`?page=1&pageSize=5`) |
| `GET` | `/tasks/{id}` | Tarea por ID |
| `GET` | `/tasks/detailed` | Lista con datos de proyecto y usuario asignado (proyección) |
| `POST` | `/tasks` | Crear tarea |
| `PUT` | `/tasks/{id}` | Actualizar tarea |
| `DELETE` | `/tasks/{id}` | Eliminar tarea |

---

## Banco de preguntas técnicas

El archivo [`references/interviews.md`](references/interviews.md) contiene respuestas modelo a preguntas frecuentes en entrevistas Senior .NET, organizadas en tres bloques:

- **Bloque 1** — Imprescindibles (Minimal APIs vs Controllers, DI lifetimes, JWT, EF Core, Clean Architecture, CQRS)
- **Bloque 2** — Diferenciadores Senior (microservicios, Outbox Pattern, observabilidad)
- **Bloque 3** — Ejercicios de coding típicos (paginación offset/cursor, async/await, deadlocks)

---

## Registro de sesiones (.cowork)

El archivo `.cowork` es metadata de [cowork](https://github.com/drincast/cowork), una herramienta CLI en desarrollo para registrar sesiones de trabajo colaborativo y medir tiempo. Si te interesa el proyecto, el repositorio está en [github.com/drincast/cowork](https://github.com/drincast/cowork).

---

## Progreso

| Semana | Estado | Rama |
|--------|--------|------|
| S1 — .NET 10 + ASP.NET Core | ✅ Completada | `week-01` |
| S2 — Entity Framework Core | ✅ Completada | `week-02` |
| S3 — DI + MediatR | ⬜ Pendiente | — |
| S4 — Clean Architecture | ⬜ Pendiente | — |
| S5 — Testing | ⬜ Pendiente | — |
| S6 — JWT / OAuth 2.0 | ⬜ Pendiente | — |
| S7 — Docker | ⬜ Pendiente | — |
| S8 — Observabilidad | ⬜ Pendiente | — |
| S9 — Microservicios | ⬜ Pendiente | — |
| S10 — Azure + CI/CD | ⬜ Pendiente | — |

---

## Licencia

[MIT](LICENSE)

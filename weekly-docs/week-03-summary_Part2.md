# Cierre Semana 3 (Parte 2) — MediatR + CQRS — Lo que construiste y debes saber defender

> Continuación de `week-03-summary_Part1.md` (DI a fondo + SOLID pragmático). Esta sesión aplica esos fundamentos al refactor real: los endpoints de `TaskEndpoints.cs` dejaron de tener lógica de negocio y ahora delegan a Commands/Queries vía MediatR.

---

## Lo que construiste

Los 5 endpoints de `TaskEndpoints.cs` migraron de lógica inline (con `TaskManagerDbContext` inyectado directo en el endpoint) a un `IRequest`/`IRequestHandler` por operación:

```
TaskManager.Api/
├── Common/
│   └── PagedResult.cs                 ← genérico, resuelve la duplicación de paginación
├── Features/
│   └── Task/
│       ├── GetAllTasks.cs             ← Query (lista paginada, DTO simple)
│       ├── GetTask.cs                 ← Query (una tarea por Id)
│       ├── GetAllDetailedTasks.cs     ← Query (lista paginada, DTO detallado con relaciones)
│       ├── CreateTask.cs              ← Command (devuelve el Id creado)
│       ├── UpdateTask.cs              ← Command (devuelve un enum de resultado, no un IResult)
│       └── DeleteTask.cs              ← Command (devuelve bool: encontrado/no encontrado)
└── TaskEndpoints.cs                   ← ahora solo traduce HTTP ↔ MediatR (ISender.Send)
```

Cada archivo agrupa **Query/Command + DTO + Handler** en una sola clase estática (`GetAllTasks`, `CreateTask`, etc.) — Vertical Slice: se agrupa por *feature*, no por tipo técnico. Es una decisión de estilo consciente, distinta a tener carpetas separadas `Queries/`, `Commands/`, `Dtos/`.

---

## Decisiones técnicas que tomaste y por qué

**MediatR clásico (v12.5.0), no la v13+ de doble licencia**
MediatR se comercializó oficialmente el 2 de julio de 2025 bajo la nueva empresa de Jimmy Bogard, Lucky Penny Software, y desde la versión 13.0.0 el paquete requiere una license key (con un tier "Community" gratuito para empresas/individuos con menos de $5,000,000 USD de ingresos brutos anuales). Las versiones 12.x y anteriores siguen bajo su licencia open-source original (Apache 2.0), sin key ni restricciones. Para este proyecto se instaló explícitamente `v12.5.0` — cero fricción, mismo código, 100% transferible si algún día se necesita migrar a v13+.

**`DbContext` inyectado directo en el Handler, sin capa de Repository**
Con CQRS + MediatR, el Repository Pattern clásico pierde buena parte de su sentido: el Handler ya es una clase de responsabilidad única (SRP, visto en Parte 1), y `DbContext` ya es Unit of Work (`SaveChanges`) con sus `DbSet<T>` actuando como repositorios. Envolver eso en un `IRepository` genérico sería una abstracción sobre otra abstracción, sin dolor real que justifique el costo.

**`UpdateTask` devuelve un enum de dominio (`UpdateResult`), no un `IResult`**
El Handler no debe conocer ASP.NET Core. Devolver `Results.BadRequest(...)` desde un Handler acoplaría la capa de aplicación al framework web. La solución: un enum (`Success`, `NotFound`, `InvalidStatus`) que el **endpoint** traduce a códigos HTTP con un `switch`. Es la respuesta correcta a la pregunta abierta desde Semana 2: cómo distinguir 400 de 404 de 204 sin ensuciar el Handler.

**`CreateTask` devuelve solo el `int` Id, no el DTO completo**
Un Command en CQRS puro no debería devolver datos de lectura — devuelve lo mínimo para que el endpoint construya la respuesta HTTP (aquí, el Id para el header `Location` del `201 Created`). Si se necesitara el objeto completo, eso es responsabilidad de una Query aparte.

**`PagedResult<T>` genérico reutilizado en `GetAllTasks` y `GetAllDetailedTasks`**
Resuelve la deuda anotada desde Semana 2 (`TotalItems` duplicado entre `GetAll` y `GetTaskDetailed`). Al ser un `record` con nombre (no un objeto anónimo), es testeable de cara a Semana 5.

---

## Detalle de código corregido en la sesión: proyección consistente en `Select`

### El problema detectado

`GetTask.cs` proyectaba a un objeto anónimo primero y comparaba `Status == TaskStatus.Done` **después**, ya en memoria:

```csharp
// Antes — comparación fuera del Select
.Select(t => new { t.Id, t.Title, t.Status })
.FirstOrDefaultAsync(ct);
// ...
new ItemDto(task.Id, task.Title, task.Status == TaskStatus.Done); // en C#, no en SQL
```

Mientras que `GetAllTasks.cs` sí resolvía la comparación **dentro** del `Select`:

```csharp
.Select(t => new TaskListItemDto(t.Id, t.Title, t.Status == TaskStatus.Done))
```

### Por qué importa

Lo que va dentro de un `Select` traducible por EF Core se convierte en SQL y lo resuelve el motor de base de datos (`SELECT Id, Title, (Status = 'Done') AS Done FROM TaskItems`) — nunca viaja el enum completo por la red. Lo que va fuera del `Select` (después de `ToListAsync`/`FirstOrDefaultAsync`) se ejecuta en memoria, en el proceso de la aplicación. Con una fila no se nota; con miles, sí.

### La regla general

**Todo lo que puedas resolver dentro del `Select` (comparaciones, aritmética simple, ternarios, concatenación, navegación a relaciones), resuélvelo ahí.** Es la misma lógica de fondo detrás del problema N+1 visto en Semana 2: mientras más trabajo delega la app a la BD, menos datos cruzan la red innecesariamente.

### Ejemplos — qué SÍ se traduce a SQL

| Operación | Ejemplo | Se traduce a |
|---|---|---|
| Comparación | `t.Status == TaskStatus.Done` | `Status = 'Done'` |
| Aritmética de fechas | `(DateTime.UtcNow - t.CreatedAt).Days` | resta de fechas nativa del motor |
| Ternario / null-check | `t.AssignedToId != null ? "Sí" : "No"` | `CASE WHEN ... THEN ... ELSE ... END` |
| Métodos de string estándar | `t.Title.Substring(0, 20) + "..."` | `SUBSTRING()`, concatenación |
| Navegación a relaciones | `t.Project!.Name` | `JOIN` |
| Agregados sobre colecciones | `p.Tasks.Count()` | `COUNT(*)` |

### Ejemplos — qué NO se traduce (y por qué)

| Operación | Ejemplo | Problema |
|---|---|---|
| Método propio de C# | `FormatearResumen(t.Title, t.Status)` | EF Core no sabe convertirlo a SQL → `InvalidOperationException` en runtime |
| Cultura/formato complejo | `t.CreatedAt.ToString("dd 'de' MMMM", new CultureInfo("es-ES"))` | `CultureInfo` no tiene equivalente en SQL |
| Efectos secundarios | `Console.WriteLine(...)`, I/O, llamadas a otros servicios | no son operaciones de datos |
| Regex complejas | soporte variable según motor (SQLite es más limitado que SQL Server) | depende de la versión del provider |

**Salida cuando algo no es traducible:** materializar primero con `ToListAsync()`, y transformar después en memoria, conscientemente:

```csharp
var tasks = await _db.TaskItems
    .Select(t => new { t.Title, t.Status, t.CreatedAt }) // esto sí se traduce
    .ToListAsync(ct);                                     // ← aquí termina el SQL

var resumen = tasks.Select(t => FormatearResumen(t.Title, t.Status)).ToList(); // C#, en memoria
```

**Pregunta de autochequeo:** *"¿esto es algo que SQL sabe hacer nativamente, o es lógica de negocio de mi aplicación?"* Lo primero va dentro del `Select`. Lo segundo va después de materializar.

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"¿Qué es CQRS y qué problema resuelve?"**

Command Query Responsibility Segregation separa lógicamente las operaciones que mutan estado (Commands: Create, Update, Delete — no idempotentes) de las que solo leen (Queries: GetAll, GetById — idempotentes). Permite optimizar cada lado por separado y evita que un mismo modelo cargue con las restricciones de ambos mundos.

**"¿Por qué MediatR y no llamar los handlers directo?"**

MediatR reduce el acoplamiento: el endpoint no conoce la clase concreta del Handler, solo envía un mensaje (`IRequest<T>`) a través de `ISender`. Facilita agregar cross-cutting concerns (logging, validación) vía Pipeline Behaviors sin tocar cada endpoint — eso es exactamente lo que se aborda en la Parte 3.

**"¿Un Command puede devolver datos?"**

Sí, pero lo mínimo necesario para que el caller construya la respuesta (un Id, un enum de resultado) — nunca una proyección de lectura completa. Para eso existe una Query.

**"¿Por qué no metiste un `IRepository` sobre el `DbContext`?"**

Porque el `DbContext` ya es Unit of Work y sus `DbSet<T>` ya actúan como repositorios. El Handler de MediatR ya cumple SRP. Agregar un Repository genérico encima sería una capa sin dolor real que justifique su costo — se introduciría solo si aparece una necesidad concreta (queries duplicadas entre handlers, cambio real de tecnología de acceso a datos).

**"¿Qué se traduce a SQL dentro de un `Select` y qué no?"**

Comparaciones, aritmética simple, ternarios, navegación a relaciones y agregados se traducen. Métodos propios de C#, formateo con `CultureInfo` y cualquier efecto secundario no — EF Core lanza `InvalidOperationException` en runtime si lo intentas. La solución es materializar primero y transformar después, en memoria, de forma consciente.

---

## Extras — para cuando se profundice más adelante (no bloqueante ahora)

Estos dos temas quedan anotados como contexto de ecosistema, sin haberlos implementado, para referencia futura o para la sección de extras al final del plan de 10 semanas:

### MediatR "nuevo" (v13+, licenciado)

A nivel de código, casi nada cambia respecto a la v12 usada en este proyecto — misma API (`IRequest<T>`, `IRequestHandler<,>`, `IPipelineBehavior<,>`). El único agregado es registrar una license key (gratuita bajo el tier Community para <$5M de ingresos):

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.LicenseKey = builder.Configuration["MediatR:LicenseKey"];
});
```

### Alternativa gratuita: `martinothamar/Mediator`

El reemplazo más cercano a "drop-in", basado en source generators (sin reflexión en runtime, compatible con Native AOT). Cambios necesarios al migrar:

| Aspecto | MediatR clásico | martinothamar/Mediator |
|---|---|---|
| `using` | `using MediatR;` | `using Mediator;` |
| Firma del Handler | `Task<T> Handle(...)` | `ValueTask<T> Handle(...)` |
| Registro de handlers | Escaneo de assembly (reflexión) | Generado en tiempo de compilación |
| Pipeline Behaviors | Auto-registrados | Se registran manualmente |

Migración mecánica: cambiar el `using`, cambiar `Task`→`ValueTask` en las firmas de los handlers, y registrar los pipeline behaviors a mano.

### Material de lectura compartido en la sesión

Para reforzar el "por qué" de la división CQRS + MediatR, más allá del código:

- Isaac Ojeda — *"Desarrollando una API en ASP.NET con CQRS y MediatR"* (DEV Community, serie): https://dev.to/isaacojeda/parte-1-cqrs-y-mediatr-implementando-cqrs-en-aspnet-56oe — mismo stack que este proyecto (ASP.NET Core + MediatR), explica el enfoque de encapsular cada funcionalidad en archivos individuales y señala una limitación clave: MediatR es in-process, no sirve si se busca separar Queries y Commands en aplicaciones completamente distintas (para eso, un Message Broker — tema de Semana 9).
- NetMentor — *"Patrón CQRS explicado en 10 minutos"*: https://www.netmentor.es/entrada/patron-cqrs-explicado-10-minutos — resumen corto y directo de la separación lecturas/comandos, con ejemplos.
- Microsoft Learn (ES) — *"Aplicación de patrones CQRS y DDD simplificados en un microservicio"*: https://learn.microsoft.com/es-es/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns — referencia oficial, más profunda, sobre CQRS y su relación con DDD.
version en español: https://learn.microsoft.com/es-es/dotnet/architecture/microservices/container-docker-introduction/

---

## Estado de la Semana 3

| Bloque | Estado |
|--------|--------|
| Parte 1 — DI a fondo + SOLID pragmático | ✅ Completado |
| **Parte 2 — MediatR + CQRS (refactor de los 5 endpoints, `PagedResult<T>`)** | ✅ **Completado** |
| Parte 3 — Pipeline Behaviors (logging) + cierre de semana | ⏳ Pendiente |

### Limpieza pendiente (no bloqueante, arrastrada de la sesión anterior)

- Borrar el código comentado (lógica vieja con `TaskManagerDbContext db` directo) en `TaskEndpoints.cs`.
- Borrar `TaskItemsListDummy()` en la región de utilería — ya no se usa en ningún lado.
- Opcional: alinear `GetTask.cs` para proyectar directo al DTO dentro del `Select` (ver sección de proyección consistente arriba), en vez de proyectar a un objeto anónimo intermedio.

**Próxima sesión — Parte 3:** un `IPipelineBehavior<,>` para logging automático de cada Command/Query — el "middleware de MediatR" —, que conecta directo con el `RequestLoggingMiddleware` ya construido en Semana 1. Con eso se cierra formalmente la Semana 3.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

# Cierre Semana 4 (Parte 2 — Variante B) — Reestructuración física a Clean Architecture con `IRepository`

> Companion de `week-04-summary_Part2_ApplicationDbContext.md`. Los Pasos 1–3 (crear los 4 proyectos, mover `Domain`, mover `Common`/`Features` a Application, mover `Persistence`/`Migrations` a Infrastructure, mover endpoints a Api) son **idénticos** a ese archivo — no se repiten aquí. Este archivo reemplaza únicamente la pieza de inversión de dependencias: en vez de `IApplicationDbContext`, cada agregado (`TaskItem`, `Project`, `User`) obtiene su propio Repository.
>
> Esta es la discusión que Semana 3 dejó pendiente ("no se envolvió el `DbContext` en Repository porque el Handler ya era SRP") y que Semana 4 Parte 1 anunció que "reaparecería con lupa" al llegar a Clean Architecture. Aquí se resuelve.

---

## Por qué esta variante existe y cuándo se prefiere sobre `IApplicationDbContext`

Con `IApplicationDbContext`, los Handlers de Application siguen escribiendo LINQ directo (`_db.TaskItems.Where(...).Select(...)`). Eso es cómodo, pero significa que Application sigue acoplada *conceptualmente* a EF Core — solo cambió quién te lo inyecta, no cómo piensas las queries.

Con `IRepository`, Application deja de saber que existe LINQ-a-SQL. Los Handlers llaman métodos de intención de negocio (`GetPagedAsync`, `GetByIdWithDetailsAsync`) y es **Infrastructure** quien decide cómo traducir eso a EF Core (o a Dapper, o a lo que sea). Es un aislamiento más profundo, a cambio de más código.

---

## Estructura de archivos — lo que cambia respecto a la Variante A

```
TaskManager.Application/
├── Common/
│   ├── PagedResult.cs
│   └── Behaviors/
│       └── LoggingBehavior.cs
│   (ya NO existe Common/Interfaces/IApplicationDbContext.cs)
├── Common/Interfaces/
│   └── ITaskRepository.cs          ← NUEVO — reemplaza a IApplicationDbContext
├── Features/
│   └── Task/
│       └── (mismos 6 archivos, pero los Handlers ahora dependen de ITaskRepository)
└── DependencyInjection.cs           (sin cambios respecto a la Variante A)

TaskManager.Infrastructure/
├── Persistence/                     (idéntico a la Variante A)
├── Repositories/
│   └── TaskRepository.cs            ← NUEVO — implementa ITaskRepository con EF Core
└── DependencyInjection.cs           (cambia el registro — ver abajo)
```

**Decisión de alcance tomada aquí:** solo `TaskItem` obtiene Repository propio (`ITaskRepository`), no uno genérico por entidad (`IProjectRepository`, `IUserRepository`). Razón: `Project` y `User` hoy no tienen Handlers propios — solo se leen como relaciones dentro de las queries de `TaskItem` (`GetAllDetailedTasks`). Crear repositorios para entidades que ningún Handler consulta de forma independiente sería exactamente el tipo de sobreingeniería que Semana 3 (SOLID pragmático) advertía evitar. Si en Semana 6+ aparecen Handlers propios de `Project` o `User` (por ejemplo, gestión de usuarios para JWT), ahí se justifica su propio Repository — no antes.

---

## El contrato — `ITaskRepository`

```csharp
// TaskManager.Application/Common/Interfaces/ITaskRepository.cs
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Common.Interfaces;

public interface ITaskRepository
{
    Task<int> CountAsync(CancellationToken ct);

    Task<IReadOnlyList<TaskListItemDto>> GetPagedAsync(int page, int size, CancellationToken ct);

    Task<TaskDetailDto?> GetDetailedByIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<TaskDetailDto>> GetPagedDetailedAsync(int page, int size, CancellationToken ct);

    Task<TaskItem?> FindAsync(int id, CancellationToken ct);   // para Update/Delete: necesita la entidad, no el DTO

    void Add(TaskItem task);

    void Remove(TaskItem task);

    Task<int> SaveChangesAsync(CancellationToken ct);
}

public record TaskListItemDto(int Id, string Title, bool Done);

public record TaskDetailDto(int Id, string Title, string? Description, TaskStatus Status,
    DateTime CreatedAt, int ProjectId, string ProjectName,
    int? AssignedToId, string? AssignedToName);
```

**Decisión clave y su justificación — el Repository devuelve DTOs de lectura, no solo entidades:**

Esta es la parte donde muchas implementaciones de "Repository Pattern de libro" se quedan cortas: si `ITaskRepository` solo expusiera `Task<TaskItem?> GetByIdAsync(int id)` devolviendo la entidad completa, perderías la proyección `Select` de Semana 2/3 — cargarías toda la entidad y proyectarías después *en memoria*, exactamente el anti-patrón que se corrigió en la sesión de N+1 de Semana 2 (la comparación `Status == TaskStatus.Done` debe resolverse en SQL, no en C#).

Por eso el Repository tiene métodos de **lectura** que devuelven DTOs directamente (`GetPagedAsync`, `GetDetailedByIdAsync` — la proyección vive en Infrastructure, donde también vive el conocimiento de EF Core) y métodos de **escritura** (`FindAsync`, `Add`, `Remove`) que sí devuelven/reciben la entidad completa, porque escribir requiere el grafo completo bajo tracking.

**Esto es exactamente la conexión con ISP de Semana 3 (Parte 1):** un cliente de solo lectura no debería depender de métodos de escritura y viceversa. Aquí se ve en la práctica — el contrato ya separa mentalmente lectura de escritura dentro de la misma interfaz, sentando además la base conceptual para si algún día se separa en `ITaskReadRepository` / `ITaskWriteRepository` (CQRS llevado al extremo de Infrastructure, generalmente innecesario salvo con datastores de lectura/escritura físicamente distintos).

---

## La implementación — `TaskRepository` en Infrastructure

```csharp
// TaskManager.Infrastructure/Repositories/TaskRepository.cs
using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly TaskManagerDbContext _db;

    public TaskRepository(TaskManagerDbContext db) => _db = db;

    public Task<int> CountAsync(CancellationToken ct) => _db.TaskItems.CountAsync(ct);

    public async Task<IReadOnlyList<TaskListItemDto>> GetPagedAsync(int page, int size, CancellationToken ct)
        => await _db.TaskItems
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new TaskListItemDto(t.Id, t.Title, t.Status == TaskStatus.Done))
            .ToListAsync(ct);

    public Task<TaskDetailDto?> GetDetailedByIdAsync(int id, CancellationToken ct)
        => _db.TaskItems
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TaskDetailDto(
                t.Id, t.Title, t.Description, t.Status, t.CreatedAt,
                t.ProjectId, t.Project!.Name,
                t.AssignedToId, t.AssignedTo != null ? t.AssignedTo.Name : null))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TaskDetailDto>> GetPagedDetailedAsync(int page, int size, CancellationToken ct)
        => await _db.TaskItems
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new TaskDetailDto(
                t.Id, t.Title, t.Description, t.Status, t.CreatedAt,
                t.ProjectId, t.Project!.Name,
                t.AssignedToId, t.AssignedTo != null ? t.AssignedTo.Name : null))
            .ToListAsync(ct);

    public Task<TaskItem?> FindAsync(int id, CancellationToken ct) => _db.TaskItems.FindAsync([id], ct).AsTask();

    public void Add(TaskItem task) => _db.TaskItems.Add(task);

    public void Remove(TaskItem task) => _db.TaskItems.Remove(task);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
```

Nota que `TaskRepository` sigue viviendo en Infrastructure y **sí** conoce el `TaskManagerDbContext` concreto directamente (no la interfaz `IApplicationDbContext` — esa ya no existe en esta variante). Toda la sabiduría de EF Core de Semanas 2 y 3 (`AsNoTracking`, `OrderBy` antes de paginar, proyección dentro del `Select`, el `!` defendible en `Project` vs el ternario explícito en `AssignedTo`) queda encapsulada aquí — y en ningún otro lado. Es la única clase del proyecto que sabe que existe EF Core además de `TaskManagerDbContext` mismo.

---

## Los Handlers — ahora dependen de `ITaskRepository`, no de un DbContext

```csharp
// TaskManager.Application/Features/Task/CreateTask.cs (fragmento)
using MediatR;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Features.Task;

public static class CreateTask
{
    public record Command(string Title, int ProjectId, int? AssignedToId) : IRequest<int>;

    public class Handler : IRequestHandler<Command, int>
    {
        private readonly ITaskRepository _repository;

        public Handler(ITaskRepository repository) => _repository = repository;

        public async Task<int> Handle(Command request, CancellationToken ct)
        {
            var task = new TaskItem
            {
                Title = request.Title,
                ProjectId = request.ProjectId,
                AssignedToId = request.AssignedToId,
                Status = TaskManager.Domain.Entities.TaskStatus.Todo
            };

            _repository.Add(task);
            await _repository.SaveChangesAsync(ct);

            return task.Id;
        }
    }
}
```

```csharp
// TaskManager.Application/Features/Task/GetAllTasks.cs (fragmento)
public class Handler : IRequestHandler<Query, PagedResult<TaskListItemDto>>
{
    private readonly ITaskRepository _repository;
    private readonly IOptions<TaskManagerOptions> _options;

    public Handler(ITaskRepository repository, IOptions<TaskManagerOptions> options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<PagedResult<TaskListItemDto>> Handle(Query request, CancellationToken ct)
    {
        var size = request.PageSize ?? _options.Value.DefaultPageSize;
        var total = await _repository.CountAsync(ct);
        var items = await _repository.GetPagedAsync(request.Page, size, ct);

        return new PagedResult<TaskListItemDto>(request.Page, size, total, items);
    }
}
```

**Compara este Handler con el equivalente de la Variante A:** ya no hay ni un solo `_db.TaskItems`, ni un `.AsNoTracking()`, ni un `.Select`. El Handler quedó reducido a **orquestación pura de negocio** — pide datos con intención (`GetPagedAsync`), no con mecánica de EF Core. Esto es lo que se gana con Repository: el Handler es literalmente ilegible como código de acceso a datos, es legible como flujo de negocio.

`UpdateTask` y `DeleteTask` usan `FindAsync` (que sí trae la entidad completa, con tracking, porque van a mutarla) — el mismo patrón que ya usaban antes de Semana 3, solo que ahora pasando por el Repository en vez de por el `DbContext` directo.

---

## Registro en `Infrastructure/DependencyInjection.cs` — lo único que cambia respecto a la Variante A

```csharp
// TaskManager.Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TaskManagerDb")
            ?? "Data Source=taskmanager.db";

        services.AddDbContext<TaskManagerDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<ITaskRepository, TaskRepository>();   // ← reemplaza el AddScoped<IApplicationDbContext>

        return services;
    }
}
```

`TaskRepository` recibe `TaskManagerDbContext` **concreto** en su constructor porque vive en Infrastructure — ahí sí está permitido conocer el tipo concreto, es la capa que lo posee. El lifetime `Scoped` es correcto por la misma razón de siempre: un `TaskRepository` por request, envolviendo un `DbContext` por request.

`Program.cs` en Api **no cambia nada** respecto a la Variante A — sigue siendo `AddApplicationServices()` + `AddInfrastructureServices()`. Ese es justo el punto: la composición en Api es indiferente a cuál de las dos variantes se eligió puertas adentro.

---

## Comparación lado a lado — `IApplicationDbContext` vs `IRepository`

| | `IApplicationDbContext` | `IRepository` (`ITaskRepository`) |
|---|---|---|
| **Qué ve el Handler** | `DbSet<T>`, LINQ completo | Métodos de intención de negocio |
| **Dónde vive el conocimiento de EF Core** | Repartido en cada Handler | Centralizado en `TaskRepository` |
| **Poder de LINQ disponible en Application** | Total (`Include`, `Select`, `AsSplitQuery`, todo) | Ninguno — Application ni referencia EF Core |
| **Costo de cambiar de ORM (EF Core → Dapper)** | Alto — reescribes cada Handler | Bajo — reescribes solo `TaskRepository` |
| **Ceremonia / código nuevo por feature** | Ninguno extra | Un método nuevo en la interfaz + su implementación por cada query distinta |
| **Riesgo de "Repository anémico"** | N/A | Real — si el Repository termina siendo `GetAll()`/`GetById()`/`Add()`/`Update()`/`Delete()` genéricos sin intención de negocio, es una capa que solo reenvía llamadas sin aportar valor (el riesgo que Parte 1 llamó "anemic layers") |
| **Testear el Handler en aislamiento** | Requiere mockear `IApplicationDbContext` (o usar SQLite in-memory, más realista pero más pesado) | Mockear `ITaskRepository` es trivial — 3-4 métodos con nombres de negocio, sin `DbSet<T>` de por medio |
| **Qué template de referencia lo usa** | `jasontaylordev/CleanArchitecture` (el más popular) | `ardalis/CleanArchitecture` (usa Repository + Specification pattern) |
| **Cuándo preferirlo** | Proyecto mediano, un solo motor de persistencia previsible, prioridad en velocidad de desarrollo | Proyecto donde se anticipa cambio de tecnología de persistencia, o donde el testing unitario de Handlers sin infraestructura real es prioridad alta (conecta directo con Semana 5) |

### La pregunta de entrevista que junta ambas variantes

**P: "Implementaste Clean Architecture — ¿usaste Repository Pattern o `IApplicationDbContext`? ¿Por qué?"**

**R modelo (con las dos variantes ya construidas, puedes hablar con propiedad de ambas):**

> "Implementé las dos para entender el trade-off de primera mano. `IApplicationDbContext` es más rápido de escribir y conserva todo el poder de LINQ en los Handlers, pero Application termina 'pensando en EF Core' aunque no lo referencie directamente como proveedor — el acoplamiento conceptual sigue ahí. `IRepository` cuesta más ceremonia — un método nuevo por cada forma distinta de consultar — pero aísla completamente a Application de cómo se accede a los datos, y hace que testear Handlers con mocks sea trivial, sin necesitar una base de datos real ni siquiera en memoria.
>
> Para un proyecto de este tamaño, con un solo motor de BD previsible, `IApplicationDbContext` es la opción pragmática — es también lo que usa el template de Jason Taylor, el más adoptado en la industria. Optaría por Repository si anticipara cambio real de tecnología de persistencia, o si el equipo prioriza unit tests puros de Application por encima de todo — que es exactamente la discusión que se retoma en Semana 5 con testing."

---

## Preguntas de entrevista específicas de esta variante

**"¿Por qué el Repository devuelve DTOs y no solo la entidad?"**

Porque si solo devolviera la entidad, perderías la proyección `Select` de EF Core — traerías la entidad completa y proyectarías en memoria, exactamente el anti-patrón de rendimiento corregido en la sesión de N+1 de Semana 2. Los métodos de lectura devuelven DTOs (la proyección vive en Infrastructure); los de escritura sí devuelven/reciben la entidad completa, porque mutarla requiere el grafo bajo tracking.

**"¿Por qué no un `IRepository<T>` genérico para todas las entidades?"**

Porque hoy solo `TaskItem` tiene Handlers propios — `Project` y `User` solo se leen como relaciones. Un repositorio genérico para entidades sin necesidad propia es sobreingeniería (mismo criterio de SOLID pragmático de Semana 3): se agrega cuando aparece la necesidad real, no antes.

**"¿Cuál es el riesgo principal del Repository Pattern mal aplicado?"**

Que termine siendo un CRUD genérico (`GetAll`, `GetById`, `Add`, `Update`, `Delete`) que solo reenvía llamadas al `DbContext` sin aportar nada — una capa sin valor real, el "anemic layer" mencionado en la Parte 1. El Repository aporta valor cuando expone *intención de negocio* (`GetPagedDetailedAsync`), no cuando es un espejo 1:1 de `DbSet<T>`.

---

## Estado de la Semana 4

| Bloque | Estado |
|--------|--------|
| Parte 1 — Fundamentos + Clean vs VSA (teoría) | ✅ Completado |
| Parte 2 (Variante A) — Reestructuración física con `IApplicationDbContext` | ✅ Completado |
| **Parte 2 (Variante B) — Misma reestructuración con `IRepository`** | ✅ **Completado** |
| Parte 3 — DDD táctico (Value Objects, Domain Events) | ⏳ Pendiente |

**Decisión que queda para el proyecto real:** el proyecto avanza con **una sola** de las dos variantes hacia Semana 5+ (no se mantienen ambas en paralelo). Recomendación práctica: `IApplicationDbContext`, por ser el patrón más reconocible en ofertas laborales — pero ya tienes el código y el discurso de ambas para defenderlas en entrevista independientemente de cuál elijas llevar en el repo.

**Próxima sesión (Parte 3):** DDD táctico dentro de `TaskManager.Domain` — Value Object `Email` para `User`, invariantes en `TaskItem`, `Domain Event` `TaskCreatedEvent`.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

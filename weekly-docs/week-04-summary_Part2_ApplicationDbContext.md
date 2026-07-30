# Cierre Semana 4 (Parte 2 — Variante A) — Reestructuración física a Clean Architecture con `IApplicationDbContext`

> Continuación de `week-04-summary_Part1.md` (fundamentos + Clean vs VSA). Esta sesión es cirugía de código real: se crean los 4 proyectos físicos, se mueve cada archivo existente a su capa, y se resuelve la inversión de dependencias con `IApplicationDbContext` — la opción recomendada al cierre de la Parte 1 (patrón del template de Jason Taylor).
>
> La Variante B (`week-04-summary_Part2_Repository.md`) resuelve la misma reestructuración física pero con `IRepository` en lugar de `IApplicationDbContext`. Los pasos 1–3 de este archivo (crear proyectos, mover Domain, mover Application/Infrastructure/Api) son **idénticos** en ambas variantes — solo cambia la pieza de inversión de dependencias (Paso 4).

---

## Punto de partida vs punto de llegada

**Antes (fin de Semana 3):** un solo proyecto `TaskManager.Api` con todo adentro — entidades, Handlers de MediatR, `DbContext`, endpoints, middleware.

**Después (fin de esta sesión):**

```
TaskManager.sln
└── src/
    ├── TaskManager.Domain/          ← sin dependencias externas, ni EF Core
    ├── TaskManager.Application/     ← depende de Domain
    ├── TaskManager.Infrastructure/  ← depende de Application y Domain
    └── TaskManager.Api/             ← depende de Application y Infrastructure
```

---

## Paso 1 — Crear la solución y los 4 proyectos

```bash
# Desde la raíz del repo, en la rama week-04
dotnet new sln -n TaskManager

dotnet new classlib -n TaskManager.Domain         -o src/TaskManager.Domain
dotnet new classlib -n TaskManager.Application     -o src/TaskManager.Application
dotnet new classlib -n TaskManager.Infrastructure  -o src/TaskManager.Infrastructure

# El proyecto Api ya existe (era TaskManager.Api) — se mueve físicamente a src/
mkdir -p src/TaskManager.Api
git mv TaskManager.Api/* src/TaskManager.Api/
```

**Por qué `classlib` y no algo más:** Domain, Application e Infrastructure no son ejecutables ni exponen HTTP — son librerías de clases puras. Solo `TaskManager.Api` es el proyecto `web` que arranca Kestrel.

**Por qué una carpeta `src/`:** convención estándar (Jason Taylor template, Clean Architecture de Ardalis) que separa código fuente de otras carpetas de nivel raíz (`tests/`, `docs/`, `.github/`). No es obligatorio, pero es lo que vas a ver en el 90% de los repos de referencia — vale la pena acostumbrarse.

```bash
dotnet sln TaskManager.sln add src/TaskManager.Domain/TaskManager.Domain.csproj
dotnet sln TaskManager.sln add src/TaskManager.Application/TaskManager.Application.csproj
dotnet sln TaskManager.sln add src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj
dotnet sln TaskManager.sln add src/TaskManager.Api/TaskManager.Api.csproj
```

## Paso 2 — Registrar las referencias (aquí es donde el compilador empieza a exigir la Regla de Dependencia)

```bash
dotnet add src/TaskManager.Application reference src/TaskManager.Domain

dotnet add src/TaskManager.Infrastructure reference src/TaskManager.Application
dotnet add src/TaskManager.Infrastructure reference src/TaskManager.Domain

dotnet add src/TaskManager.Api reference src/TaskManager.Application
dotnet add src/TaskManager.Api reference src/TaskManager.Infrastructure
```

**Fíjate en lo que NO se hizo:** `TaskManager.Api` no referencia `TaskManager.Domain` directamente (le llega transitivamente vía Application/Infrastructure), y ningún proyecto referencia hacia afuera. Si alguna vez escribes `dotnet add src/TaskManager.Domain reference src/TaskManager.Application`, el compilador lo permite — la Regla de Dependencia no la fuerza `dotnet add`, la fuerzas tú al decidir qué referencias crear. Donde sí se vuelve imposible violarla por accidente es en el código: `Domain` no puede usar un tipo de `Application` porque ni siquiera tiene la referencia del `.csproj`.

## Paso 3 — Mover los archivos existentes a su capa

### → `TaskManager.Domain`

```
TaskManager.Domain/
└── Entities/
    ├── TaskItem.cs
    ├── Project.cs
    ├── User.cs
    └── TaskStatus.cs        ← el enum, como archivo propio
```

```csharp
// TaskManager.Domain/Entities/TaskItem.cs
namespace TaskManager.Domain.Entities;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
}
```

**La prueba de fuego, ejecutada de verdad:** abre `TaskManager.Domain.csproj` — no debe tener ningún `<PackageReference>` a `Microsoft.EntityFrameworkCore`. Ni un `[Key]`, ni un `[Required]`, ni un `[ForeignKey]` en las entidades. Esto ya estaba resuelto de facto desde la Semana 2 por haber elegido Fluent API sobre Data Annotations — ahora el compilador lo hace cumplir, no solo la disciplina.

**Detalle importante — `Enum TaskStatus` choca con `System.Threading.Tasks.TaskStatus`:** ya convivía con este problema desde Semana 1 (por eso el proyecto probablemente ya usa el enum propio calificado o un `using` explícito). Al moverlo a `TaskManager.Domain.Entities`, cualquier archivo en Application/Infrastructure que también tenga `using System.Threading.Tasks;` (casi todos, por `Task<T>`) va a necesitar el enum calificado o un alias:

```csharp
using TaskStatus = TaskManager.Domain.Entities.TaskStatus;
```

Anótalo — es un detalle real que vas a pisar al compilar, no una advertencia teórica.

### → `TaskManager.Application`

```
TaskManager.Application/
├── Common/
│   ├── PagedResult.cs
│   ├── Behaviors/
│   │   └── LoggingBehavior.cs
│   └── Interfaces/
│       └── IApplicationDbContext.cs      ← NUEVO — el corazón de esta variante
├── Features/
│   └── Task/
│       ├── GetAllTasks.cs
│       ├── GetTask.cs
│       ├── GetAllDetailedTasks.cs
│       ├── CreateTask.cs
│       ├── UpdateTask.cs
│       └── DeleteTask.cs
└── DependencyInjection.cs                 ← NUEVO
```

**Paquetes NuGet de este proyecto:**

```bash
dotnet add src/TaskManager.Application package MediatR --version 12.5.0
dotnet add src/TaskManager.Application package Microsoft.EntityFrameworkCore --version 8.0.*
dotnet add src/TaskManager.Application package Microsoft.Extensions.Options
```

**Por qué Application referencia el paquete `Microsoft.EntityFrameworkCore` (y no rompe la Regla de Dependencia):**

Esto sorprende la primera vez: dijimos que Domain no puede saber de EF Core, pero Application sí lo referencia. La distinción es sutil y es justo el tipo de cosa que un entrevistador senior pregunta:

- El paquete `Microsoft.EntityFrameworkCore` (el core, sin sufijo) define las **abstracciones** — `DbContext`, `DbSet<T>`, `IQueryable`. Son parte del lenguaje de persistencia agnóstico de proveedor.
- El paquete `Microsoft.EntityFrameworkCore.Sqlite` (o `.SqlServer`) es el **detalle de proveedor concreto** — ese sí queda exclusivamente en Infrastructure.

Application necesita `DbSet<TaskItem>` para poder declarar `IApplicationDbContext` con ese tipo de retorno. Es una zona gris aceptada en Clean Architecture "pragmático" (así lo hace el template de Jason Taylor). La alternativa purista —Application sin ninguna referencia a EF Core, exponiendo solo `IQueryable<TaskItem>` envuelto en tipos propios— existe, pero la mayoría de proyectos reales no la aplican porque el costo no compensa: perderías `Include`, `AsSplitQuery`, y buena parte del poder de LINQ-a-SQL que ya dominas desde Semana 2.

**`IApplicationDbContext.cs` — la interfaz que invierte la dependencia:**

```csharp
// TaskManager.Application/Common/Interfaces/IApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TaskItem> TaskItems { get; }
    DbSet<Project> Projects { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

**Los Handlers cambian su dependencia — de `TaskManagerDbContext` concreto a `IApplicationDbContext`:**

```csharp
// TaskManager.Application/Features/Task/CreateTask.cs (fragmento relevante)
using MediatR;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Features.Task;

public static class CreateTask
{
    public record Command(string Title, int ProjectId, int? AssignedToId) : IRequest<int>;

    public class Handler : IRequestHandler<Command, int>
    {
        private readonly IApplicationDbContext _db;   // ← ya no TaskManagerDbContext

        public Handler(IApplicationDbContext db) => _db = db;

        public async Task<int> Handle(Command request, CancellationToken ct)
        {
            var task = new TaskItem
            {
                Title = request.Title,
                ProjectId = request.ProjectId,
                AssignedToId = request.AssignedToId,
                Status = TaskManager.Domain.Entities.TaskStatus.Todo
            };

            _db.TaskItems.Add(task);
            await _db.SaveChangesAsync(ct);

            return task.Id;
        }
    }
}
```

Repite el mismo cambio (`TaskManagerDbContext db` → `IApplicationDbContext db` en el constructor) en `GetAllTasks`, `GetTask`, `GetAllDetailedTasks`, `UpdateTask` y `DeleteTask`. La lógica interna de cada Handler (proyecciones `Select`, `AsNoTracking`, paginación) **no cambia nada** — solo cambia de dónde viene la dependencia. Eso es exactamente lo que debe pasar cuando aplicas DIP correctamente: el código de negocio es indiferente a si el `DbContext` real vive en Infrastructure.

**`Common/PagedResult.cs` y `Common/Behaviors/LoggingBehavior.cs`** se mueven tal cual — no dependen de nada de EF Core ni de HTTP, así que el `using MediatR;` y `using System.Diagnostics;` son sus únicas dependencias externas, ambas ya presentes en Application.

**`DependencyInjection.cs` — el patrón de "un método de extensión por capa":**

```csharp
// TaskManager.Application/DependencyInjection.cs
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Common.Behaviors;

namespace TaskManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
```

**Por qué este patrón (y no seguir registrando todo en `Program.cs`):** cada capa expone su propio "cómo me registro" y `Program.cs` en Api solo orquesta llamadas de una línea. Si Application crece (más Behaviors, FluentValidation en el futuro), el cambio vive en `Application/DependencyInjection.cs`, no en un `Program.cs` que se vuelve una lista interminable de `AddX()`. Es composición explícita por capa — mismo espíritu que `ApplyConfigurationsFromAssembly` de Semana 2: cada pieza sabe registrarse a sí misma.

### → `TaskManager.Infrastructure`

```
TaskManager.Infrastructure/
├── Persistence/
│   ├── TaskManagerDbContext.cs             ← ahora implementa IApplicationDbContext
│   └── Configurations/
│       ├── TaskItemConfiguration.cs
│       ├── ProjectConfiguration.cs
│       └── UserConfiguration.cs
├── Migrations/
│   ├── 20260620202701_InitialCreate.cs
│   └── TaskManagerDbContextModelSnapshot.cs
└── DependencyInjection.cs
```

```bash
dotnet add src/TaskManager.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.*
dotnet add src/TaskManager.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.0.*
```

```csharp
// TaskManager.Infrastructure/Persistence/TaskManagerDbContext.cs
using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Infrastructure.Persistence;

public sealed class TaskManagerDbContext : DbContext, IApplicationDbContext
{
    public TaskManagerDbContext(DbContextOptions<TaskManagerDbContext> options)
        : base(options) { }

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskManagerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

Nota el `=> Set<TaskItem>()` en vez de un `{ get; set; }` con backing field: es la forma moderna recomendada por EF Core 8 para exponer `DbSet<T>`, evita inicializarlos manualmente y es compatible con `IApplicationDbContext` sin fricción (la interfaz solo pide un getter).

Las clases de `Configurations/` (`TaskItemConfiguration`, etc.) **se mueven sin cambios de lógica** — solo actualiza el namespace a `TaskManager.Infrastructure.Persistence.Configurations` y el `using TaskManager.Domain.Entities;` para referenciar `TaskItem`/`Project`/`User` desde su nueva ubicación.

**Migrations — el detalle que rompe builds si no se hace bien:**

Las migrations autogeneradas referencian el `DbContext` y las entidades por su namespace completo en el código generado. Al mover el proyecto:

```bash
# Borra las migrations viejas (referencian namespaces del proyecto Api anterior)
rm -rf src/TaskManager.Infrastructure/Migrations/*

# Regenera desde cero contra la nueva ubicación
dotnet ef migrations add InitialCreate \
    --project src/TaskManager.Infrastructure \
    --startup-project src/TaskManager.Api \
    --output-dir Migrations

dotnet ef database update \
    --project src/TaskManager.Infrastructure \
    --startup-project src/TaskManager.Api
```

**Por qué `--startup-project` es obligatorio ahora:** antes había un solo proyecto, así que `dotnet ef` sabía dónde estaba el `Program.cs` con la configuración de conexión y DI. Ahora el `DbContext` vive en Infrastructure, pero la app arrancable (con `appsettings.json`, connection string, y `IServiceProvider` configurado) es Api. `dotnet ef` necesita las dos rutas: dónde está el `DbContext` (`--project`) y desde dónde arrancar la app para resolver la connection string (`--startup-project`). Olvidar `--startup-project` es el error más común al mover a Clean Architecture — vale la pena anotarlo tal cual para entrevista.

```csharp
// TaskManager.Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Infrastructure.Persistence;

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

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<TaskManagerDbContext>());

        return services;
    }
}
```

**El `AddScoped<IApplicationDbContext>(sp => ...)` es la línea que materializa DIP en runtime:** el contenedor de DI resuelve `TaskManagerDbContext` (concreto, Scoped, registrado por `AddDbContext`) y lo entrega disfrazado de `IApplicationDbContext` cada vez que un Handler de Application lo pida. Application nunca ve `TaskManagerDbContext` — solo la abstracción que ella misma definió.

### → `TaskManager.Api`

```
TaskManager.Api/
├── Program.cs                         ← ahora orquesta AddApplicationServices + AddInfrastructureServices
├── TaskEndpoints.cs                   ← sin cambios de lógica, solo namespace
├── CreateTaskRequest.cs
├── UpdateTaskRequest.cs
├── RequestLoggingMiddleware.cs
├── ExceptionHandlingMiddleware.cs
└── TaskManagerOptions.cs              ← ver nota abajo
```

```csharp
// TaskManager.Api/Program.cs (fragmento relevante, fase de servicios)
using TaskManager.Application;
using TaskManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.Configure<TaskManagerOptions>(
    builder.Configuration.GetSection("TaskManager"));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapTaskEndpoints();

app.Run();
```

**Program.cs quedó casi vacío de detalle técnico — y eso es la señal de que la reestructuración salió bien.** Antes tenía `AddDbContext`, configuraciones de EF Core, registro de MediatR, todo mezclado. Ahora son dos llamadas de una línea. Api pasó de "saber cómo se arma todo" a solo "componer lo que cada capa ya sabe armar de sí misma".

**Nota sobre `TaskManagerOptions` — un archivo que no es obvio dónde va:**

`TaskManagerOptions` (con `DefaultPageSize`) es un caso interesante porque **se usa en dos lugares distintos con necesidades distintas**:

- El **binding** desde `appsettings.json` (`builder.Services.Configure<TaskManagerOptions>(...)`) es infraestructura de configuración de ASP.NET Core → eso sí es legítimamente de Api.
- Pero el **Handler** `GetAllTasks` en Application necesita leer `options.Value.DefaultPageSize` — y Application no puede depender de Api (violaría la Regla de Dependencia en la dirección contraria).

**Solución:** la clase `TaskManagerOptions` (el POCO simple, sin nada de `IConfiguration` ni atributos de binding) se mueve a `TaskManager.Application/Common/TaskManagerOptions.cs`. Api sigue haciendo el `Configure<TaskManagerOptions>` (porque ahí vive `IConfiguration`), pero el *tipo* en sí vive en Application porque es Application quien lo consume vía `IOptions<TaskManagerOptions>`. Api solo lo puebla; no le pertenece.

Esto es exactamente el mismo patrón que `IApplicationDbContext`: **quien consume el contrato define el contrato**, no quien lo provee.

---

## Paso 4 — Verificación de la Regla de Dependencia

```bash
dotnet build
```

Si compila, las referencias de proyecto ya fuerzan la dirección correcta. Chequeo manual rápido (antes de tener NetArchTest en Semana 5):

```bash
# Domain no debe tener ningún PackageReference de EF Core
cat src/TaskManager.Domain/TaskManager.Domain.csproj | grep -i entityframework
# → debe salir vacío
```

Si sale vacío, la Regla de Dependencia se sostiene. En Semana 5 esto se automatiza con un test de arquitectura (`NetArchTest`) en vez de un `grep` manual — pero por ahora, con 4 proyectos, el chequeo a mano es perfectamente razonable.

---

## Trade-off real de esta variante (`IApplicationDbContext`)

**Qué ganas:**
- Los Handlers conservan **todo el poder de LINQ y EF Core** — `Select`, `Include`, `AsNoTracking`, `AsSplitQuery`, todo lo de Semana 2 sigue funcionando exactamente igual, solo detrás de una interfaz.
- Cero código nuevo de "traducción" — no hay una capa de Repository que envolver y mantener.
- Es el patrón del template más usado en la industria (`jasontaylordev/CleanArchitecture`), lo cual pesa en una entrevista: es lo que el entrevistador probablemente ya conoce.

**Qué pierdes / cuándo duele:**
- **No es un aislamiento real de EF Core.** Application sigue "pensando en LINQ" — si mañana cambias de EF Core a Dapper, tienes que reescribir cada Handler, no solo una capa de Infrastructure. La abstracción es de *quién provee el DbContext*, no de *qué tecnología de acceso a datos usas*.
- Application referencia el paquete `Microsoft.EntityFrameworkCore` — para un purista, eso ya es una fuga de un detalle de infraestructura hacia adentro, aunque sea solo el core agnóstico de proveedor.
- Los Handlers pueden crecer con lógica de query compleja repetida entre Handlers (ej. la misma proyección en `GetAllTasks` y `GetAllDetailedTasks`) sin un lugar natural para centralizarla — un Repository sí te daría ese lugar.

La Variante B (`IRepository`, en el otro archivo) ataca exactamente esos dos últimos puntos, a cambio de más ceremonia. Contrástalos ahí.

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"¿Cómo resolviste el DIP entre Application e Infrastructure en tu proyecto?"**

Con `IApplicationDbContext`: una interfaz que vive en Application y expone los `DbSet<T>` que los Handlers necesitan. `TaskManagerDbContext`, en Infrastructure, la implementa. `Program.cs`, en Api, es el único punto que conoce ambos tipos y hace el registro concreto en el contenedor de DI.

**"¿Por qué Application referencia el paquete de EF Core si dijiste que Domain no puede?"**

Porque `Microsoft.EntityFrameworkCore` (sin sufijo de proveedor) son las abstracciones agnósticas — `DbContext`, `DbSet<T>`. El proveedor concreto (`.Sqlite`, `.SqlServer`) es el detalle volátil, y ese sí queda exclusivamente en Infrastructure. Es una zona gris aceptada, no una violación de la regla.

**"¿Qué comando de EF Core cambia al mover a Clean Architecture y por qué?"**

`dotnet ef migrations add` y `database update` necesitan `--project` (dónde está el `DbContext`) y `--startup-project` (desde dónde arranca la app y resuelve la connection string), porque ahora son proyectos distintos. Con un solo proyecto, `dotnet ef` los infería solo.

**"¿Dónde pondrías una clase de opciones (`IOptions<T>`) que necesita tanto Application como Api?"**

El POCO simple va en Application (quien lo consume), el binding a `IConfiguration` (`Configure<T>`) se queda en Api (quien tiene acceso a la configuración real). El principio es: quien consume el contrato lo define; quien lo provee, se adapta.

---

## Estado de la Semana 4

| Bloque | Estado |
|--------|--------|
| Parte 1 — Fundamentos + Clean vs VSA (teoría) | ✅ Completado |
| **Parte 2 (Variante A) — Reestructuración física con `IApplicationDbContext`** | ✅ **Completado** |
| Parte 2 (Variante B) — Misma reestructuración con `IRepository` | Ver archivo aparte |
| Parte 3 — DDD táctico (Value Objects, Domain Events) | ⏳ Pendiente |

**Próxima sesión (Parte 3):** con las 4 capas ya separadas, se introduce DDD táctico dentro de `TaskManager.Domain` — un Value Object `Email` para `User`, invariantes en `TaskItem` (constructor que valida en vez de propiedades públicas mutables sin control), y un primer `Domain Event` (`TaskCreatedEvent`) que sienta la base para el microservicio de notificaciones de Semana 9.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

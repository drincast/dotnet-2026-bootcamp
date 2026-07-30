# Cierre Semana 4 (Parte 2) — Reestructuración física a Clean Architecture — Lo que construiste y debes saber defender

> Continuación de `week-04-summary_Part1.md` (fundamentos + Clean vs VSA). Esta sesión fue cirugía de código real: los 4 proyectos físicos, el movimiento de cada archivo a su capa, y las dos variantes de inversión de dependencias documentadas en detalle en `week-04-summary_Part2_ApplicationDbContext.md` y `week-04-summary_Part2_Repository.md`. Este archivo resume las decisiones y cierra con los problemas reales de migración de base de datos que aparecieron al ejecutar la reestructuración contra la BD existente de Semana 2.

---

## Lo que construiste

```
TaskManager.sln
└── src/
    ├── TaskManager.Domain/          ← entidades puras, cero dependencias externas
    │   └── Entities/
    │       ├── TaskItem.cs
    │       ├── Project.cs
    │       ├── User.cs
    │       └── TaskStatus.cs
    ├── TaskManager.Application/     ← depende de Domain
    │   ├── Common/
    │   │   ├── PagedResult.cs
    │   │   ├── TaskManagerOptions.cs        ← movido aquí, no se quedó en Api
    │   │   ├── Behaviors/LoggingBehavior.cs
    │   │   └── Interfaces/IApplicationDbContext.cs
    │   ├── Features/Task/*.cs               ← los 6 Commands/Queries, sin cambios de lógica
    │   └── DependencyInjection.cs           ← AddApplicationServices()
    ├── TaskManager.Infrastructure/   ← depende de Application y Domain
    │   ├── Persistence/
    │   │   ├── TaskManagerDbContext.cs      ← implementa IApplicationDbContext
    │   │   └── Configurations/*.cs
    │   ├── Migrations/                       ← regeneradas desde cero, consolidadas
    │   └── DependencyInjection.cs           ← AddInfrastructureServices()
    └── TaskManager.Api/              ← depende de Application y Infrastructure
        ├── Program.cs                        ← casi vacío de detalle técnico
        ├── TaskEndpoints.cs
        ├── CreateTaskRequest.cs / UpdateTaskRequest.cs
        └── RequestLoggingMiddleware.cs / ExceptionHandlingMiddleware.cs
```

Se implementaron y documentaron **las dos variantes** de la pieza de inversión de dependencias (DIP) entre Application e Infrastructure:

- **`IApplicationDbContext`** — la que quedó corriendo en el proyecto real. Los Handlers conservan LINQ completo (`Select`, `AsNoTracking`, proyecciones) detrás de una interfaz que expone `DbSet<T>`.
- **`IRepository`** (`ITaskRepository`) — documentada en paralelo como alternativa. Los Handlers pasan a pedir datos por intención de negocio (`GetPagedAsync`, `GetDetailedByIdAsync`) sin saber que existe EF Core.

---

## Decisiones técnicas que tomaste y por qué

**`IApplicationDbContext` sobre `IRepository` para el proyecto real**

Conserva todo el poder de LINQ/EF Core de Semanas 2-3 sin reescribir Handlers, y es el patrón del template más adoptado en ofertas laborales (`jasontaylordev/CleanArchitecture`). El trade-off aceptado: Application referencia el paquete `Microsoft.EntityFrameworkCore` (las abstracciones agnósticas de proveedor, no el driver de SQLite/SQL Server) — zona gris consciente, no una violación real de la Regla de Dependencia.

**`TaskManagerOptions` vive en Application, no en Api**

El POCO simple se movió a `Application/Common/` porque quien lo *consume* (`GetAllTasks` Handler, vía `IOptions<TaskManagerOptions>`) vive en Application. Api solo hace el *binding* contra `IConfiguration` (`Configure<TaskManagerOptions>(...)`) porque solo Api tiene acceso directo a la configuración real. Regla general aplicada: **quien consume el contrato lo define; quien lo provee, se adapta** — la misma lógica de `IApplicationDbContext`.

**Migrations regeneradas desde cero en vez de editar las viejas**

Al mover el `DbContext` a Infrastructure, las migrations autogeneradas de Semana 2 quedaron con namespaces del proyecto anterior. Se borraron y se regeneró una `InitialCreate` consolidada contra la nueva ubicación, en vez de intentar parchear los archivos generados a mano.

**`--startup-project` obligatorio en todo comando `dotnet ef`**

Con un solo proyecto, `dotnet ef` inferías dónde estaba todo. Con 4 proyectos, necesita `--project` (dónde vive el `DbContext`, en Infrastructure) y `--startup-project` (desde dónde arranca la app y resuelve la connection string, en Api) en cada comando.

---

## Problema real de la sesión: la base de datos existente no sabía de la migration nueva

### Sensación inicial: "¿por qué falla si la base ya tiene la estructura correcta?"

Al mover `TaskManager.Api/*` a `src/TaskManager.Api/` con `git mv`, el archivo físico `taskmanager.db` **viajó junto** — con sus tablas, sus datos, y su propio `__EFMigrationsHistory` ya poblado desde Semana 2. Al regenerar `InitialCreate` con un Id de timestamp nuevo y correr `database update`, EF Core no encontró ese Id nuevo en el historial, así que intentó ejecutar el `CREATE TABLE` completo — contra una base que físicamente ya tenía esas tablas. De ahí el error `table "Projects" already exists`.

**La causa raíz no era una base nueva sin crear — era una base vieja con historial desincronizado del código nuevo.**

### La decisión: conservar los datos (Opción B) en vez de recrear la BD desde cero

Como el contenido de la migration nueva era idéntico en estructura a la aplicada en Semana 2 (mismas tablas, mismos índices), se optó por **sincronizar el historial manualmente** en vez de borrar la base y perder los datos de prueba:

```sql
-- Se eliminaron los registros de migrations viejas sin archivo .cs correspondiente
DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20260620202701_InitialCreate';
DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20260626223957_RemoveCreatedAtSqlDefault';

-- Se insertó el registro correspondiente a la migration InitialCreate consolidada nueva
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('<MigrationId real generado>', '8.0.10');
```

**Por qué es seguro hacerlo así:** `__EFMigrationsHistory` es solo un libro de contabilidad — EF Core lo consulta para decidir qué SQL falta por ejecutar, no valida que el SQL realmente se haya corrido. Si el schema físico ya coincide con lo que la migration describiría, "marcarla como aplicada" sin ejecutarla es seguro. Es peligroso únicamente si el schema real y el que describe la migration nueva **no coinciden** — ahí sí se estaría mintiendo al historial.

### El hallazgo colateral: `ProjectConfiguration` tenía un default roto que el código no reflejaba

Revisar el historial (`RemoveCreatedAtSqlDefault`) destapó que el fix de `GETUTCDATE()` — aplicado a `TaskItemConfiguration` en Semana 2 — **nunca se replicó en el código de `ProjectConfiguration`**, aunque sí se había ejecutado alguna vez contra la base física. El código fuente movido a Infrastructure estaba desactualizado respecto a la base real.

**Corrección aplicada:**

```csharp
// ProjectConfiguration.cs — se eliminó
.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()")
```

```csharp
// Project.cs (Domain) — ya lo asigna en C#, agnóstico de proveedor
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

Y se regeneró la migration `InitialCreate` una segunda vez para que el snapshot generado reflejara el código corregido, seguido del mismo procedimiento de sincronización manual del historial.

**Lección general:** cuando el código y la base de datos tienen historias divergentes (por ediciones directas a la BD, o por reestructuraciones como esta), el historial de migrations (`__EFMigrationsHistory`) es la fuente de verdad de *qué se ejecutó*, pero no garantiza que el código fuente actual coincida con lo que se ejecutó. Vale la pena, tras una reestructuración de este tipo, comparar explícitamente el `.schema` real contra lo que el código de `Configurations/` describiría hoy.

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"Moviste el DbContext a otro proyecto en una reestructuración a Clean Architecture — ¿qué pasó con la base de datos existente?"**

> El archivo físico de la BD no se mueve solo — viaja si lo mueves manualmente, y su `__EFMigrationsHistory` queda con Ids de migrations que ya no tienen archivo `.cs` correspondiente (porque las regeneraste). Hay dos caminos: recrear la base desde cero (limpio, pierde datos) o sincronizar el historial manualmente insertando el Id de la migration nueva sin ejecutar su SQL, siempre que el schema real coincida con lo que esa migration describiría.

**"¿Qué garantiza `__EFMigrationsHistory` y qué no garantiza?"**

> Garantiza que EF Core no vuelva a intentar ejecutar una migration cuyo Id ya está registrado. No garantiza que el schema físico coincida con lo que el código de `Configurations/` describe hoy — son dos fuentes que pueden divergir si se edita la BD a mano o se regenera código sin correr una migration real.

**"¿Por qué `dotnet ef` necesita `--startup-project` después de separar en capas?"**

> Porque el `DbContext` (en Infrastructure) y la app arrancable con `IConfiguration` resuelta (en Api) ya son proyectos distintos. `--project` le dice dónde está el `DbContext`; `--startup-project`, desde dónde resolver la connection string y el `IServiceProvider`.

**"¿Cómo resuelves la dependencia circular entre Application e Infrastructure?"**

> "No existe dependencia circular si se respeta la Regla de Dependencia. Application define interfaces; Infrastructure las implementa. Application nunca referencia a Infrastructure. La única 'flecha' que parece ir de Application hacia Infrastructure es la implementación concreta, que ocurre en runtime a través del contenedor de DI.
>
> Si hubiera una dependencia circular, sería señal de que se violó la regla: probablemente Application está llamando directamente a clases concretas de Infrastructure en lugar de interfaces de Application. La solución es extraer la interfaz a Application y hacer que Infrastructure implemente."

**"¿Cómo testeas el dominio si no tiene acceso a la base de datos?"**

> "Exacto — ese es el punto. Domain no necesita la base de datos para nada. Para testar una entidad `TaskItem` con una regla de negocio como 'una tarea no puede marcarse Completed si tiene subtareas pendientes', creo un test que:
>
> ```csharp
> [Fact]
> public void Complete_WithPendingSubtasks_Throws()
> {
>     var task = new TaskItem { Title = "Parent", Status = TaskStatus.InProgress };
>     task.SubTasks.Add(new TaskItem { Status = TaskStatus.Pending });
>
>     // Llamar al método de dominio directamente — cero mocks
>     var act = () => task.MarkCompleted();
>
>     act.Should().Throw<DomainException>();
> }
> ```
>
> El test vive en un proyecto de tests que referencia a Domain. No necesita DbContext, no necesita contenedor DI, no necesita base de datos. Se ejecuta en milisegundos. Esto es lo que Clean Architecture permite: testeabilidad real de lógica de negocio."

**"¿Clean Architecture o Repository Pattern — cuál es la diferencia y cuándo usas cada uno?"**

> "Son ortogonales — responden preguntas distintas. Clean Architecture responde: ¿qué assembly contiene qué y quién puede depender de quién? Repository Pattern responde: ¿cómo abstracto el acceso a datos? Clean Architecture puede usar Repository Pattern dentro de Application (definir `ITaskRepository : IApplicationDbContext`), o puede usar `IApplicationDbContext` directamente como abstracción de datos, que es lo que hace el template de Jason Taylor.
>
> Repository Pattern agrega una capa de indirección. Ventaja: testear un Handler sin DbContext real es más fácil (mockeas el repository en lugar del DbContext). Desventaja: en la práctica, EF Core ya es una abstracción, y envolverla en un repository que re-expone los mismos métodos (`GetByIdAsync`, `AddAsync`) es frecuentemente redundante.
>
> Yo lo uso cuando la operación de dominio es más compleja que un CRUD directo — cuando necesito agregar lógica de negocio dentro del repository (ej. 'GetWithProjectAndTasks' que hace Include + AsNoTracking + proyección), o cuando el mismo caso de uso necesita acceder a múltiples DbSets y quiero que eso viva en un lugar con nombre semántico. Para un CRUD simple, `IApplicationDbContext` directo es suficiente."

---

## Estado de la Semana 4

| Bloque | Estado |
|--------|--------|
| Parte 1 — Fundamentos arquitectónicos + Clean Architecture vs VSA (teoría y decisión) | ✅ Completado |
| **Parte 2 — Reestructuración física (Domain/Application/Infrastructure/Api), ambas variantes de DIP documentadas, migración de BD resuelta** | ✅ **Completado** |
| Parte 3 — DDD táctico (Value Objects, invariantes, Domain Events) + cierre | ⏳ Pendiente |

### Limpieza pendiente (no bloqueante, arrastrada para cuando se retome)

- Confirmar que `.schema` de todas las tablas (no solo `Projects`) no tenga otros defaults de SQL Server colados desde Semana 2.
- Decidir formalmente si el proyecto avanza con `IApplicationDbContext` o `IRepository` hacia Semana 5+ (recomendación dada: `IApplicationDbContext`, ya documentada en `week-04-summary_Part2_ApplicationDbContext.md`).

**Próxima sesión — Parte 3:** DDD táctico dentro de `TaskManager.Domain` — un Value Object `Email` para `User`, invariantes en `TaskItem` (constructor que valida en vez de propiedades públicas sin control), y un primer `Domain Event` (`TaskCreatedEvent`), sentando la base del microservicio de notificaciones de Semana 9.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

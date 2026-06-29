# Cierre Semana 2 (Parte 2.1) — Relaciones, N+1 y Proyecciones — Lo que profundizaste y validaste

> Continuación de `week-02-summary_Part2.md`. Esta sesión cubre los temas de relaciones en queries que el plan listaba como pendientes del bloque EF Core (los más preguntados en entrevista), más la aplicación de los 2 pendientes de limpieza y la validación del endpoint `/tasks/detailed`.

---

## Lo que cerramos en esta sesión

- **Los 2 pendientes de limpieza** quedaron aplicados y validados sobre el código real.
- **Sesión de relaciones + N+1 + proyecciones** — el bloque de EF Core más preguntado en entrevista senior.
- **Endpoint `/tasks/detailed` con `TaskDetailDto`** revisado, corregido y coherente.

---

## Pendientes de limpieza — aplicados

### 1. `GetAll` limpio
- Se eliminó el total falso: ahora solo se reporta `TotalItems = totalItems` (conteo real de la BD). Desapareció `TotalItems2`.
- El array dummy de 11 tareas quedó aislado en la región de utilería (`TaskItemsListDummy`), fuera de `GetAll`, así que ya no contamina la respuesta.
- **Higiene pendiente (no bloqueante):** borrar `TaskItemsListDummy()` (código muerto) y corregir el comentario obsoleto "retorna lista harcodeada" en `GetAll`.

### 2. `Enum.TryParse` en `Update` — 400 en lugar de 500
- `Enum.Parse` lanzaba excepción ante un estado inexistente → el middleware lo convertía en `500`. Pero un dato inválido es **culpa del cliente**, no del servidor.
- Ahora con `Enum.TryParse<TaskStatus>(request.Status, ignoreCase: true, out var status)` se devuelve `400 Bad Request` con mensaje claro.
- **Detalle senior:** se valida ANTES de mutar la entidad. El `TryParse` va antes de tocar `taskItem.Title`, para no dejar la entidad con cambios parciales bajo tracking.

---

## Concepto clave: Round-trip

Un **round-trip** es un viaje completo de ida y vuelta entre la app y la BD: la app manda la query → la BD la procesa → devuelve el resultado. Ese ciclo entero es **un** round-trip.

Lo costoso no es solo que la BD ejecute la query, sino la **latencia de red** de ir y volver. En local con SQLite el costo es casi cero (la BD es un archivo en el mismo proceso). En producción, API y SQL Server suelen estar en máquinas distintas y cada round-trip paga el viaje por red antes de que la BD haga nada útil.

**Intuición clave:** 10 queries que traen 1 fila cada una son casi siempre más lentas que 1 query que trae 10 filas, aunque el volumen de datos sea idéntico — porque pagas la latencia 10 veces en lugar de 1. (Analogía: pedir 10 platos en un pedido vs hacer 10 pedidos separados al mismo restaurante.)

Aplicado al proyecto:
- `GetAll`: `CountAsync` + query de items = **2 round-trips**. Aceptable, es lo estándar.
- N+1 (`foreach` accediendo a `t.Project`): **101 round-trips** para 100 tareas. Inaceptable.
- `Include` o proyección con `Select`: **1 round-trip** (un JOIN). La cura.

---

## El problema N+1

Listar tareas con el nombre de su proyecto, de forma ingenua:

```csharp
var tasks = await db.TaskItems.AsNoTracking().ToListAsync(ct);
foreach (var t in tasks)
    Console.WriteLine($"{t.Title} - {t.Project?.Name}"); // 1 query por cada tarea
```

Con lazy loading activo esto ejecuta **1** query (las N tareas) + **N** queries (un `Project` por tarea). 100 tareas = **101 queries**.

**Cómo detectarlo:** en los logs de EF Core, ver la misma query repetida en bucle (`SELECT ... FROM Projects WHERE Id = @p`). En entrevista esa es la respuesta esperada: se detecta en los logs de SQL o con herramientas como MiniProfiler.

**Detalle del proyecto:** NO hay lazy loading activado (sin paquete `Proxies`, sin navegaciones `virtual`). Por eso `t.Project` sería `null` en vez de disparar query oculta. Esto es **mejor** — lazy loading es la causa #1 de N+1 accidentales. Respuesta senior a "¿activarías lazy loading?": **no por defecto**, porque convierte accesos a propiedades en queries invisibles.

---

## Las tres formas de traer relaciones

### a) `Include` — eager loading
```csharp
var tasks = await db.TaskItems
    .AsNoTracking()
    .Include(t => t.Project)
    .Include(t => t.AssignedTo)
    .ToListAsync(ct);
```
Genera **un** `LEFT JOIN` por `Include`. Una sola query, sin N+1. Trae las entidades completas.

**Riesgo — explosión cartesiana:** al incluir **colecciones** (ej. `Project.Include(p => p.Tasks)`), el JOIN multiplica filas — un proyecto con 50 tareas devuelve 50 filas con los datos del proyecto repetidos. EF Core deduplica en memoria, pero la data redundante ya viajó por red.

### b) Split Queries — solución a la explosión cartesiana
```csharp
var projects = await db.Projects
    .AsNoTracking()
    .Include(p => p.Tasks)
    .AsSplitQuery()
    .ToListAsync(ct);
```
En vez de un JOIN gigante, dispara **una query por nivel de navegación**.

| | Single Query (JOIN) | Split Query |
|---|---|---|
| Round-trips | 1 | N (uno por colección) |
| Datos duplicados en red | Sí (explosión cartesiana) | No |
| Consistencia | Atómica (un snapshot) | Posible inconsistencia con escrituras concurrentes |

**Regla:** `Include` de una colección grande → considerar `AsSplitQuery`. `Include` de relaciones 1:1 o N:1 (como `Project`/`User` desde `TaskItem`) → single query está perfecto, no multiplica.

### c) Proyección con `Select` — lo correcto para APIs de lectura
```csharp
var tasks = await db.TaskItems
    .AsNoTracking()
    .OrderBy(t => t.Id)
    .Select(t => new TaskListItemDto
    {
        Id = t.Id,
        Title = t.Title,
        Done = t.Status == TaskStatus.Done,
        ProjectName = t.Project!.Name,    // EF traduce a JOIN solo para esta columna
        AssignedTo  = t.AssignedTo != null ? t.AssignedToName :  null // AssignedTo es opcional → maneja el null de verdad
    })
    .ToListAsync(ct);
```
**Clave:** con proyección NO se necesita `Include`. Al navegar `t.Project.Name` dentro de un `Select`, EF Core genera el JOIN solo para la columna pedida — no trae la entidad completa. Estrictamente mejor que `Include` para lectura: menos columnas en red, sin tracking relevante, sin ciclos de serialización, sin N+1.

---

## Jerarquía de decisión (respuesta de entrevista)

> **"¿Include o Select?"**
> Para lectura en una API, casi siempre `Select` con proyección a DTO: traes solo lo necesario, desacoplas el contrato del esquema y evitas ciclos de serialización. Uso `Include` cuando necesito la entidad completa con su grafo para **modificarla** (escritura). Si ese `Include` trae una colección grande, le pongo `AsSplitQuery` para evitar la explosión cartesiana. Lazy loading no lo activo: convierte accesos a propiedades en queries invisibles y es la causa #1 de N+1 accidentales.

---

## Dudas resueltas en la sesión

### ¿Cómo ver los logs de EF Core?
1. **Output window de Visual Studio** (Debug) — rápido pero con ruido.
2. **Subir nivel en `appsettings.Development.json`:**
   ```json
   { "Logging": { "LogLevel": {
       "Microsoft.EntityFrameworkCore.Database.Command": "Information" } } }
   ```
3. **`LogTo` + `EnableSensitiveDataLogging` (solo dev):**
   ```csharp
   options.UseSqlite(connectionString)
          .EnableSensitiveDataLogging()   // NUNCA en producción: filtra datos reales
          .LogTo(Console.WriteLine, LogLevel.Information);
   ```
   Por defecto EF Core oculta los valores de parámetros (`@p0`). Para cazar N+1 conviene ver los valores reales — pero `EnableSensitiveDataLogging` solo en Development.

### ¿Lazy loading es global o por consulta?
Lazy loading es un **interruptor global** (`UseLazyLoadingProxies()` + navegaciones `virtual`) — todo o nada, no por query. Lo que SÍ es puntual:

| Técnica | Granularidad | Query |
|---|---|---|
| Lazy loading | Global (todo o nada) | Oculta — peligrosa |
| Eager (`Include`) | Por query | Visible |
| **Explicit (`.Load`)** | Por navegación puntual | Visible |
| Proyección (`Select`) | Por query | Visible |

Lo que buscaba la duda ("cargar según necesidad") se llama **explicit loading**, no lazy:
```csharp
var task = await db.TaskItems.FirstAsync(t => t.Id == id, ct);
await db.Entry(task).Reference(t => t.Project).LoadAsync(ct);   // referencia
// await db.Entry(project).Collection(p => p.Tasks).LoadAsync(ct); // colección
```
Cargas la relación a mano cuando la necesitas, con la query visible y bajo tu control.

### Con Split Query y 10 proyectos, ¿cuántas queries?
**2 queries totales**, no 20. Una por nivel de navegación, no una por entidad:
```sql
SELECT * FROM Projects;                                  -- Query 1: todos los proyectos
SELECT * FROM TaskItems WHERE ProjectId IN (1,2,...,10); -- Query 2: todas las tareas
```
EF Core junta todos los IDs y trae las tareas con un solo `WHERE ... IN (...)`, luego reparte en memoria. Da igual si son 10 o 10.000 proyectos: siguen siendo 2 queries. (Una query por proyecto = 11 round-trips = eso SÍ sería N+1.) Con 3 niveles (proyecto → tareas → comentarios) serían 3 queries. Siempre **una por nivel**.

---

## Validación del endpoint `/tasks/detailed` + `TaskDetailDto`

### El record (correcto, sin cambios)
```csharp
record TaskDetailDto(int Id, string Title, string? Description, TaskStatus Status,
    DateTime CreatedAt, int ProjectId, string ProjectName,
    int? AssignedToId, string? AssignedToName);
```
Modela bien la nullability: `ProjectName` no-null (FK obligatoria), `AssignedToName` nullable (relación opcional por `OnDelete.SetNull`).

### El bug que se corrigió: `!` en una navegación opcional
Versión inicial (problemática):
```csharp
t.AssignedTo!.Name   // AFIRMA que nunca es null — mentira: AssignedToId es nullable
```
El `!` (null-forgiving) solo apaga la advertencia del compilador; **no cambia nada en runtime**. Como el destino es `string?`, ni siquiera daba warning, lo que lo hacía traicionero. Funcionaba "de casualidad" porque la proyección se traduce a SQL (`LEFT JOIN` → `NULL` → `null` materializado). Si esa expresión se evaluara en memoria, sería `NullReferenceException`.

Versión corregida (aplicada):
```csharp
t.Project!.Name,                                   // FK obligatoria → ! defendible
t.AssignedTo != null ? t.AssignedTo.Name : null    // opcional → null explícito
```
**Asimetría intencional:** `!` defendible en `Project` (FK obligatoria por `DeleteBehavior.Cascade`), NO defendible en `AssignedTo` (opcional por diseño). Ahora query y record dicen lo mismo.

### Lo que quedó bien (checklist)
- ✅ Proyección con `Select` directo al DTO, sin `Include` innecesario
- ✅ `AsNoTracking()` (es lectura)
- ✅ `OrderBy` antes de `Skip/Take`
- ✅ DTO nombrado (no anónimo) → testeable de cara a Semana 5
- ✅ `CancellationToken` propagado
- ✅ `TotalItems` corregido (ya no `TotalItems2`)
- ✅ Null de `AssignedTo` manejado explícitamente

### Higiene menor pendiente (no bloqueante)
- Borrar `TaskItemsListDummy()` (código muerto en región de utilería)
- Corregir comentario obsoleto "retorna lista harcodeada" en `GetAll`
- **Anotado para Semana 3:** la lógica de paginación está duplicada entre `GetAll` y `GetTaskDetailed`. Con MediatR encuentra su lugar natural (un `PagedResult<T>` o behavior reutilizable). Mencionarlo en entrevista como decisión diferida demuestra criterio.

---

## Preguntas de entrevista que ya puedes responder

**"¿Qué es el problema N+1 y cómo lo detectas?"**
Una query para traer N entidades, más N queries adicionales (una por entidad) al acceder a una navegación. Se detecta en los logs de SQL de EF Core viendo la misma query repetida en bucle. Se evita con `Include` (eager) o, mejor para lectura, con proyección `Select`.

**"¿El operador `!` (null-forgiving) cambia algo en runtime?"**
No. Solo silencia al compilador. Si se usa para tapar un null que sí puede ocurrir, esconde un bug en vez de manejarlo. Solo es defendible cuando el dominio garantiza que el valor existe (ej. una FK obligatoria).

**"¿Include o Select para un endpoint de lectura?"**
`Select` con proyección a DTO: trae solo las columnas necesarias, desacopla el contrato del esquema, evita ciclos de serialización y N+1. `Include` para cargar la entidad completa cuando se va a modificar.

**"¿Activarías lazy loading?"**
No por defecto. Convierte accesos a propiedades en queries invisibles y es la causa #1 de N+1 accidentales. Para carga puntual prefiero explicit loading (`.Load`), que es visible y controlado.

**"Con Split Query y 10 proyectos con tareas, ¿cuántas queries?"**
Dos: una por nivel de navegación (proyectos y tareas), no una por proyecto. EF Core usa `WHERE ProjectId IN (...)` y reparte en memoria.
Reparte en memoria: esto se realiza en el servidor de aplicaciones, para el ejemplo de proyectos y tareas, EF core realiza el mapeo de las tareas a su respectivo proyecto.
Esto se conoce como stitching, la ventaja real es que no viaja data redundante en red (de Servidor DB a Servidor de App)

**"¿Por qué tu paginación necesita un `OrderBy`?"**
SQL no garantiza orden sin `ORDER BY`; `Skip/Take` sin orden puede repetir o saltar filas entre páginas. EF Core incluso lanza warning.

---

## Estado de la Semana 2

| Bloque | Estado |
|--------|--------|
| Parte 1 — DbContext, entidades, Fluent API, migrations, BD creada | ✅ Completado |
| Parte 2 — CRUD real conectado a SQLite, AsNoTracking, middleware validado | ✅ Completado |
| Parte 2.1 — Pendientes de limpieza + relaciones/N+1/proyecciones | ✅ Completado |

**Semana 2 cerrada formalmente.** Listo para **Semana 3 — DI + SOLID + MediatR (CQRS)**: el refactor separará estos endpoints en Commands (`Create`, `Update`, `Delete`) y Queries (`GetAll`, `GetById`, `GetTaskDetailed`), y la duplicación de paginación encontrará su lugar natural.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

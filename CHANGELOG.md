# Changelog

Registro de evolución del proyecto **TaskManager API** semana a semana.

---

## [Week 02] — 2026-06-28

**Rama:** `week-02`  
**Tema:** Entity Framework Core 10 — Code First, Fluent API y queries de lectura optimizadas

### Agregado
- Persistencia real con EF Core sobre SQLite (`Microsoft.EntityFrameworkCore.Sqlite` + `.Design`)
- Entidades POCO `Project`, `TaskItem` y `User` con relaciones 1:N (`Entities/`)
- `TaskManagerDbContext` con `ApplyConfigurationsFromAssembly` (`Persistence/`)
- Configuración Fluent API por entidad en `Persistence/Configurations/` (relaciones, `MaxLength`, `HasConversion<string>` del enum `TaskStatus`, `DeleteBehavior` Cascade/SetNull)
- Migrations: `InitialCreate` y `RemoveCreatedAtSqlDefault`
- CRUD completo contra SQLite en `TaskEndpoints.cs` — `GET`/`POST`/`PUT`/`DELETE` con `DbContext` inyectado por request (Scoped)
- `AsNoTracking()` + proyección con `Select` a DTO en todas las lecturas
- Endpoint `GET /tasks/detailed` con `TaskDetailDto` (record) — proyección con navegaciones a `Project` y `AssignedTo`
- `UpdateTaskRequest` para el contrato de `PUT`
- `CancellationToken` propagado a todas las operaciones async de EF Core

### Cambiado
- `CreatedAt` se genera en C# (`DateTime.UtcNow`) en lugar de `HasDefaultValueSql("GETUTCDATE()")` — código agnóstico del proveedor (rompía en SQLite)
- `GetAll` limpiado: se eliminó el total falso del array hardcodeado de Semana 1; ahora reporta solo el conteo real de la BD

### Corregido
- `Update` usa `Enum.TryParse` (validado antes de mutar la entidad) → devuelve `400 Bad Request` ante un estado inválido en lugar de `500`
- Operador null-forgiving (`!`) corregido en la proyección de `/tasks/detailed`: `Project!` (FK obligatoria) vs manejo explícito de null en `AssignedTo` (relación opcional)

### Conceptos profundizados (entrevista)
- Problema N+1: detección en logs de EF Core y por qué no activar lazy loading por defecto
- `Include` (eager) vs proyección `Select` vs Split Queries vs explicit loading — cuándo usar cada uno
- Explosión cartesiana y stitching en memoria con `AsSplitQuery`
- Round-trips y latencia de red como costo real de las queries

### Decisiones técnicas
- Fluent API sobre Data Annotations: entidades de dominio como POCO limpios, configuración de persistencia separada
- SQLite en desarrollo, SQL Server en producción: solo cambia connection string y paquete NuGet
- DTO nombrado (no anónimo) en `/tasks/detailed`: testeable de cara a Semana 5

### Deuda diferida a Semana 3
- Lógica de paginación duplicada entre `GetAll` y `GetTaskDetailed` — encuentra su lugar natural con MediatR (`PagedResult<T>` o pipeline behavior)
- Código muerto menor: `TaskItemsListDummy()` y comentario obsoleto en `GetAll`

---

## [Week 01] — 2026-06-13

**Rama:** `week-01`  
**Tema:** .NET 10 + ASP.NET Core + Minimal APIs

### Agregado
- Proyecto `TaskManager.Api` sobre .NET 10 con top-level statements
- Endpoints CRUD (`GET /tasks`, `GET /tasks/{id}`, `POST /tasks`, `DELETE /tasks/{id}`) con Minimal APIs organizados via extension methods
- Paginación en `GET /tasks` con parámetros `page` y `pageSize`
- `RequestLoggingMiddleware` — loguea método HTTP, path y tiempo de respuesta de cada request
- `ExceptionHandlingMiddleware` — captura excepciones no controladas y devuelve `500` limpio
- Configuración tipada con `IOptions<T>` (`MaxTasksPerUser`, `AllowCompletedTaskDeletion`, `DefaultPageSize`)
- Documentación OpenAPI nativa (`MapOpenApi`)

### Decisiones técnicas
- Minimal APIs sobre Controllers: proyecto nuevo, endpoints simples, menos overhead
- Datos en memoria: la persistencia real llega en S2 con EF Core
- `IOptions<T>` sobre `IOptionsMonitor<T>`: no se necesitan cambios en caliente

---

<!-- Semanas siguientes se agregan aquí -->

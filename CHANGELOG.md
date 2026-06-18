# Changelog

Registro de evolución del proyecto **TaskManager API** semana a semana.

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

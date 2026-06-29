# Banco de preguntas técnicas para entrevistas Senior .NET

## Bloque 1 — Imprescindibles (aparecen en casi toda entrevista)

### ¿Cuáles son las diferencias principales entre .NET Framework y .NET moderno (.NET 8+)?

**Respuesta modelo**:
.NET moderno (antes .NET Core, hoy simplemente .NET) es cross-platform, open-source y con un modelo
de hosting unificado. Las diferencias clave para una entrevista:
- El modelo de hosting: ya no existe Startup.cs separado, todo se configura en Program.cs con
  el nuevo WebApplication builder
- DI nativa desde el día 1, no como add-on
- Rendimiento: Kestrel como servidor embebido, mejoras sustanciales en throughput por versión
- Sin dependencia de IIS (aunque puede usarse detrás de él)
- Sistema de configuración más rico: appsettings.json + environment variables + secrets + IOptions<T>
- Sin System.Web — limpia toda la deuda técnica de ASP.NET clásico

**Lo que NO deberías decir**: "Es básicamente lo mismo pero multiplataforma." No lo es — el modelo
de programación cambió significativamente.

---

### ¿Cuándo usarías Minimal APIs vs Controllers?

**Respuesta modelo**:
Minimal APIs son mejores para: microservicios pequeños, APIs simples con pocos endpoints,
máximo rendimiento (menos overhead de reflection), y cuando quieres colocación de código
(el handler vive junto al registro del endpoint).

Controllers siguen siendo mejores para: APIs grandes con mucha lógica de filtros, cuando el equipo
ya conoce el patrón, cuando necesitas Model Binding complejo, o cuando tienes muchos endpoints
que comparten filtros de autorización o validación vía atributos.

En la práctica: muchos proyectos reales mezclan ambos. No es una decisión binaria.

---

### Explica los lifetimes de DI y un error típico con Scoped en Singleton

**Respuesta modelo**:
- **Transient**: nueva instancia cada vez que se solicita. Para servicios stateless ligeros.
- **Scoped**: una instancia por request HTTP (en el contexto de una API). Ideal para DbContext.
- **Singleton**: una instancia para toda la vida de la aplicación.

**El error clásico** — "Captive dependency": si un Singleton toma una dependencia Scoped,
el Scoped se resuelve una vez y queda atrapado en el Singleton para siempre, efectivamente
convirtiéndose en Singleton. Con DbContext esto es un desastre: un DbContext con lifetime
de aplicación acumula change tracking sin límite y no es thread-safe.

.NET detecta esto en desarrollo con `ValidateScopes = true` (activado por defecto en Development).

---

### ¿Qué es Clean Architecture? Explica la Dependency Rule.

**Respuesta modelo**:
Clean Architecture es una forma de organizar el código en capas concéntricas donde la dirección
de las dependencias siempre va hacia adentro — nunca hacia afuera.

Las capas típicas en .NET:
- **Domain**: entidades, value objects, interfaces de repositorio, domain events. Sin dependencias externas.
- **Application**: casos de uso (commands/queries), interfaces de servicios, DTOs. Solo depende de Domain.
- **Infrastructure**: implementaciones concretas (EF Core, HTTP clients, servicios externos). Depende de Application.
- **API/Presentation**: controllers o endpoints. Depende de Application.

**La Dependency Rule**: Infrastructure puede implementar interfaces definidas en Application
(inversión de dependencias), pero Application nunca sabe que EF Core existe.

**Trade-off honesto**: es más código, más archivos, más indirección. Para APIs simples o equipos pequeños
puede ser over-engineering. Vertical Slice Architecture es una alternativa más pragmática.

---

### ¿Qué es CQRS y para qué sirve MediatR?

**Respuesta modelo**:
CQRS (Command Query Responsibility Segregation) separa las operaciones que modifican estado
(Commands) de las que solo leen (Queries). Los beneficios reales:
- Los Commands pueden validarse, loguearse y auditarse de forma uniforme
- Las Queries pueden optimizarse independientemente (proyecciones directas, sin pasar por el dominio)
- Separación clara de intenciones en el código

MediatR es una librería que implementa el patrón Mediator: en vez de que el controller llame
directamente al servicio, despacha un mensaje (Command o Query) y el Handler lo procesa.
Esto facilita añadir Pipeline Behaviors (como anillos transversales) para logging, validación con
FluentValidation, o manejo de transacciones.

**Lo que NO es**: un requerimiento para usar Clean Architecture. Se puede tener Clean Architecture
sin MediatR. Pero en el mercado .NET van muy de la mano.

---

### ¿Cómo funciona JWT? ¿Qué contiene un access token?

**Respuesta modelo**:
JWT (JSON Web Token) es un token firmado digitalmente compuesto de tres partes en Base64URL
separadas por puntos: Header.Payload.Signature.

- **Header**: algoritmo de firma (HS256, RS256) y tipo de token
- **Payload**: claims — sub (subject/userId), iat (issued at), exp (expiration), y claims custom
  (roles, permisos, etc.)
- **Signature**: HMAC o RSA del header + payload. Verifica que el token no fue alterado.

**El access token es stateless**: el servidor no necesita consultar una BD para validarlo,
solo verifica la firma con la clave secreta o clave pública.

**Refresh token**: token opaco (no JWT) de larga duración, guardado en BD. Sirve para obtener
nuevos access tokens sin re-autenticar al usuario. Si se compromete, se puede revocar.

---

### Diferencia entre OAuth 2.0 y OpenID Connect

**Respuesta modelo**:
- **OAuth 2.0** es un protocolo de **autorización**: permite a una app acceder a recursos en nombre
  del usuario sin exponer sus credenciales. Define flujos (authorization code, client credentials,
  device code, etc.).
- **OpenID Connect (OIDC)** es una capa de **identidad** encima de OAuth 2.0: añade el concepto
  de ID Token (un JWT que contiene información sobre el usuario autenticado).

Regla práctica: si necesitas saber **quién es** el usuario → OIDC. Si solo necesitas que la app
**acceda a algo** en nombre del usuario → OAuth 2.0. En la práctica la mayoría de los sistemas
usa OIDC (que incluye OAuth 2.0).

El flujo estándar actual es **Authorization Code con PKCE** — el Implicit Flow está deprecado.

---

### Optimización de EF Core: menciona al menos 3 técnicas

**Respuesta modelo**:
1. **AsNoTracking()**: para queries de solo lectura. Elimina el overhead del change tracker.
   Reduce uso de memoria y CPU. Crítico en endpoints GET de alto tráfico.

2. **Proyecciones con Select**: en vez de traer la entidad completa, proyectar solo los campos
   necesarios. `db.Users.Select(u => new UserDto { Id = u.Id, Name = u.Name })` genera
   un `SELECT id, name` en vez de `SELECT *`.

3. **Split queries**: para queries con múltiples Includes que generan JOIN cartesiano explosivo.
   `AsSplitQuery()` divide en múltiples queries SQL optimizadas.

4. **Compiled queries**: para queries ejecutadas miles de veces por segundo, eliminar el overhead
   de compilación de expresión en cada llamada.

5. **Bulk operations**: EF Core 7+ tiene `ExecuteUpdateAsync` y `ExecuteDeleteAsync` para
   operaciones masivas sin cargar entidades en memoria.

---

## Bloque 2 — Diferenciadores Senior

### ¿Cuándo usarías microservicios y cuándo no?

**Respuesta modelo**:
Microservicios resuelven problemas de **escala organizacional** más que técnica:
son útiles cuando tienes equipos grandes que necesitan deployar independientemente,
cuando diferentes partes del sistema tienen requisitos de escala muy distintos, o cuando
necesitas usar tecnologías distintas por dominio.

**Cuándo NO usarlos**:
- Equipos pequeños (< 5-8 devs): el overhead operacional supera los beneficios
- Producto en fases tempranas: los límites de dominio aún no están claros
- Sin infraestructura madura: Kubernetes, observabilidad distribuida, service mesh son pre-requisitos reales

**La respuesta honesta para entrevista**: "Empezaría con un monolito modular bien estructurado
y extraería servicios cuando aparezca una necesidad real — equipos independientes, escala divergente,
o un bounded context con requisitos muy distintos."

---

### ¿Qué es el patrón Outbox y por qué es necesario?

**Respuesta modelo**:
El problema: en microservicios, si guardas en BD y luego publicas un evento a un message broker,
pueden ocurrir dos fallos: guardas en BD pero el broker está caído (evento perdido), o el broker
recibe el evento pero falla el commit de BD (dato inconsistente).

El Outbox Pattern resuelve esto en dos pasos:
1. En la misma transacción de BD, guardar el evento en una tabla "outbox" junto con el dato
2. Un proceso separado (background worker) lee la tabla outbox y publica al broker, marcando
   los eventos como publicados

Esto garantiza que si el commit de BD falla, el evento tampoco se publica. Y si el broker falla,
el evento queda en la outbox para reintentarse.

La librería MassTransit tiene soporte nativo para Outbox Pattern con EF Core.

---

### Explica los tres pilares de observabilidad

**Respuesta modelo**:
1. **Logs**: registros de eventos discretos. Structured logging (con Serilog) vs logging de texto plano.
   El campo clave: usar correlation IDs para trazar un request a través de múltiples servicios.

2. **Métricas**: datos numéricos agregados en el tiempo — latencia p99, requests por segundo,
   tasa de errores. Permiten alertas y dashboards. Herramientas: Prometheus, Application Insights.

3. **Trazas distribuidas (traces)**: seguimiento de un request a través de múltiples servicios.
   Cada "span" representa una operación. OpenTelemetry es el estándar actual, agnóstico de vendor.

La diferencia entre logs y traces: los logs te dicen qué pasó en un servicio, las trazas te
dicen cómo se propagó una llamada a través de todo el sistema.

---

## Bloque 3 — Ejercicios de coding típicos

### Paginación con offset y cursor

```csharp
// Offset pagination (simple, con problema de "shifting" en datos que cambian)
public async Task<PagedResult<TaskDto>> Handle(GetTasksQuery query, CancellationToken ct)
{
    var total = await _context.Tasks.CountAsync(ct);
    var items = await _context.Tasks
        .AsNoTracking()
        .OrderBy(t => t.CreatedAt)
        .Skip((query.Page - 1) * query.PageSize)
        .Take(query.PageSize)
        .Select(t => new TaskDto(t.Id, t.Title, t.Status))
        .ToListAsync(ct);

    return new PagedResult<TaskDto>(items, total, query.Page, query.PageSize);
}

// Cursor pagination (más eficiente para grandes datasets)
public async Task<CursorResult<TaskDto>> Handle(GetTasksCursorQuery query, CancellationToken ct)
{
    var items = await _context.Tasks
        .AsNoTracking()
        .Where(t => t.CreatedAt > query.Cursor) // cursor es el último timestamp visto
        .OrderBy(t => t.CreatedAt)
        .Take(query.PageSize + 1) // +1 para saber si hay más páginas
        .Select(t => new TaskDto(t.Id, t.Title, t.Status, t.CreatedAt))
        .ToListAsync(ct);

    var hasMore = items.Count > query.PageSize;
    var nextCursor = hasMore ? items[^1].CreatedAt : (DateTime?)null;
    return new CursorResult<TaskDto>(items.Take(query.PageSize).ToList(), nextCursor);
}
```

---

### Async/await — error típico de deadlock

```csharp
// MAL: .Result o .Wait() en código sincrónico bloquea el thread
public IActionResult GetData()
{
    var result = _service.GetDataAsync().Result; // DEADLOCK en contextos con SynchronizationContext
    return Ok(result);
}

// BIEN: async all the way down
public async Task<IActionResult> GetData()
{
    var result = await _service.GetDataAsync();
    return Ok(result);
}

// MAL: fire-and-forget sin manejo de excepciones
_ = _service.SendEmailAsync(); // si lanza, la excepción se pierde silenciosamente

// BIEN: si necesitas fire-and-forget, al menos logea errores
_ = _service.SendEmailAsync().ContinueWith(t =>
    _logger.LogError(t.Exception, "Error sending email"),
    TaskContinuationOptions.OnlyOnFaulted);
// O mejor: usa un background service / queue
```

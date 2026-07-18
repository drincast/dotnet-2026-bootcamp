# Cierre Semana 3 (Parte 3) — Pipeline Behaviors — Lo que construiste y debes saber defender

> Continuación de `week-03-summary_Part2.md` (MediatR + CQRS). Esta sesión agrega el último concepto de Semana 3: interceptar el pipeline de MediatR con un `IPipelineBehavior<,>` para logging automático, cerrando formalmente la semana. FluentValidation queda para una parte posterior para no mezclar conceptos.

---

## Lo que construiste

```
TaskManager.Api/
├── Common/
│   ├── PagedResult.cs
│   └── Behaviors/
│       └── LoggingBehavior.cs      ← IPipelineBehavior<TRequest, TResponse> genérico
├── Features/
│   └── Task/                       ← sin cambios, los Commands/Queries no se tocaron
└── Program.cs                      ← AddOpenBehavior(typeof(LoggingBehavior<,>))
```

Un solo `LoggingBehavior` genérico que se aplica automáticamente a **todos** los Commands y Queries existentes (`CreateTask`, `UpdateTask`, `DeleteTask`, `GetAllTasks`, `GetTask`, `GetAllDetailedTasks`) sin tocar ninguno de sus Handlers.

---

## Concepto clave: Pipeline Behavior = middleware de MediatR

Un `IPipelineBehavior<TRequest, TResponse>` intercepta cada `Send()` de MediatR, antes y después de que el Handler corra — misma idea bidireccional que el `RequestLoggingMiddleware` de Semana 1 (código antes de `_next(context)`, código después), un nivel más abajo: en vez de interceptar el pipeline HTTP completo, intercepta cada mensaje individual de MediatR.

**Por qué no basta con el middleware HTTP que ya tienes:**

El middleware ve el *request HTTP* (`POST /tasks`) sin saber nada del dominio (concepto de separacion de responsabilidades → Infraestructura === Aplicación). El Behavior ve el *mensaje tipado* (`CreateTask` con su `ProjectId`, por ejemplo) y puede razonar por tipo de request. Son capas complementarias: el middleware loguea a nivel HTTP, el Behavior loguea a nivel de Command/Query — no se reemplazan.

**Cadena en cebolla:**

```
Endpoint → ISender.Send() → Behavior1 → Behavior2 → ... → Handler → (vuelve hacia arriba)
```

Se ejecutan en el orden en que se registran; el primero registrado envuelve a todos los demás.

- `next()` es un `RequestHandlerDelegate<TResponse>` que apunta al siguiente eslabón
  de la cadena (otro behavior o, al final, el Handler).
  
### Middleware HTTP vs Pipeline Behavior
| Aspecto | Middleware HTTP | Pipeline Behavior |
|--------|-----------------|-------------------|
| Capa | Request HTTP completo | Command/Query individual |
| Qué ve | Método, path, status code | Nombre y tipo del Request (`CreateTask`) |
| Qué NO ve | El nombre del Command | El detalle HTTP |
| Ejemplo en el proyecto | `RequestLoggingMiddleware` | `LoggingBehavior<,>` |

Analogía: el middleware es la **aduana del aeropuerto** (ve el vuelo y la puerta,
no quién eres); el behavior es el **revisor de sala VIP** (te conoce por nombre,
no ve la logística del vuelo).

---

---

## Decisiones técnicas que tomaste y por qué

**`Common/Behaviors/LoggingBehavior.cs`, no en la raíz ni en `Features/`**

- No en `Features/Task/`: el Behavior no pertenece a un feature — `AddOpenBehavior` lo aplica globalmente, meterlo en una carpeta de feature ataría visualmente algo transversal a un solo slice.
- No en la raíz junto a `RequestLoggingMiddleware`/`ExceptionHandlingMiddleware`: esos quedaron ahí por convención vieja de Semana 1, antes de que existieran `Common/` y `Features/`. Seguir la convención ya establecida es más consistente que replicar el patrón antiguo.
- Sí en `Common/Behaviors/`: es infraestructura genérica de MediatR, mismo espíritu que `PagedResult<T>` (transversal, no pertenece a ningún feature). La subcarpeta `Behaviors/` anticipa que ahí mismo va a vivir `ValidationBehavior.cs` cuando se aborde FluentValidation, y eventualmente un behavior de correlation IDs en Semana 8.

**`AddOpenBehavior(typeof(LoggingBehavior<,>))` con tipo genérico abierto**

Aplica el behavior a todo `IRequest<T>` existente y futuro sin registrar uno por uno. Alternativa descartada: registrar `AddScoped<IPipelineBehavior<CreateTask, int>, LoggingBehavior<...>>` por cada Command/Query — no escala, cada feature nueva obligaría a un registro manual extra.

**Re-lanzar (`throw;`) la excepción capturada dentro del behavior, no tragarla**

El `LoggingBehavior` loguea el fallo con su tiempo transcurrido, pero no decide qué responder al cliente — esa responsabilidad sigue siendo del `ExceptionHandlingMiddleware` de Semana 1, que actúa después, a nivel HTTP. Tragar la excepción ahí rompería esa separación de responsabilidades.

**FluentValidation, deferido conscientemente (no aplicado en esta sesión)**

Razones para no meterlo junto con logging:
1. `LoggingBehavior` es observacional (no cambia el resultado); un `ValidationBehavior` sí decide si continúa o corta la cadena — son dos naturalezas distintas de behavior, mejor entendidas por separado.
2. Ya existe una validación manual de referencia (`Enum.TryParse` → `400` en `UpdateTask`, Semana 2) — vale la pena que el contraste "esto lo hice a mano vs esto lo centraliza un Behavior" quede consciente antes de automatizarlo.
3. El orden de behaviors importa (validación antes que logging de éxito, para no loguear "completado" algo rechazado) — más claro de razonar con una sola pieza en la cadena antes de sumar una segunda.
4. No está en el objetivo explícito de Semana 3 del plan — queda anotado como extra para una mini-sesión dedicada.

---

## Código

### LoggingBehavior.cs
```csharp
// Common/Behaviors/LoggingBehavior.cs
using System.Diagnostics;
using MediatR;

namespace TaskManager.Api.Common
{
    public sealed class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
            => _logger = logger;

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("→ Handling {Request}", requestName);

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await next();
                sw.Stop();

                if (sw.ElapsedMilliseconds > 500)
                    _logger.LogWarning("← {Request} tardó {Ms}ms (lento)",
                        requestName, sw.ElapsedMilliseconds);
                else
                    _logger.LogInformation("← Handled {Request} ({Ms}ms)",
                        requestName, sw.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "✗ {Request} falló tras {Ms}ms",
                    requestName, sw.ElapsedMilliseconds);
                throw; // NO se traga la excepción
            }
        }
    }
}
```

```csharp
// Program.cs
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});
```

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"¿Cómo evitas repetir logging (o validación, o transacciones) en cada Handler de MediatR?"**

Con Pipeline Behaviors — interceptores genéricos que envuelven cada `Send()`, registrados una vez con `AddOpenBehavior` sobre el tipo genérico abierto, sin tocar los Handlers individuales. Mismo patrón que el middleware de ASP.NET Core, aplicado un nivel más abajo: a nivel de mensaje de MediatR en vez de a nivel de request HTTP.

**"¿Qué diferencia hay entre tu middleware HTTP y un Pipeline Behavior de MediatR?"**

Middleware envuelve el request HTTP; el behavior envuelve un Command/Query. Uno ve path/status, el otro ve el tipo del Request.

El middleware ve el request HTTP crudo (verbo, ruta, headers) sin conocer el dominio. El Behavior ve el mensaje tipado (`CreateTask`, `GetAllTasks`) y puede razonar según el tipo de Command/Query. Son complementarios: uno loguea a nivel de transporte, el otro a nivel de caso de uso.

**"Si un Handler lanza una excepción, ¿quién la maneja: el Behavior o el Middleware?"**

El Behavior puede loguearla (con contexto de qué Command/Query falló y cuánto tardó) pero debe re-lanzarla (`throw;`), no decidir la respuesta HTTP. Esa decisión sigue siendo del `ExceptionHandlingMiddleware`, que actúa en una capa superior y es el único que conoce el mapeo a códigos de estado HTTP.

**"¿Dónde pones la validación de input: endpoint, Behavior o Handler?"**

En el endpoint solo validación de forma (binding, tipos). En un `ValidationBehavior` las reglas declarativas transversales (campo requerido, rango válido) porque no deben ensuciar el Handler. Dentro del Handler solo la validación que necesita consultar estado (ej. verificar que un `ProjectId` exista en BD), porque eso requiere el `DbContext` y un Behavior genérico no debería acoplarse a eso.

**"¿Qué es un open generic <,> y por qué se registra así?"**

Permite que un único behavior aplique a todos los pares Request/Response sin registrarlos uno por uno.

**"¿Por qué en el catch se hace throw y no se traga la excepción?"**

Para no ocultar el error; el ExceptionHandlingMiddleware sigue siendo el responsable de traducirlo a respuesta HTTP. El behavior solo lo registra.

---

### Aprendizajes del experimento (Logging levels)
Por defecto ASP.NET Core usa nivel mínimo Information: LogDebug y LogTrace se descartan aunque el código se ejecute. Un breakpoint en LogDebug sí pausa (el código corre), pero no se escribe salida porque el nivel no está habilitado. Se controla en appsettings.Development.json → Logging:LogLevel:Default. 

Jerarquía: Trace < Debug < Information < Warning < Error < Critical. 

Guarda de rendimiento para logs costosos:

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
	_logger.LogDebug("Estado: {@Data}", dataCara);
```

Regla aplicada: entrada/salida del LoggingBehavior en LogInformation (se quieren ver siempre); LogDebug reservado para dumps de detalle.

---

## Extras — anotado para más adelante (no bloqueante ahora)

**FluentValidation vía `ValidationBehavior<TRequest, TResponse>`**
Mismo patrón `AddOpenBehavior`, registrando validators con `AddValidatorsFromAssembly`. Se ubicará en `Common/Behaviors/ValidationBehavior.cs`, junto al `LoggingBehavior`. Pendiente definir el orden exacto de registro (validación antes que logging de éxito).

---

## Estado de la Semana 3

| Bloque | Estado |
|--------|--------|
| Parte 1 — DI a fondo + SOLID pragmático | ✅ Completado |
| Parte 2 — MediatR + CQRS (refactor de los 5 endpoints, `PagedResult<T>`) | ✅ Completado |
| **Parte 3 — Pipeline Behaviors (logging) + cierre** | ✅ **Completado** |

**Semana 3 cerrada formalmente.**

### Limpieza pendiente (arrastrada, aún no aplicada — queda para después de esta sesión)

- Borrar `TaskItemsListDummy()` en la región de utilería — ya no se usa en ningún lado.
- Opcional: alinear `GetTask.cs` para proyectar directo al DTO dentro del `Select`, en vez de un objeto anónimo intermedio.
- Opcional: mover `RequestLoggingMiddleware.cs` y `ExceptionHandlingMiddleware.cs` de la raíz a una carpeta consistente con la convención actual (ej. `Common/Middleware/`).

**Extra pendiente (fuera del plan base, anotado para mini-sesión dedicada):** `ValidationBehavior` con FluentValidation.

**Próxima sesión — Semana 4:** Clean Architecture y Vertical Slice Architecture. Reestructurar la TaskManager API en capas (Domain, Application, Infrastructure, API), introducir Value Objects y Domain Events tácticos de DDD.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

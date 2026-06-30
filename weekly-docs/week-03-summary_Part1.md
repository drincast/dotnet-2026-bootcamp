# Cierre Semana 3 (Parte 1) — Inyección de Dependencias a fondo + SOLID pragmático — Lo que profundizaste y debes saber defender

> Parte conceptual de la Semana 3. Sienta las bases (lifetimes, SOLID) que justifican el refactor a MediatR/CQRS de la Parte 2. No se tocó código del proyecto todavía: esta sesión es de fundamentos anclados en el código que ya existe.

---

## Lo que cubriste

- **Los tres lifetimes de DI** (Transient, Scoped, Singleton) y el porqué de que el `DbContext` sea Scoped.
- **Captive Dependency** — la trampa estrella de DI en entrevista, con su detección y su solución vía `IServiceScopeFactory`.
- **Thread-safety** — qué significa y por qué es la razón de fondo de varios comportamientos de EF Core.
- **SOLID pragmático** — los cinco principios con teoría, ejemplo .NET y anclaje en el código real del proyecto.
- **DIP vs DI** — la diferencia entre el principio y la técnica.

---

## 1. Los tres lifetimes

El lifetime decide **cada cuánto se crea una instancia nueva** de un servicio registrado en el contenedor.

| Lifetime | Cuándo se crea instancia nueva | Uso típico |
|----------|-------------------------------|------------|
| **Transient** | Cada vez que se pide | Servicios ligeros, sin estado |
| **Scoped** | Una por scope (= un request HTTP en una API web) | `DbContext`, servicios por request |
| **Singleton** | Una para toda la vida de la app | Caches, configuración cara, estado compartido thread-safe |

```csharp
builder.Services.AddTransient<IEmailFormatter, EmailFormatter>();  // nueva siempre
builder.Services.AddScoped<IOrderService, OrderService>();          // una por request
builder.Services.AddSingleton<IClock, SystemClock>();               // una para toda la app
```

### Por qué el DbContext es Scoped (3 razones para articular)

1. **No es thread-safe** — dos hilos a la vez lo corrompen. Si fuera Singleton, dos requests simultáneos lo compartirían y EF Core lanzaría `A second operation was started on this context...`.
2. **Acumula change-tracking** — guarda snapshots de todo lo materializado. Singleton = esos snapshots nunca se liberan → fuga de memoria.
3. **Aísla la unidad de trabajo** — un request = una transacción lógica. Scoped garantiza que todos los handlers del request comparten el mismo `DbContext` y un solo `SaveChanges` persiste todo junto.

---

## 2. Thread-safety

Un **hilo** (thread) es una línea de ejecución. Una API web atiende muchos requests a la vez, cada uno en su propio hilo. Algo es **thread-safe** cuando varios hilos pueden usarlo simultáneamente sin corromperlo.

**Analogía:** una libreta donde dos personas escriben a la vez en la misma línea → queda ilegible. No es thread-safe. Una máquina de escribir con candado (uno a la vez) sí lo es.

- El `DbContext` lleva estado mutable (cambios pendientes, snapshots de tracking) → **no es thread-safe**. Por eso se aísla por request con Scoped.
- Un Singleton **sí debe ser thread-safe**, porque por definición lo comparten todos los requests a la vez. Un `IClock` que solo lee la hora del sistema lo es (no tiene estado mutable que corromper).

---

## 3. Captive Dependency (la trampa estrella)

**Un servicio no puede depender de otro con un lifetime más corto que el suyo.** Si lo hace, el de vida corta queda "capturado" — atrapado vivo más tiempo del que debería.

Caso clásico: inyectar un **Scoped** dentro de un **Singleton**.

```csharp
public class CacheService // registrado como Singleton
{
    private readonly TaskManagerDbContext _db; // ¡Scoped!
    public CacheService(TaskManagerDbContext db) => _db = db;
}
```

El Singleton se crea una vez al arrancar y vive para siempre. Captura *ese* `DbContext` del primer scope y nunca lo suelta → el `DbContext` que debía morir al terminar el primer request vive eternamente, y todos los requests siguientes usan ese mismo objeto viejo (no thread-safe, tracking acumulado, datos rancios).

### Detección — scope validation

El contenedor lo detecta y lanza al arrancar:

```
Cannot consume scoped service 'TaskManagerDbContext' from singleton 'CacheService'.
```

**¿Está activo por defecto?** Depende del entorno:
- **Development**: activo por defecto (por eso se caza localmente).
- **Production**: desactivado por defecto (por rendimiento).

Para forzarlo siempre (recomendado, que un bug no se cuele a producción):

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;    // detecta captive dependencies
    options.ValidateOnBuild = true;   // valida el grafo completo en builder.Build(), no al primer request
});
```

`ValidateOnBuild = true` es el extra senior: falla en el arranque, no con un 500 sorpresa cuando alguien pega al endpoint.

### Solución — si un Singleton necesita la BD

No se inyecta el `DbContext` directo. Se inyecta `IServiceScopeFactory`, se crea un scope a mano y se libera:

```csharp
public class CacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public CacheService(IServiceScopeFactory f) => _scopeFactory = f;

    public async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
        // usar db dentro del scope; se libera al salir del using
    }
}
```

Es lo que hacen los `BackgroundService` y workers para tocar la BD.

### Regla de oro (una frase)

**Puedes depender hacia lifetimes más largos, nunca hacia más cortos.** Transient → Scoped → Singleton. Transient usa todo; Scoped usa Scoped y Singleton; Singleton solo usa Singleton (o crea scopes a mano).

---

## 4. SOLID pragmático

| Sigla | Nombre en inglés | Nombre en español |
|-------|------------------|-------------------|
| **S** | Single Responsibility Principle | Principio de Responsabilidad Única |
| **O** | Open/Closed Principle | Principio de Abierto/Cerrado |
| **L** | Liskov Substitution Principle | Principio de Sustitución de Liskov |
| **I** | Interface Segregation Principle | Principio de Segregación de Interfaces |
| **D** | Dependency Inversion Principle | Principio de Inversión de Dependencias |

### S — Single Responsibility
Una clase, **una razón para cambiar** (un actor) — no "una clase hace una sola cosa".
**En tu código:** separar `TaskItemConfiguration` (cambia por persistencia) de `TaskItem` (cambia por dominio). Dos motivos → dos clases. Es por qué elegiste Fluent API sobre Data Annotations.

### O — Open/Closed
Abierta a extensión, cerrada a modificación.
**En tu código:** `ApplyConfigurationsFromAssembly`. Agregar una entidad solo requiere crear su `Configuration`; el `DbContext` no se toca.

### L — Liskov Substitution
Un subtipo debe poder reemplazar a su base sin romper el contrato (ni lanzar excepciones nuevas, ni endurecer precondiciones, ni debilitar postcondiciones).
**Violación clásica .NET:** una `ReadOnlyList : List<T>` que lanza excepción en `Add()`. El llamador espera poder agregar y revienta.

### I — Interface Segregation
Ningún cliente debería depender de (o implementar) métodos que no usa. Mejor varias interfaces específicas que una gorda.
**Ejemplo — impresora multifunción:** en vez de un `IMaquinaOficina` con `Imprimir/Escanear/EnviarFax` (que obliga a una `ImpresoraBasica` a lanzar `NotSupportedException` en lo que no hace), separar en `IImpresora`, `IEscaner`, `IFax`. Cada máquina implementa solo lo que sabe hacer.
**Conexión clave:** `IReadRepository` / `IWriteRepository` es ISP, y es la base teórica de **CQRS** (Parte 2): separar lectura de escritura para que cada lado dependa solo de lo suyo.

### D — Dependency Inversion
Los módulos de alto nivel no dependen de los de bajo nivel; **ambos dependen de abstracciones**. Los detalles dependen de la abstracción, no al revés.

```csharp
public interface IEmailSender
{
    Task EnviarAsync(string destino, string mensaje, CancellationToken ct);
}

public class RegistroUsuarioService
{
    private readonly IEmailSender _emailSender;                 // depende de la abstracción
    public RegistroUsuarioService(IEmailSender s) => _emailSender = s;
}

public class SmtpEmailSender    : IEmailSender { /* SMTP */ }   // detalles que apuntan
public class SendGridEmailSender: IEmailSender { /* SendGrid */ } // hacia arriba a la abstracción
public class FakeEmailSender    : IEmailSender { /* tests */ }
```

Beneficios: testeable (inyectas `FakeEmailSender`), intercambiable (SMTP→SendGrid es una línea en `Program.cs`), y el alto nivel nunca se ensucia con detalles de infraestructura.

### Actitud pragmática (respuesta de entrevista)
SOLID son **heurísticas para manejar el cambio, no leyes**. Aplicarlos a ciegas produce sobreingeniería (interfaces de un solo implementador, capas que no aportan). El criterio senior: aplicar el principio cuando el dolor que evita es **real y probable**, no preventivamente.

---

## DIP vs DI — la diferencia

- **DIP (principio)** dice *de qué* dependes: de una **abstracción** (interfaz), no de una concreta.
- **DI (técnica)** dice *de dónde viene*: te la **pasan desde afuera** (constructor), no la creas con `new` adentro.

Son independientes — puedes tener uno sin el otro:

```csharp
// DIP sí, DI no: dependes de interfaz pero la creas tú
public Servicio() => _sender = new SmtpEmailSender();  // ✅ no aplica: depende de concreta

// DI sí, DIP no: te inyectan, pero una clase concreta
public Servicio(SmtpEmailSender sender) => _sender = sender;  // ✅ DI, ❌ DIP

// Los dos (lo ideal):
public Servicio(IEmailSender sender) => _sender = sender;     // ✅ DIP ✅ DI
```

**Resumen:** DIP = de qué dependes (abstracción vs concreta). DI = de dónde viene (afuera vs adentro). El contenedor de .NET es un DI container que materializa DIP en la práctica.

---

## Preguntas de entrevista que ya puedes responder

**"¿Por qué el DbContext es Scoped?"**
No es thread-safe, acumula tracking que debe liberarse por request, y representa la unidad de trabajo del request. Singleton rompe las tres cosas.

**"¿Qué es una captive dependency?"**
Cuando un servicio captura otro de vida más corta y lo mantiene vivo más allá de su lifetime. El caso típico es un Scoped dentro de un Singleton. El contenedor lo detecta al arrancar si la validación de scopes está activa (Development por defecto; en Production hay que activarla).

**"Un Singleton necesita tocar la BD, ¿cómo lo haces sin romper lifetimes?"**
Inyectando `IServiceScopeFactory`, creando un scope con `using` y resolviendo el `DbContext` dentro. Se libera al salir del scope.

**"¿SRP significa que una clase hace una sola cosa?"**
No exactamente; significa una sola *razón para cambiar*. Puede tener varios métodos si todos sirven al mismo motivo de cambio.

**"¿Include o Select... / qué principio justifica separar lectura de escritura?"**
ISP: un cliente de solo lectura no debería depender de métodos de escritura. Es la base teórica de CQRS.

**"¿El operador `!` ... / inyectar DbContext directo viola DIP?"**
Técnicamente es clase concreta, pero el `DbContext` ya abstrae la BD, es intercambiable de motor y es testeable solo. Envolverlo en `IRepository` por dogma suele ser over-engineering. DIP busca desacoplar de detalles *volátiles*; el `DbContext` no es ese detalle.

**"¿Diferencia entre DIP y DI?"**
DIP es el principio (depender de abstracciones); DI es la técnica (inyectar la dependencia desde afuera). Se puede cumplir uno sin el otro.

---

## Estado de la Semana 3

| Bloque | Estado |
|--------|--------|
| Parte 1 — DI a fondo + SOLID pragmático | ✅ Completado |
| Parte 2 — MediatR + CQRS (refactor de endpoints a Commands/Queries, `PagedResult<T>`) | ⏳ Pendiente |
| Parte 3 — Pipeline Behaviors (logging + validación) + cierre | ⏳ Pendiente |

**Próxima sesión — Parte 2:** refactor con MediatR. Separar los endpoints actuales en Commands (`Create`, `Update`, `Delete`) y Queries (`GetAll`, `GetById`, `GetTaskDetailed`), y resolver la duplicación de paginación anotada en Semana 2 con un `PagedResult<T>`.

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

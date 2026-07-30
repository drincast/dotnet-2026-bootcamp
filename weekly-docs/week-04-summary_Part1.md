---
# Semana 4 — Parte 1: Fundamentos Arquitectónicos — Clean Architecture vs Vertical Slice Architecture

> **Foco:** Fundamentos arquitectónicos + Clean Architecture (teoría y decisión), comparados contra Vertical Slice Architecture.
> **Naturaleza:** Conceptual + diseño. No se tocó código de reestructuración todavía — eso es la Parte 2.
> **Contexto:** Proyecto TaskManager API — repo https://github.com/drincast/dotnet-2026-bootcamp (actualizado a Semana 3). Esta sesión ancla la teoría en decisiones ya tomadas en semanas anteriores (Fluent API sobre Data Annotations en Semana 2, no envolver el `DbContext` en Repository en Semana 3).

---

## División de la Semana 4

Semana 4 mete tres bloques conceptualmente distintos y el plan le da 18-20h — el rango más alto de las 10 semanas.

| Parte | Foco | Qué se construye | Naturaleza |
|-------|------|-------------------|------------|
| **Parte 1** | Fundamentos arquitectónicos + Clean Architecture vs Vertical Slice (teoría y decisión) | Nada de código todavía. Entender la Regla de Dependencia, las 4 capas, dónde va cada cosa del proyecto actual, y Clean vs VSA con trade-offs para entrevista | Conceptual + diseño |
| **Parte 2** | Reestructuración física a Clean Architecture | Crear los 4 proyectos (`Domain`, `Application`, `Infrastructure`, `Api`), mover entidades / handlers / DbContext / configuraciones, arreglar referencias, migrar el DbContext y las migrations a Infrastructure | Cirugía de código |
| **Parte 3** | DDD táctico aplicado | Value Objects (ej. un `Email` para `User`), invariantes en la entidad `TaskItem`, Domain Events (`TaskCreatedEvent`) — sentando la base del microservicio de notificaciones de S9 | Modelado de dominio |

**Nota de criterio senior:** en esta Parte 1 se decide *conscientemente* aplicar Clean Architecture aun sabiendo que para una API de este tamaño Vertical Slice sería más pragmático. La razón es de empleabilidad: **Clean Architecture es lo que más piden en ofertas y lo que más preguntan en entrevistas**, y hay que haberla construido a mano para defenderla.

---

## 1. El problema que resuelve la arquitectura en capas

Hoy `TaskManager.Api` tiene **todo mezclado en un solo proyecto**: entidades de dominio (`TaskItem`), lógica de aplicación (los Handlers de MediatR), acceso a datos (`DbContext`, configuraciones EF Core) y detalles de transporte (endpoints, middleware). Funciona. ¿Cuál es el problema?

El problema es la **dirección de las dependencias**. Ahora mismo la entidad `TaskItem` y la lógica de negocio están en el mismo assembly que EF Core y ASP.NET Core. Nada impide, mañana, escribir dentro de un Handler algo que dependa de un detalle HTTP, o poner una anotación de EF Core sobre una entidad de dominio. El compilador no lo detiene. **La disciplina es solo tuya.** En un equipo, eso se degrada.

**Clean Architecture convierte esa disciplina en una regla que el compilador hace cumplir**, separando el código en assemblies físicos con dependencias unidireccionales.

---

## 2. La Regla de Dependencia (el corazón de todo)

> Las dependencias del código fuente solo pueden apuntar **hacia adentro**, hacia políticas de más alto nivel. Nada de una capa interna sabe absolutamente nada de una capa externa.

Clean Architecture (y sus primas: Onion, Hexagonal/Ports&Adapters) organiza el código en **círculos concéntricos**:

```
┌─────────────────────────────────────────────┐
│  API / Presentation                         │  ← endpoints, middleware, Program.cs
│  ┌───────────────────────────────────────┐  │
│  │  Infrastructure                       │  │  ← DbContext, EF Core, envío de emails
│  │  ┌─────────────────────────────────┐  │  │
│  │  │  Application                    │  │  │  ← Handlers MediatR, DTOs, interfaces
│  │  │  ┌───────────────────────────┐  │  │  │
│  │  │  │  Domain                   │  │  │  │  ← entidades, Value Objects, reglas
│  │  │  │  (no depende de NADA)     │  │  │  │     de negocio, Domain Events
│  │  │  └───────────────────────────┘  │  │  │
│  │  └─────────────────────────────────┘  │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
        Las flechas de dependencia apuntan ↓ hacia adentro
```

**Qué va en cada capa (mapeado al proyecto actual):**

| Capa | Qué contiene | Qué del código actual va aquí | Referencia a... |
|------|--------------|--------------------------------|-----------------|
| **Domain** | Entidades, Value Objects, Domain Events, reglas de negocio puras, interfaces de dominio | `TaskItem`, `Project`, `User`, el enum `TaskStatus` | **nada** (cero dependencias, ni siquiera EF Core) |
| **Application** | Handlers de MediatR (Commands/Queries), DTOs, `PagedResult<T>`, Behaviors, interfaces de repositorios/servicios que Infrastructure implementará | `Features/Task/*`, `Common/PagedResult`, `LoggingBehavior` | Domain |
| **Infrastructure** | `DbContext`, configuraciones Fluent API, migraciones, implementaciones de interfaces (email, etc.) | `TaskManagerDbContext`, `Persistence/Configurations/*`, `Migrations/*` | Application, Domain |
| **API** | Endpoints, middleware, `Program.cs`, composición de DI | `TaskEndpoints`, middlewares, `Program.cs` | Application, Infrastructure |

**La prueba de fuego** para saber si se respetó la regla: **`Domain.csproj` no debería tener ninguna referencia de paquete a `Microsoft.EntityFrameworkCore`.** Si `TaskItem` tiene un atributo de EF Core encima, ya se violó la regla — por eso la decisión de Fluent API sobre Data Annotations desde Semana 2 ya preparaba el terreno para esto, sin saberlo todavía.

---

## 3. La pieza que hace que todo funcione: Inversión de Dependencias (DIP)

Aquí se conecta con el **DIP** de la Semana 3 (Parte 1).

Surge una paradoja aparente: un Handler en **Application** necesita guardar datos, pero guardar datos es cosa de **Infrastructure** (el `DbContext`), que está *más afuera*. Application no puede depender de Infrastructure — violaría la Regla de Dependencia.

**Solución (esto es DIP puro):**

- **Application define la interfaz** (`ITaskRepository`, o posiblemente `IApplicationDbContext`). La interfaz vive en Application.
- **Infrastructure implementa esa interfaz** (`TaskManagerDbContext : IApplicationDbContext`).
- En tiempo de ejecución, `Program.cs` (en API, la capa más externa, que conoce a todos) inyecta la implementación concreta.

Application depende de una **abstracción que ella misma posee**. La dependencia real (`DbContext`) se resuelve en runtime, pero la dependencia de *código fuente* apunta hacia adentro. La flecha se invirtió. De ahí el nombre.

Ese es exactamente el momento en el que **reaparece la discusión del Repository Pattern** descartada en Semana 3 — cuando se decidió no envolver el `DbContext` en un Repository. En Clean Architecture "puro", muchos introducen `IApplicationDbContext` o repositorios precisamente para que Application no dependa directo de EF Core. Se verá con lupa en la Parte 2 — hay una decisión de trade-off ahí, y una alternativa es dejar que Infrastructure referencie directo a Application, rompiendo formalmente la regla en un punto puntual, con el trade-off explícito documentado.

### Código de ejemplo (mecanismo, no reestructuración — eso es Parte 2)

```csharp
// En el proyecto Application — Application/Common/Interfaces/IApplicationDbContext.cs
public interface IApplicationDbContext
{
    DbSet<TaskItem> TaskItems { get; }
    DbSet<Project> Projects { get; }
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

```csharp
// En el proyecto Infrastructure — el DbContext implementa la interfaz de Application
public sealed class TaskManagerDbContext : DbContext, IApplicationDbContext
{
    // ... DbSets y OnModelCreating igual que ahora
}
```

```csharp
// Tu Handler en Application ahora depende de la interfaz, NO del DbContext concreto
public sealed class CreateTaskHandler : IRequestHandler<CreateTask, int>
{
    private readonly IApplicationDbContext _db;   // ← interfaz, vive en Application
    public CreateTaskHandler(IApplicationDbContext db) => _db = db;
    // ...
}
```

```csharp
// En Program.cs (API) — la composición: aquí y solo aquí se conoce lo concreto
builder.Services.AddScoped<IApplicationDbContext>(sp =>
    sp.GetRequiredService<TaskManagerDbContext>());
```

Fíjate: el Handler ya no menciona `TaskManagerDbContext`. Application no tiene ni referencia al proyecto Infrastructure. Compila igual. Esa es la magia de la Regla de Dependencia hecha cumplir por el compilador.

---

## 4. Vertical Slice Architecture — la alternativa

VSA no organiza por **capa técnica** (todo lo de EF Core junto, todo lo de HTTP junto), sino por **feature** (todo lo de `CreateTask` junto: su Command, su Handler, su validación, su acceso a datos).

Ya se está usando: la carpeta `Features/Task/` con `CreateTask.cs`, `UpdateTask.cs`, etc., cada uno agrupando Query/Command + DTO + Handler en un solo archivo, es VSA pura. Se dijo explícitamente en el cierre de Semana 3: *"es una decisión de estilo consciente, distinta a tener carpetas separadas Queries/, Commands/, Dtos/"*.

**El punto que muchos juniors no ven:** Clean Architecture y VSA no son mutuamente excluyentes. Son ejes distintos:

- Clean Architecture responde: **¿en qué capa técnica vive esto y qué puede depender de qué?**
- VSA responde: **¿cómo agrupo el código dentro de esas capas — por feature o por tipo técnico?**

Lo que se va a construir en la Parte 2 es exactamente esa combinación híbrida: **Clean Architecture a nivel de proyectos** (Domain/Application/Infrastructure/API, dependencias correctas) + **VSA dentro de Application** (la carpeta `Features/Task/` no se disuelve en `Commands/`, `Queries/`, `Handlers/` — se mueve completa a `Application/Features/Task/`).

---

## 5. Trade-offs reales

Esto es lo que más preguntan. Es donde se separa un junior que "leyó sobre Clean Architecture" de un senior que entiende *cuándo no usarla*.

### Clean Architecture

**Qué ganas:**
- Reglas de dependencia forzadas por el compilador (no dependes de disciplina de equipo).
- Domain testeable en aislamiento total, sin BD ni framework.
- Cambiar de EF Core a Dapper, o de SQL Server a Postgres, no toca Domain ni Application.
- Es el estándar de facto en ofertas enterprise — te hace *contratable*.

**Qué pierdes / cuándo NO usarla:**
- **Ceremonia y sobrecoste**. Para un CRUD simple, crear 4 proyectos y una interfaz por cada dependencia es sobreingeniería evidente.
- **Feature dispersa**. Agregar "crear tarea" te obliga a tocar archivos en 4 assemblies distintos si se hace "por capas puro". La cohesión de una feature se rompe.
- **Abstracciones prematuras**. `IApplicationDbContext` sobre algo que ya es abstracción (`DbContext`) puede ser abstracción sobre abstracción.

### Vertical Slice Architecture

**Qué ganas:**
- **Cohesión máxima**. Todo lo de una feature (request, handler, validación, respuesta) vive junto. Es *exactamente* lo que ya tienes en `Features/Task/CreateTask.cs`.
- Agregar o borrar una feature toca un solo lugar. Bajo acoplamiento entre features.
- Menos ceremonia: cada slice usa lo que necesita, sin forzar una abstracción global.

**Qué pierdes / cuándo NO usarla:**
- Menos disciplina forzada: nada impide que un slice acceda directo a EF Core (a veces eso está *bien*, a veces se degrada).
- Puede haber duplicación entre slices (a veces preferible al acoplamiento oculto).
- Menos "reconocible" en ofertas que piden explícitamente Clean Architecture.
- Riesgo real cuando el equipo crece: nadie sabe "dónde se supone que va esto".

### Comparación lado a lado

| | Clean "por capas" (`Domain/Application/...` con subcarpetas `Commands/`, `Queries/`, `Handlers/` dentro de cada capa) | Vertical Slice (lo que ya se tiene) | Híbrido (a donde se va) |
|---|---|---|---|
| Para tocar una feature | Saltar entre 4-5 carpetas | Un solo archivo/carpeta | Un solo lugar dentro de Application |
| Para entender el límite arquitectónico | Explícito por proyecto | Difuso — todo vive junto | Explícito por proyecto, feature-cohesivo dentro |
| Costo de setup inicial | Alto (4 proyectos, referencias) | Bajo | Alto una vez, luego bajo por feature |
| Riesgo real | Anemic layers — capas que solo reenvían llamadas sin aportar valor | Acoplamiento oculto entre features que "se copian" código | Ninguno si el proyecto es mediano; over-engineering si el proyecto es trivial |
| Cuándo NO usarlo | — | Cuando el equipo crece y nadie sabe "dónde se supone que va esto" | Cuando el proyecto es un CRUD de 3 endpoints — ahí VSA sola sobra |

### La síntesis 

No son mutuamente excluyentes. **Puedes tener Clean Architecture como estructura macro (las capas) y Vertical Slice como organización dentro de Application (por feature, no por tipo técnico).** De hecho es lo que se va aplicar: `Application` va a tener carpetas `Features/Task/` (vertical slice) *dentro* de la disciplina de capas de Clean. Lo mejor de ambos.

**Postura pragmática para entrevista:** Clean Architecture da valor real cuando el dominio tiene lógica de negocio no trivial y se va a cambiar infraestructura (motor de BD, mensajería) sin tocar reglas de negocio. Para un CRUD puro, es ceremonia. El TaskManager está en el punto justo para aplicarlo — no porque el dominio sea complejo hoy, sino porque el objetivo es demostrarlo en entrevista, y eso es una razón legítima y distinta a "lo necesito en producción ahora mismo".

---

## 6. Pregunta de entrevista típica

**P: "¿Cuál es la diferencia entre Clean Architecture y Vertical Slice Architecture, y cuál usarías?"**

**R modelo:**

> "Depende del tamaño, la vida esperada del proyecto y el equipo. Clean Architecture organiza por capa técnica con una regla de dependencia estricta hacia el dominio; VSA organiza por feature, priorizando cohesión sobre separación técnica. No son excluyentes.
>
> Clean Architecture brilla en sistemas grandes y longevos, con equipos numerosos, donde necesitas que las reglas de dependencia las fuerce el compilador y no la disciplina — cambiar infraestructura o testear el dominio en aislamiento sale gratis. El costo es ceremonia: para un CRUD, montar cuatro proyectos e interfaces por cada dependencia es sobreingeniería.
>
> Vertical Slice prioriza la cohesión: todo lo de una feature vive junto, agregar o quitar funcionalidad toca un solo lugar. Es ideal para APIs medianas donde la velocidad de desarrollo importa más que aislar capas.
>
> En la práctica no las veo como opuestas. Suelo usar Clean Architecture como estructura macro — separación en Domain, Application, Infrastructure, API con la Regla de Dependencia — pero organizo la capa Application por features verticales en lugar de por tipos técnicos como `Commands/`, `Queries/`, `Dtos/`. Así obtengo la disciplina de las capas y la cohesión de los slices al mismo tiempo. Se agregan los límites duros de Clean Architecture cuando el dominio va a evolucionar independientemente de la infraestructura, o cuando se necesita que un test de arquitectura (NetArchTest, Semana 5) garantice que nadie mete EF Core en el dominio por accidente. Lo que evito es aplicar Clean dogmáticamente a un microservicio pequeño donde Vertical Slice puro sería más honesto."

**Por qué funciona esta respuesta:** demuestra que conoces ambas, que entiendes trade-offs, que no eres dogmático, y que has pensado en la *combinación* — que es lo que hacen los proyectos reales serios.

---

## 7. Siguiente paso práctico — qué preparar para la Parte 2

Antes de la sesión de reestructuración (Parte 2), tener esto listo/pensado:

1. **Crear la rama de trabajo de la semana** (`git checkout -b week-04` desde `main`), para no romper `main` durante la cirugía.
2. **Decidir una cosa en la próxima sesión** (no ahora): ¿`IApplicationDbContext` o `IRepository` para la inversión de dependencias? Recomendación adelantada: `IApplicationDbContext` — es lo que usa el template de Jason Taylor (el más popular), mantiene el poder de LINQ/proyecciones de EF Core, y evita la ceremonia de un repositorio por entidad. Pero se discute con el trade-off completo — incluyendo la alternativa de romper formalmente la regla en un punto puntual (Infrastructure referenciando directo a Application), documentando el trade-off.
3. **Repasar mentalmente** qué archivo del proyecto actual va a qué capa (usar la tabla de la sección 2). En la Parte 2 se estarán moviendo archivos y hay que tener el mapa claro.

En la Parte 2 se crea la estructura de carpetas/proyectos real: `TaskManager.Domain`, `TaskManager.Application`, `TaskManager.Infrastructure`, `TaskManager.Api`, se mueven las entidades, se arreglan referencias y se migra el DbContext y las migrations a Infrastructure.

**Nada de código de reestructuración todavía.** Eso es la Parte 2 — ahí se ensucian las manos.

---

## Estado de la Semana 4

| Bloque | Estado |
|--------|--------|
| **Parte 1 — Fundamentos arquitectónicos + Clean Architecture vs VSA (teoría y decisión)** | ✅ **Completado** |
| Parte 2 — Reestructuración física (Domain/Application/Infrastructure/API) | ⏳ Pendiente |
| Parte 3 — DDD táctico (Aggregate, Value Object, Domain Events) + cierre | ⏳ Pendiente |

Recordatorio: ya se está en condiciones de empezar a postularse desde la Semana 5–6 sin esperar el plan completo.
(✅, ⏳)

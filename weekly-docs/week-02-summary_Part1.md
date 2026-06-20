# Cierre Semana 2 (Parte 1) — Entity Framework Core — Lo que construiste y lo que debes saber defender

## Lo que construiste

La TaskManager API tiene ahora persistencia real con EF Core:

```
TaskManager.Api/
├── Program.cs                          ← DbContext registrado con AddDbContext + SQLite
├── Entities/
│   ├── Project.cs                      ← entidad con relación 1:N a TaskItem
│   ├── TaskItem.cs                     ← entidad principal con dos FK (Project, User)
│   └── User.cs                         ← entidad con colección de tareas asignadas
├── Persistence/
│   ├── TaskManagerDbContext.cs         ← DbContext con DbSet y ApplyConfigurationsFromAssembly
│   └── Configurations/
│       ├── TaskItemConfiguration.cs    ← Fluent API: relaciones, MaxLength, HasConversion
│       ├── ProjectConfiguration.cs     ← Fluent API: PK, MaxLength, DefaultValueSql
│       └── UserConfiguration.cs       ← Fluent API: PK, Email y Name requeridos
└── Migrations/
    ├── 20260620202701_InitialCreate.cs ← migration autogenerada — SQL real
    └── TaskManagerDbContextModelSnapshot.cs
```

La base de datos `taskmanager.db` está creada y contiene las tablas `Projects`, `Users` y `TaskItems` con sus relaciones e índices.

---

## Decisiones técnicas que tomaste y por qué

**Fluent API en lugar de Data Annotations**
Las entidades son POCO limpios sin atributos de infraestructura. La configuración de mapeo vive en `Configurations/`, separada del dominio. Si mañana cambias de EF Core a Dapper, las entidades no necesitan modificación.

**`ApplyConfigurationsFromAssembly` en lugar de registrar configuraciones manualmente**
EF Core escanea el assembly y aplica todas las clases que implementen `IEntityTypeConfiguration<T>` automáticamente. Agregar una entidad nueva solo requiere crear su `Configuration` — el `DbContext` no se toca.

**`HasConversion<string>()` en el enum `TaskStatus`**
La BD guarda `"Todo"`, `"InProgress"`, `"Done"` en lugar de `0`, `1`, `2`. Más legible en consultas directas a la BD y sin riesgo de romper datos si el orden del enum cambia.

**`OnDelete(DeleteBehavior.Cascade)` en ProjectId**
Si se elimina un proyecto, sus tareas se eliminan automáticamente.

**`OnDelete(DeleteBehavior.SetNull)` en AssignedToId**
Si se elimina un usuario, las tareas quedan sin asignar pero no se eliminan. Decisión de negocio explícita en el código.

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"¿Data Annotations o Fluent API, y por qué?"**

Fluent API siempre para proyectos serios. Data Annotations mezclan preocupaciones de infraestructura dentro del dominio — si mañana cambias el ORM, tus entidades quedan contaminadas. Con Fluent API las entidades son POCO limpios y la configuración de persistencia vive donde corresponde: en Infrastructure.

**"¿Qué es el DbContext y qué responsabilidades tiene?"**

Es el puente entre el código C# y la base de datos. Gestiona tres cosas: qué entidades mapear a tablas (`DbSet<T>`), cómo mapearlas (nombres, restricciones, relaciones via `OnModelCreating`), y el tracking de cambios en los objetos en memoria.

**"¿Qué es una migration en EF Core y para qué sirve?"**

Es un archivo C# autogenerado que describe los cambios al esquema de BD en un momento dado. EF Core lee tus clases y genera el SQL equivalente. `migrations add` genera el archivo, `database update` ejecuta ese SQL contra la BD. La tabla `__EFMigrationsHistory` registra qué migrations ya se aplicaron para no ejecutarlas dos veces.

**"¿Qué hace `OnModelCreating` y cuándo se ejecuta?"**

EF Core lo invoca una sola vez al arrancar la app, cuando construye el modelo interno. Es donde se define todo lo que EF Core no puede inferir por convención: longitudes máximas, conversiones de tipo, comportamientos de eliminación, valores por defecto.

---

## Lo que debes tener claro conceptualmente

**EF Core infiere por convención** — una propiedad `Id` es PK, una propiedad `ProjectId` en `TaskItem` es FK a `Project`. Fluent API solo es necesario para lo que las convenciones no cubren.

**`__EFMigrationsHistory`** — tabla que EF Core crea automáticamente para saber qué migrations ya aplicó. No la toques manualmente.

**Índices automáticos en FK** — EF Core creó `IX_TaskItems_ProjectId` e `IX_TaskItems_AssignedToId` sin que los definieras. Es comportamiento por defecto para todas las FK.

**SQLite en desarrollo, SQL Server en producción** — el cambio es solo en la cadena de conexión y el paquete NuGet. El código de la aplicación no cambia.

---

## Herramientas instaladas esta semana

| Herramienta | Tipo | Para qué |
|-------------|------|----------|
| `Microsoft.EntityFrameworkCore.Sqlite` | NuGet (proyecto) | Provider de SQLite para EF Core |
| `Microsoft.EntityFrameworkCore.Design` | NuGet (proyecto) | Soporte para generación de migrations |
| `dotnet-ef` | Herramienta global CLI | Comandos `migrations` y `database update` |

---

## Lo que viene (Parte 2 — Semana 2)

Los endpoints de `TaskEndpoints.cs` todavía tienen datos hardcodeados en memoria. La siguiente sesión conecta el CRUD completo a la BD:

- Inyectar `TaskManagerDbContext` en los endpoints de Minimal APIs
- `AsNoTracking()` en queries de lectura — por qué importa en APIs y cuándo omitirlo
- CRUD completo: `GET`, `POST`, `PUT`, `DELETE` contra SQLite
- El `ExceptionHandlingMiddleware` de Semana 1 capturando errores de BD automáticamente

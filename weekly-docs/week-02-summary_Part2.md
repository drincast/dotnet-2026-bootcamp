# Cierre Semana 2 (Parte 2) — CRUD real con EF Core — Lo que construiste y lo que debes saber defender

## Lo que construiste

Los endpoints de `TaskEndpoints.cs` dejaron de tener datos hardcodeados en memoria y ahora operan contra la base de datos SQLite real. El CRUD completo está conectado y probado:

```
TaskManager.Api/
├── TaskEndpoints.cs                ← CRUD completo contra TaskManagerDbContext
│   ├── GetAll    → GET    /tasks            (paginado, AsNoTracking, proyección)
│   ├── GetById   → GET    /tasks/{id:int}   (AsNoTracking, proyección)
│   ├── Create    → POST   /tasks            (Add + SaveChangesAsync, 201 Created)
│   ├── Update    → PUT    /tasks/{id:int}   (FindAsync + SaveChangesAsync, 204)
│   └── Delete    → DELETE /tasks/{id:int}   (FindAsync + Remove, 204)
└── (BD taskmanager.db generada con dotnet ef database update)
```

Se probó el flujo completo end-to-end: crear, modificar y consultar tareas funcionan contra SQLite. El `ExceptionHandlingMiddleware` de Semana 1 quedó validado capturando errores de BD automáticamente.

---

## Decisiones técnicas que tomaste y por qué

**`DbContext` inyectado por parámetro en los endpoints**
El `AddDbContext` registrado en `Program.cs` deja el `TaskManagerDbContext` como servicio **Scoped**. Minimal APIs lo inyecta por request solo con pedirlo en la firma del método. Un `DbContext` por request es el lifetime correcto: no es thread-safe y no debe compartirse entre requests.

**`AsNoTracking()` en todas las lecturas (`GetAll`, `GetById`)**
EF Core, por defecto, guarda un snapshot de cada entidad materializada para detectar cambios en `SaveChanges`. En queries de solo lectura eso es trabajo y memoria desperdiciados. `AsNoTracking` lo desactiva. En las escrituras (`Update`, `Delete`) se mantiene el tracking por defecto, que es lo que EF Core necesita para generar el UPDATE/DELETE correcto.

**Proyección con `Select` a un objeto anónimo, no devolver la entidad**
Devolver `TaskItem` directamente acoplaría el contrato de la API al esquema de BD, arrastraría propiedades de navegación y abriría la puerta a ciclos de serialización. El `Select` además hace que EF Core traiga solo las columnas necesarias. El enum `TaskStatus` se traduce a `Done = t.Status == TaskStatus.Done` para mantener el contrato de salida.

**`OrderBy(t => t.Id)` obligatorio antes de paginar**
SQL no garantiza orden sin `ORDER BY`. `Skip/Take` sobre datos sin ordenar puede repetir o saltar filas entre páginas. EF Core incluso lanza warning. Con el array en memoria el orden venía gratis por posición; contra BD hay que declararlo explícitamente.

**`CancellationToken` propagado a todos los métodos async de EF Core**
Representa la conexión del cliente. Si el cliente abandona el request, EF Core cancela la query en la BD en lugar de seguir consumiendo recursos. Casi nadie junior lo hace; en producción es lo correcto.

**`CreatedAt` generado en C# con `= DateTime.UtcNow`, no con `HasDefaultValueSql`**
Se quitó `HasDefaultValueSql("GETUTCDATE()")` del `TaskItemConfiguration` porque `GETUTCDATE()` es función de SQL Server y no existe en SQLite (`unknown function`). Asignar el valor en la entidad mantiene el código agnóstico del proveedor: funciona igual en SQLite (dev) y SQL Server (prod), y la lógica de "cuándo se creó" vive en el dominio, no en SQL específico de un motor.

---

## Pendientes a resolver ANTES de continuar a Semana 3

Estos dos son limpieza/corrección, no bloquean la funcionalidad pero deben hacerse antes del refactor a MediatR para no arrastrar deuda.

### 1. Limpieza de código muerto en `GetAll`

Quedaron restos de la Semana 1 que ensucian el método:
- El array hardcodeado de 11 tareas ya no se usa, pero la respuesta todavía devuelve `TotalItems = tasks.Length` (conteo del array viejo = 11) **junto a** `TotalItems2 = totalItems` (conteo real de la BD). Se reportan dos totales y uno es falso. Debe quedar solo el de la BD (`TotalItems = totalItems`).
- Comentarios obsoletos ("retorna lista harcodeada por ahora", bloques `Skip/Take` comentados sobre el array, la línea `throw` de prueba).

`GetAll` limpio debe quedar:

```csharp
private static async Task<IResult> GetAll(
        IOptions<TaskManagerOptions> options,
        TaskManagerDbContext db,
        CancellationToken ct,
        int page = 1,
        int? pageSize = null)
{
    var size = pageSize ?? options.Value.DefaultPageSize;
    var totalItems = await db.TaskItems.CountAsync(ct);

    var items = await db.TaskItems
        .AsNoTracking()
        .OrderBy(t => t.Id)
        .Skip((page - 1) * size)
        .Take(size)
        .Select(t => new { t.Id, t.Title, Done = t.Status == TaskStatus.Done })
        .ToListAsync(ct);

    return Results.Ok(new
    {
        Page = page,
        PageSize = size,
        TotalItems = totalItems,   // solo el de la BD
        Items = items
    });
}
```

### 2. `Enum.Parse` en `Update` debe ser `Enum.TryParse` → 400, no 500

Línea actual:

```csharp
taskItem.Status = Enum.Parse<TaskStatus>(request.Status);
```

Si el cliente manda un valor inexistente (ej. `"Status": "Terminado"`), `Enum.Parse` lanza excepción → el middleware la captura → `500`. Pero esto es un **error del cliente** (dato inválido), no del servidor, así que debe ser `400 Bad Request`:

```csharp
if (!Enum.TryParse<TaskStatus>(request.Status, ignoreCase: true, out var status))
    return Results.BadRequest($"Estado inválido: {request.Status}");

taskItem.Title = request.Title;
taskItem.Status = status;
await db.SaveChangesAsync(ct);
```

`TryParse` no lanza y deja elegir el código de respuesta correcto. Distinguir 400 (culpa del cliente) de 500 (culpa del servidor) es un detalle típico de entrevista senior.

---

## Anotado para más adelante: el `transfer closed` de curl (se ve a fondo en Semana 8)

Al probar la captura de errores, el cliente recibía:

```
{"error":"Ocurrio un error interno. Comuniquese con el administrador."}
curl: (18) transfer closed with outstanding read data remaining
```

…y en consola sí aparecía la pila completa. El warning clave era:
`"The response has already started, the error page middleware will not be executed."`

**Explicación:** ese error no es de la API, es de **cómo el middleware escribe la respuesta**. Pasa cuando se escribe el body del error pero no se ajusta bien el `Content-Length`, o cuando la respuesta ya había empezado a enviar bytes al cliente antes de que el middleware intentara escribir el JSON de error (eso es lo que dice `response has already started`).

En este caso funcionó porque el error vino temprano, pero en otros escenarios el middleware no podrá sobreescribir una respuesta que ya empezó. La práctica senior es validar `context.Response.HasStarted` antes de escribir, y usar `Results.Problem` / `ProblemDetails` (formato estándar RFC 7807) en lugar de un JSON casero.

**Esto se refina a fondo en la Semana 8 (Observabilidad)** — por ahora queda anotado, no es bloqueante.

---

## Preguntas de entrevista que ya puedes responder con código real detrás

**"¿Por qué no devuelves la entidad de EF Core directamente desde el endpoint?"**

Porque acopla el contrato público de la API al esquema de persistencia. Cualquier cambio en la BD rompe clientes; las propiedades de navegación causan ciclos de serialización o N+1 al lazy-loadear; y expones campos internos. Un DTO con proyección `Select` desacopla ambos mundos, controla exactamente qué se serializa y deja que EF Core traiga solo las columnas necesarias.

**"¿Por qué tu paginación necesita un `OrderBy`?"**

Porque SQL no garantiza orden sin `ORDER BY`. Sin él, `Skip/Take` puede devolver filas repetidas o saltadas entre páginas, ya que el motor es libre de devolver las filas en cualquier orden. EF Core incluso lanza warning si paginas sin ordenar.

**"¿Cuándo usas `AsNoTracking` y cuándo no?"**

`AsNoTracking` en lecturas (`GET`): más rápido y menos memoria porque EF Core no guarda el snapshot de cambios. Sin él (tracking por defecto) en operaciones de cargar-modificar-guardar, porque EF Core necesita el snapshot para detectar qué cambió y generar el UPDATE. Con proyección a DTO el tracking es irrelevante de todas formas, pero ponerlo deja la intención explícita.

**"¿Dónde generas valores como `CreatedAt`: en la BD o en la aplicación?"**

En la app, salvo necesidad concreta (auditoría centralizada con relojes desincronizados). Generar en C# mantiene el código agnóstico del proveedor y testeable sin tocar la BD. `HasDefaultValueSql` mete SQL específico de un motor y truena al cambiar de proveedor — justo lo que pasó con `GETUTCDATE()` en SQLite.

**"¿Cuál es la diferencia entre `migrations add` y `database update`?"**

`add` genera el archivo C# que describe el cambio de esquema (no toca la BD). `update` ejecuta ese SQL contra la base y registra la migration en `__EFMigrationsHistory` para no repetirla. Compilar (`build`) no crea la BD: solo valida el código; la BD nace con `update`.

---

## Dónde vamos en la Semana 2

| Bloque | Estado |
|--------|--------|
| Parte 1 — DbContext, entidades, Fluent API, migrations, BD creada | ✅ Completado |
| Parte 2 — CRUD real conectado a SQLite, AsNoTracking, middleware validado | ✅ Completado (con 2 pendientes de limpieza) |

### Lo que falta de Semana 2 (objetivos del plan aún no cubiertos)

Estos temas los lista el plan para Semana 2 y son los **más preguntados en entrevista** del bloque EF Core. Recomendado verlos antes de saltar a Semana 3 (MediatR), porque CQRS asume que ya dominas las queries:

- **Relaciones en queries**: `Include` vs proyección con `Select`
- **El problema N+1** — qué es, cómo detectarlo, cómo evitarlo
- **`Include` vs Split Queries** — traer Tareas con Proyecto/Usuario sin multiplicar filas
- **Cuándo EF Core no basta**: Dapper / ADO.NET / Raw SQL seguro (`FromSql` parametrizado)
- **Migrations en CI/CD** — `migrations bundle`, aplicar migrations en pipeline
- **Repository Pattern** — cuándo agrega valor y cuándo es over-engineering (se solapa con Semana 3)

---

## Próxima sesión

Opciones para retomar:
1. Aplicar los 2 pendientes de limpieza (código muerto + `Enum.TryParse`) y cerrar la semana formalmente
2. Mini-sesión de relaciones + N+1 + proyecciones (lo más valioso para entrevista del bloque EF Core)
3. Saltar a Semana 3 — MediatR / CQRS

Recordatorio: ya estás en condiciones de empezar a postularte desde la Semana 5–6 sin esperar el plan completo.

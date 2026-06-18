# Semana 1 — .NET 10 + ASP.NET Core + Minimal APIs

**Plan de estudio:** Senior .NET — Hoja de ruta 10 semanas  
**Rama:** `week-01`  
**Horas estimadas:** 15–18h  
**Estado:** ✅ Completada

---

## Objetivo de la semana

Migración mental de .NET Framework 4.8 a .NET moderno. Entender el nuevo modelo de hosting,
construir el esqueleto de la TaskManager API con Minimal APIs, configuración tipada y middleware custom.

---

## Conceptos cubiertos

### 1. Nuevo modelo de hosting — `Program.cs`

- Desaparece `Global.asax`, `Web.config`, `Startup.cs` separado
- Todo el arranque de la aplicación vive en un único `Program.cs` con **top-level statements**
- El compilador genera el método `Main` implícitamente (C# 9+)
- El archivo tiene **dos fases bien separadas**:

| Fase | Método | Qué hace |
|------|--------|----------|
| Registro de servicios | `builder.Services.Add...()` | Declara qué existe en el contenedor DI |
| Pipeline HTTP | `app.Use...()` / `app.Map...()` | Define el orden de ejecución de middleware |

### 2. Dependency Injection (DI)

- DI nativa desde el inicio — sin librerías externas
- El framework entrega las dependencias, no las creas con `new`
- Registro en Fase 1: `builder.Services.AddScoped<IServicio, Implementacion>()`
- Inyección automática en endpoints como parámetros del método

### 3. Minimal APIs

- Estilo moderno para definir endpoints HTTP sin Controllers
- Endpoints registrados directamente: `app.MapGet()`, `app.MapPost()`, etc.
- Patrón de organización con **extension methods** para mantener `Program.cs` limpio
- Parámetros de ruta, query string y body resueltos automáticamente por el framework

### 4. Sistema de configuración

- Reemplaza completamente a `Web.config` y `ConfigurationManager`
- **Jerarquía de capas** (menor a mayor prioridad):
  1. `appsettings.json` — valores base
  2. `appsettings.{Environment}.json` — sobreescribe por ambiente
  3. Variables de entorno — sobreescriben todo
  4. Argumentos de línea de comandos — máxima prioridad
- `IOptions<T>` para configuración tipada — recomendado para el 90% de los casos
- `IOptionsMonitor<T>` solo cuando se necesitan cambios en caliente sin redeploy
- En producción la config vive en variables de entorno, no en archivos JSON

### 5. Pipeline de Middleware

- Cadena de componentes que procesan cada request HTTP en orden
- Flujo **bidireccional**: el código antes de `await _next(context)` ejecuta en la entrada,
  el código después ejecuta cuando la respuesta vuelve del endpoint
- El orden de registro importa — `ExceptionHandling` siempre primero
- Diferencia clave: **middleware** es para lógica transversal (auth, logging, errores),
  los **servicios DI** son para lógica de negocio

---

## Proyecto — TaskManager API

### Estructura de archivos

```
TaskManager.Api/
├── Program.cs                      ← configuración limpia, dos fases claras
├── TaskEndpoints.cs                ← endpoints organizados con extension methods
├── CreateTaskRequest.cs            ← modelo de entrada (record)
├── TaskManagerOptions.cs           ← configuración tipada con IOptions<T>
├── RequestLoggingMiddleware.cs     ← logging transversal (método + path + tiempo)
└── ExceptionHandlingMiddleware.cs  ← manejo global de errores (devuelve 500 limpio)
```

### Endpoints implementados

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/tasks` | Lista paginada de tareas |
| `GET` | `/tasks/{id}` | Tarea por ID |
| `POST` | `/tasks` | Crear tarea |
| `DELETE` | `/tasks/{id}` | Eliminar tarea |

### Paginación en `GET /tasks`

```
GET /tasks                    → página 1, pageSize del config
GET /tasks?page=2&pageSize=3  → página 2, 3 items por página
```

Respuesta estructurada:
```json
{
  "page": 1,
  "pageSize": 5,
  "totalItems": 11,
  "items": [ ... ]
}
```

### Configuración (`appsettings.json`)

```json
{
  "TaskManager": {
    "MaxTasksPerUser": 100,
    "AllowCompletedTaskDeletion": true,
    "DefaultPageSize": 5
  }
}
```

### Pipeline de middleware configurado

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>(); // primero — captura errores de toda la cadena
app.UseMiddleware<RequestLoggingMiddleware>();    // loguea método, path y tiempo de respuesta
app.UseHttpsRedirection();
app.MapTaskEndpoints();
```

---

## Decisiones técnicas tomadas

| Decisión | Alternativa descartada | Motivo |
|----------|----------------------|--------|
| Minimal APIs | Controllers | Proyecto nuevo, endpoints simples, menos ceremonial |
| `IOptions<T>` | `IOptionsMonitor<T>` | No se necesitan cambios en caliente; en producción se usa variables de entorno |
| Extension methods para endpoints | Todo en `Program.cs` | Mantenibilidad — `Program.cs` limpio solo con configuración |
| Datos hardcodeados en memoria | — | Persistencia real se agrega en Semana 2 con EF Core |

---

## Preguntas de entrevista que puedo responder

- ¿Qué cambió en el modelo de hosting de .NET moderno respecto a .NET Framework?
- ¿Cuándo usarías Minimal APIs y cuándo Controllers?
- ¿Cómo funciona el pipeline de middleware y por qué importa el orden?
- ¿Cómo manejas configuración en .NET moderno?
- ¿Diferencia entre `IOptions<T>`, `IOptionsSnapshot<T>` e `IOptionsMonitor<T>`?
- ¿Qué son los top-level statements y cómo funciona el punto de entrada en .NET moderno?

---

## Pendiente para Semana 2

- [ ] Agregar EF Core con Code First
- [ ] Crear entidades: `Task`, `Project`, `User`
- [ ] Configurar `DbContext` con SQLite (local)
- [ ] Implementar migrations
- [ ] Reemplazar datos hardcodeados por persistencia real en todos los endpoints
- [ ] El `ExceptionHandlingMiddleware` capturará automáticamente errores de BD

---

## Recursos utilizados

- [ASP.NET Core fundamentals — docs.microsoft.com](https://docs.microsoft.com/aspnet/core)
- [Nick Chapsas — Getting started with .NET 8 and ASP.NET Core (YouTube)](https://www.youtube.com/@nickchapsas)
- [dotnet/aspnetcore GitHub](https://github.com/dotnet/aspnetcore)

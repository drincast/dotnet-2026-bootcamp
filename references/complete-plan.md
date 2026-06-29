# Plan completo — Detalle por semana

## Semana 1 — .NET 10 y ASP.NET Core (15–18h)

**Tecnologías**: .NET 10 SDK, ASP.NET Core, Minimal APIs, Program.cs sin Startup, top-level statements

**Objetivos**:
- Entender el nuevo modelo de hosting unificado y diferencias con .NET Framework
- Crear Web APIs con Minimal APIs y con controllers — saber cuándo usar cada uno
- Dominar el sistema de configuración (appsettings, environment variables, IOptions<T>)
- Comprender el pipeline de middleware y cómo añadir el tuyo propio

**Recursos**:
- Docs oficiales: ASP.NET Core fundamentals — docs.microsoft.com/aspnet/core
- Nick Chapsas — "Getting started with .NET 8 and ASP.NET Core" (YouTube)
- dotnet/aspnetcore GitHub — ejemplos oficiales
- Andrew Lock — "ASP.NET Core in Action" 3ra edición (Manning)

**Proyecto**: Crear el esqueleto de TaskManager API. Configurar proyecto, endpoints CRUD básicos
con Minimal APIs, middleware de logging custom.

---

## Semana 2 — Entity Framework Core (15–18h)

**Tecnologías**: EF Core 10, Code First, Migrations, DbContext pooling, Raw SQL seguro, Repository pattern

**Objetivos**:
- Dominar Code First con Fluent API (no Data Annotations para proyectos serios)
- Optimizar rendimiento: AsNoTracking, proyecciones con Select, Include vs Split queries
- Configurar migraciones correctamente en entornos de CI/CD
- Conocer cuándo EF Core es suficiente y cuándo usar Dapper o ADO.NET directo

**Recursos**:
- EF Core docs oficiales — learn.microsoft.com/ef/core
- Milan Jovanović — serie EF Core (YouTube)
- dotnet/EntityFramework.Docs — repositorios de ejemplo

**Proyecto**: Añadir persistencia a TaskManager API. DbContext con SQLite o SQL Server.
Relaciones Tarea/Proyecto/Usuario. Migrations automatizadas.

---

## Semana 3 — DI, SOLID y patrones vigentes (15h)

**Tecnologías**: DI nativo .NET, Lifetimes, CQRS básico, MediatR, Repository + Unit of Work, Options pattern

**Objetivos**:
- Dominar los tres lifetimes (Transient, Scoped, Singleton) y sus implicaciones en APIs
- Implementar CQRS con MediatR — patrón dominante en proyectos reales actuales
- Entender cuándo el Repository Pattern agrega valor y cuándo es over-engineering
- Aplicar los principios SOLID de forma pragmática (no dogmática)

**Recursos**:
- Nick Chapsas — "Clean Architecture with .NET" (YouTube)
- ardalis/CleanArchitecture — template de referencia en GitHub
- MediatR docs — github.com/jbogard/MediatR/wiki

**Proyecto**: Refactorizar TaskManager API introduciendo MediatR. Separar Commands y Queries.
Pipeline Behavior para logging automático de requests.

---

## Semana 4 — Clean Architecture y Vertical Slice (18–20h)

**Tecnologías**: Clean Architecture, Vertical Slice Architecture, Domain-Driven Design (táctico), feature folders

**Objetivos**:
- Construir solución completa con Clean Architecture (Domain, Application, Infrastructure, API)
- Entender Vertical Slice Architecture como alternativa más pragmática para APIs
- Conocer conceptos tácticos de DDD: Aggregates, Value Objects, Domain Events
- Articular en entrevista los trade-offs de cada arquitectura

**Recursos**:
- jasontaylordev/CleanArchitecture — template oficial más usado en GitHub
- Milan Jovanović — "Clean Architecture" y "Vertical Slice Architecture" (YouTube)
- "Domain-Driven Design Distilled" — Vaughn Vernon (libro corto)

**Proyecto**: Reestructurar TaskManager API en Clean Architecture. Domain layer con entidades
y value objects. Application layer con casos de uso. Infrastructure con EF Core.

---

## Semana 5 — Testing (15–18h)

**Tecnologías**: xUnit, Moq / NSubstitute, FluentAssertions, WebApplicationFactory, TestContainers, Architecture tests

**Objetivos**:
- Escribir tests unitarios para Application layer con mocks efectivos
- Crear integration tests de API usando WebApplicationFactory con BD real (TestContainers)
- Conocer architecture tests con NetArchTest para validar dependencias entre capas
- Entender la pirámide de testing y qué nivel priorizar según contexto

**Recursos**:
- Nick Chapsas — "Integration Testing in ASP.NET Core" (YouTube)
- testcontainers/testcontainers-dotnet — docs y ejemplos
- FluentAssertions docs — fluentassertions.com

**Proyecto**: Suite de tests para TaskManager API. Unit tests para todos los handlers.
Integration tests end-to-end con TestContainers (SQL Server real).

---

## Semana 6 — Seguridad: JWT, OAuth 2.0, OpenID Connect (15–18h)

**Tecnologías**: JWT, OAuth 2.0, OpenID Connect, ASP.NET Core Identity, Keycloak / Azure Entra ID, Policy-based authorization

**Objetivos**:
- Implementar autenticación JWT desde cero y entender cada parte del token
- Entender el flujo OAuth 2.0 Authorization Code con PKCE (estándar actual)
- Integrar con un Identity Provider externo (Keycloak local o Azure Entra ID)
- Implementar authorization policies basadas en claims y roles

**Recursos**:
- Milan Jovanović — "JWT Authentication and Authorization in .NET 8" (YouTube)
- OAuth 2.0 RFC 6749 — resumen en oauth.net/2
- keycloak/keycloak — para montar un IdP local con Docker

**Proyecto**: Asegurar TaskManager API con JWT. Endpoints públicos y protegidos.
Refresh tokens. Roles: Admin y User con autorización diferenciada.

---

## Semana 7 — Docker y containerización (15h)

**Tecnologías**: Docker, Dockerfile multi-stage, docker-compose, container registries, health checks

**Objetivos**:
- Crear Dockerfiles optimizados y multi-stage para APIs .NET 8
- Orquestar servicios localmente con docker-compose (API + DB + Redis)
- Configurar health checks y variables de entorno para contenedores
- Publicar imágenes a un container registry (Docker Hub o ACR)

**Recursos**:
- Docs oficiales: Containerize a .NET app — learn.microsoft.com
- Nick Chapsas — "Docker for .NET developers" (YouTube)
- dotnet/dotnet-docker — ejemplos de Dockerfiles oficiales

**Proyecto**: Dockerizar TaskManager API completa. docker-compose con la API, SQL Server y Seq.
Configurar health checks. Imagen publicada en Docker Hub.

---

## Semana 8 — Observabilidad (12–15h)

**Tecnologías**: Serilog, Structured logging, OpenTelemetry, Seq, Prometheus + Grafana, Correlation IDs

**Objetivos**:
- Implementar structured logging con Serilog y enrichers útiles (userId, correlationId)
- Entender los tres pilares de observabilidad: logs, métricas y trazas distribuidas
- Configurar OpenTelemetry para tracing básico entre servicios
- Conocer las herramientas del ecosistema: Seq (dev), Application Insights (Azure)

**Recursos**:
- serilog/serilog — repositorio oficial con ejemplos
- OpenTelemetry .NET docs — opentelemetry.io/docs/instrumentation/net
- Milan Jovanović — "Observability in .NET 8" (YouTube)

**Proyecto**: Añadir Serilog con sink a Seq. Correlation IDs en todos los requests.
MediatR Pipeline Behavior para logging estructurado. Health check endpoint.

---

## Semana 9 — Microservicios y mensajería (18–20h)

**Tecnologías**: RabbitMQ, MassTransit, Azure Service Bus, Saga Pattern, Outbox Pattern, API Gateway (YARP)

**Objetivos**:
- Entender cuándo y por qué usar microservicios (y cuándo NO)
- Implementar comunicación asíncrona con RabbitMQ vía MassTransit
- Conocer el patrón Outbox para garantizar consistencia eventual
- Configurar un API Gateway básico con YARP

**Recursos**:
- Milan Jovanović — "Microservices in .NET" serie completa (YouTube)
- MassTransit docs — masstransit.io
- MassTransit/MassTransit — ejemplos con RabbitMQ y Azure Service Bus

**Proyecto**: Extraer notificaciones como microservicio separado. TaskManager publica eventos
de dominio. NotificationService los consume vía RabbitMQ/MassTransit. Outbox Pattern implementado.

---

## Semana 10 — Azure, CI/CD y preparación final (15–18h)

**Tecnologías**: Azure App Service, Azure Container Apps, GitHub Actions, Azure DevOps (básico), ARM / Bicep (nociones)

**Objetivos**:
- Desplegar la aplicación a Azure App Service o Container Apps desde GitHub Actions
- Crear un pipeline CI/CD completo: build, test, Docker build & push, deploy
- Conocer los servicios Azure más relevantes para un dev Senior .NET
- Preparar respuestas para las preguntas técnicas más frecuentes en entrevistas

**Recursos**:
- Azure learning paths — learn.microsoft.com/azure (gratuito)
- GitHub Actions docs — docs.github.com/actions
- Nick Chapsas — "CI/CD for .NET with GitHub Actions" (YouTube)

**Proyecto**: Pipeline CI/CD completo en GitHub Actions para TaskManager API.
Deploy automático a Azure Container Apps. README profesional para el portfolio.

---

## Proyecto práctico incremental: TaskManager API

Un solo proyecto que evoluciona semana a semana:

- **S1**: Skeleton de Web API con Minimal APIs, middleware custom, configuración
- **S2**: Persistencia con EF Core, relaciones Tarea/Proyecto/Usuario, migrations
- **S3**: Refactor con MediatR — Commands, Queries, Pipeline Behaviors para logging
- **S4**: Restructuración en Clean Architecture (Domain / Application / Infrastructure / API)
- **S5**: Suite de tests — unit para handlers, integration con TestContainers
- **S6**: Seguridad JWT — endpoints protegidos, refresh tokens, roles Admin/User
- **S7**: Dockerización completa — Dockerfile multi-stage, docker-compose (API + SQL + Seq)
- **S8**: Structured logging con Serilog, correlation IDs, health checks, OpenTelemetry básico
- **S9**: Microservicio de notificaciones — domain events, RabbitMQ vía MassTransit, Outbox Pattern
- **S10**: Pipeline CI/CD en GitHub Actions, deploy a Azure Container Apps, README profesional

---

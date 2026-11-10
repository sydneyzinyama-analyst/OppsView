# OppsView API

ASP.NET Core 8 Web API with EF Core 8 and PostgreSQL. Setup steps are in the [root README](../README.md#running-it-locally).

## Endpoints

All `/applications` endpoints need `Authorization: Bearer <token>`. In Swagger, call `/auth/login`, then click **Authorize** and paste the token.

| Method | Route | Description |
|---|---|---|
| POST | `/auth/register` | Create an account (`email`, `password` of 8+ characters) and get a token. Returns 409 if the email is taken. |
| POST | `/auth/login` | Get a token. Returns 401 for a wrong email or password (same message for both). |
| GET | `/applications` | Your applications, newest `appliedOn` first. |
| GET | `/applications/{id}` | One application. |
| POST | `/applications` | Create one. Returns 201 with a `Location` header. |
| PUT | `/applications/{id}` | Edit company, role, platform, date, link and notes. Status is not editable here. |
| DELETE | `/applications/{id}` | Delete it and its history. Returns 204. |
| PATCH | `/applications/{id}/status` | `{ "status": "Interview" }`. Validated against the transition rules. |
| GET | `/applications/{id}/history` | Status changes, oldest first. |

Errors use [RFC 7807 problem details](https://www.rfc-editor.org/rfc/rfc7807). A validation error is a 400 with camelCase field names, for example `{ "errors": { "appliedOn": ["Applied date can't be in the future."] } }`. Data annotation failures and domain rule failures produce the same shape.

### Status transitions

```
Applied ──▶ Viewed ──▶ Interview ──▶ Offer
   │          │            │
   └──────────┴────────────┴──────▶ Rejected
```

Moves only go forward (stages can be skipped), and Offer and Rejected are final. The rules are in `Domain/StatusTransitions.cs`, and every application response includes `allowedNextStatuses`.

## Project structure

```
src/OppsView.Api/
  Domain/          Entities and business rules. No EF Core or ASP.NET dependencies.
  Data/            AppDbContext, Fluent API mappings (Configurations/), Migrations/
  Features/
    Applications/  Controller, service and DTOs for applications
    Auth/          Register/login, JWT issuing, options
  Infrastructure/  Cross-cutting pieces: current user, DomainException → 400 filter
  Program.cs       Composition root: DI, auth, JSON, CORS, Swagger
tests/OppsView.Api.Tests/
  Domain/          Unit tests for entities and transition rules
  Auth/            Token lifetime rules
  Api/             Integration tests over HTTP (WebApplicationFactory + SQLite)
```

The folders group code **by feature** (`Features/Applications`), not by technical layer (`Controllers/`, `Services/`), so everything about one feature is in one place.

## .NET patterns used here (a quick guide)

- **Dependency injection.** `Program.cs` registers services (`builder.Services.AddScoped<IApplicationService, ApplicationService>()`), and ASP.NET Core passes them into constructors. *Scoped* means one instance per HTTP request, the same lifetime as the `DbContext` they use. *Singleton* means one instance for the whole app.
- **Primary constructors.** `class ApplicationService(AppDbContext db, ...)` is C# 12 shorthand for a constructor that stores its parameters.
- **Options pattern.** `JwtOptions` is bound from the `Jwt` configuration section and validated with data annotations at startup (`ValidateOnStart`), so a missing signing key fails immediately instead of on the first login.
- **Configuration layering.** `appsettings.json` → `appsettings.Development.json` → user-secrets (Development only) → environment variables. Later sources win. In an environment variable, `Jwt__SigningKey` maps to `Jwt:SigningKey`.
- **`TimeProvider`** (new in .NET 8). Code asks for the time through an injected clock, so tests can use `FakeTimeProvider`. Token issuing and token validation both use it.
- **EF Core Fluent API.** Mappings live in `IEntityTypeConfiguration<T>` classes rather than attributes on entities, which keeps the domain free of persistence concerns.
- **`[ApiController]`.** Automatically returns a 400 `ValidationProblemDetails` when a request DTO fails its `[Required]`/`[MaxLength]` checks.

## Configuration reference

| Key | Where | Notes |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets / env `ConnectionStrings__Default` | Npgsql connection string |
| `Jwt:SigningKey` | user-secrets / env `Jwt__SigningKey` | **Secret.** Random, 32+ characters |
| `Jwt:Issuer`, `Jwt:Audience` | appsettings.json | |
| `Jwt:ExpiryMinutes` | appsettings.json | Default 480 (8 hours) |
| `Cors:AllowedOrigins` | appsettings.Development.json | `http://localhost:5173` locally |

## Common commands

```powershell
dotnet test                                                     # all tests
dotnet ef migrations add <Name> --project src/OppsView.Api --output-dir Data/Migrations
dotnet ef database update --project src/OppsView.Api
dotnet user-secrets list --project src/OppsView.Api
```

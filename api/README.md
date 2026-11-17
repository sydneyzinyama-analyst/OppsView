# OppsView API

ASP.NET Core 10 Web API with EF Core 10 and PostgreSQL. .NET 10 is the current long-term-support (LTS) release, supported until November 2028. Setup steps are in the [root README](../README.md#running-it-locally).

## Endpoints

All `/applications`, `/email`, `/cv`, `/match` and `/reminders` endpoints (except the Google callback) need `Authorization: Bearer <token>`. In Swagger, call `/auth/login`, then click **Authorize** and paste the token.

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
| GET | `/email/status` | Whether Gmail import is available and connected, the last sync time and the number of emails needing review. |
| POST | `/email/google/connect` | Returns Google's consent URL (read-only Gmail, PKCE). Returns 503 if the server has no Google settings. |
| GET | `/email/google/callback` | Google's redirect back after consent (anonymous; the encrypted `state` identifies the user). Redirects to the web app with `?gmail=connected`, `denied`, `scope`, `expired` or `error`. |
| DELETE | `/email/google` | Disconnect: revokes the token at Google and deletes it. |
| POST | `/email/sync` | Import new job emails. Returns counts (`created`, `updated`, `needsReview`...). Returns 409 if Gmail needs reconnecting or a sync is already running. |
| GET | `/email/review` | Emails OppsView wasn't sure about. |
| POST | `/email/review/{id}/application` | Create an application from a reviewed email (same body as `POST /applications`). A rejection email marks it Rejected. |
| POST | `/email/review/{id}/dismiss` | Ignore a reviewed email. |
| GET | `/cv` | Your stored CV (file name, upload time, preview), or 404. |
| PUT | `/cv` | Upload or replace your CV (multipart `file`: PDF, .docx, .txt, max 5 MB). The matcher reads it, and only the text is kept. 400 if it can't be read. |
| DELETE | `/cv` | Remove your CV. |
| POST | `/match` | `{ jobAd, applicationId? }`: match your CV against the ad. With `applicationId`, the score and result are saved on that application (`matchScore`). 409 without a CV, 503 if the matcher isn't running. |
| GET | `/reminders` | Follow-up reminders under the bell: open, not snoozed, and still relevant to the application's status. |
| GET | `/reminders/count` | `{ open }` for the bell's badge. |
| POST | `/reminders/{id}/done` · `/dismiss` · `/snooze` | Close a reminder, or hide it for `{ days }` (1–30). |
| GET / PUT | `/reminders/settings` | `{ enabled, followUpDays, moveOnDays, afterInterviewDays, offerReplyDays }` (0 = off). Saving announces the change to the notifier. |
| GET | `/analytics?weeks=12` | Insights: totals, applications per week, response rate by platform, funnel, where rejections happen, waiting times, top missing skills, email vs manual. |
| GET | `/health` · `/health/ready` | Anonymous. "Up", and "up and the database answers": for the hosting platform's probes. |
| GET | `/applications/{id}/match` | The match saved on an application (the ad and the full result). |

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
    Reminders/     Reminders from the notifier: RabbitMQ consumer, inbox, service, settings
    Analytics/     The Insights numbers (GET /analytics)
    Matching/      CV upload and matching: MatcherClient (HTTP to the Python matcher), service, controller
    EmailImport/   Gmail import:
      Parsing/       Rule-based email parser (text cleaning, classifier, company/role/platform)
      Gmail/         Google OAuth, Gmail API calls, sync service
      EmailImporter  Turns parsed emails into application changes (or "needs review")
  Infrastructure/  Cross-cutting pieces: current user, DomainException → 400 filter,
                   Messaging/ (outbox interceptor, dispatcher, RabbitMQ publisher, event types)
  Program.cs       Composition root: DI, auth, JSON, CORS, Swagger
tests/OppsView.Api.Tests/
  Domain/          Unit tests for entities and transition rules
  Auth/            Token lifetime rules
  EmailImport/     Parser, importer and Gmail message tests; Samples/ holds real anonymised emails
  Api/             Integration tests over HTTP (WebApplicationFactory + SQLite)
```

The folders group code **by feature** (`Features/Applications`), not by technical layer (`Controllers/`, `Services/`), so everything about one feature is in one place.

## .NET patterns used here (a quick guide)

- **Dependency injection.** `Program.cs` registers services (`builder.Services.AddScoped<IApplicationService, ApplicationService>()`), and ASP.NET Core passes them into constructors. *Scoped* means one instance per HTTP request, the same lifetime as the `DbContext` they use. *Singleton* means one instance for the whole app.
- **Primary constructors.** `class ApplicationService(AppDbContext db, ...)` is C# 12 shorthand for a constructor that stores its parameters.
- **Options pattern.** `JwtOptions` is bound from the `Jwt` configuration section and validated with data annotations at startup (`ValidateOnStart`), so a missing signing key fails immediately instead of on the first login.
- **Configuration layering.** `appsettings.json` → `appsettings.Development.json` → user-secrets (Development only) → environment variables. Later sources win. In an environment variable, `Jwt__SigningKey` maps to `Jwt:SigningKey`.
- **`TimeProvider`** (added in .NET 8). Code asks for the time through an injected clock, so tests can use `FakeTimeProvider`. Token issuing and token validation both use it.
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
| `Google:ClientId`, `Google:ClientSecret` | user-secrets / env `Google__ClientId`, `Google__ClientSecret` | **Secret.** Optional: without them, email import reports it isn't set up |
| `Google:RedirectUri` | default `http://localhost:5226/email/google/callback` | Must match the Google Cloud console exactly |
| `Google:WebAppUrl` | default `http://localhost:5173` | Where the browser goes after connecting |
| `Google:InitialSyncDays`, `Google:MaxMessagesPerSync` | defaults 60 and 200 | |
| `RateLimiting:AuthPerMinute` | default 10 | Login / register attempts per minute per client IP; more get 429 |
| `RabbitMq:Url` | user-secrets / env `RabbitMq__Url` | **Secret.** `amqps://user:pass@host/vhost`. Optional: without it, events wait in the outbox |
| `Outbox:PollSeconds` | default 5 | How often waiting events are sent; 0 turns the sender off |
| `Matcher:BaseUrl` | default `http://localhost:8000` | Where the Python matcher runs |
| `Matcher:ApiKey` | user-secrets / env `Matcher__ApiKey` | Optional shared key; must equal the matcher's `MATCHER_API_KEY` |
| `EmailImport:NoReplyCheckMinutes` | default 60 | How often the background job applies passed "no reply by X" deadlines; 0 turns it off |

Data Protection keys (used to encrypt Gmail tokens) are stored in your user profile (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` on Windows). If they are lost, stored Gmail connections can't be decrypted, and OppsView simply asks you to reconnect.

## Common commands

```powershell
dotnet test                                                     # all tests
dotnet ef migrations add <Name> --project src/OppsView.Api --output-dir Data/Migrations
dotnet ef database update --project src/OppsView.Api
dotnet user-secrets list --project src/OppsView.Api
```

## Events (RabbitMQ)

The API publishes to the durable topic exchange `oppsview.events` through a **transactional outbox**:

| Routing key | When | Payload |
|---|---|---|
| `application.changed` | Created, or status / company / role / platform / applied date changed | The application's full state, plus `previousStatus` |
| `application.deleted` | Deleted | `applicationId`, `userId` |
| `user.reminder-settings-changed` | Reminder settings saved | The settings |

It consumes `reminder.due` (from the notifier) on its own queue, `api.reminders`, with dead-letter queue `api.reminders.dlq`. Every message carries a `MessageId`, and consumers ignore duplicates. Without `RabbitMq:Url`, events wait in `OutboxMessages` (pruned after 30 days).

## Container image

```bash
docker build -t oppsview-api .     # from api/
docker run -p 8080:8080 -e ConnectionStrings__Default="..." -e Jwt__SigningKey="..." oppsview-api
```

Two stages: build with the .NET SDK, run on the ASP.NET runtime image as the non-root `app` user, on port 8080. CI builds it on every push.

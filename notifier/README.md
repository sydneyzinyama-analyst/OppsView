# OppsView notifier

Phase 4: a **Java 25 / Spring Boot 4** background worker that sends **follow-up reminders**. It has no web server: it only listens to and sends messages on **RabbitMQ**.

```
.NET API ──(outbox)──▶ RabbitMQ "oppsview.events" ──application.*──▶ notifier
   ▲                                                                    │ plans reminders,
   └───────────── api.reminders ◀──reminder.due──────────────────────────┘ sends them when due
```

## What it does

| Application status | Reminder | When (default) |
|---|---|---|
| Applied / Viewed | **Follow up**: send a polite email | 7 days after applying |
| Applied / Viewed | **Time to move on?** | 21 days after applying |
| Interview | **After the interview**: thank-you or ask for feedback | 5 days after the invitation |
| Offer | **Reply to the offer** | 2 days after the offer |
| Rejected | none | – |

- Reminders become due at 08:00 UTC (10:00 in South Africa), and a job checks every minute.
- Every status change re-plans. Unsent reminders that no longer apply are cancelled. Ones already sent are never sent again for the same thing.
- Users choose their own days, or switch reminders off, in OppsView. The API announces those choices (`user.reminder-settings-changed`) and the notifier re-plans.
- Reminders already **more than 7 days overdue** when planned are skipped, so importing old emails doesn't cause a flood.

## How it's built

| Package | What's in it |
|---|---|
| `reminders/ReminderRules` | The rules, as pure functions (no database, no RabbitMQ), unit tested |
| `events/` | The message types, the RabbitMQ listener, and the handler (one transaction per message) |
| `reminders/DueReminderJob` | Every minute: send due reminders (with publisher confirms), then mark them sent |
| `store/NotifierStore` | All the SQL, with Spring's `JdbcClient` (no ORM, nothing hidden) |
| `config/MessagingConfig` | The exchange, queue, bindings and dead-letter queue |
| `db/migration/` | Flyway migrations, in the notifier's own `notifier` schema |

**Reliability:**
- RabbitMQ delivers "at least once", so processed message ids are stored and a duplicate is ignored. Events that arrive out of order are ignored too.
- A malformed message goes straight to `notifier.application-events.dlq`. Other failures are retried 3 times first.
- A reminder only counts as sent once RabbitMQ has confirmed it.

**Memory:** the heap is capped at 256 MB in `pom.xml`, and there's no web server. It runs in about 180 MB.

## Running it (Windows, PowerShell)

### 1. Java 25

```powershell
scoop bucket add java
scoop install temurin-lts-jdk
java -version          # should say 25
```

Maven doesn't need installing: `mvnw.cmd` (the Maven wrapper) downloads it the first time.

### 2. RabbitMQ: a free CloudAMQP instance

1. Sign up at https://www.cloudamqp.com and create an instance on the **Little Lemur (free)** plan. Pick a region close to you (e.g. Azure South Africa North, AWS Cape Town, or Europe).
2. Open the instance and copy its **AMQP URL** (`amqps://user:password@host/vhost`). **It's a secret:** don't commit it or paste it anywhere public.

### 3. Settings (secrets stay out of Git)

From `notifier/`:

```powershell
Copy-Item config\local.properties.example config\local.properties
notepad config\local.properties
```

Fill in your PostgreSQL password (the `oppsview` user's) and the AMQP URL. `config/local.properties` is git-ignored.

The .NET API needs the same AMQP URL. From `api/`:

```powershell
dotnet user-secrets set "RabbitMq:Url" 'amqps://user:password@host/vhost' --project src/OppsView.Api
```

### 4. Run it

```powershell
.\mvnw.cmd spring-boot:run
```

The first run downloads Maven and the libraries (a few minutes). It's ready when the log says `Started NotifierApplication`. On first start it also creates its `notifier` schema in the `oppsview` database.

### Tests

```powershell
.\mvnw.cmd test
```

The tests use H2 (an in-memory database) and replace RabbitMQ with mocks, so they need neither PostgreSQL nor RabbitMQ.

## Configuration

`src/main/resources/application.yml`, overridable in `config/local.properties` or with environment variables:

| Setting | Env variable | Default |
|---|---|---|
| `spring.datasource.url` | `NOTIFIER_DB_URL` | `jdbc:postgresql://localhost:5432/oppsview?currentSchema=notifier` |
| `spring.datasource.username` / `password` | `NOTIFIER_DB_USER` / `NOTIFIER_DB_PASSWORD` | `oppsview` / (none) |
| `spring.rabbitmq.addresses` | `RABBITMQ_URL` | `amqp://guest:guest@localhost:5672/` |
| `notifier.reminders.*-days` | – | 7, 21, 5, 2 |
| `notifier.check-interval` | – | `PT1M` (every minute) |

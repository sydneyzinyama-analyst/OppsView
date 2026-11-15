using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.DataProtection;
using OppsView.Api.Data;
using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// Boots the real API in memory (same Program.cs, routing, auth, validation),
/// swapping only two things: PostgreSQL becomes in-memory SQLite, and the
/// clock becomes a FakeTimeProvider the tests control.
///
/// SQLite, not EF's InMemory provider: SQLite is a real relational database,
/// so foreign keys, unique indexes, cascades and ExecuteDelete behave the way
/// they will in Postgres.
/// </summary>
public sealed class OppsViewApiFactory : WebApplicationFactory<Program>
{
    // A fixed start makes runs repeatable. It works because token issuing and
    // token validation both read the injected TimeProvider.
    public static readonly DateTimeOffset StartTime = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // An in-memory SQLite database lives only as long as a connection to it
    // is open, so the factory holds one open for its whole lifetime.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTimeProvider Time { get; } = new(StartTime);

    /// <summary>Pretend Google (OAuth + Gmail) for the email import tests.</summary>
    public FakeGoogle Google { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-chars");
        builder.UseSetting("Google:ClientId", "test-client-id");
        builder.UseSetting("Google:ClientSecret", "test-client-secret");

        builder.ConfigureServices(services =>
        {
            // Remove the app's PostgreSQL registration. Since EF Core 9 the
            // configuration lambda is registered separately from the options,
            // so both have to go or Npgsql would still be configured.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.RemoveAll<IGoogleOAuthClient>();
            services.RemoveAll<IGmailApi>();
            services.AddSingleton<IGoogleOAuthClient>(Google);
            services.AddSingleton<IGmailApi>(Google);

            // Encryption keys in memory only, instead of the user profile.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        EnsureDatabase();
    }

    private bool _created;

    private void EnsureDatabase()
    {
        if (_created) return;
        _connection.Open();
        using var scope = Services.CreateScope();
        // The migrations are written for Postgres, so build the SQLite schema
        // straight from the model instead.
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        _created = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

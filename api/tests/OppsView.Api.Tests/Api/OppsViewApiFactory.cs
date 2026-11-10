using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OppsView.Api.Data;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-chars");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
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

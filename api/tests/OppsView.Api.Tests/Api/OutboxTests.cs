using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OppsView.Api.Data;
using OppsView.Api.Infrastructure.Messaging;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// Application changes produce outbox events in the same save, and the
/// dispatcher sends them in order. RabbitMQ is faked (FakePublisher).
/// </summary>
public class OutboxTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private List<OutboxMessage> EventsFor(Guid applicationId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.OutboxMessages.AsNoTracking().AsEnumerable()
            .Where(m => m.Payload.Contains(applicationId.ToString()))
            .OrderBy(m => m.OccurredAt).ThenBy(m => m.Type)
            .ToList();
    }

    private static JsonElement Payload(OutboxMessage m) => JsonDocument.Parse(m.Payload).RootElement;

    private async Task<int> DispatchAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Creating_an_application_writes_an_application_changed_event_with_its_state()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client, "Absa");

        var e = Assert.Single(EventsFor(app.Id));
        Assert.Equal(EventTypes.ApplicationChanged, e.Type);
        var p = Payload(e);
        Assert.Equal("Absa", p.GetProperty("company").GetString());
        Assert.Equal("Applied", p.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, p.GetProperty("previousStatus").ValueKind);
        Assert.Null(e.PublishedAt);
    }

    [Fact]
    public async Task A_status_change_announces_the_old_and_new_status()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);

        await client.PatchAsJsonAsync($"/applications/{app.Id}/status", new { status = "Interview" });

        var last = EventsFor(app.Id).Last();
        Assert.Equal(("Interview", "Applied"),
            (Payload(last).GetProperty("status").GetString(), Payload(last).GetProperty("previousStatus").GetString()));
    }

    [Fact]
    public async Task Editing_only_notes_announces_nothing()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);

        await client.PutAsJsonAsync($"/applications/{app.Id}", new
        {
            company = app.Company, role = app.Role, platform = app.Platform.ToString(),
            appliedOn = app.AppliedOn.ToString("yyyy-MM-dd"), jobUrl = app.JobUrl, notes = "Called the recruiter",
        });

        Assert.Single(EventsFor(app.Id));
    }

    [Fact]
    public async Task Deleting_an_application_announces_it()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);

        await client.DeleteAsync($"/applications/{app.Id}");

        Assert.Equal(EventTypes.ApplicationDeleted, EventsFor(app.Id).Last().Type);
    }

    [Fact]
    public async Task Imported_applications_are_announced_too()
    {
        var client = await SignedInClientAsync();
        // Email import goes through the same SaveChanges, so the interceptor sees it.
        using var scope = Factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<Features.EmailImport.EmailImporter>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auth = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var userId = Guid.Parse(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(auth).Subject);

        await importer.ImportAsync(userId,
        [
            new IncomingEmail($"m-{Guid.NewGuid():N}", "careers@yoco.com", "Thanks",
                "Thank you for applying for the Tester role at Yoco.", Factory.Time.GetUtcNow().UtcDateTime),
        ], CancellationToken.None);

        var created = await db.Applications.AsNoTracking().SingleAsync(a => a.UserId == userId && a.Company == "Yoco");
        Assert.Single(EventsFor(created.Id));
    }

    [Fact]
    public async Task The_dispatcher_publishes_pending_events_in_order_and_marks_them_sent()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);
        await client.PatchAsJsonAsync($"/applications/{app.Id}/status", new { status = "Viewed" });

        await DispatchAsync();

        var published = Factory.Publisher.Published.Where(m => m.Payload.Contains(app.Id.ToString())).ToList();
        Assert.Equal(2, published.Count);
        Assert.Equal("Applied", Payload(published[0]).GetProperty("status").GetString());
        Assert.Equal("Viewed", Payload(published[1]).GetProperty("status").GetString());
        Assert.All(EventsFor(app.Id), m => Assert.NotNull(m.PublishedAt));

        // Nothing is sent twice.
        var before = Factory.Publisher.Published.Count;
        await DispatchAsync();
        Assert.Equal(before, Factory.Publisher.Published.Count);
    }

    [Fact]
    public async Task When_RabbitMQ_is_down_events_wait_and_are_sent_later()
    {
        var client = await SignedInClientAsync();
        Factory.Publisher.Down = true;
        Guid appId;
        try
        {
            appId = (await CreateApplicationAsync(client)).Id;
            Assert.Equal(0, await DispatchAsync());

            Assert.Null(Assert.Single(EventsFor(appId)).PublishedAt);
            // The dispatcher stops at the oldest waiting event (to keep order)
            // and records why it couldn't be sent.
            using var scope = Factory.Services.CreateScope();
            var oldestWaiting = scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.AsNoTracking()
                .Where(m => m.PublishedAt == null).OrderBy(m => m.OccurredAt).First();
            Assert.Contains("unreachable", oldestWaiting.LastError);
        }
        finally
        {
            Factory.Publisher.Down = false;
        }

        // Back up: everything waiting goes out (in batches if there's a backlog).
        while (await DispatchAsync() > 0) { }

        Assert.NotNull(Assert.Single(EventsFor(appId)).PublishedAt);
    }

    [Fact]
    public async Task Without_RabbitMQ_configured_nothing_is_sent_and_events_are_kept()
    {
        var client = await SignedInClientAsync();
        Factory.Publisher.IsConfigured = false;
        try
        {
            var app = await CreateApplicationAsync(client);

            Assert.Equal(0, await DispatchAsync());
            Assert.Null(Assert.Single(EventsFor(app.Id)).PublishedAt);
        }
        finally
        {
            Factory.Publisher.IsConfigured = true;
        }
    }
}

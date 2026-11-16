using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OppsView.Api.Data;
using OppsView.Api.Features.Reminders;
using OppsView.Api.Infrastructure.Messaging;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// Reminders arriving from the notifier ("reminder.due", handled by
/// ReminderInbox exactly as the RabbitMQ consumer calls it), and the bell's
/// endpoints, and reminder settings.
/// </summary>
public class RemindersTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private Guid UserIdOf(HttpClient client) =>
        Guid.Parse(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(client.DefaultRequestHeaders.Authorization!.Parameter!).Subject);

    /// <summary>A reminder.due message as the Java notifier sends it.</summary>
    private static byte[] ReminderDue(Guid reminderId, Guid applicationId, Guid userId, string kind = "FOLLOW_UP") =>
        Encoding.UTF8.GetBytes($$"""
            {"reminderId":"{{reminderId}}","applicationId":"{{applicationId}}","userId":"{{userId}}",
             "kind":"{{kind}}","company":"Acme","role":"Junior .NET Developer",
             "message":"You applied to Acme (Junior .NET Developer) 7 days ago and haven't heard back.",
             "dueAt":"2026-11-17T08:00:00Z","version":1}
            """);

    private async Task<ReminderInbox.Result> DeliverAsync(byte[] body)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderInbox>().HandleAsync(body, CancellationToken.None);
    }

    private async Task<(HttpClient Client, Guid AppId, Guid ReminderId)> WithReminderAsync(string kind = "FOLLOW_UP")
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);
        var reminderId = Guid.NewGuid();
        Assert.Equal(ReminderInbox.Result.Stored, await DeliverAsync(ReminderDue(reminderId, app.Id, UserIdOf(client), kind)));
        return (client, app.Id, reminderId);
    }

    private static async Task<List<ReminderResponse>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ReminderResponse>>("/reminders", Json))!;

    [Fact]
    public async Task A_delivered_reminder_shows_under_the_bell()
    {
        var (client, appId, reminderId) = await WithReminderAsync();

        var reminder = Assert.Single(await ListAsync(client));
        Assert.Equal((reminderId, appId, "Acme", "FOLLOW_UP"), (reminder.Id, reminder.ApplicationId, reminder.Company, reminder.Kind));
        Assert.Contains("7 days ago", reminder.Message);
        Assert.Equal(1, (await client.GetFromJsonAsync<ReminderCountResponse>("/reminders/count", Json))!.Open);
    }

    [Fact]
    public async Task The_same_reminder_delivered_twice_is_stored_once()
    {
        var (client, appId, reminderId) = await WithReminderAsync();

        Assert.Equal(ReminderInbox.Result.Duplicate, await DeliverAsync(ReminderDue(reminderId, appId, UserIdOf(client))));
        Assert.Single(await ListAsync(client));
    }

    [Fact]
    public async Task A_reminder_for_a_deleted_application_is_ignored()
    {
        var client = await SignedInClientAsync();
        var app = await CreateApplicationAsync(client);
        await client.DeleteAsync($"/applications/{app.Id}");

        Assert.Equal(ReminderInbox.Result.Ignored, await DeliverAsync(ReminderDue(Guid.NewGuid(), app.Id, UserIdOf(client))));
    }

    [Fact]
    public async Task An_unreadable_message_throws_so_it_goes_to_the_dead_letter_queue() =>
        await Assert.ThrowsAnyAsync<JsonException>(() => DeliverAsync("{not json"u8.ToArray()));

    [Fact]
    public async Task Deleting_the_application_removes_its_reminders()
    {
        var (client, appId, _) = await WithReminderAsync();
        await client.DeleteAsync($"/applications/{appId}");
        Assert.Empty(await ListAsync(client));
    }

    [Fact]
    public async Task A_follow_up_reminder_disappears_once_there_is_an_interview()
    {
        var (client, appId, _) = await WithReminderAsync();

        await client.PatchAsJsonAsync($"/applications/{appId}/status", new { status = "Interview" });

        Assert.Empty(await ListAsync(client));
    }

    [Fact]
    public async Task Done_and_dismiss_close_a_reminder_once()
    {
        var (client, _, reminderId) = await WithReminderAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/reminders/{reminderId}/done", null)).StatusCode);
        Assert.Empty(await ListAsync(client));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/reminders/{reminderId}/dismiss", null)).StatusCode);
    }

    [Fact]
    public async Task Snoozing_hides_a_reminder_until_the_time_comes()
    {
        var (client, _, reminderId) = await WithReminderAsync();

        var response = await client.PostAsJsonAsync($"/reminders/{reminderId}/snooze", new { days = 3 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync(client));

        // Three days later it's back. (The user's login token has expired by
        // then, so this checks the stored reminder directly.)
        Factory.Time.Advance(TimeSpan.FromDays(3));
        using var scope = Factory.Services.CreateScope();
        var reminder = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Reminders.AsNoTracking()
            .SingleAsync(r => r.Id == reminderId);
        Assert.True(reminder.IsVisible(Factory.Time.GetUtcNow().UtcDateTime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task Snooze_days_are_validated(int days)
    {
        var (client, _, reminderId) = await WithReminderAsync();
        var response = await client.PostAsJsonAsync($"/reminders/{reminderId}/snooze", new { days });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_reminders_are_invisible_and_untouchable()
    {
        var (_, _, reminderId) = await WithReminderAsync();
        var intruder = await SignedInClientAsync();

        Assert.Empty(await ListAsync(intruder));
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/reminders/{reminderId}/done", null)).StatusCode);
    }

    // ---- Settings ----------------------------------------------------------

    [Fact]
    public async Task Settings_start_with_the_defaults()
    {
        var client = await SignedInClientAsync();
        var settings = await client.GetFromJsonAsync<ReminderSettingsResponse>("/reminders/settings", Json);
        Assert.Equal(new ReminderSettingsResponse(true, 7, 21, 5, 2), settings);
    }

    [Fact]
    public async Task Saving_settings_stores_them_and_announces_them_to_the_notifier()
    {
        var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/reminders/settings",
            new { enabled = true, followUpDays = 10, moveOnDays = 30, afterInterviewDays = 3, offerReplyDays = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ReminderSettingsResponse(true, 10, 30, 3, 0),
            await client.GetFromJsonAsync<ReminderSettingsResponse>("/reminders/settings", Json));

        using var scope = Factory.Services.CreateScope();
        var userId = UserIdOf(client).ToString();
        var announced = scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.AsNoTracking().AsEnumerable()
            .Single(m => m.Type == EventTypes.ReminderSettingsChanged && m.Payload.Contains(userId));
        var payload = JsonDocument.Parse(announced.Payload).RootElement;
        Assert.Equal(10, payload.GetProperty("followUpDays").GetInt32());
        Assert.Equal(0, payload.GetProperty("offerReplyDays").GetInt32());
    }

    [Fact]
    public async Task Settings_are_validated()
    {
        var client = await SignedInClientAsync();

        var tooMany = await client.PutAsJsonAsync("/reminders/settings",
            new { enabled = true, followUpDays = 100, moveOnDays = 21, afterInterviewDays = 5, offerReplyDays = 2 });
        var wrongOrder = await client.PutAsJsonAsync("/reminders/settings",
            new { enabled = true, followUpDays = 14, moveOnDays = 7, afterInterviewDays = 5, offerReplyDays = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongOrder.StatusCode);
        Assert.Contains("moveOnDays", (await ReadValidationErrorsAsync(wrongOrder)).Keys);
    }
}

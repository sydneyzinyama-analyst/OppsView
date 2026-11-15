using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OppsView.Api.Data;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.EmailImport;
using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// The whole Gmail flow over HTTP: connect, Google's redirect back, sync,
/// review and disconnect. Only Google itself is faked (see FakeGoogle).
/// Each test class gets its own factory, so this class's fake mailbox is
/// its own; tests use their own users and message ids.
/// </summary>
public class EmailEndpointsTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private HttpClient NoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Clicks "Connect Gmail" and plays Google's redirect back. Returns where the browser ends up.</summary>
    private async Task<Uri> ConnectAsync(HttpClient client)
    {
        var connect = await client.PostAsync("/email/google/connect", null);
        connect.EnsureSuccessStatusCode();
        var url = (await connect.Content.ReadFromJsonAsync<ConnectResponse>(Json))!.AuthorizationUrl;
        var state = QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();

        var callback = await NoRedirectClient().GetAsync($"/email/google/callback?code=abc&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return callback.Headers.Location!;
    }

    private async Task<EmailStatusResponse> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<EmailStatusResponse>("/email/status", Json))!;

    private void AddToMailbox(string id, string subject, string body, string from = "careers@acme.co.za") =>
        Factory.Google.Mailbox[id] = new IncomingEmail(id, from, subject, body, Factory.Time.GetUtcNow().UtcDateTime.AddHours(-1));

    [Fact]
    public async Task Status_before_connecting()
    {
        var status = await StatusAsync(await SignedInClientAsync());
        Assert.Equal((true, false, 0), (status.Available, status.Connected, status.NeedsReviewCount));
    }

    [Fact]
    public async Task Connect_returns_a_Google_consent_url_with_PKCE()
    {
        var client = await SignedInClientAsync();
        var response = await client.PostAsync("/email/google/connect", null);

        var url = (await response.Content.ReadFromJsonAsync<ConnectResponse>(Json))!.AuthorizationUrl;
        Assert.StartsWith("https://accounts.google.com/", url);
        Assert.Contains("code_challenge=", url);
    }

    [Fact]
    public async Task Connecting_stores_the_token_encrypted_and_returns_to_the_web_app()
    {
        var email = $"conn-{Guid.NewGuid():N}@example.com";
        var client = await SignedInClientAsync(email);

        var landing = await ConnectAsync(client);

        Assert.Equal("http://localhost:5173/?gmail=connected", landing.ToString());
        var status = await StatusAsync(client);
        Assert.Equal((true, "me@gmail.com"), (status.Connected, status.EmailAddress));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = db.Users.Single(u => u.Email == email).Id;
        var stored = db.GmailConnections.Single(c => c.UserId == userId);
        Assert.DoesNotContain(FakeGoogle.RefreshToken, stored.EncryptedRefreshToken);
    }

    [Fact]
    public async Task A_forged_or_expired_state_is_rejected()
    {
        var response = await NoRedirectClient().GetAsync("/email/google/callback?code=abc&state=forged");
        Assert.EndsWith("?gmail=expired", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Declining_on_Googles_screen_is_reported()
    {
        var response = await NoRedirectClient().GetAsync("/email/google/callback?error=access_denied");
        Assert.EndsWith("?gmail=denied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Unticking_the_read_permission_is_reported_and_nothing_is_stored()
    {
        var client = await SignedInClientAsync();
        Factory.Google.GrantedScopes = "openid";
        try
        {
            Assert.EndsWith("?gmail=scope", (await ConnectAsync(client)).ToString());
            Assert.False((await StatusAsync(client)).Connected);
        }
        finally
        {
            Factory.Google.GrantedScopes = Features.EmailImport.Gmail.GoogleOAuthClient.ReadOnlyScope;
        }
    }

    [Fact]
    public async Task Sync_imports_emails_and_a_second_sync_skips_them()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-1", "Application received", "Thank you for applying for the Tester role at Yoco.");
        AddToMailbox($"{id}-2", "Update", "Please log in to see the next steps for your application.");

        var first = await client.PostAsync("/email/sync", null);
        var summary = await first.Content.ReadFromJsonAsync<ImportSummary>(Json);
        var fetchedAfterFirst = Factory.Google.MessagesFetched;
        var second = await (await client.PostAsync("/email/sync", null)).Content.ReadFromJsonAsync<ImportSummary>(Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(summary!.Created >= 1);
        Assert.True(summary.NeedsReview >= 1);
        Assert.Equal(0, second!.Created);
        // Only the email still waiting for review is read again; handled ones never are.
        Assert.Equal(fetchedAfterFirst + summary.NeedsReview, Factory.Google.MessagesFetched);
        Assert.Equal(0, second.NeedsReview); // and it isn't reported as new

        var apps = await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json);
        var yoco = Assert.Single(apps!, a => a.Company == "Yoco");
        Assert.Equal(ApplicationSource.Email, yoco.Source);
        Assert.NotNull((await StatusAsync(client)).LastSyncedAt);
    }

    [Fact]
    public async Task The_first_sync_looks_back_60_days()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        Factory.Google.Queries.Clear();

        await client.PostAsync("/email/sync", null);

        var expected = new DateTimeOffset(Factory.Time.GetUtcNow().UtcDateTime.AddDays(-60)).ToUnixTimeSeconds();
        Assert.Contains($"after:{expected}", Assert.Single(Factory.Google.Queries));
    }

    [Fact]
    public async Task Sync_without_a_connection_is_a_409()
    {
        var response = await (await SignedInClientAsync()).PostAsync("/email/sync", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task When_Google_revokes_access_the_user_is_asked_to_reconnect()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        Factory.Google.Revoked = true;
        try
        {
            var response = await client.PostAsync("/email/sync", null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.True((await StatusAsync(client)).NeedsReconnect);
        }
        finally
        {
            Factory.Google.Revoked = false;
        }

        // Connecting again clears it.
        await ConnectAsync(client);
        Assert.False((await StatusAsync(client)).NeedsReconnect);
    }

    [Fact]
    public async Task Review_items_can_be_turned_into_applications_or_dismissed()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        // A rejection for a job OppsView never saw: created automatically.
        AddToMailbox($"{id}-r", "Your application",
            "Thank you for your interest in working for Cartrack by applying to the Support Technician position. Your application has been unsuccessful.");
        // Two emails about an application that OppsView can't read: for review.
        AddToMailbox($"{id}-u1", "Next steps", "Please log in to see the next steps for your application.", from: "careers@globex.co.za");
        AddToMailbox($"{id}-u2", "Profile", "Please log in to see the next steps for your application.");
        await client.PostAsync("/email/sync", null);

        var apps = (await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json))!;
        Assert.Equal(ApplicationStatus.Rejected, apps.Single(a => a.Company == "Cartrack").Status);

        var items = await client.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json);
        var toAdd = Assert.Single(items!, i => i.Subject == "Next steps");
        var toIgnore = Assert.Single(items!, i => i.Subject == "Profile");
        Assert.Equal("Globex", toAdd.Company); // found from the address, for the pre-filled form

        var created = await client.PostAsJsonAsync($"/email/review/{toAdd.Id}/application", new
        {
            company = "Globex", role = "Analyst", platform = "CompanyPortal",
            appliedOn = Today.AddDays(-10).ToString("yyyy-MM-dd"),
        });
        var dismissed = await client.PostAsync($"/email/review/{toIgnore.Id}/dismiss", null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(ApplicationStatus.Applied, (await created.Content.ReadFromJsonAsync<ApplicationResponse>(Json))!.Status);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json))!,
            i => i.Id == toAdd.Id || i.Id == toIgnore.Id);

        // Dealt with already: a second decision is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/email/review/{toIgnore.Id}/dismiss", null)).StatusCode);
    }

    [Fact]
    public async Task Another_users_review_items_are_not_reachable()
    {
        var owner = await SignedInClientAsync();
        await ConnectAsync(owner);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-x", "Profile", "Please log in to see the next steps for your application.");
        await owner.PostAsync("/email/sync", null);
        var item = (await owner.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json))!.First();

        var intruder = await SignedInClientAsync();
        Assert.Empty((await intruder.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/email/review/{item.Id}/dismiss", null)).StatusCode);
    }

    [Fact]
    public async Task Disconnect_revokes_the_token_and_forgets_the_connection()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);

        var response = await client.DeleteAsync("/email/google");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(FakeGoogle.RefreshToken, Factory.Google.RevokedTokens);
        Assert.False((await StatusAsync(client)).Connected);
    }

    [Fact]
    public async Task Without_Google_settings_the_feature_reports_it_is_not_set_up()
    {
        using var unconfigured = Factory.WithWebHostBuilder(b => b.UseSetting("Google:ClientId", ""));
        var client = unconfigured.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register",
            new { email = $"u-{Guid.NewGuid():N}@example.com", password = "correct horse battery" });
        var token = (await register.Content.ReadFromJsonAsync<OppsView.Api.Features.Auth.AuthResponse>(Json))!.Token;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/email/google/connect", null)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<EmailStatusResponse>("/email/status", Json))!.Available);
    }

    // ---- Gmail errors (the 403 seen on a real mailbox) -------------------

    [Fact]
    public async Task An_email_Gmail_refuses_is_skipped_and_retried_next_time_without_failing_the_sync()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-ok", "Thanks", "Thank you for applying for the Analyst role at Absa.");
        AddToMailbox($"{id}-bad", "Thanks", "Thank you for applying for the Engineer role at Takealot.");
        Factory.Google.FailingMessages[$"{id}-bad"] = new GmailApiException(403, "forbidden", "Not allowed");
        try
        {
            var first = await client.PostAsync("/email/sync", null);
            var summary = await first.Content.ReadFromJsonAsync<ImportSummary>(Json);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(1, summary!.Failed);
            var apps = (await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json))!;
            Assert.Contains(apps, a => a.Company == "Absa");
            Assert.Null((await StatusAsync(client)).LastSyncedAt); // not marked as fully synced
        }
        finally
        {
            Factory.Google.FailingMessages.Clear();
        }

        // Next sync: Gmail cooperates, and the skipped email is imported.
        await client.PostAsync("/email/sync", null);
        var after = (await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json))!;
        Assert.Contains(after, a => a.Company == "Takealot");
    }

    [Fact]
    public async Task Being_rate_limited_stops_fetching_and_keeps_what_was_read()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-a", "Thanks", "Thank you for applying for the Analyst role at Nedbank.");
        AddToMailbox($"{id}-b", "Thanks", "Thank you for applying for the Tester role at Investec.");
        Factory.Google.FailingMessages[$"{id}-b"] = new GmailApiException(429, "rateLimitExceeded", "Too many requests");
        try
        {
            var response = await client.PostAsync("/email/sync", null);
            var summary = await response.Content.ReadFromJsonAsync<ImportSummary>(Json);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(summary!.Failed >= 1);
            Assert.Contains((await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json))!, a => a.Company == "Nedbank");
        }
        finally
        {
            Factory.Google.FailingMessages.Clear();
        }
    }

    [Fact]
    public async Task Revoked_access_while_reading_messages_asks_the_user_to_reconnect()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-x", "Thanks", "Thank you for applying for the Analyst role at Absa.");
        Factory.Google.FailingMessages[$"{id}-x"] = new GmailAccessRevokedException();
        try
        {
            var response = await client.PostAsync("/email/sync", null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.True((await StatusAsync(client)).NeedsReconnect);
        }
        finally
        {
            Factory.Google.FailingMessages.Clear();
        }
    }

    [Fact]
    public async Task A_Gmail_error_while_searching_is_a_clear_502_not_a_crash()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        Factory.Google.SearchFailure = new GmailApiException(429, "userRateLimitExceeded", "Slow down");
        try
        {
            var response = await client.PostAsync("/email/sync", null);
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Contains("slow down", problem!.Title, StringComparison.OrdinalIgnoreCase);
            Assert.False((await StatusAsync(client)).NeedsReconnect);
        }
        finally
        {
            Factory.Google.SearchFailure = null;
        }
    }

    [Fact]
    public async Task Sync_applies_no_reply_deadlines_and_reports_them()
    {
        var client = await SignedInClientAsync();
        await ConnectAsync(client);
        var id = Guid.NewGuid().ToString("N");
        // Received 20 days ago with a 2-week rule: already past.
        Factory.Google.Mailbox[$"{id}-d"] = new IncomingEmail($"{id}-d", "careers@tymebank.co.za", "Application received",
            "Thank you for applying for the Graduate Developer role at TymeBank. If you do not hear from us within 2 weeks, please consider your application unsuccessful.",
            Factory.Time.GetUtcNow().UtcDateTime.AddDays(-20));

        var summary = await (await client.PostAsync("/email/sync", null)).Content.ReadFromJsonAsync<ImportSummary>(Json);

        Assert.True(summary!.NoReplyRejected >= 1);
        var app = (await client.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json))!.Single(a => a.Company == "TymeBank");
        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        var history = await client.GetFromJsonAsync<List<StatusChangeResponse>>($"/applications/{app.Id}/history", Json);
        Assert.Equal(ApplicationSource.NoReply, history!.Single().Source);
    }
}

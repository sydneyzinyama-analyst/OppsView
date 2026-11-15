using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OppsView.Api.Data;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.EmailImport;

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
        AddToMailbox($"{id}-2", "Update", "Please update your candidate profile with your latest CV.");

        var first = await client.PostAsync("/email/sync", null);
        var summary = await first.Content.ReadFromJsonAsync<ImportSummary>(Json);
        var fetchedAfterFirst = Factory.Google.MessagesFetched;
        var second = await (await client.PostAsync("/email/sync", null)).Content.ReadFromJsonAsync<ImportSummary>(Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(summary!.Created >= 1);
        Assert.True(summary.NeedsReview >= 1);
        Assert.Equal(0, second!.Created);
        Assert.Equal(fetchedAfterFirst, Factory.Google.MessagesFetched); // nothing fetched twice

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
        // A rejection from a company with no application yet, and an unclear email.
        AddToMailbox($"{id}-r", "Your application",
            "Thank you for your interest in working for Cartrack by applying to the Support Technician position. Your application has been unsuccessful.");
        AddToMailbox($"{id}-u", "Profile", "Please update your candidate profile.");
        await client.PostAsync("/email/sync", null);

        var items = await client.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json);
        var rejection = Assert.Single(items!, i => i.Company == "Cartrack");
        var unclear = Assert.Single(items!, i => i.Subject == "Profile");

        var created = await client.PostAsJsonAsync($"/email/review/{rejection.Id}/application", new
        {
            company = "Cartrack", role = "Support Technician", platform = "CompanyPortal",
            appliedOn = Today.AddDays(-10).ToString("yyyy-MM-dd"),
        });
        var dismissed = await client.PostAsync($"/email/review/{unclear.Id}/dismiss", null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var app = await created.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal(ApplicationStatus.Rejected, app!.Status); // the email was a rejection
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ReviewItemResponse>>("/email/review", Json))!
            .Where(i => i.Id == rejection.Id || i.Id == unclear.Id));

        // Dealt with already: a second decision is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/email/review/{unclear.Id}/dismiss", null)).StatusCode);
    }

    [Fact]
    public async Task Another_users_review_items_are_not_reachable()
    {
        var owner = await SignedInClientAsync();
        await ConnectAsync(owner);
        var id = Guid.NewGuid().ToString("N");
        AddToMailbox($"{id}-x", "Profile", "Please update your candidate profile.");
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
}

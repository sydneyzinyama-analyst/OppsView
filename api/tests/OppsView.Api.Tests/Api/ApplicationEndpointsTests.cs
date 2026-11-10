using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using OppsView.Api.Features.Applications;

namespace OppsView.Api.Tests.Api;

public class ApplicationEndpointsTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_returns_201_with_location_and_the_new_application()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/applications", ValidApplication());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal($"/applications/{created!.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Acme", created.Company);
        Assert.Equal(ApplicationStatus.Applied, created.Status);
        Assert.Equal(
            [ApplicationStatus.Viewed, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Rejected],
            created.AllowedNextStatuses);
    }

    [Fact]
    public async Task Create_returns_camelCase_field_errors_for_missing_fields()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/applications", new { company = "", role = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadValidationErrorsAsync(response);
        Assert.Contains("company", errors.Keys);
        Assert.Contains("role", errors.Keys);
        Assert.Contains("platform", errors.Keys);
        Assert.Contains("appliedOn", errors.Keys);
    }

    [Fact]
    public async Task Domain_rule_failures_come_back_as_field_errors_too()
    {
        var client = await SignedInClientAsync();
        var future = Today.AddDays(5).ToString("yyyy-MM-dd");

        var response = await client.PostAsJsonAsync("/applications", new
        {
            company = "Acme", role = "Dev", platform = "Indeed", appliedOn = future,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("appliedOn", (await ReadValidationErrorsAsync(response)).Keys);
    }

    [Fact]
    public async Task List_returns_only_my_applications_newest_first()
    {
        var me = await SignedInClientAsync();
        var someoneElse = await SignedInClientAsync();
        await CreateApplicationAsync(someoneElse, "Not mine");

        var older = new
        {
            company = "Older", role = "Dev", platform = "LinkedIn",
            appliedOn = Today.AddDays(-7).ToString("yyyy-MM-dd"),
        };
        (await me.PostAsJsonAsync("/applications", older)).EnsureSuccessStatusCode();
        await CreateApplicationAsync(me, "Newer");

        var list = await me.GetFromJsonAsync<List<ApplicationResponse>>("/applications", Json);

        Assert.Equal(["Newer", "Older"], list!.Select(a => a.Company));
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var client = await SignedInClientAsync();
        var response = await client.GetAsync($"/applications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_application_is_invisible_and_untouchable()
    {
        var owner = await SignedInClientAsync();
        var intruder = await SignedInClientAsync();
        var application = await CreateApplicationAsync(owner);
        var url = $"/applications/{application.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync(url, ValidApplication("Hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PatchAsJsonAsync($"{url}/status", new { status = "Rejected" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{url}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);

        var stillThere = await owner.GetFromJsonAsync<ApplicationResponse>(url, Json);
        Assert.Equal("Acme", stillThere!.Company);
        Assert.Equal(ApplicationStatus.Applied, stillThere.Status);
    }

    [Fact]
    public async Task Put_edits_the_application()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);
        Factory.Time.Advance(TimeSpan.FromHours(1));

        var response = await client.PutAsJsonAsync($"/applications/{application.Id}", new
        {
            company = "Globex",
            role = "Backend Developer",
            platform = "Workable",
            appliedOn = Today.AddDays(-1).ToString("yyyy-MM-dd"),
            notes = "Second round next week",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal("Globex", updated!.Company);
        Assert.Equal(JobPlatform.Workable, updated.Platform);
        Assert.Null(updated.JobUrl);
        Assert.Equal("Second round next week", updated.Notes);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
    }

    [Fact]
    public async Task Put_validates_like_post()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);

        var response = await client.PutAsJsonAsync($"/applications/{application.Id}", new { company = "", role = "Dev" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_application_and_its_history()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);
        var url = $"/applications/{application.Id}";
        await client.PatchAsJsonAsync($"{url}/status", new { status = "Viewed" });

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url)).StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OppsView.Api.Data.AppDbContext>();
        Assert.False(db.StatusHistory.Any(h => h.ApplicationId == application.Id));
    }

    [Fact]
    public async Task Patch_status_moves_forward_and_records_history()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);
        var url = $"/applications/{application.Id}";

        Factory.Time.Advance(TimeSpan.FromHours(2)); // stay inside the 8h token lifetime
        var response = await client.PatchAsJsonAsync($"{url}/status", new { status = "Interview" });
        Factory.Time.Advance(TimeSpan.FromHours(3));
        await client.PatchAsJsonAsync($"{url}/status", new { status = "Offer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var afterFirst = await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal(ApplicationStatus.Interview, afterFirst!.Status);
        Assert.Equal([ApplicationStatus.Offer, ApplicationStatus.Rejected], afterFirst.AllowedNextStatuses);

        var history = await client.GetFromJsonAsync<List<StatusChangeResponse>>($"{url}/history", Json);
        Assert.Equal(2, history!.Count);
        Assert.Equal((ApplicationStatus.Applied, ApplicationStatus.Interview), (history[0].FromStatus, history[0].ToStatus));
        Assert.Equal((ApplicationStatus.Interview, ApplicationStatus.Offer), (history[1].FromStatus, history[1].ToStatus));
        Assert.True(history[0].ChangedAt < history[1].ChangedAt);
        Assert.Equal(DateTimeKind.Utc, history[0].ChangedAt.Kind);
    }

    [Fact]
    public async Task Patch_status_rejects_a_disallowed_transition()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);
        var url = $"/applications/{application.Id}";
        await client.PatchAsJsonAsync($"{url}/status", new { status = "Rejected" });

        var response = await client.PatchAsJsonAsync($"{url}/status", new { status = "Interview" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("status", (await ReadValidationErrorsAsync(response)).Keys);
        var history = await client.GetFromJsonAsync<List<StatusChangeResponse>>($"{url}/history", Json);
        Assert.Single(history!);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"status\":\"Ghosted\"}")]
    public async Task Patch_status_rejects_missing_or_unknown_status(string body)
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);

        var response = await client.PatchAsync(
            $"/applications/{application.Id}/status",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

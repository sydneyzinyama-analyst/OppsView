using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using OppsView.Api.Features.Analytics;
using OppsView.Api.Features.Applications;

namespace OppsView.Api.Tests.Api;

/// <summary>The dashboard numbers, over HTTP, for a small realistic job hunt.</summary>
public class AnalyticsTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<ApplicationResponse> AddAsync(HttpClient client, string company, string platform, int daysAgo)
    {
        var response = await client.PostAsJsonAsync("/applications", new
        {
            company, role = "Developer", platform, appliedOn = Today.AddDays(-daysAgo).ToString("yyyy-MM-dd"),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json))!;
    }

    private static Task Move(HttpClient client, ApplicationResponse app, string status) =>
        client.PatchAsJsonAsync($"/applications/{app.Id}/status", new { status });

    private async Task<AnalyticsResponse> GetAsync(HttpClient client, int weeks = 4) =>
        (await client.GetFromJsonAsync<AnalyticsResponse>($"/analytics?weeks={weeks}", Json))!;

    [Fact]
    public async Task A_new_user_gets_empty_but_complete_numbers()
    {
        var a = await GetAsync(await SignedInClientAsync());

        Assert.Equal(0, a.Totals.Applications);
        Assert.Equal(0, a.Totals.ResponseRate);
        Assert.Null(a.Totals.AverageDaysToResponse);
        Assert.Equal(4, a.PerWeek.Count); // empty weeks are still there, for the chart
        Assert.All(a.PerWeek, w => Assert.Equal(0, w.Applications));
        Assert.Equal(4, a.Funnel.Count);
        Assert.Empty(a.TopMissingSkills);
    }

    [Fact]
    public async Task Counts_a_job_hunt()
    {
        var client = await SignedInClientAsync();
        var acme = await AddAsync(client, "Acme", "Pnet", daysAgo: 2);
        var globex = await AddAsync(client, "Globex", "Pnet", daysAgo: 3);
        var initech = await AddAsync(client, "Initech", "LinkedIn", daysAgo: 10);
        await AddAsync(client, "Umbrella", "LinkedIn", daysAgo: 20);

        await Move(client, acme, "Viewed");
        await Move(client, acme, "Interview");
        await Move(client, acme, "Offer");
        await Move(client, globex, "Rejected");
        await Move(client, initech, "Viewed");
        await Move(client, initech, "Rejected");

        var a = await GetAsync(client);

        // Totals: 3 of 4 heard back (offer, 2 rejections); the 4th is still waiting.
        Assert.Equal(new Totals(4, 1, 1, 1, 2, 0.75, a.Totals.AverageDaysToResponse), a.Totals);
        Assert.NotNull(a.Totals.AverageDaysToResponse);

        // Per platform: Pnet 2 of 2 replied, LinkedIn 1 of 2.
        Assert.Contains(new PlatformStats(JobPlatform.Pnet, 2, 2, 1.0), a.ByPlatform);
        Assert.Contains(new PlatformStats(JobPlatform.LinkedIn, 2, 1, 0.5), a.ByPlatform);

        // Funnel: all applied, 2 viewed (Acme, Initech), 1 interview, 1 offer.
        Assert.Equal([4, 2, 1, 1], a.Funnel.Select(f => f.Reached));

        // Rejections: one straight after applying, one after being viewed.
        Assert.Equal(
            [new RejectionPoint(ApplicationStatus.Applied, 1), new RejectionPoint(ApplicationStatus.Viewed, 1)],
            a.RejectedAt);

        // Still waiting: Umbrella, in Applied.
        // Waiting: Umbrella, applied 20 days ago, counted from the day it was applied.
        var applied = a.Waiting.Single(w => w.Stage == ApplicationStatus.Applied);
        Assert.Equal((1, 1), (applied.Count, applied.WaitingOver14Days));
        Assert.True(applied.AverageDays >= 20);

        Assert.Equal(new SourceCounts(4, 0), a.Sources);
    }

    [Fact]
    public async Task Applications_per_week_land_in_the_right_week()
    {
        var client = await SignedInClientAsync();
        await AddAsync(client, "This week", "Pnet", daysAgo: 0);
        await AddAsync(client, "Three weeks ago", "Pnet", daysAgo: 21);
        await AddAsync(client, "Too old for the chart", "Pnet", daysAgo: 60);

        var weeks = (await GetAsync(client, weeks: 4)).PerWeek;

        Assert.Equal(4, weeks.Count);
        Assert.All(weeks, w => Assert.Equal(DayOfWeek.Monday, w.WeekStart.DayOfWeek));
        Assert.Equal(1, weeks[^1].Applications); // this week
        Assert.Equal(1, weeks[0].Applications);  // three weeks ago
        Assert.Equal(2, weeks.Sum(w => w.Applications));
    }

    [Fact]
    public async Task Top_missing_skills_come_from_saved_CV_matches()
    {
        var client = await SignedInClientAsync();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("C#"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        (await client.PutAsync("/cv", new MultipartFormDataContent { { file, "file", "cv.txt" } })).EnsureSuccessStatusCode();
        foreach (var company in new[] { "One", "Two" })
        {
            var app = await AddAsync(client, company, "Pnet", 1);
            await client.PostAsJsonAsync("/match", new { jobAd = "Requirements: C#, SQL Server", applicationId = app.Id });
        }

        var skills = (await GetAsync(client)).TopMissingSkills;

        // FakeMatcher reports SQL Server (required) and Docker (nice to have) as missing each time.
        Assert.Equal([new SkillCount("Docker", 2), new SkillCount("SQL Server", 2)], skills);
    }

    [Fact]
    public async Task Only_the_signed_in_users_applications_count()
    {
        var other = await SignedInClientAsync();
        await AddAsync(other, "Not mine", "Pnet", 1);

        Assert.Equal(0, (await GetAsync(await SignedInClientAsync())).Totals.Applications);
    }

    [Fact]
    public async Task Weeks_are_validated()
    {
        var client = await SignedInClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/analytics?weeks=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/analytics?weeks=53")).StatusCode);
    }

    [Fact]
    public async Task Requires_sign_in() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousClient().GetAsync("/analytics")).StatusCode);
}

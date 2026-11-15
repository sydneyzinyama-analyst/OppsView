using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.Matching;

namespace OppsView.Api.Tests.Api;

/// <summary>CV upload and matching over HTTP. The Python matcher is faked (FakeMatcher).</summary>
public class MatchingEndpointsTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private static MultipartFormDataContent CvFile(string text = "C#, ASP.NET Core, PostgreSQL", string name = "my-cv.pdf")
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", name } };
    }

    private static async Task UploadCvAsync(HttpClient client) =>
        (await client.PutAsync("/cv", CvFile())).EnsureSuccessStatusCode();

    [Fact]
    public async Task Uploading_a_CV_stores_its_text_and_replacing_it_works()
    {
        var client = await SignedInClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/cv")).StatusCode);

        var first = await client.PutAsync("/cv", CvFile("first version", "old.docx"));
        var second = await client.PutAsync("/cv", CvFile("second version", "new.pdf"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var cv = await client.GetFromJsonAsync<CvResponse>("/cv", Json);
        Assert.Equal("new.pdf", cv!.FileName);
        Assert.Contains("second version", cv.Preview);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task A_file_the_matcher_cannot_read_is_a_400_with_its_message()
    {
        var client = await SignedInClientAsync();
        Factory.Matcher.RejectWith = "No text could be found in that file.";
        try
        {
            var response = await client.PutAsync("/cv", CvFile());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("No text could be found", (await ReadValidationErrorsAsync(response))["file"][0]);
        }
        finally
        {
            Factory.Matcher.RejectWith = null;
        }
    }

    [Fact]
    public async Task Uploading_without_a_file_is_a_400()
    {
        var client = await SignedInClientAsync();
        var response = await client.PutAsync("/cv", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Matching_needs_a_CV_first()
    {
        var client = await SignedInClientAsync();
        var response = await client.PostAsJsonAsync("/match", new { jobAd = "Requirements: C#" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Matching_without_an_application_returns_the_result_and_saves_nothing()
    {
        var client = await SignedInClientAsync();
        await UploadCvAsync(client);

        var response = await client.PostAsJsonAsync("/match", new { jobAd = "Requirements: C# and SQL Server" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MatchResponse>(Json);
        Assert.Equal(75, result!.Score);
        Assert.Equal(["SQL Server"], result.Required.Missing);
        Assert.Null(result.ApplicationId);
        // The stored CV text is what was sent to the matcher.
        Assert.Contains(Factory.Matcher.Matches, m => m.CvText.Contains("C#, ASP.NET Core") && m.JobAd.Contains("SQL Server"));
    }

    [Fact]
    public async Task Matching_for_an_application_saves_the_score_and_the_result()
    {
        var client = await SignedInClientAsync();
        await UploadCvAsync(client);
        var application = await CreateApplicationAsync(client);

        var response = await client.PostAsJsonAsync("/match", new { jobAd = "Requirements: C#", applicationId = application.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var app = await client.GetFromJsonAsync<ApplicationResponse>($"/applications/{application.Id}", Json);
        Assert.Equal(75, app!.MatchScore);

        var saved = await client.GetFromJsonAsync<SavedMatchResponse>($"/applications/{application.Id}/match", Json);
        Assert.Equal("Requirements: C#", saved!.JobAd);
        Assert.Equal(["Docker"], saved.Result.NiceToHave.Missing);
    }

    [Fact]
    public async Task An_application_never_matched_has_no_saved_match()
    {
        var client = await SignedInClientAsync();
        var application = await CreateApplicationAsync(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/applications/{application.Id}/match")).StatusCode);
    }

    [Fact]
    public async Task Another_users_application_cannot_be_matched_or_read()
    {
        var owner = await SignedInClientAsync();
        var application = await CreateApplicationAsync(owner);
        var intruder = await SignedInClientAsync();
        await UploadCvAsync(intruder);

        var match = await intruder.PostAsJsonAsync("/match", new { jobAd = "C#", applicationId = application.Id });

        Assert.Equal(HttpStatusCode.NotFound, match.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/applications/{application.Id}/match")).StatusCode);
    }

    [Fact]
    public async Task When_the_matcher_is_down_the_user_is_told_clearly()
    {
        var client = await SignedInClientAsync();
        await UploadCvAsync(client);
        Factory.Matcher.Down = true;
        try
        {
            var match = await client.PostAsJsonAsync("/match", new { jobAd = "C#" });
            var upload = await client.PutAsync("/cv", CvFile());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, match.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
            var problem = await match.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
            Assert.Contains("isn't running", problem!.Title);
        }
        finally
        {
            Factory.Matcher.Down = false;
        }
    }

    [Fact]
    public async Task An_empty_job_ad_is_a_400()
    {
        var client = await SignedInClientAsync();
        await UploadCvAsync(client);
        var response = await client.PostAsJsonAsync("/match", new { jobAd = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_CV_removes_it()
    {
        var client = await SignedInClientAsync();
        await UploadCvAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/cv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/cv")).StatusCode);
    }
}

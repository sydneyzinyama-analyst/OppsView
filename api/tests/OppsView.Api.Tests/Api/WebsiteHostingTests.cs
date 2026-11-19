using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// The cloud setup: the API serves the built React app from wwwroot, and its
/// own endpoints move under /api so they can't clash with the website's pages.
/// </summary>
public sealed class WebsiteHostingTests(OppsViewApiFactory factory) : ApiTestBase(factory), IDisposable
{
    private readonly string _webRoot = CreateWebRoot();

    private static string CreateWebRoot()
    {
        var dir = Directory.CreateTempSubdirectory("oppsview-wwwroot-").FullName;
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><title>OppsView</title><div id=root></div>");
        Directory.CreateDirectory(Path.Combine(dir, "assets"));
        File.WriteAllText(Path.Combine(dir, "assets", "index-abc123.js"), "console.log('hi')");
        return dir;
    }

    private HttpClient CloudClient() => Factory
        .WithWebHostBuilder(b => b.UseSetting("Hosting:ApiPrefix", "api").UseWebRoot(_webRoot))
        .CreateClient();

    [Fact]
    public async Task Api_endpoints_live_under_the_prefix()
    {
        var client = CloudClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/no-such-endpoint")).StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/reminders")] // also an API route name: must be the page, not the API
    [InlineData("/insights")]
    public async Task Website_pages_get_the_react_app(string path)
    {
        var response = await CloudClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=root>", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Hashed_assets_are_cached_for_a_year()
    {
        var response = await CloudClient().GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Health_checks_stay_at_the_root()
    {
        Assert.Equal(HttpStatusCode.OK, (await CloudClient().GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Without_a_website_or_prefix_nothing_changes()
    {
        // The default (local development and the other tests): no prefix, no website.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousClient().GetAsync("/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AnonymousClient().GetAsync("/insights")).StatusCode);
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);
}

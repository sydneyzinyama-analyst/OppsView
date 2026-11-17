using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace OppsView.Api.Tests.Api;

/// <summary>Login rate limiting and the health endpoints Azure uses.</summary>
public class ProtectionAndHealthTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Login_attempts_are_limited_per_minute()
    {
        using var strict = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPerMinute", "3"));
        var client = strict.CreateClient();
        var attempt = new { email = "nobody@example.com", password = "wrong password" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.PostAsJsonAsync("/auth/login", attempt)).StatusCode);

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            statuses);
    }

    [Fact]
    public async Task Other_endpoints_are_not_rate_limited()
    {
        using var strict = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPerMinute", "1"));
        var client = strict.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register",
            new { email = $"rl-{Guid.NewGuid():N}@example.com", password = "correct horse battery" });
        var token = (await register.Content.ReadFromJsonAsync<Features.Auth.AuthResponse>(Json))!.Token;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/applications")).StatusCode);
    }

    [Fact]
    public async Task Health_says_the_API_is_up_without_signing_in()
    {
        var response = await AnonymousClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_also_checks_the_database()
    {
        var response = await AnonymousClient().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

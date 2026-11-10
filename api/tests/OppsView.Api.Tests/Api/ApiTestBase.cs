using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.Auth;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// One factory (one database) per test class via IClassFixture. Each test
/// registers its own user with a unique email, and because every query is
/// scoped to the current user, tests can't see each other's data.
/// </summary>
public abstract class ApiTestBase(OppsViewApiFactory factory) : IClassFixture<OppsViewApiFactory>
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected OppsViewApiFactory Factory { get; } = factory;

    protected static DateOnly Today => DateOnly.FromDateTime(OppsViewApiFactory.StartTime.UtcDateTime);

    protected HttpClient AnonymousClient() => Factory.CreateClient();

    protected async Task<HttpClient> SignedInClientAsync(string? email = null)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = email ?? $"user-{Guid.NewGuid():N}@example.com",
            password = "correct horse battery",
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    protected static object ValidApplication(string company = "Acme") => new
    {
        company,
        role = "Junior .NET Developer",
        platform = "Pnet",
        appliedOn = Today.ToString("yyyy-MM-dd"),
        jobUrl = "https://www.pnet.co.za/jobs/123",
        notes = (string?)null,
    };

    protected static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client, string company = "Acme")
    {
        var response = await client.PostAsJsonAsync("/applications", ValidApplication(company));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json))!;
    }

    protected static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(Json)!;
    }
}

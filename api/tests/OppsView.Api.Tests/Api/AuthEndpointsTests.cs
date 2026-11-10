using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using OppsView.Api.Features.Auth;

namespace OppsView.Api.Tests.Api;

public class AuthEndpointsTests(OppsViewApiFactory factory) : ApiTestBase(factory)
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Register_returns_a_token_and_the_normalised_email()
    {
        var client = AnonymousClient();
        var email = $"New-{Guid.NewGuid():N}@Example.com";

        var issuedAt = Factory.Time.GetUtcNow().UtcDateTime;
        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal(email.ToLowerInvariant(), auth.Email);
        Assert.Equal(issuedAt.AddMinutes(480), auth.ExpiresAt);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409_regardless_of_case()
    {
        var client = AnonymousClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", Password, "email")]
    [InlineData("ok@example.com", "short", "password")]
    public async Task Register_validates_input(string email, string password, string field)
    {
        var response = await AnonymousClient().PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await ReadValidationErrorsAsync(response)).Keys);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_a_token()
    {
        var client = AnonymousClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/auth/login", new { email = $"  {email.ToUpperInvariant()} ", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal(email, auth!.Email);
    }

    [Fact]
    public async Task Login_failures_look_the_same_for_unknown_email_and_wrong_password()
    {
        var client = AnonymousClient();
        var email = $"fail-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var wrongPassword = await client.PostAsJsonAsync("/auth/login", new { email, password = "wrong password!" });
        var unknownEmail = await client.PostAsJsonAsync("/auth/login", new { email = "nobody@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Same title for both, so the response doesn't reveal which emails exist.
        var a = await wrongPassword.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
        var b = await unknownEmail.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
        Assert.Equal(a!.Title, b!.Title);
    }

    [Fact]
    public async Task Passwords_are_stored_hashed()
    {
        var email = $"hash-{Guid.NewGuid():N}@example.com";
        await SignedInClientAsync(email);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OppsView.Api.Data.AppDbContext>();
        var user = db.Users.Single(u => u.Email == email);
        Assert.DoesNotContain("correct horse battery", user.PasswordHash);
        Assert.True(user.PasswordHash.Length > 40);
    }

    [Fact]
    public async Task Applications_require_a_token()
    {
        var response = await AnonymousClient().GetAsync("/applications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var client = await SignedInClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/applications")).StatusCode);

        // Tokens last 8 hours. The fake clock is shared by this class's tests
        // and only moves forward, so no test depends on the absolute time.
        Factory.Time.Advance(TimeSpan.FromMinutes(481));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/applications")).StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var client = await SignedInClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/applications")).StatusCode);
    }
}

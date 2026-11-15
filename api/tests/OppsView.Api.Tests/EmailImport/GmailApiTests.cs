using System.Net;
using System.Text;
using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.EmailImport;

/// <summary>How GmailApi reacts to Gmail's error responses.</summary>
public class GmailApiTests
{
    private const string ProfileOk = """{"emailAddress":"me@gmail.com"}""";

    private static string Error(int code, string reason, string message = "msg") =>
        System.Text.Json.JsonSerializer.Serialize(new { error = new { code, message, errors = new[] { new { reason } } } });

    private static (GmailApi Api, ScriptedHandler Handler) Create(params (HttpStatusCode Status, string Body)[] responses)
    {
        var handler = new ScriptedHandler(responses);
        var api = new GmailApi(new HttpClient(handler)) { RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero] };
        return (api, handler);
    }

    [Fact]
    public async Task A_rate_limit_403_is_retried_and_then_succeeds()
    {
        var (api, handler) = Create(
            (HttpStatusCode.Forbidden, Error(403, "userRateLimitExceeded")),
            (HttpStatusCode.Forbidden, Error(403, "rateLimitExceeded")),
            (HttpStatusCode.OK, ProfileOk));

        Assert.Equal("me@gmail.com", await api.GetEmailAddressAsync("token", CancellationToken.None));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task A_rate_limit_that_never_clears_gives_up_with_a_clear_error()
    {
        var (api, handler) = Create(Enumerable.Repeat((HttpStatusCode.TooManyRequests, Error(429, "rateLimitExceeded")), 10).ToArray());

        var ex = await Assert.ThrowsAsync<GmailApiException>(() => api.GetEmailAddressAsync("token", CancellationToken.None));

        Assert.True(ex.IsRateLimit);
        Assert.Equal(4, handler.Calls); // first try + 3 retries
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authError")]
    [InlineData(HttpStatusCode.Forbidden, "insufficientPermissions")]
    public async Task Only_real_auth_problems_mean_reconnect(HttpStatusCode status, string reason)
    {
        var (api, _) = Create((status, Error((int)status, reason)));
        await Assert.ThrowsAsync<GmailAccessRevokedException>(() => api.GetEmailAddressAsync("token", CancellationToken.None));
    }

    [Fact]
    public async Task Other_403s_are_reported_with_Googles_reason_and_not_retried()
    {
        var (api, handler) = Create((HttpStatusCode.Forbidden, Error(403, "forbidden", "Delegation denied")));

        var ex = await Assert.ThrowsAsync<GmailApiException>(() => api.GetEmailAddressAsync("token", CancellationToken.None));

        Assert.False(ex.IsRateLimit);
        Assert.Contains("forbidden", ex.Message);
        Assert.Contains("Delegation denied", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Server_errors_are_retried()
    {
        var (api, handler) = Create((HttpStatusCode.ServiceUnavailable, "oops"), (HttpStatusCode.OK, ProfileOk));
        Assert.Equal("me@gmail.com", await api.GetEmailAddressAsync("token", CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }

    private sealed class ScriptedHandler((HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

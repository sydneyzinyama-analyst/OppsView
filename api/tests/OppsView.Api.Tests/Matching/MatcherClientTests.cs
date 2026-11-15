using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using OppsView.Api.Features.Matching;

namespace OppsView.Api.Tests.Matching;

/// <summary>The real MatcherClient against scripted HTTP responses.</summary>
public class MatcherClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private static (MatcherClient, Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond, string? apiKey = "k")
    {
        var handler = new Handler(respond);
        var client = new MatcherClient(new HttpClient(handler),
            Options.Create(new MatcherOptions { BaseUrl = "http://matcher:8000", ApiKey = apiKey }));
        return (client, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Match_sends_camelCase_json_with_the_api_key_and_reads_the_result()
    {
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK,
            """{"score":80,"required":{"matched":["C#"],"missing":[]},"niceToHave":{"matched":[],"missing":["Docker"]},"extraInCv":[],"summary":"80%"}"""));

        var result = await client.MatchAsync("cv text", "the ad", CancellationToken.None);

        Assert.Equal(80, result.Score);
        Assert.Equal(["Docker"], result.NiceToHave.Missing);
        Assert.Equal("http://matcher:8000/match", handler.Last!.RequestUri!.ToString());
        Assert.Equal("k", handler.Last.Headers.GetValues("X-Api-Key").Single());
    }

    [Fact]
    public async Task A_422_becomes_a_rejection_with_the_matchers_message()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.UnprocessableEntity, """{"detail":"Please upload your CV as a PDF."}"""));

        var ex = await Assert.ThrowsAsync<MatcherRejectedException>(() =>
            client.ExtractTextAsync("cv.jpg", new MemoryStream([1, 2]), CancellationToken.None));

        Assert.Equal("Please upload your CV as a PDF.", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)] // wrong API key: a setup problem, not the user's
    public async Task Other_errors_mean_the_matcher_is_unavailable(HttpStatusCode status)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(status));
        await Assert.ThrowsAsync<MatcherUnavailableException>(() => client.MatchAsync("cv", "ad", CancellationToken.None));
    }

    [Fact]
    public async Task A_connection_failure_means_the_matcher_is_unavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("Connection refused"));
        await Assert.ThrowsAsync<MatcherUnavailableException>(() => client.MatchAsync("cv", "ad", CancellationToken.None));
    }

    [Fact]
    public async Task No_api_key_header_when_none_is_configured()
    {
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK, """{"text":"hello"}"""), apiKey: null);

        Assert.Equal("hello", await client.ExtractTextAsync("cv.txt", new MemoryStream([1]), CancellationToken.None));
        Assert.False(handler.Last!.Headers.Contains("X-Api-Key"));
    }
}

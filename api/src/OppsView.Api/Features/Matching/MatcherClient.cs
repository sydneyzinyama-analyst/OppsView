using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OppsView.Api.Features.Matching;

/// <summary>Settings for the Python matcher service, from the "Matcher" config section.</summary>
public sealed class MatcherOptions
{
    public const string SectionName = "Matcher";

    public string BaseUrl { get; init; } = "http://localhost:8000";

    /// <summary>Shared secret sent as X-Api-Key; must equal the matcher's MATCHER_API_KEY. Optional locally.</summary>
    public string? ApiKey { get; init; }
}

// The matcher's JSON, mirrored as C# records (camelCase on the wire).
public sealed record SkillGroup(IReadOnlyList<string> Matched, IReadOnlyList<string> Missing);

public sealed record MatcherResult(
    int? Score,
    SkillGroup Required,
    SkillGroup NiceToHave,
    IReadOnlyList<string> ExtraInCv,
    string Summary);

/// <summary>The matcher isn't running or didn't answer.</summary>
public sealed class MatcherUnavailableException(Exception? inner = null)
    : Exception("The CV matcher isn't reachable.", inner);

/// <summary>The matcher understood the request but refused it (e.g. a CV it can't read); Message is for the user.</summary>
public sealed class MatcherRejectedException(string message) : Exception(message);

public interface IMatcherClient
{
    Task<string> ExtractTextAsync(string fileName, Stream content, CancellationToken ct);
    Task<MatcherResult> MatchAsync(string cvText, string jobAd, CancellationToken ct);
}

/// <summary>Calls the Python matcher over HTTP (a typed HttpClient).</summary>
public sealed class MatcherClient(HttpClient http, IOptions<MatcherOptions> options) : IMatcherClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MatcherOptions _options = options.Value;

    public async Task<string> ExtractTextAsync(string fileName, Stream content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        var body = await SendAsync(new HttpRequestMessage(HttpMethod.Post, Url("extract")) { Content = form }, ct);
        return body.GetProperty("text").GetString() ?? "";
    }

    public async Task<MatcherResult> MatchAsync(string cvText, string jobAd, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url("match"))
        {
            Content = JsonContent.Create(new { cvText, jobAd }, options: Json),
        };
        var body = await SendAsync(request, ct);
        return body.Deserialize<MatcherResult>(Json)!;
    }

    private Uri Url(string path) => new(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), path);

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            if (!string.IsNullOrEmpty(_options.ApiKey)) request.Headers.Add("X-Api-Key", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new MatcherUnavailableException(ex); // not running / wrong URL
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new MatcherUnavailableException(ex); // timed out
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                    throw new MatcherRejectedException(await ReadDetailAsync(response, ct));
                if (!response.IsSuccessStatusCode)
                    throw new MatcherUnavailableException(new HttpRequestException($"Matcher returned {(int)response.StatusCode}"));
                return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            }
        }
    }

    /// <summary>FastAPI errors: {"detail": "message"} or {"detail": [{"msg": "..."}]} for validation.</summary>
    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var detail = body.GetProperty("detail");
            return detail.ValueKind == JsonValueKind.String
                ? detail.GetString()!
                : detail.EnumerateArray().First().GetProperty("msg").GetString()!;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return "The CV matcher couldn't process that.";
        }
    }
}

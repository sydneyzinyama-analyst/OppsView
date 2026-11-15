using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>The three Gmail API calls OppsView needs. All read-only.</summary>
public interface IGmailApi
{
    Task<string> GetEmailAddressAsync(string accessToken, CancellationToken ct);
    Task<IReadOnlyList<string>> SearchMessageIdsAsync(string accessToken, string query, int max, CancellationToken ct);
    Task<IncomingEmail> GetMessageAsync(string accessToken, string messageId, CancellationToken ct);
}

/// <summary>Gmail answered with an error other than "access revoked" (rate limit, server error...).</summary>
public sealed class GmailApiException(int statusCode, string? reason, string? googleMessage)
    : Exception($"Gmail API error {statusCode}{(reason is null ? "" : $" ({reason})")}: {googleMessage ?? "no details"}")
{
    public int StatusCode { get; } = statusCode;
    public string? Reason { get; } = reason;
    public bool IsRateLimit => StatusCode == 429 || Reason is "rateLimitExceeded" or "userRateLimitExceeded";
}

public sealed class GmailApi(HttpClient http) : IGmailApi
{
    private const string Base = "https://gmail.googleapis.com/gmail/v1/users/me";

    /// <summary>
    /// Pauses between retries when Gmail says "slow down" (rate limit) or has
    /// a temporary server error. Settable so tests don't wait.
    /// </summary>
    internal TimeSpan[] RetryDelays { get; set; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    public async Task<string> GetEmailAddressAsync(string accessToken, CancellationToken ct)
    {
        var profile = await GetJsonAsync(accessToken, $"{Base}/profile", ct);
        return profile.GetProperty("emailAddress").GetString()!;
    }

    public async Task<IReadOnlyList<string>> SearchMessageIdsAsync(string accessToken, string query, int max, CancellationToken ct)
    {
        var ids = new List<string>();
        string? pageToken = null;
        do
        {
            var url = QueryHelpers.AddQueryString($"{Base}/messages", new Dictionary<string, string?>
            {
                ["q"] = query,
                ["maxResults"] = Math.Min(100, max - ids.Count).ToString(),
                ["pageToken"] = pageToken,
            });
            var page = await GetJsonAsync(accessToken, url, ct);
            if (page.TryGetProperty("messages", out var messages))
                ids.AddRange(messages.EnumerateArray().Select(m => m.GetProperty("id").GetString()!));
            pageToken = page.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        } while (pageToken is not null && ids.Count < max);

        return ids;
    }

    public async Task<IncomingEmail> GetMessageAsync(string accessToken, string messageId, CancellationToken ct)
    {
        var message = await GetJsonAsync(accessToken, $"{Base}/messages/{Uri.EscapeDataString(messageId)}?format=full", ct);
        return GmailMessageReader.Read(message);
    }

    private async Task<JsonElement> GetJsonAsync(string accessToken, string url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            var (reason, message) = await ReadErrorAsync(response, ct);
            var status = (int)response.StatusCode;

            // Only these mean "the user has to connect again". Gmail also
            // uses 403 for rate limits, so a 403 alone proves nothing.
            if (status == 401 || reason is "insufficientPermissions" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT" or "authError")
                throw new GmailAccessRevokedException();

            var error = new GmailApiException(status, reason, message);
            var temporary = error.IsRateLimit || status >= 500;
            if (!temporary || attempt >= RetryDelays.Length) throw error;

            var wait = response.Headers.RetryAfter?.Delta ?? RetryDelays[attempt];
            await Task.Delay(wait, ct);
        }
    }

    /// <summary>
    /// Gmail errors look like {"error":{"code":403,"message":"...","errors":[{"reason":"rateLimitExceeded"}],"status":"..."}}.
    /// The first "reason" says what actually went wrong.
    /// </summary>
    private static async Task<(string? Reason, string? Message)> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!body.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return (null, null);

            var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            string? reason = null;
            if (error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                reason = errors.EnumerateArray().Select(e => e.TryGetProperty("reason", out var r) ? r.GetString() : null).FirstOrDefault(r => r is not null);
            if (reason is null && error.TryGetProperty("status", out var st)) reason = st.GetString();
            return (reason, message);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return (null, null);
        }
    }
}

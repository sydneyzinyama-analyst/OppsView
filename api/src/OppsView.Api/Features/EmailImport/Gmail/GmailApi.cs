using System.Net;
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

public sealed class GmailApi(HttpClient http) : IGmailApi
{
    private const string Base = "https://gmail.googleapis.com/gmail/v1/users/me";

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
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new GmailAccessRevokedException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }
}

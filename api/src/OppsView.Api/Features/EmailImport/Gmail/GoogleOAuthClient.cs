using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>Google's OAuth 2.0 endpoints: the consent page, code exchange, refresh and revoke.</summary>
public interface IGoogleOAuthClient
{
    string BuildAuthorizationUrl(string state, string codeChallenge);
    Task<GoogleTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);

    /// <exception cref="GmailAccessRevokedException">Google no longer accepts the refresh token.</exception>
    Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken ct);

    Task RevokeAsync(string token, CancellationToken ct);
}

public sealed record GoogleTokens(string AccessToken, string? RefreshToken, string GrantedScopes);

/// <summary>The user has to connect Gmail again (token revoked or expired).</summary>
public sealed class GmailAccessRevokedException() : Exception("Gmail access has expired or was revoked.");

public sealed class GoogleOAuthClient(HttpClient http, IOptions<GoogleOptions> options) : IGoogleOAuthClient
{
    public const string ReadOnlyScope = "https://www.googleapis.com/auth/gmail.readonly";
    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    private readonly GoogleOptions _options = options.Value;

    public string BuildAuthorizationUrl(string state, string codeChallenge) =>
        QueryHelpers.AddQueryString(AuthorizeEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = ReadOnlyScope,
            // offline + consent: Google returns a refresh token, so OppsView
            // can sync later without the user being present.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state,
            // PKCE: proves the code exchange comes from whoever started the login.
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        });

    public async Task<GoogleTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        var token = await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = _options.RedirectUri,
        }, ct);
        return new GoogleTokens(token.AccessToken!, token.RefreshToken, token.Scope ?? "");
    }

    public async Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var token = await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, ct);
        return token.AccessToken!;
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        using var response = await http.PostAsync(RevokeEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }), ct);
        // Best effort: a token that's already invalid can't be revoked, and
        // that's fine; disconnecting still deletes it on our side.
    }

    private async Task<TokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        form["client_id"] = _options.ClientId!;
        form["client_secret"] = _options.ClientSecret!;

        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

        // invalid_grant = the refresh token (or code) is no longer valid.
        if (body?.Error == "invalid_grant") throw new GmailAccessRevokedException();
        if (!response.IsSuccessStatusCode || body?.AccessToken is null)
            throw new HttpRequestException($"Google token request failed: {(int)response.StatusCode} {body?.Error}");
        return body;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("error")] string? Error);
}

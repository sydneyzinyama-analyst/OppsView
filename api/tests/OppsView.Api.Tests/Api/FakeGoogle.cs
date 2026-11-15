using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.Api;

/// <summary>
/// Stands in for Google in integration tests: a pretend OAuth server and a
/// pretend Gmail mailbox that tests can fill with emails.
/// </summary>
public sealed class FakeGoogle : IGoogleOAuthClient, IGmailApi
{
    public const string RefreshToken = "refresh-token-secret";
    public const string AccessToken = "access-token";

    public string EmailAddress { get; set; } = "me@gmail.com";
    public string GrantedScopes { get; set; } = GoogleOAuthClient.ReadOnlyScope;
    public bool Revoked { get; set; }
    public List<string> RevokedTokens { get; } = [];
    public List<string> Queries { get; } = [];
    public Dictionary<string, IncomingEmail> Mailbox { get; } = [];
    public int MessagesFetched { get; private set; }

    public string BuildAuthorizationUrl(string state, string codeChallenge) =>
        $"https://accounts.google.com/o/oauth2/v2/auth?state={Uri.EscapeDataString(state)}&code_challenge={codeChallenge}";

    public Task<GoogleTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct) =>
        Task.FromResult(new GoogleTokens(AccessToken, RefreshToken, GrantedScopes));

    public Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken ct) =>
        Revoked || refreshToken != RefreshToken
            ? throw new GmailAccessRevokedException()
            : Task.FromResult(AccessToken);

    public Task RevokeAsync(string token, CancellationToken ct)
    {
        RevokedTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<string> GetEmailAddressAsync(string accessToken, CancellationToken ct) => Task.FromResult(EmailAddress);

    public Task<IReadOnlyList<string>> SearchMessageIdsAsync(string accessToken, string query, int max, CancellationToken ct)
    {
        Queries.Add(query);
        return Task.FromResult<IReadOnlyList<string>>(Mailbox.Keys.Take(max).ToList());
    }

    public Task<IncomingEmail> GetMessageAsync(string accessToken, string messageId, CancellationToken ct)
    {
        MessagesFetched++;
        return Task.FromResult(Mailbox[messageId]);
    }
}

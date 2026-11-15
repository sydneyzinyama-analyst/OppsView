using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>
/// Packs "which user started this, and their PKCE secret" into the OAuth
/// <c>state</c> parameter, encrypted and signed with ASP.NET Core Data
/// Protection and valid for 10 minutes.
///
/// The browser carries it to Google and back, but can't read or change it,
/// so when Google calls our callback (a plain browser redirect, with no JWT)
/// we still know reliably who connected, and nobody can forge a callback
/// that attaches their mailbox to someone else's account.
/// </summary>
public sealed class OAuthStateProtector(IDataProtectionProvider provider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("OppsView.Gmail.OAuthState").ToTimeLimitedDataProtector();

    public sealed record State(Guid UserId, string CodeVerifier);

    public (string Protected, string CodeChallenge) Create(Guid userId)
    {
        // PKCE: a random secret (verifier) whose SHA-256 hash (challenge)
        // goes to Google up front; the secret itself is only sent when
        // exchanging the code.
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var json = JsonSerializer.Serialize(new State(userId, verifier));
        return (_protector.Protect(json, Lifetime), challenge);
    }

    /// <returns>null if the state was tampered with or has expired.</returns>
    public State? Read(string? protectedState)
    {
        if (string.IsNullOrEmpty(protectedState)) return null;
        try
        {
            return JsonSerializer.Deserialize<State>(_protector.Unprotect(protectedState));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

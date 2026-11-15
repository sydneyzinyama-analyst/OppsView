using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OppsView.Api.Data;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>Connect, sync and disconnect Gmail for a user.</summary>
public sealed class GmailSyncService(
    AppDbContext db,
    IGoogleOAuthClient oauth,
    IGmailApi gmail,
    EmailImporter importer,
    OAuthStateProtector states,
    IDataProtectionProvider dataProtection,
    IOptions<GoogleOptions> options,
    TimeProvider time,
    ILogger<GmailSyncService> logger)
{
    // One sync per user at a time; a double-click shouldn't import twice.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SyncLocks = new();

    private readonly GoogleOptions _options = options.Value;
    private readonly IDataProtector _tokens = dataProtection.CreateProtector("OppsView.Gmail.RefreshToken");

    public bool IsAvailable => _options.IsConfigured;

    public string StartConnect(Guid userId)
    {
        var (state, challenge) = states.Create(userId);
        return oauth.BuildAuthorizationUrl(state, challenge);
    }

    /// <summary>Handles Google's redirect back to us. Returns a short result code for the web app.</summary>
    public async Task<string> CompleteConnectAsync(string? code, string? state, string? error, CancellationToken ct)
    {
        if (error is not null) return error == "access_denied" ? "denied" : "error";

        var parsed = states.Read(state);
        if (parsed is null || string.IsNullOrEmpty(code)) return "expired";

        try
        {
            var tokens = await oauth.ExchangeCodeAsync(code, parsed.CodeVerifier, ct);
            // Google lets people untick permissions on the consent screen.
            if (!tokens.GrantedScopes.Split(' ').Contains(GoogleOAuthClient.ReadOnlyScope)) return "scope";
            if (tokens.RefreshToken is null) return "error";

            var address = await gmail.GetEmailAddressAsync(tokens.AccessToken, ct);
            var encrypted = _tokens.Protect(tokens.RefreshToken);
            var now = time.GetUtcNow().UtcDateTime;

            var connection = await db.GmailConnections.FindAsync([parsed.UserId], ct);
            if (connection is null)
                db.GmailConnections.Add(GmailConnection.Create(parsed.UserId, address, encrypted, now));
            else
                connection.Reconnect(address, encrypted, now);
            await db.SaveChangesAsync(ct);
            return "connected";
        }
        catch (Exception ex) when (ex is HttpRequestException or GmailAccessRevokedException)
        {
            logger.LogWarning(ex, "Connecting Gmail failed for user {UserId}", parsed.UserId);
            return "error";
        }
    }

    public async Task<EmailStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.GmailConnections.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == userId, ct);
        var reviewCount = await db.ImportedEmails.CountAsync(e => e.UserId == userId && e.Outcome == ImportOutcome.NeedsReview, ct);
        return new EmailStatusResponse(
            IsAvailable,
            connection is not null,
            connection?.EmailAddress,
            connection?.LastSyncedAt is { } t ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null,
            connection?.NeedsReconnect ?? false,
            reviewCount);
    }

    /// <exception cref="GmailNotConnectedException">No usable connection.</exception>
    /// <exception cref="SyncInProgressException">A sync for this user is already running.</exception>
    public async Task<ImportSummary> SyncAsync(Guid userId, CancellationToken ct)
    {
        var gate = SyncLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct)) throw new SyncInProgressException();
        try
        {
            return await SyncCoreAsync(userId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ImportSummary> SyncCoreAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.GmailConnections.SingleOrDefaultAsync(c => c.UserId == userId, ct);
        if (connection is null || connection.NeedsReconnect) throw new GmailNotConnectedException();

        var now = time.GetUtcNow().UtcDateTime;
        string accessToken;
        try
        {
            accessToken = await oauth.GetAccessTokenAsync(_tokens.Unprotect(connection.EncryptedRefreshToken), ct);
        }
        catch (Exception ex) when (ex is GmailAccessRevokedException or System.Security.Cryptography.CryptographicException)
        {
            // CryptographicException: the encryption keys changed (e.g. a new
            // machine), so the stored token can't be read. Same fix: reconnect.
            connection.MarkNeedsReconnect();
            await db.SaveChangesAsync(ct);
            throw new GmailNotConnectedException();
        }

        // Overlap the previous sync by two days, in case an email arrived
        // while that sync was running. Already-processed emails are skipped.
        var since = connection.LastSyncedAt?.AddDays(-2) ?? now.AddDays(-_options.InitialSyncDays);
        var ids = await gmail.SearchMessageIdsAsync(accessToken, GmailQuery.Build(since), _options.MaxMessagesPerSync, ct);

        var known = (await db.ImportedEmails
                .Where(e => e.UserId == userId && ids.Contains(e.MessageId))
                .Select(e => e.MessageId)
                .ToListAsync(ct))
            .ToHashSet();
        var newIds = ids.Where(id => !known.Contains(id)).ToList();

        var emails = new List<IncomingEmail>(newIds.Count);
        foreach (var id in newIds)
            emails.Add(await gmail.GetMessageAsync(accessToken, id, ct));

        var summary = await importer.ImportAsync(userId, emails, ct);

        connection.MarkSynced(now);
        await db.SaveChangesAsync(ct);

        return summary with { AlreadyProcessed = summary.AlreadyProcessed + known.Count };
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.GmailConnections.SingleOrDefaultAsync(c => c.UserId == userId, ct);
        if (connection is null) return;

        try
        {
            await oauth.RevokeAsync(_tokens.Unprotect(connection.EncryptedRefreshToken), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Security.Cryptography.CryptographicException)
        {
            logger.LogInformation(ex, "Could not revoke Google token for {UserId}; deleting it anyway", userId);
        }

        db.GmailConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class GmailNotConnectedException() : Exception("Gmail is not connected.");
public sealed class SyncInProgressException() : Exception("A sync is already running.");

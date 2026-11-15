namespace OppsView.Api.Domain;

/// <summary>
/// A user's link to their Gmail account. At most one per user.
///
/// The refresh token (Google's long-lived "OppsView may read this mailbox"
/// key) is stored encrypted; this entity only ever holds the encrypted
/// form. Encryption happens in the service layer with ASP.NET Core Data
/// Protection, so the domain doesn't depend on a crypto library.
/// </summary>
public class GmailConnection
{
    public const int EmailMaxLength = 320;

    public Guid UserId { get; private set; }

    /// <summary>The Gmail address that was connected (shown in the UI).</summary>
    public string EmailAddress { get; private set; } = null!;

    public string EncryptedRefreshToken { get; private set; } = null!;
    public DateTime ConnectedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    /// <summary>
    /// Google stopped accepting the token (revoked, password changed, or the
    /// 7-day limit for apps in Testing mode). The user has to connect again.
    /// </summary>
    public bool NeedsReconnect { get; private set; }

    private GmailConnection() { }

    public static GmailConnection Create(Guid userId, string emailAddress, string encryptedRefreshToken, DateTime nowUtc)
    {
        var connection = new GmailConnection { UserId = userId };
        connection.Reconnect(emailAddress, encryptedRefreshToken, nowUtc);
        return connection;
    }

    /// <summary>Connecting again replaces the token and clears the reconnect flag; sync history is kept.</summary>
    public void Reconnect(string emailAddress, string encryptedRefreshToken, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(encryptedRefreshToken))
            throw new ArgumentException("A token is required.", nameof(encryptedRefreshToken));
        EmailAddress = emailAddress.Trim();
        EncryptedRefreshToken = encryptedRefreshToken;
        ConnectedAt = nowUtc;
        NeedsReconnect = false;
    }

    public void MarkSynced(DateTime nowUtc) => LastSyncedAt = nowUtc;

    public void MarkNeedsReconnect() => NeedsReconnect = true;
}

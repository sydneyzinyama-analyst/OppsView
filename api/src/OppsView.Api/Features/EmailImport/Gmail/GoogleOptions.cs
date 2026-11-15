namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>
/// Settings for "Connect Gmail", bound from the "Google" config section.
/// ClientId and ClientSecret come from the Google Cloud console and live in
/// user-secrets (never appsettings). Unlike the JWT settings these are
/// optional: without them the app runs normally and email import simply
/// reports that it isn't set up.
/// </summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    /// <summary>Must exactly match an "Authorized redirect URI" in the Google Cloud console.</summary>
    public string RedirectUri { get; init; } = "http://localhost:5226/email/google/callback";

    /// <summary>Where the browser is sent after connecting (the React app).</summary>
    public string WebAppUrl { get; init; } = "http://localhost:5173";

    /// <summary>How far back the first sync looks.</summary>
    public int InitialSyncDays { get; init; } = 60;

    /// <summary>Upper bound per sync, to keep one sync quick.</summary>
    public int MaxMessagesPerSync { get; init; } = 200;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

namespace OppsView.Api.Domain;

/// <summary>What an email means for the user's job hunt, as decided by an email parser.</summary>
public enum EmailKind
{
    /// <summary>"Thanks for applying": a new application.</summary>
    ApplicationReceived,
    /// <summary>The employer looked at the application (some platforms send this).</summary>
    ApplicationViewed,
    InterviewInvite,
    Offer,
    Rejection,
    /// <summary>From a job site but not about an application (job alerts, newsletters).</summary>
    NotRelevant,
    /// <summary>Looks job-related, but the parser couldn't tell what it is.</summary>
    Unknown,
}

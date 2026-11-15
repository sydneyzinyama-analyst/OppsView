namespace OppsView.Api.Domain;

/// <summary>What OppsView did with an imported email.</summary>
public enum ImportOutcome
{
    /// <summary>A new application was created.</summary>
    Created,
    /// <summary>An existing application's status was changed.</summary>
    Updated,
    /// <summary>The email matched an application that already reflected it (e.g. a second confirmation).</summary>
    NoChange,
    /// <summary>Not about an application (job alerts and the like).</summary>
    Ignored,
    /// <summary>OppsView wasn't sure, so it left the decision to the user.</summary>
    NeedsReview,
    /// <summary>The user looked at a NeedsReview email and chose to ignore it.</summary>
    Dismissed,
}

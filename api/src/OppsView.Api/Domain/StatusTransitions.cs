namespace OppsView.Api.Domain;

/// <summary>
/// The rules for which status changes are allowed.
///
/// Applications only move forward through the pipeline:
///   Applied -> Viewed -> Interview -> Offer
/// and can be Rejected from any non-final stage. Stages can be skipped
/// (many companies go straight from Applied to Interview, or reject without
/// ever "viewing"). Offer and Rejected are final.
///
/// Forward-only keeps the history honest, which matters for the Phase 5
/// "where do applications stall" analytics. A mis-click can still be fixed
/// by deleting and re-adding the application.
/// </summary>
public static class StatusTransitions
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Applied] = [ApplicationStatus.Viewed, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Rejected],
        [ApplicationStatus.Viewed] = [ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Rejected],
        [ApplicationStatus.Interview] = [ApplicationStatus.Offer, ApplicationStatus.Rejected],
        [ApplicationStatus.Offer] = [],
        [ApplicationStatus.Rejected] = [],
    };

    public static IReadOnlyList<ApplicationStatus> AllowedFrom(ApplicationStatus from) => Allowed[from];

    public static bool IsAllowed(ApplicationStatus from, ApplicationStatus to) => Allowed[from].Contains(to);
}

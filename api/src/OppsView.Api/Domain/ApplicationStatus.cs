namespace OppsView.Api.Domain;

/// <summary>
/// Where an application is in the hiring pipeline. The order matters:
/// it is the "forward" direction used by <see cref="StatusTransitions"/>.
/// </summary>
public enum ApplicationStatus
{
    Applied,
    Viewed,
    Interview,
    Offer,
    Rejected,
}

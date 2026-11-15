namespace OppsView.Api.Features.EmailImport;

/// <summary>What one sync did, for the "Sync now" result message.</summary>
public sealed record ImportSummary(
    int Created,
    int Updated,
    int NoChange,
    int Ignored,
    int NeedsReview,
    int AlreadyProcessed,
    /// <summary>Emails Gmail wouldn't hand over this time; the next sync tries them again.</summary>
    int Failed = 0)
{
    public static readonly ImportSummary Empty = new(0, 0, 0, 0, 0, 0);
}

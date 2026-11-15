namespace OppsView.Api.Features.EmailImport;

/// <summary>What one sync did, for the "Sync now" result message.</summary>
public sealed record ImportSummary(
    int Created,
    int Updated,
    int NoChange,
    int Ignored,
    int NeedsReview,
    int AlreadyProcessed)
{
    public static readonly ImportSummary Empty = new(0, 0, 0, 0, 0, 0);
}

using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport;

public sealed record EmailStatusResponse(
    bool Available,
    bool Connected,
    string? EmailAddress,
    DateTime? LastSyncedAt,
    bool NeedsReconnect,
    int NeedsReviewCount);

public sealed record ConnectResponse(string AuthorizationUrl);

public sealed record ReviewItemResponse(
    Guid Id,
    DateTime ReceivedAt,
    string Sender,
    string Subject,
    EmailKind Kind,
    JobPlatform? Platform,
    string? Company,
    string? Role,
    string? Reason,
    Guid? ApplicationId)
{
    public static ReviewItemResponse From(ImportedEmail e) => new(
        e.Id, DateTime.SpecifyKind(e.ReceivedAt, DateTimeKind.Utc), e.Sender, e.Subject, e.Kind, e.Platform,
        e.Company, e.Role, e.Reason, e.ApplicationId);
}

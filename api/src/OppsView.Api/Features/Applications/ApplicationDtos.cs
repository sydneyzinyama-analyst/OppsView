using System.ComponentModel.DataAnnotations;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.Applications;

/// <summary>
/// Body for both POST (create) and PUT (edit). Status is not here on purpose:
/// it changes through PATCH /applications/{id}/status so every change is
/// validated and recorded.
///
/// Platform and AppliedOn are nullable only so [Required] can tell "missing"
/// apart from the default value (otherwise a missing platform would quietly
/// become Pnet, the first enum member).
///
/// These attributes are a first, cheap check at the HTTP edge; the domain
/// entity re-checks everything, so the rules hold even if a new endpoint
/// forgets to validate.
///
/// Request DTOs are classes with init properties rather than positional
/// records: validation attributes on record constructor parameters aren't
/// seen by the provider that reports errors under JSON (camelCase) names.
/// </summary>
public sealed class SaveApplicationRequest
{
    [Required, MaxLength(JobApplication.CompanyMaxLength)] public string Company { get; init; } = "";
    [Required, MaxLength(JobApplication.RoleMaxLength)] public string Role { get; init; } = "";
    [Required] public JobPlatform? Platform { get; init; }
    [Required] public DateOnly? AppliedOn { get; init; }
    [MaxLength(JobApplication.JobUrlMaxLength), Url] public string? JobUrl { get; init; }
    [MaxLength(JobApplication.NotesMaxLength)] public string? Notes { get; init; }
}

public sealed class ChangeStatusRequest
{
    [Required] public ApplicationStatus? Status { get; init; }
}

public sealed record ApplicationResponse(
    Guid Id,
    string Company,
    string Role,
    JobPlatform Platform,
    ApplicationStatus Status,
    DateOnly AppliedOn,
    string? JobUrl,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ApplicationStatus> AllowedNextStatuses)
{
    public static ApplicationResponse From(JobApplication a) => new(
        a.Id,
        a.Company,
        a.Role,
        a.Platform,
        a.Status,
        a.AppliedOn,
        a.JobUrl,
        a.Notes,
        Utc(a.CreatedAt),
        Utc(a.UpdatedAt),
        // The server owns the transition rules; sending the allowed next
        // steps means the UI dropdown never has to duplicate them.
        StatusTransitions.AllowedFrom(a.Status));

    // Postgres returns UTC DateTimes; marking Kind explicitly guarantees the
    // JSON always ends in "Z" whatever provider loaded the row.
    internal static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed record StatusChangeResponse(
    ApplicationStatus FromStatus,
    ApplicationStatus ToStatus,
    DateTime ChangedAt)
{
    public static StatusChangeResponse From(StatusChange c) =>
        new(c.FromStatus, c.ToStatus, ApplicationResponse.Utc(c.ChangedAt));
}

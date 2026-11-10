using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Infrastructure;

namespace OppsView.Api.Features.Applications;

public interface IApplicationService
{
    Task<IReadOnlyList<ApplicationResponse>> ListAsync(CancellationToken ct);
    Task<ApplicationResponse?> GetAsync(Guid id, CancellationToken ct);
    Task<ApplicationResponse> CreateAsync(SaveApplicationRequest request, CancellationToken ct);
    Task<ApplicationResponse?> UpdateAsync(Guid id, SaveApplicationRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<ApplicationResponse?> ChangeStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct);
    Task<IReadOnlyList<StatusChangeResponse>?> GetHistoryAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Application use cases. Every query starts from <see cref="Mine"/>, so a
/// user can never read or change someone else's applications. Another user's
/// application looks exactly like a missing one (null, so 404), which avoids
/// revealing which ids exist.
/// </summary>
public sealed class ApplicationService(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IApplicationService
{
    private IQueryable<JobApplication> Mine => db.Applications.Where(a => a.UserId == currentUser.Id);

    private DateTime NowUtc => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ApplicationResponse>> ListAsync(CancellationToken ct)
    {
        var applications = await Mine
            .AsNoTracking()
            .OrderByDescending(a => a.AppliedOn)
            .ThenBy(a => a.Company)
            .ToListAsync(ct);
        return applications.Select(ApplicationResponse.From).ToList();
    }

    public async Task<ApplicationResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var application = await Mine.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        return application is null ? null : ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse> CreateAsync(SaveApplicationRequest r, CancellationToken ct)
    {
        var application = JobApplication.Create(
            currentUser.Id, r.Company, r.Role, r.Platform!.Value, r.AppliedOn!.Value, r.JobUrl, r.Notes, NowUtc);
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse?> UpdateAsync(Guid id, SaveApplicationRequest r, CancellationToken ct)
    {
        var application = await Mine.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (application is null) return null;

        application.UpdateDetails(r.Company, r.Role, r.Platform!.Value, r.AppliedOn!.Value, r.JobUrl, r.Notes, NowUtc);
        await db.SaveChangesAsync(ct);
        return ApplicationResponse.From(application);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        // ExecuteDeleteAsync issues a single DELETE ... WHERE without loading
        // the row first. Status history goes with it (ON DELETE CASCADE).
        var deleted = await Mine.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public async Task<ApplicationResponse?> ChangeStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct)
    {
        var application = await Mine.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (application is null) return null;

        // The entity validates the transition and appends the history row;
        // EF saves both in one transaction.
        application.ChangeStatus(status, NowUtc);
        await db.SaveChangesAsync(ct);
        return ApplicationResponse.From(application);
    }

    public async Task<IReadOnlyList<StatusChangeResponse>?> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        if (!await Mine.AnyAsync(a => a.Id == id, ct)) return null;

        var history = await db.StatusHistory
            .AsNoTracking()
            .Where(h => h.ApplicationId == id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(ct);
        return history.Select(StatusChangeResponse.From).ToList();
    }
}

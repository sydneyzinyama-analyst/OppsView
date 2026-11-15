using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport;

/// <summary>
/// Marks applications Rejected when the employer's "no reply by X means
/// unsuccessful" date has passed with no news. Runs every hour in the
/// background (<see cref="NoReplyCheckService"/>) and at the start of each
/// sync for that user.
/// </summary>
public sealed class NoReplyChecker(AppDbContext db, TimeProvider time)
{
    /// <param name="userId">Only this user's applications, or everyone's when null.</param>
    /// <returns>How many applications were marked Rejected.</returns>
    public async Task<int> RunAsync(Guid? userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        var query = db.Applications.Where(a =>
            a.NoReplyDeadline != null && a.NoReplyDeadline < today
            && (a.Status == ApplicationStatus.Applied || a.Status == ApplicationStatus.Viewed));
        if (userId is { } id) query = query.Where(a => a.UserId == id);

        var overdue = await query.ToListAsync(ct);
        var rejected = overdue.Count(a => a.ApplyNoReplyDeadline(today));
        if (rejected > 0) await db.SaveChangesAsync(ct);
        return rejected;
    }
}

/// <summary>
/// Background job (a .NET BackgroundService, started with the API) that runs
/// <see cref="NoReplyChecker"/> for all users once an hour, so deadlines are
/// applied even if nobody clicks "Sync now".
/// </summary>
public sealed class NoReplyCheckService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IConfiguration config,
    ILogger<NoReplyCheckService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = config.GetValue("EmailImport:NoReplyCheckMinutes", 60);
        if (minutes <= 0) return; // switched off (the tests do this)

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes), time);
        do
        {
            try
            {
                // DbContext is scoped (one per request), and a background
                // service has no request, so it creates its own scope.
                using var scope = scopes.CreateScope();
                var rejected = await scope.ServiceProvider.GetRequiredService<NoReplyChecker>().RunAsync(null, stoppingToken);
                if (rejected > 0) logger.LogInformation("Marked {Count} applications Rejected: no reply by the deadline", rejected);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed check (e.g. database briefly down) mustn't stop the API.
                logger.LogWarning(ex, "No-reply deadline check failed; will retry next time");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

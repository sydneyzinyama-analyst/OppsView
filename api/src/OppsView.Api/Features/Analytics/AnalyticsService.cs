using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Features.Matching;
using OppsView.Api.Infrastructure;

namespace OppsView.Api.Features.Analytics;

/// <summary>
/// Works out the dashboard numbers for the signed-in user.
///
/// A personal tracker has hundreds of applications at most, so this loads
/// them (with their status history) once and counts in memory: simple to
/// read and test, and fast enough. With millions of rows you'd push these
/// counts into SQL instead.
/// </summary>
public sealed class AnalyticsService(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly ApplicationStatus[] Replies =
        [ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Rejected];

    public async Task<AnalyticsResponse> GetAsync(int weeks, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var apps = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == currentUser.Id)
            .Include(a => a.History)
            .ToListAsync(ct);

        return new AnalyticsResponse(
            Totals(apps),
            PerWeek(apps, DateOnly.FromDateTime(now), weeks),
            ByPlatform(apps),
            Funnel(apps),
            RejectedAt(apps),
            Waiting(apps, now),
            TopMissingSkills(apps),
            new SourceCounts(apps.Count(a => a.Source == ApplicationSource.Manual), apps.Count(a => a.Source == ApplicationSource.Email)));
    }

    /// <summary>A reply means the employer got back to you: interview, offer or rejection.</summary>
    private static bool GotReply(JobApplication a) => FirstReplyAt(a) is not null || Replies.Contains(a.Status);

    private static DateTime? FirstReplyAt(JobApplication a) =>
        a.History.Where(h => Replies.Contains(h.ToStatus)).OrderBy(h => h.ChangedAt).Select(h => (DateTime?)h.ChangedAt).FirstOrDefault();

    private static bool Reached(JobApplication a, ApplicationStatus stage) =>
        stage == ApplicationStatus.Applied || a.Status == stage || a.History.Any(h => h.ToStatus == stage)
        // Reaching a later stage means the earlier one was passed too (stages can be skipped).
        || stage == ApplicationStatus.Viewed && Reached(a, ApplicationStatus.Interview)
        || stage == ApplicationStatus.Interview && Reached(a, ApplicationStatus.Offer);

    private static Totals Totals(List<JobApplication> apps)
    {
        var replyDays = apps
            .Select(a => FirstReplyAt(a) is { } at ? (at - a.AppliedOn.ToDateTime(TimeOnly.MinValue)).TotalDays : (double?)null)
            .Where(d => d is >= 0)
            .Select(d => d!.Value)
            .ToList();

        return new Totals(
            apps.Count,
            apps.Count(a => a.Status is ApplicationStatus.Applied or ApplicationStatus.Viewed or ApplicationStatus.Interview),
            apps.Count(a => Reached(a, ApplicationStatus.Interview)),
            apps.Count(a => a.Status == ApplicationStatus.Offer),
            apps.Count(a => a.Status == ApplicationStatus.Rejected),
            apps.Count == 0 ? 0 : Math.Round((double)apps.Count(GotReply) / apps.Count, 3),
            replyDays.Count == 0 ? null : Math.Round(replyDays.Average(), 1));
    }

    /// <summary>The last <paramref name="weeks"/> weeks (Monday to Sunday), oldest first, including empty weeks.</summary>
    internal static IReadOnlyList<WeekCount> PerWeek(List<JobApplication> apps, DateOnly today, int weeks)
    {
        var thisMonday = Monday(today);
        var counts = apps.GroupBy(a => Monday(a.AppliedOn)).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, weeks)
            .Select(i => thisMonday.AddDays(-7 * (weeks - 1 - i)))
            .Select(monday => new WeekCount(monday, counts.GetValueOrDefault(monday)))
            .ToList();
    }

    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static IReadOnlyList<PlatformStats> ByPlatform(List<JobApplication> apps) => apps
        .GroupBy(a => a.Platform)
        .Select(g =>
        {
            var replies = g.Count(GotReply);
            return new PlatformStats(g.Key, g.Count(), replies, Math.Round((double)replies / g.Count(), 3));
        })
        .OrderByDescending(p => p.Applications)
        .ToList();

    private static IReadOnlyList<FunnelStage> Funnel(List<JobApplication> apps) =>
        new[] { ApplicationStatus.Applied, ApplicationStatus.Viewed, ApplicationStatus.Interview, ApplicationStatus.Offer }
            .Select(stage => new FunnelStage(stage, apps.Count(a => Reached(a, stage))))
            .ToList();

    private static IReadOnlyList<RejectionPoint> RejectedAt(List<JobApplication> apps) => apps
        .Where(a => a.Status == ApplicationStatus.Rejected)
        .Select(a => a.History.Where(h => h.ToStatus == ApplicationStatus.Rejected).OrderBy(h => h.ChangedAt)
            .Select(h => (ApplicationStatus?)h.FromStatus).FirstOrDefault() ?? ApplicationStatus.Applied)
        .GroupBy(from => from)
        .Select(g => new RejectionPoint(g.Key, g.Count()))
        .OrderBy(r => r.From)
        .ToList();

    private static IReadOnlyList<WaitingStage> Waiting(List<JobApplication> apps, DateTime now) =>
        new[] { ApplicationStatus.Applied, ApplicationStatus.Viewed, ApplicationStatus.Interview }
            .Select(stage =>
            {
                // "Applied" waits from the day you applied (it may be before the
                // application reached OppsView, e.g. imported from an old email);
                // later stages from when that status started.
                var days = apps.Where(a => a.Status == stage)
                    .Select(a => (now - (stage == ApplicationStatus.Applied
                        ? a.AppliedOn.ToDateTime(TimeOnly.MinValue)
                        : a.StatusChangedAt)).TotalDays)
                    .ToList();
                return new WaitingStage(stage, days.Count, days.Count == 0 ? 0 : Math.Round(days.Average(), 1), days.Count(d => d > 14));
            })
            .ToList();

    /// <summary>The skills most often missing across the user's saved CV matches.</summary>
    private static IReadOnlyList<SkillCount> TopMissingSkills(List<JobApplication> apps) => apps
        .Where(a => a.MatchResultJson is not null)
        .SelectMany(a =>
        {
            var result = JsonSerializer.Deserialize<MatcherResult>(a.MatchResultJson!, Json);
            return result is null ? [] : result.Required.Missing.Concat(result.NiceToHave.Missing);
        })
        .GroupBy(skill => skill)
        .Select(g => new SkillCount(g.Key, g.Count()))
        .OrderByDescending(s => s.Count).ThenBy(s => s.Skill)
        .Take(10)
        .ToList();
}

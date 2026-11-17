using OppsView.Api.Domain;

namespace OppsView.Api.Features.Analytics;

/// <summary>Everything the dashboard shows, worked out in one go for the signed-in user.</summary>
public sealed record AnalyticsResponse(
    Totals Totals,
    IReadOnlyList<WeekCount> PerWeek,
    IReadOnlyList<PlatformStats> ByPlatform,
    IReadOnlyList<FunnelStage> Funnel,
    IReadOnlyList<RejectionPoint> RejectedAt,
    IReadOnlyList<WaitingStage> Waiting,
    IReadOnlyList<SkillCount> TopMissingSkills,
    SourceCounts Sources);

/// <param name="ResponseRate">Share (0-1) of applications that got a reply: interview, offer or rejection.</param>
/// <param name="AverageDaysToResponse">From applying to the first reply, over applications that got one.</param>
public sealed record Totals(
    int Applications,
    int Active,
    int Interviews,
    int Offers,
    int Rejected,
    double ResponseRate,
    double? AverageDaysToResponse);

/// <param name="WeekStart">The Monday the week starts on.</param>
public sealed record WeekCount(DateOnly WeekStart, int Applications);

public sealed record PlatformStats(JobPlatform Platform, int Applications, int Responses, double ResponseRate);

/// <summary>How many applications reached each stage (at any point, even if they moved on).</summary>
public sealed record FunnelStage(ApplicationStatus Stage, int Reached);

/// <summary>Where rejections happened: straight after applying, after being viewed, or after an interview.</summary>
public sealed record RejectionPoint(ApplicationStatus From, int Count);

/// <summary>Applications still waiting in a stage, and for how long.</summary>
public sealed record WaitingStage(ApplicationStatus Stage, int Count, double AverageDays, int WaitingOver14Days);

public sealed record SkillCount(string Skill, int Count);

public sealed record SourceCounts(int Manual, int Email);

using System.ComponentModel.DataAnnotations;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.Matching;

public sealed record CvResponse(string FileName, DateTime UploadedAt, int Characters, string Preview)
{
    public static CvResponse From(Cv cv) => new(
        cv.FileName,
        DateTime.SpecifyKind(cv.UploadedAt, DateTimeKind.Utc),
        cv.Text.Length,
        // Enough to confirm it's the right CV and was read properly.
        cv.Text.Length <= 300 ? cv.Text : cv.Text[..300] + "…");
}

public sealed class MatchRequest
{
    [Required, MaxLength(JobApplication.JobAdMaxLength)] public string JobAd { get; init; } = "";

    /// <summary>Optional: save the result on this application.</summary>
    public Guid? ApplicationId { get; init; }
}

public sealed record MatchResponse(
    int? Score,
    SkillGroup Required,
    SkillGroup NiceToHave,
    IReadOnlyList<string> ExtraInCv,
    string Summary,
    Guid? ApplicationId,
    DateTime MatchedAt);

/// <summary>A match saved on an application, with the ad it was made against.</summary>
public sealed record SavedMatchResponse(string JobAd, MatchResponse Result);

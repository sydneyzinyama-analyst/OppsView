using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Infrastructure;

namespace OppsView.Api.Features.Matching;

/// <summary>The user's CV and matching it against job ads, via the Python matcher.</summary>
public sealed class MatchingService(AppDbContext db, IMatcherClient matcher, ICurrentUser currentUser, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CvResponse?> GetCvAsync(CancellationToken ct)
    {
        var cv = await db.Cvs.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == currentUser.Id, ct);
        return cv is null ? null : CvResponse.From(cv);
    }

    /// <exception cref="MatcherRejectedException">The matcher couldn't read the file (message is for the user).</exception>
    /// <exception cref="MatcherUnavailableException">The matcher isn't running.</exception>
    public async Task<CvResponse> UploadCvAsync(string fileName, Stream content, CancellationToken ct)
    {
        var text = await matcher.ExtractTextAsync(fileName, content, ct);
        var now = time.GetUtcNow().UtcDateTime;

        var cv = await db.Cvs.SingleOrDefaultAsync(c => c.UserId == currentUser.Id, ct);
        if (cv is null)
        {
            cv = Cv.Create(currentUser.Id, fileName, text, now);
            db.Cvs.Add(cv);
        }
        else
        {
            cv.Replace(fileName, text, now);
        }
        await db.SaveChangesAsync(ct);
        return CvResponse.From(cv);
    }

    public async Task DeleteCvAsync(CancellationToken ct) =>
        await db.Cvs.Where(c => c.UserId == currentUser.Id).ExecuteDeleteAsync(ct);

    /// <returns>null if the application doesn't exist (or isn't the user's).</returns>
    /// <exception cref="NoCvException">No CV uploaded yet.</exception>
    public async Task<MatchResponse?> MatchAsync(MatchRequest request, CancellationToken ct)
    {
        JobApplication? application = null;
        if (request.ApplicationId is { } id)
        {
            application = await db.Applications.SingleOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.Id, ct);
            if (application is null) return null;
        }

        var cv = await db.Cvs.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == currentUser.Id, ct)
            ?? throw new NoCvException();

        var result = await matcher.MatchAsync(cv.Text, request.JobAd, ct);
        var now = time.GetUtcNow().UtcDateTime;

        if (application is not null)
        {
            application.RecordMatch(request.JobAd, result.Score, JsonSerializer.Serialize(result, Json), now);
            await db.SaveChangesAsync(ct);
        }

        return ToResponse(result, application?.Id, now);
    }

    public async Task<SavedMatchResponse?> GetSavedMatchAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.UserId == currentUser.Id, ct);
        if (app?.MatchResultJson is null || app.JobAd is null || app.MatchedAt is null) return null;

        var result = JsonSerializer.Deserialize<MatcherResult>(app.MatchResultJson, Json)!;
        return new SavedMatchResponse(app.JobAd, ToResponse(result, app.Id, app.MatchedAt.Value));
    }

    private static MatchResponse ToResponse(MatcherResult r, Guid? applicationId, DateTime matchedAt) => new(
        r.Score, r.Required, r.NiceToHave, r.ExtraInCv, r.Summary, applicationId,
        DateTime.SpecifyKind(matchedAt, DateTimeKind.Utc));
}

public sealed class NoCvException() : Exception("Upload your CV first.");

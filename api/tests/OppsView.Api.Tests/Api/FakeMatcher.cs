using OppsView.Api.Features.Matching;

namespace OppsView.Api.Tests.Api;

/// <summary>Stands in for the Python matcher in integration tests.</summary>
public sealed class FakeMatcher : IMatcherClient
{
    public bool Down { get; set; }
    public string? RejectWith { get; set; }
    public List<(string CvText, string JobAd)> Matches { get; } = [];

    public async Task<string> ExtractTextAsync(string fileName, Stream content, CancellationToken ct)
    {
        Throw();
        using var reader = new StreamReader(content);
        return $"CV from {fileName}: " + await reader.ReadToEndAsync(ct);
    }

    public Task<MatcherResult> MatchAsync(string cvText, string jobAd, CancellationToken ct)
    {
        Throw();
        Matches.Add((cvText, jobAd));
        return Task.FromResult(new MatcherResult(
            75,
            new SkillGroup(["C#"], ["SQL Server"]),
            new SkillGroup([], ["Docker"]),
            ["Python"],
            "75% match: you have 1 of 2 required skills and 0 of 1 nice-to-have skills."));
    }

    private void Throw()
    {
        if (Down) throw new MatcherUnavailableException();
        if (RejectWith is not null) throw new MatcherRejectedException(RejectWith);
    }
}

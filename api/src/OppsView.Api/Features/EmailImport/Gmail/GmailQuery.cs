namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>
/// The Gmail search used to find job emails, written in Gmail's own search
/// syntax (the same as the search box in Gmail). Deliberately broad: the
/// parser decides what's relevant, so it's better to fetch a few extra
/// emails than to miss a rejection.
/// </summary>
public static class GmailQuery
{
    private const string JobWords =
        "{application applying applied interview unsuccessful vacancy candidacy \"your interest\" \"thank you for applying\" " +
        "from:pnet.co.za from:indeed.com from:workablemail.com from:linkedin.com}";

    public static string Build(DateTime sinceUtc) =>
        // after: takes seconds since 1970. -from:me skips emails you sent.
        // Social and Forums (LinkedIn group posts and the like) never hold
        // application emails. Promotions stays in: some job sites'
        // confirmations land there, and the parser ignores the adverts.
        $"after:{new DateTimeOffset(DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()} " +
        $"-from:me -in:chats -category:social -category:forums {JobWords}";
}

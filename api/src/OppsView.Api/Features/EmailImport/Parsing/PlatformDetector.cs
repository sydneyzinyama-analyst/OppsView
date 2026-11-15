using System.Text.RegularExpressions;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>Works out where the application was made, from the sender and tell-tale phrases.</summary>
public static partial class PlatformDetector
{
    public static JobPlatform? Detect(string senderAddress, string text)
    {
        var domain = SenderDomain.DomainOf(senderAddress) ?? "";

        if (IsDomain(domain, "pnet.co.za") || PnetPhrase().IsMatch(text)) return JobPlatform.Pnet;
        if (IsDomain(domain, "indeed.com") || IndeedPhrase().IsMatch(text)) return JobPlatform.Indeed;
        if (IsDomain(domain, "workable.com") || IsDomain(domain, "workablemail.com") || WorkablePhrase().IsMatch(text)) return JobPlatform.Workable;
        if (IsDomain(domain, "linkedin.com") || LinkedInPhrase().IsMatch(text)) return JobPlatform.LinkedIn;

        // Sent by the company itself or its hiring system (Workday,
        // SuccessFactors, Greenhouse...): applied through their own site.
        return SenderDomain.Classify(senderAddress) switch
        {
            SenderDomain.Kind.Company or SenderDomain.Kind.HiringSystem or SenderDomain.Kind.MailService => JobPlatform.CompanyPortal,
            SenderDomain.Kind.JobBoard => JobPlatform.Other,
            _ => null,
        };
    }

    private static bool IsDomain(string domain, string expected) =>
        domain == expected || domain.EndsWith("." + expected, StringComparison.Ordinal);

    // Mentions in footers ("Follow us on LinkedIn") must not count, so these
    // look for phrases only the platform itself would write.
    [GeneratedRegex(@"\bMy Pnet\b|\bvia Pnet\b|\bon Pnet\b|\bPnet\.co\.za\b", RegexOptions.IgnoreCase)]
    private static partial Regex PnetPhrase();

    [GeneratedRegex(@"\b(via|on|through) Indeed\b|\bIndeed Apply\b|\bsent from Indeed\b", RegexOptions.IgnoreCase)]
    private static partial Regex IndeedPhrase();

    [GeneratedRegex(@"\bpowered by Workable\b|\bvia Workable\b", RegexOptions.IgnoreCase)]
    private static partial Regex WorkablePhrase();

    [GeneratedRegex(@"\bEasy Apply\b|\b(applied|application sent) (on|via|through) LinkedIn\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinkedInPhrase();
}

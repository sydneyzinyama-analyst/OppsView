using System.Text.RegularExpressions;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Finds the company and job title in an email, trying the most reliable
/// patterns first ("…the Java Engineer role at BBD") and falling back to
/// weaker clues (the sign-off, the sender's name). Returns null for anything
/// it can't find with reasonable confidence; the importer then asks the user.
/// </summary>
public static partial class DetailsExtractor
{
    public static (string? Company, string? Role) Extract(string subject, IReadOnlyList<string> sentences, string? senderName)
    {
        string? company = null, role = null;

        // 1. Patterns that give both at once, subject first (shortest, most regular).
        foreach (var line in sentences.Prepend(subject))
        {
            foreach (var pattern in BothPatterns)
            {
                var m = pattern.Match(line);
                if (!m.Success) continue;
                role ??= CleanRole(m.Groups["role"].Value);
                company ??= CleanCompany(m.Groups["company"].Value);
                if (role is not null && company is not null) return (company, role);
            }
        }

        // 2. Role on its own ("Your application for X has been sent").
        role ??= FirstMatch(sentences.Prepend(subject), RoleOnly(), "role", CleanRole);

        // 3. Company on its own: phrases, then the sign-off, then the sender's name.
        company ??= FirstMatch(sentences.Prepend(subject), CompanyOnly(), "company", CleanCompany)
            ?? FromSignOff(sentences)
            ?? CleanCompany(senderName);

        return (company, role);
    }

    private static readonly Regex[] BothPatterns =
    [
        // "applying for the Developer (Junior) career opportunity at Discovery"
        // "apply for the Java Engineer (SA26) role at BBD"
        new(@"\b(?:apply|applying|applied|application|applications?)\s+(?:for|to)\s+(?:the\s+)?(?:position\s+of\s+)?(?<role>.{2,100}?)\s+(?:career\s+opportunity|opportunity|role|position|vacancy|job|post)\s+(?:at|with)\s+(?<company>[^.,!?;\n]{2,80})",
            RegexOptions.IgnoreCase),
        // "interest in working for Cartrack by applying to the IT Support Technician position"
        new(@"\b(?:working\s+(?:for|at|with)|joining|work\s+(?:for|at))\s+(?<company>.{2,80}?)\s+by\s+applying\s+(?:for|to)\s+(?:the\s+)?(?<role>.{2,100}?)\s+(?:role|position|vacancy|job|opportunity)\b",
            RegexOptions.IgnoreCase),
        // "Your application for Junior Developer at Acme"
        new(@"\b(?:application|applied)\s+(?:for|to)\s+(?:the\s+)?(?<role>.{2,100}?)\s+at\s+(?<company>[^.,!?;\n]{2,80})",
            RegexOptions.IgnoreCase),
    ];

    // "Your application for Junior AI & Full-Stack Developer has been successfully sent"
    [GeneratedRegex(@"\byour\s+application\s+(?:for|to)\s+(?:the\s+)?(?<role>.{2,100}?)\s+(?:has\s+been|was|is)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RoleOnly();

    // "Thank you for choosing FutureSoft", "your interest in joining Acme"
    // The phrase is case-insensitive ((?i:...)); the name must start with a capital.
    [GeneratedRegex(@"\b(?i:thank\s+you\s+for\s+choosing|thanks\s+for\s+choosing|interest\s+in\s+(?:working\s+(?:for|at|with)\s+|joining\s+)?|careers?\s+at|welcome\s+to)\s+(?<company>[A-Z][^.,!?;\n]{1,80})")]
    private static partial Regex CompanyOnly();

    private static string? FirstMatch(IEnumerable<string> lines, Regex pattern, string group, Func<string?, string?> clean)
    {
        foreach (var line in lines)
        {
            var m = pattern.Match(line);
            if (m.Success && clean(m.Groups[group].Value) is { } value) return value;
        }
        return null;
    }

    /// <summary>The line after "Kind regards," / "Sincerely," is usually the company or its hiring team.</summary>
    private static string? FromSignOff(IReadOnlyList<string> sentences)
    {
        for (var i = sentences.Count - 2; i >= 0; i--)
        {
            if (SignOff().IsMatch(sentences[i]) && CleanCompany(sentences[i + 1]) is { } company)
                return company;
        }
        return null;
    }

    [GeneratedRegex(@"^(kind|warm|best|warmest)?\s*regards,?$|^sincerely,?$|^best wishes,?$|^yours (sincerely|faithfully),?$|^thank you,?$|^cheers,?$", RegexOptions.IgnoreCase)]
    private static partial Regex SignOff();

    internal static string? CleanRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var role = raw.Trim().Trim('"', '\'', '*');
        role = TrailingReferenceCode().Replace(role, "");   // "… - cartr_000922", "… (SA26)"
        role = role.Trim(' ', '-', ':');
        var words = role.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 12 || role.Length > 120) return null;
        return string.Join(' ', words);
    }

    internal static string? CleanCompany(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var company = raw.Trim().Trim('"', '\'', '*', '!', '.');
        company = TeamWords().Replace(company, "").Trim(' ', '-', ',');
        var words = company.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 6 || company.Length > 80) return null;
        if (NotACompany().IsMatch(company)) return null;
        return string.Join(' ', words);
    }

    // Reference codes have digits: "(SA26)", "- cartr_000922", "#12345". "(Junior)" is kept.
    [GeneratedRegex(@"(\s*[-–]\s*[A-Za-z]*_?\d[\w-]*|\s*\((?=[^)]*\d)[\w\s/-]+\)|\s*#\s*\d+)+\s*$")]
    private static partial Regex TrailingReferenceCode();

    // "BBD Global Talent Acquisition Team" -> "BBD"
    [GeneratedRegex(@"\s*(\b(global|the)\s+)?\b(talent\s+acquisition|recruitment|recruiting|hiring|hr|human\s+resources|people|careers?|jobs?|admin)(\s+(team|department|dept))?\s*$|\s+team\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TeamWords();

    // Things that turn up where a company name would be, but aren't one.
    [GeneratedRegex(@"^(us|our team|the team|you|your|the company|team|no-?reply|noreply|do not reply|notifications?|info|support|mailer-daemon|[\w.+-]+@[\w.-]+)$|\b(pnet|indeed|workable|linkedin)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotACompany();
}

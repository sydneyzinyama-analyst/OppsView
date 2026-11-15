using System.Globalization;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Understands the domain an email came from: careers@discovery.co.za is
/// Discovery itself, noreply@pnet.co.za is a job site, and
/// notifications@myworkday.com is a hiring system working for some company.
/// Only the first kind tells us the company's name.
/// </summary>
public static class SenderDomain
{
    public enum Kind { None, Company, JobBoard, HiringSystem, MailService, PersonalMail }

    // Job sites: they send confirmations *and* lots of marketing (job alerts,
    // "recommended for you"), so their emails get stricter rules.
    private static readonly string[] JobBoards =
    [
        "pnet.co.za", "indeed.com", "linkedin.com", "workable.com", "workablemail.com", "careerjunction.co.za",
        "careers24.com", "jobmail.co.za", "glassdoor.com", "offerzen.com", "jobvine.co.za", "executiveplacements.com",
        "gumtree.co.za", "joburgjobs.co.za", "ziprecruiter.com", "monster.com",
    ];

    // Applicant-tracking systems that email on behalf of companies.
    private static readonly string[] HiringSystems =
    [
        "myworkday.com", "myworkdayjobs.com", "workday.com", "successfactors.com", "successfactors.eu", "sapsf.com",
        "sapsf.eu", "greenhouse.io", "greenhouse-mail.io", "lever.co", "smartrecruiters.com", "bamboohr.com",
        "teamtailor.com", "teamtailor-mail.com", "jobvite.com", "icims.com", "taleo.net", "oraclecloud.com",
        "recruitee.com", "breezy.hr", "personio.de", "personio.com", "pinpointhq.com", "zohorecruit.com",
        "applytojob.com", "ashbyhq.com", "freshteam.com", "jazzhr.com", "hirehive.com", "manatal.com",
        "simplicity.co.za", "jobjack.co.za", "hirequest.co.za",
    ];

    // Bulk-email services companies send through.
    private static readonly string[] MailServices =
    [
        "sendgrid.net", "amazonses.com", "mailgun.org", "mcsv.net", "mailchimpapp.net", "sparkpostmail.com",
        "hubspotemail.net", "mandrillapp.com", "postmarkapp.com", "sendinblue.com", "brevo.com", "mailjet.com",
    ];

    private static readonly string[] PersonalMailProviders =
    [
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "yahoo.com", "icloud.com",
        "me.com", "aol.com", "proton.me", "protonmail.com", "webmail.co.za", "mweb.co.za", "telkomsa.net",
        "vodamail.co.za", "ymail.com",
    ];

    // Second-level labels that are part of the "suffix", not the company:
    // discovery.co.za -> "discovery", not "co".
    private static readonly HashSet<string> SecondLevel =
        ["co", "com", "org", "net", "ac", "gov", "edu", "ltd", "plc", "nom", "web"];

    // Subdomains that never name a company (mail.acme.com, wd3.myworkdayjobs.com).
    private static readonly HashSet<string> GenericLabels =
    [
        "mail", "mailer", "email", "e", "em", "news", "info", "noreply", "no-reply", "notifications", "notification", "alerts",
        "careers", "career", "jobs", "job", "hr", "talent", "recruit", "recruiting", "recruitment", "apply", "hire",
        "www", "app", "apps", "us", "eu", "uk", "za", "emea", "mx", "smtp", "bounce", "reply", "candidates", "candidate",
        "wd1", "wd2", "wd3", "wd5", "wd12", "wd103", "wd105",
    ];

    public static string? DomainOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var at = address.LastIndexOf('@');
        var domain = (at >= 0 ? address[(at + 1)..] : address).Trim().TrimEnd('>', '.').ToLowerInvariant();
        return domain.Contains('.') ? domain : null;
    }

    public static Kind Classify(string? address)
    {
        var domain = DomainOf(address);
        if (domain is null) return Kind.None;
        if (Matches(domain, PersonalMailProviders)) return Kind.PersonalMail;
        if (Matches(domain, JobBoards)) return Kind.JobBoard;
        if (Matches(domain, HiringSystems)) return Kind.HiringSystem;
        if (Matches(domain, MailServices)) return Kind.MailService;
        return Kind.Company;
    }

    public static bool IsJobBoard(string? address) => Classify(address) == Kind.JobBoard;

    // Proper names for job sites and hiring systems, where the domain alone
    // would give the wrong capitalisation ("Linkedin", "Successfactors").
    private static readonly Dictionary<string, string> IntermediaryNames = new()
    {
        ["pnet.co.za"] = "Pnet", ["indeed.com"] = "Indeed", ["linkedin.com"] = "LinkedIn", ["workable.com"] = "Workable",
        ["workablemail.com"] = "Workable", ["careerjunction.co.za"] = "CareerJunction", ["careers24.com"] = "Careers24",
        ["jobmail.co.za"] = "Jobmail", ["glassdoor.com"] = "Glassdoor", ["offerzen.com"] = "OfferZen", ["jobvine.co.za"] = "JobVine",
        ["executiveplacements.com"] = "Executive Placements", ["ziprecruiter.com"] = "ZipRecruiter",
        ["myworkday.com"] = "Workday", ["myworkdayjobs.com"] = "Workday", ["workday.com"] = "Workday",
        ["successfactors.com"] = "SuccessFactors", ["successfactors.eu"] = "SuccessFactors", ["sapsf.com"] = "SuccessFactors",
        ["sapsf.eu"] = "SuccessFactors", ["greenhouse.io"] = "Greenhouse", ["greenhouse-mail.io"] = "Greenhouse", ["lever.co"] = "Lever",
        ["smartrecruiters.com"] = "SmartRecruiters", ["bamboohr.com"] = "BambooHR", ["teamtailor.com"] = "Teamtailor",
        ["teamtailor-mail.com"] = "Teamtailor", ["icims.com"] = "iCIMS", ["oraclecloud.com"] = "Oracle Recruiting",
        ["zohorecruit.com"] = "Zoho Recruit", ["jobjack.co.za"] = "JobJack", ["ashbyhq.com"] = "Ashby", ["manatal.com"] = "Manatal",
    };

    // Every job site / hiring system name, for recognising a provisional company.
    private static readonly HashSet<string> AllIntermediaryNames = JobBoards.Concat(HiringSystems)
        .Select(d => IntermediaryNames.TryGetValue(d, out var name) ? name : Prettify(d.Split('.')[0]))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Last resort for the company: the name of whoever sent the email, even
    /// if that's a job site or hiring system (info@info.pnet.co.za -> "Pnet").
    /// Null only for personal and bulk-mail addresses, which say nothing.
    /// </summary>
    public static string? SenderOrganisation(string? address)
    {
        var domain = DomainOf(address);
        var kind = Classify(address);
        if (domain is null || kind is Kind.None or Kind.PersonalMail or Kind.MailService) return null;
        if (CompanyFrom(address) is { } company) return company;

        var known = IntermediaryNames.FirstOrDefault(n => domain == n.Key || domain.EndsWith("." + n.Key, StringComparison.Ordinal));
        if (known.Value is not null) return known.Value;

        var labels = domain.Split('.');
        var suffixLength = labels.Length >= 3 && SecondLevel.Contains(labels[^2]) ? 2 : 1;
        var label = labels.Length > suffixLength ? labels[labels.Length - suffixLength - 1] : null;
        return label is null || GenericLabels.Contains(label) ? null : Prettify(label);
    }

    /// <summary>"Pnet", "LinkedIn", "Workday"...: a go-between, not the employer.</summary>
    public static bool IsIntermediaryName(string? name) => name is not null && AllIntermediaryNames.Contains(name.Trim());

    /// <summary>
    /// The company name implied by the sender's address, or null when the
    /// address belongs to a job site, mail provider or similar.
    /// careers@discovery.co.za -> "Discovery", jobs@cape-union-mart.co.za -> "Cape Union Mart",
    /// noreply@acme.wd3.myworkdayjobs.com -> "Acme".
    /// </summary>
    public static string? CompanyFrom(string? address)
    {
        var domain = DomainOf(address);
        if (domain is null) return null;

        var labels = domain.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        string? label;
        switch (Classify(address))
        {
            case Kind.Company:
                // Drop the suffix (".co.za", ".com") and keep the label before it.
                var suffixLength = labels.Count >= 3 && SecondLevel.Contains(labels[^2]) ? 2 : 1;
                label = labels.Count > suffixLength ? labels[labels.Count - suffixLength - 1] : null;
                break;
            case Kind.HiringSystem:
                // Hiring systems sometimes put the company first: acme.wd3.myworkdayjobs.com.
                label = labels.Count >= 3 ? labels[0] : null;
                break;
            default:
                return null;
        }

        if (label is null || GenericLabels.Contains(label) || label.Any(char.IsDigit) && label.Length < 4) return null;
        return Prettify(label);
    }

    private static bool Matches(string domain, string[] known) =>
        known.Any(k => domain == k || domain.EndsWith("." + k, StringComparison.Ordinal));

    /// <summary>"discovery" -> "Discovery", "bbd" -> "BBD", "cape-union-mart" -> "Cape Union Mart".</summary>
    private static string Prettify(string label)
    {
        var words = label.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length <= 3 ? w.ToUpperInvariant() : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w));
        return string.Join(' ', words);
    }
}

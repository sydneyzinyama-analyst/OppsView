using OppsView.Api.Features.EmailImport.Parsing;

namespace OppsView.Api.Tests.EmailImport;

public class RuleBasedEmailParserTests
{
    private readonly RuleBasedEmailParser _parser = new();

    private static IncomingEmail Sample(string file)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "EmailImport", "Samples", file));
        var from = lines[0]["From:".Length..].Trim();
        var subject = lines[1]["Subject:".Length..].Trim();
        var body = string.Join('\n', lines.Skip(3));
        return new IncomingEmail(file, from, subject, body, DateTime.UtcNow);
    }

    private static IncomingEmail Email(string body, string subject = "", string from = "jobs@acme.co.za") =>
        new("id", from, subject, body, DateTime.UtcNow);

    // ---- Real emails -----------------------------------------------------

    public static TheoryData<string, EmailKind, string?, string?, JobPlatform?> RealEmails => new()
    {
        { "futuresoft-received.txt", EmailKind.ApplicationReceived, "FutureSoft", null, null },
        { "discovery-received.txt", EmailKind.ApplicationReceived, "Discovery", "Developer (Junior)", null },
        { "pnet-received.txt", EmailKind.ApplicationReceived, null, "Junior AI & Full-Stack Developer", JobPlatform.Pnet },
        { "cartrack-rejected.txt", EmailKind.Rejection, "Cartrack", "IT Support Technician - Tier 1", null },
        { "bbd-rejected.txt", EmailKind.Rejection, "BBD", "Java Engineer", null },
    };

    [Theory]
    [MemberData(nameof(RealEmails))]
    public void Reads_real_emails(string file, EmailKind kind, string? company, string? role, JobPlatform? platform)
    {
        var parsed = _parser.Parse(Sample(file));

        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(company, parsed.Company);
        Assert.Equal(role, parsed.Role);
        Assert.Equal(platform, parsed.Platform);
    }

    [Fact]
    public void A_confirmation_that_mentions_a_possible_rejection_is_not_a_rejection()
    {
        // Discovery: "If you do not meet all the requirements, we will let you
        // know that your application was unsuccessful this time around."
        Assert.Equal(EmailKind.ApplicationReceived, _parser.Parse(Sample("discovery-received.txt")).Kind);
    }

    // ---- Kinds of email (phrasing commonly used by employers) ------------

    [Theory]
    [InlineData("We regret to inform you that we will not be proceeding with your application.")]
    [InlineData("After careful consideration we have decided to move forward with other candidates whose experience more closely matches.")]
    [InlineData("Unfortunately your application has not been successful on this occasion.")]
    [InlineData("Please note that the position has been filled.")]
    public void Recognises_rejections(string body) =>
        Assert.Equal(EmailKind.Rejection, _parser.Parse(Email(body)).Kind);

    [Theory]
    [InlineData("We would like to invite you to an interview for the Developer role at Acme.")]
    [InlineData("Please use the link below to schedule an interview with our team.")]
    [InlineData("Congratulations, you have been shortlisted for the Developer position.")]
    public void Recognises_interview_invitations(string body) =>
        Assert.Equal(EmailKind.InterviewInvite, _parser.Parse(Email(body)).Kind);

    [Fact]
    public void Recognises_offers() =>
        Assert.Equal(EmailKind.Offer, _parser.Parse(Email("We are pleased to offer you the position of Developer at Acme.")).Kind);

    [Fact]
    public void Recognises_viewed_notifications() =>
        Assert.Equal(EmailKind.ApplicationViewed, _parser.Parse(Email("Good news: the recruiter viewed your application.")).Kind);

    [Theory]
    [InlineData("Thank you for your application. Only shortlisted candidates will be contacted.")]
    [InlineData("Thank you for applying. If you are shortlisted we will invite you to an interview.")]
    public void Common_confirmation_disclaimers_are_not_interviews(string body) =>
        Assert.Equal(EmailKind.ApplicationReceived, _parser.Parse(Email(body)).Kind);

    [Fact]
    public void Job_alerts_are_not_relevant() =>
        Assert.Equal(EmailKind.NotRelevant, _parser.Parse(Email("12 new jobs matching Junior Developer in Johannesburg", subject: "Your job alert")).Kind);

    [Fact]
    public void Emails_with_nothing_job_related_are_not_relevant() =>
        Assert.Equal(EmailKind.NotRelevant, _parser.Parse(Email("Your order has shipped and will arrive on Friday.")).Kind);

    [Fact]
    public void Job_related_emails_that_fit_no_rule_are_unknown() =>
        Assert.Equal(EmailKind.Unknown, _parser.Parse(Email("Please update your candidate profile with your latest CV.")).Kind);

    // ---- Details ---------------------------------------------------------

    [Fact]
    public void Reads_company_and_role_from_the_subject()
    {
        var parsed = _parser.Parse(Email("Thank you for applying.", subject: "Your application for Data Analyst at Absa"));
        Assert.Equal(("Absa", "Data Analyst"), (parsed.Company, parsed.Role));
    }

    [Fact]
    public void Falls_back_to_the_senders_name_for_the_company()
    {
        var parsed = _parser.Parse(Email("Thank you for your application.", from: "Capitec Recruitment <noreply@capitecbank.co.za>"));
        Assert.Equal("Capitec", parsed.Company);
    }

    [Fact]
    public void Generic_sender_names_are_not_taken_for_companies()
    {
        var parsed = _parser.Parse(Email("Thank you for your application.", from: "noreply <noreply@mailer.example>"));
        Assert.Null(parsed.Company);
    }

    [Theory]
    [InlineData("noreply@pnet.co.za", JobPlatform.Pnet)]
    [InlineData("alerts@mail.indeed.com", JobPlatform.Indeed)]
    [InlineData("noreply@candidates.workablemail.com", JobPlatform.Workable)]
    [InlineData("jobs-noreply@linkedin.com", JobPlatform.LinkedIn)]
    [InlineData("careers@discovery.co.za", JobPlatform.CompanyPortal)]
    public void Detects_the_platform_from_the_sender(string from, JobPlatform expected) =>
        Assert.Equal(expected, _parser.Parse(Email("Thank you for applying for the Developer role at Acme.", from: from)).Platform);

    [Fact]
    public void A_footer_mention_of_LinkedIn_does_not_make_it_a_LinkedIn_application() =>
        Assert.NotEqual(JobPlatform.LinkedIn,
            _parser.Parse(Email("Thank you for applying for the Developer role at Acme.\nFollow us on LinkedIn")).Platform);

    [Fact]
    public void Reads_html_emails()
    {
        var html = "<html><body><p>Thank you for applying for the <b>QA Tester</b> role at <a href='https://x.example'>Yoco</a>.</p>"
                   + "<style>p{color:red}</style></body></html>";
        var parsed = _parser.Parse(Email(html));
        Assert.Equal((EmailKind.ApplicationReceived, "Yoco", "QA Tester"), (parsed.Kind, parsed.Company, parsed.Role));
    }

    // ---- Company from the sender's address --------------------------------

    [Fact]
    public void Takes_the_company_from_the_address_when_the_email_does_not_name_it()
    {
        var parsed = _parser.Parse(Email("Thank you for applying for the Junior Developer position. We will be in touch.",
            from: "noreply@entelect.co.za"));
        Assert.Equal(("Entelect", "Junior Developer"), (parsed.Company, parsed.Role));
    }

    [Fact]
    public void A_company_named_in_the_text_wins_over_the_address()
    {
        var parsed = _parser.Parse(Email("Thank you for applying for the Analyst role at Old Mutual.", from: "noreply@oldmutual.co.za"));
        Assert.Equal("Old Mutual", parsed.Company);
    }

    [Fact]
    public void The_senders_display_name_wins_over_the_address()
    {
        var parsed = _parser.Parse(Email("Thank you for your application.", from: "Standard Bank Careers <noreply@standardbank.co.za>"));
        Assert.Equal("Standard Bank", parsed.Company);
    }

    // ---- Job-site marketing is not an application --------------------------
    // Phrasing typical of job-site emails; replace with real samples when available.

    [Theory]
    [InlineData("Recommended jobs for you", "Based on your profile, these jobs might interest you. Junior Developer at Acme. Apply now!")]
    [InlineData("New jobs matching your search", "3 new jobs matching Junior Developer in Johannesburg.")]
    [InlineData("Is this your next job?", "Candidates like you also applied for: Software Developer at Globex. Apply now.")]
    [InlineData("A recruiter viewed your profile", "Good news! A recruiter viewed your profile on Pnet.")]
    [InlineData("Don't miss out", "Companies are actively hiring for roles like yours.")]
    public void Job_site_recommendations_and_notifications_are_not_relevant(string subject, string body) =>
        Assert.Equal(EmailKind.NotRelevant, _parser.Parse(Email(body, subject, from: "noreply@pnet.co.za")).Kind);

    [Fact]
    public void A_job_site_confirmation_that_also_lists_recommended_jobs_is_still_a_confirmation()
    {
        var body = "Your application for Junior Developer has been successfully sent. "
                   + "Jobs you might also like: Tester at Acme. Apply now.";
        Assert.Equal(EmailKind.ApplicationReceived, _parser.Parse(Email(body, from: "noreply@pnet.co.za")).Kind);
    }

    [Fact]
    public void Vague_job_site_emails_are_ignored_rather_than_sent_for_review() =>
        Assert.Equal(EmailKind.NotRelevant,
            _parser.Parse(Email("Your candidate profile is 80% complete.", from: "noreply@pnet.co.za")).Kind);

    [Fact]
    public void Softer_wording_from_an_employer_is_still_a_confirmation() =>
        Assert.Equal(EmailKind.ApplicationReceived,
            _parser.Parse(Email("Thank you for your interest in the Developer position at Acme.", from: "hr@acme.co.za")).Kind);

    [Fact]
    public void Softer_wording_from_a_job_site_is_not() =>
        Assert.Equal(EmailKind.NotRelevant,
            _parser.Parse(Email("Thank you for your interest in Pnet Premium.", from: "noreply@pnet.co.za")).Kind);
}

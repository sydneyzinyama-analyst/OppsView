namespace OppsView.Api.Tests.Domain;

public class JobApplicationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static JobApplication NewApplication(
        string company = "Acme",
        string role = "Junior .NET Developer",
        DateOnly? appliedOn = null,
        string? jobUrl = null) =>
        JobApplication.Create(UserId, company, role, JobPlatform.Pnet, appliedOn ?? Today, jobUrl, null, Now);

    [Fact]
    public void Create_starts_as_Applied_with_no_history()
    {
        var application = NewApplication();

        Assert.Equal(ApplicationStatus.Applied, application.Status);
        Assert.Empty(application.History);
        Assert.Equal(UserId, application.UserId);
        Assert.Equal(Now, application.CreatedAt);
        Assert.Equal(Now, application.UpdatedAt);
    }

    [Fact]
    public void Create_trims_text_and_turns_blank_optional_fields_into_null()
    {
        var application = JobApplication.Create(UserId, "  Acme  ", " Dev ", JobPlatform.Indeed, Today, "   ", "  ", Now);

        Assert.Equal("Acme", application.Company);
        Assert.Equal("Dev", application.Role);
        Assert.Null(application.JobUrl);
        Assert.Null(application.Notes);
    }

    [Theory]
    [InlineData("", "Dev", "Company")]
    [InlineData("   ", "Dev", "Company")]
    [InlineData("Acme", "", "Role")]
    public void Create_requires_company_and_role(string company, string role, string field)
    {
        var ex = Assert.Throws<DomainException>(() => NewApplication(company, role));
        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public void Create_rejects_a_company_name_that_is_too_long()
    {
        var ex = Assert.Throws<DomainException>(() => NewApplication(new string('a', JobApplication.CompanyMaxLength + 1)));
        Assert.Equal("Company", ex.Field);
    }

    [Fact]
    public void Create_allows_tomorrow_in_UTC_because_the_user_may_be_ahead_of_UTC()
    {
        var application = NewApplication(appliedOn: Today.AddDays(1));
        Assert.Equal(Today.AddDays(1), application.AppliedOn);
    }

    [Fact]
    public void Create_rejects_dates_further_in_the_future()
    {
        var ex = Assert.Throws<DomainException>(() => NewApplication(appliedOn: Today.AddDays(2)));
        Assert.Equal("AppliedOn", ex.Field);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/job")]
    [InlineData("javascript:alert(1)")]
    public void Create_rejects_job_urls_that_are_not_http(string url)
    {
        var ex = Assert.Throws<DomainException>(() => NewApplication(jobUrl: url));
        Assert.Equal("JobUrl", ex.Field);
    }

    [Fact]
    public void UpdateDetails_changes_fields_and_UpdatedAt_but_not_status()
    {
        var application = NewApplication();
        var later = Now.AddHours(2);

        application.UpdateDetails("Globex", "Backend Dev", JobPlatform.LinkedIn, Today.AddDays(-1),
            "https://example.com/jobs/1", "Referred by Thandi", later);

        Assert.Equal("Globex", application.Company);
        Assert.Equal("Backend Dev", application.Role);
        Assert.Equal(JobPlatform.LinkedIn, application.Platform);
        Assert.Equal(Today.AddDays(-1), application.AppliedOn);
        Assert.Equal("https://example.com/jobs/1", application.JobUrl);
        Assert.Equal("Referred by Thandi", application.Notes);
        Assert.Equal(later, application.UpdatedAt);
        Assert.Equal(Now, application.CreatedAt);
        Assert.Equal(ApplicationStatus.Applied, application.Status);
    }

    [Fact]
    public void UpdateDetails_enforces_the_same_rules_as_Create()
    {
        var application = NewApplication();
        var ex = Assert.Throws<DomainException>(() =>
            application.UpdateDetails("", "Dev", JobPlatform.Pnet, Today, null, null, Now));
        Assert.Equal("Company", ex.Field);
        Assert.Equal("Acme", application.Company); // unchanged after a failed edit
    }

    [Fact]
    public void ChangeStatus_records_history_and_updates_status_and_UpdatedAt()
    {
        var application = NewApplication();
        var later = Now.AddDays(3);

        var change = application.ChangeStatus(ApplicationStatus.Interview, later);

        Assert.Equal(ApplicationStatus.Interview, application.Status);
        Assert.Equal(later, application.UpdatedAt);
        var recorded = Assert.Single(application.History);
        Assert.Same(change, recorded);
        Assert.Equal(application.Id, recorded.ApplicationId);
        Assert.Equal(ApplicationStatus.Applied, recorded.FromStatus);
        Assert.Equal(ApplicationStatus.Interview, recorded.ToStatus);
        Assert.Equal(later, recorded.ChangedAt);
    }

    [Fact]
    public void ChangeStatus_keeps_every_step_in_order()
    {
        var application = NewApplication();
        application.ChangeStatus(ApplicationStatus.Viewed, Now.AddDays(1));
        application.ChangeStatus(ApplicationStatus.Interview, Now.AddDays(2));
        application.ChangeStatus(ApplicationStatus.Offer, Now.AddDays(3));

        Assert.Equal(
            [ApplicationStatus.Viewed, ApplicationStatus.Interview, ApplicationStatus.Offer],
            application.History.Select(h => h.ToStatus));
    }

    [Fact]
    public void ChangeStatus_to_the_current_status_is_rejected()
    {
        var application = NewApplication();
        var ex = Assert.Throws<DomainException>(() => application.ChangeStatus(ApplicationStatus.Applied, Now));
        Assert.Equal("Status", ex.Field);
        Assert.Empty(application.History);
    }

    [Fact]
    public void A_rejected_transition_leaves_the_application_unchanged()
    {
        var application = NewApplication();
        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1));

        Assert.Throws<DomainException>(() => application.ChangeStatus(ApplicationStatus.Interview, Now.AddDays(2)));

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Single(application.History);
        Assert.Equal(Now.AddDays(1), application.UpdatedAt);
    }
}

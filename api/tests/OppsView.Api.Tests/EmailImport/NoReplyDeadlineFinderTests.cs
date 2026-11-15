using OppsView.Api.Features.EmailImport.Parsing;

namespace OppsView.Api.Tests.EmailImport;

public class NoReplyDeadlineFinderTests
{
    // A Thursday.
    private static readonly DateOnly EmailDate = new(2026, 11, 12);

    private static DateOnly? Find(string text) =>
        NoReplyDeadlineFinder.Find(EmailText.Sentences(EmailText.Clean(text)), EmailDate);

    [Theory]
    [InlineData("If you do not hear from us within 2 weeks, please consider your application unsuccessful.", 14)]
    [InlineData("Should you not hear from us within two (2) weeks, kindly consider your application as unsuccessful.", 14)]
    [InlineData("If you have not been contacted within 14 days, please regard your application as unsuccessful.", 14)]
    [InlineData("If you haven't heard back within 30 days of the closing date, please consider your application unsuccessful.", 30)]
    [InlineData("Applicants who have not received a response within three weeks should consider their application unsuccessful.", 21)]
    [InlineData("If you don't hear from us in the next 2-3 weeks, please assume your application was not successful.", 21)]
    [InlineData("If no feedback has been received within a month, please consider your application as unsuccessful.", 30)]
    [InlineData("If you do not hear from us within 2 weeks. Please then consider your application unsuccessful.", 14)]
    public void Works_out_the_deadline(string text, int days) =>
        Assert.Equal(EmailDate.AddDays(days), Find(text));

    [Fact]
    public void Working_days_skip_weekends()
    {
        // 5 working days after Thursday 12 Nov: Fri 13, Mon 16, Tue 17, Wed 18, Thu 19.
        Assert.Equal(new DateOnly(2026, 11, 19),
            Find("If you have not heard from us within 5 working days, please consider your application unsuccessful."));
    }

    [Theory]
    // The Discovery confirmation: unsuccessful is mentioned, but no time limit.
    [InlineData("If you do not meet all the requirements, we will let you know that your application was unsuccessful this time around.")]
    // A time limit, but no "consider it unsuccessful".
    [InlineData("You will hear from us within 2 weeks.")]
    [InlineData("We aim to respond within 10 working days.")]
    // An actual rejection is not a deadline.
    [InlineData("Unfortunately your application has been unsuccessful.")]
    public void Ignores_emails_without_a_no_reply_rule(string text) => Assert.Null(Find(text));
}

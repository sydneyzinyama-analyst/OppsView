using OppsView.Api.Features.EmailImport.Parsing;

namespace OppsView.Api.Tests.EmailImport;

public class SenderDomainTests
{
    [Theory]
    [InlineData("careers@discovery.co.za", "Discovery")]
    [InlineData("noreply@mail.cartrack.com", "Cartrack")]
    [InlineData("talent@bbd.co.za", "BBD")]
    [InlineData("jobs@cape-union-mart.co.za", "Cape Union Mart")]
    [InlineData("Recruitment <hr@futuresoft.co.za>", "Futuresoft")]
    [InlineData("noreply@acme.wd3.myworkdayjobs.com", "Acme")]
    [InlineData("recruit@takealot.com", "Takealot")]
    public void Takes_the_company_from_the_senders_domain(string address, string expected) =>
        Assert.Equal(expected, SenderDomain.CompanyFrom(address));

    [Theory]
    [InlineData("noreply@pnet.co.za")]                 // job site
    [InlineData("alerts@mail.indeed.com")]             // job site
    [InlineData("jobs-noreply@linkedin.com")]          // job site
    [InlineData("someone@gmail.com")]                  // personal mail
    [InlineData("notifications@myworkday.com")]        // hiring system, no company in it
    [InlineData("bounce@sendgrid.net")]                // bulk-email service
    [InlineData("noreply@mailer.example")]             // generic label
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_domains_that_are_not_the_employer(string? address) =>
        Assert.Null(SenderDomain.CompanyFrom(address));

    [Theory]
    [InlineData("noreply@pnet.co.za", SenderDomain.Kind.JobBoard)]
    [InlineData("x@careers.indeed.com", SenderDomain.Kind.JobBoard)]
    [InlineData("x@successfactors.eu", SenderDomain.Kind.HiringSystem)]
    [InlineData("x@outlook.com", SenderDomain.Kind.PersonalMail)]
    [InlineData("x@discovery.co.za", SenderDomain.Kind.Company)]
    public void Classifies_senders(string address, SenderDomain.Kind expected) =>
        Assert.Equal(expected, SenderDomain.Classify(address));
}

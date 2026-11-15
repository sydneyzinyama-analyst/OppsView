using System.Net.Mail;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// The email parser: cleans the text, classifies the email, then finds the
/// company, role and platform. Plain rules rather than machine learning, so
/// every decision can be explained and covered by a test.
///
/// Built from real confirmation and rejection emails (see the samples in the
/// test project). Companies write their own emails, so the rules look for
/// common phrasing rather than one fixed template per job site.
/// </summary>
public sealed class RuleBasedEmailParser : IEmailParser
{
    public ParsedEmail Parse(IncomingEmail email)
    {
        var text = EmailText.Clean(email.Body);
        var sentences = EmailText.Sentences(text);
        var subject = EmailText.Clean(email.Subject);

        var (senderName, senderAddress) = SplitSender(email.Sender);

        var kind = EmailClassifier.Classify(subject, sentences, SenderDomain.IsJobBoard(senderAddress));
        if (kind is EmailKind.NotRelevant) return new ParsedEmail(kind);

        var (company, role) = DetailsExtractor.Extract(subject, sentences, senderName, senderAddress);
        var platform = PlatformDetector.Detect(senderAddress, text);
        var deadline = NoReplyDeadlineFinder.Find(sentences, DateOnly.FromDateTime(email.ReceivedAt));

        return new ParsedEmail(kind, platform, company, role, deadline);
    }

    /// <summary>"Discovery Careers &lt;careers@discovery.co.za&gt;" -> ("Discovery Careers", "careers@discovery.co.za").</summary>
    internal static (string? Name, string Address) SplitSender(string? sender)
    {
        // A missing or malformed From header must not stop a whole sync.
        if (string.IsNullOrWhiteSpace(sender)) return (null, "");
        try
        {
            var address = new MailAddress(sender);
            return (string.IsNullOrWhiteSpace(address.DisplayName) ? null : address.DisplayName, address.Address);
        }
        catch (FormatException)
        {
            return (null, sender.Trim());
        }
    }
}

namespace OppsView.Api.Domain;

/// <summary>
/// An email as it arrives from the mail provider, reduced to what the parsers
/// need. It exists only in memory while being processed and is never saved.
/// </summary>
/// <param name="MessageId">The provider's unique id for the message.</param>
/// <param name="Sender">The From header, e.g. "Pnet &lt;noreply@pnet.co.za&gt;".</param>
/// <param name="Body">Plain-text body (HTML converted to text before parsing).</param>
public sealed record IncomingEmail(
    string MessageId,
    string Sender,
    string Subject,
    string Body,
    DateTime ReceivedAt);

/// <summary>What a parser understood from an email.</summary>
public sealed record ParsedEmail(
    EmailKind Kind,
    JobPlatform? Platform = null,
    string? Company = null,
    string? Role = null)
{
    public static readonly ParsedEmail Unknown = new(EmailKind.Unknown);
}

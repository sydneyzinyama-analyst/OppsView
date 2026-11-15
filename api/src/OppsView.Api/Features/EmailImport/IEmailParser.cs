using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport;

/// <summary>
/// Reads one email and says what it means (confirmation, rejection, ...) and,
/// where it can, the company, role and platform. Parsers never touch the
/// database; <see cref="EmailImporter"/> decides what to do with the result.
/// </summary>
public interface IEmailParser
{
    ParsedEmail Parse(IncomingEmail email);
}

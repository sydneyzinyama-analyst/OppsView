namespace OppsView.Api.Domain;

/// <summary>
/// Thrown when a domain rule is broken (e.g. an empty company name or a
/// status change that isn't allowed). <see cref="Field"/> names the input
/// that caused it so the API can return a per-field validation error.
/// </summary>
public sealed class DomainException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

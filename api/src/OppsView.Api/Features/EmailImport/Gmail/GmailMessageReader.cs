using System.Text;
using System.Text.Json;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Gmail;

/// <summary>
/// Converts a Gmail API message (format=full) into an <see cref="IncomingEmail"/>.
///
/// Gmail returns emails as a tree of MIME parts (plain text, HTML,
/// attachments...), with each body base64url-encoded. We take the plain-text
/// part if there is one, otherwise the HTML part (the parser strips the tags).
/// </summary>
public static class GmailMessageReader
{
    public static IncomingEmail Read(JsonElement message)
    {
        var id = message.GetProperty("id").GetString()!;
        var payload = message.GetProperty("payload");

        string Header(string name) =>
            payload.TryGetProperty("headers", out var headers)
                ? headers.EnumerateArray()
                    .FirstOrDefault(h => string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                    is { ValueKind: JsonValueKind.Object } h ? h.GetProperty("value").GetString() ?? "" : ""
                : "";

        // internalDate = when Gmail received it, in milliseconds since 1970.
        var received = message.TryGetProperty("internalDate", out var date) && long.TryParse(date.GetString(), out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
            : DateTime.UtcNow;

        var body = FindBody(payload, "text/plain") ?? FindBody(payload, "text/html") ?? "";
        return new IncomingEmail(id, Header("From"), Header("Subject"), body, received);
    }

    private static string? FindBody(JsonElement part, string mimeType)
    {
        var type = part.TryGetProperty("mimeType", out var t) ? t.GetString() : null;
        if (string.Equals(type, mimeType, StringComparison.OrdinalIgnoreCase)
            && part.TryGetProperty("body", out var body)
            && body.TryGetProperty("data", out var data)
            && data.GetString() is { Length: > 0 } encoded)
        {
            return DecodeBase64Url(encoded);
        }

        if (part.TryGetProperty("parts", out var children))
        {
            foreach (var child in children.EnumerateArray())
                if (FindBody(child, mimeType) is { } found) return found;
        }
        return null;
    }

    internal static string DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}

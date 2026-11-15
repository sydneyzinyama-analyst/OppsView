using System.Text;
using System.Text.Json;
using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.EmailImport;

public class GmailMessageReaderTests
{
    private static string B64Url(string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static JsonElement Message(object payload) =>
        JsonSerializer.SerializeToElement(new { id = "abc", internalDate = "1794733200000", payload });

    private static readonly object[] Headers =
    [
        new { name = "From", value = "Acme Careers <jobs@acme.co.za>" },
        new { name = "Subject", value = "Your application" },
    ];

    [Fact]
    public void Prefers_the_plain_text_part_of_a_multipart_email()
    {
        var email = GmailMessageReader.Read(Message(new
        {
            mimeType = "multipart/alternative",
            headers = Headers,
            parts = new object[]
            {
                new { mimeType = "text/html", body = new { data = B64Url("<p>HTML version</p>") } },
                new { mimeType = "text/plain", body = new { data = B64Url("Plain version: café ✓") } },
            },
        }));

        Assert.Equal("abc", email.MessageId);
        Assert.Equal("Acme Careers <jobs@acme.co.za>", email.Sender);
        Assert.Equal("Your application", email.Subject);
        Assert.Equal("Plain version: café ✓", email.Body);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1794733200000).UtcDateTime, email.ReceivedAt);
    }

    [Fact]
    public void Falls_back_to_HTML_and_finds_nested_parts()
    {
        var email = GmailMessageReader.Read(Message(new
        {
            mimeType = "multipart/mixed",
            headers = Headers,
            parts = new object[]
            {
                new
                {
                    mimeType = "multipart/related",
                    parts = new object[] { new { mimeType = "text/html", body = new { data = B64Url("<p>Only HTML</p>") } } },
                },
                new { mimeType = "application/pdf", body = new { attachmentId = "x" } },
            },
        }));

        Assert.Equal("<p>Only HTML</p>", email.Body);
    }

    [Fact]
    public void Missing_headers_become_empty_strings()
    {
        var email = GmailMessageReader.Read(Message(new { mimeType = "text/plain", body = new { data = B64Url("hi") } }));
        Assert.Equal(("", "", "hi"), (email.Sender, email.Subject, email.Body));
    }
}

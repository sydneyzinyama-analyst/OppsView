using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OppsView.Api.Features.EmailImport.Gmail;

namespace OppsView.Api.Tests.EmailImport;

public class GoogleOAuthClientTests
{
    [Fact]
    public void The_consent_url_asks_for_read_only_Gmail_offline_access_with_PKCE()
    {
        var client = new GoogleOAuthClient(new HttpClient(), Options.Create(new GoogleOptions
        {
            ClientId = "client-123",
            ClientSecret = "secret",
        }));

        var url = new Uri(client.BuildAuthorizationUrl("the-state", "the-challenge"));
        var q = QueryHelpers.ParseQuery(url.Query);

        Assert.Equal("accounts.google.com", url.Host);
        Assert.Equal("client-123", q["client_id"]);
        Assert.Equal("https://www.googleapis.com/auth/gmail.readonly", q["scope"]);
        Assert.Equal("http://localhost:5226/email/google/callback", q["redirect_uri"]);
        Assert.Equal("offline", q["access_type"]);
        Assert.Equal(("the-state", "the-challenge", "S256"), (q["state"].ToString(), q["code_challenge"].ToString(), q["code_challenge_method"].ToString()));
        Assert.DoesNotContain("secret", url.ToString()); // the client secret never goes to the browser
    }
}

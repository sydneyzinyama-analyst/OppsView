using OppsView.Api.Features.Auth;

namespace OppsView.Api.Tests.Auth;

public class JwtLifetimeTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    [Fact]
    public void Valid_between_not_before_and_expiry() =>
        Assert.True(JwtLifetime.IsValid(Now.AddMinutes(-1), Now.AddMinutes(1), Now, Skew));

    [Fact]
    public void Expired_tokens_are_invalid() =>
        Assert.False(JwtLifetime.IsValid(Now.AddHours(-9), Now.AddMinutes(-1), Now, Skew));

    [Fact]
    public void Tokens_from_the_future_are_invalid() =>
        Assert.False(JwtLifetime.IsValid(Now.AddMinutes(5), Now.AddHours(8), Now, Skew));

    [Fact]
    public void Clock_skew_is_tolerated() =>
        Assert.True(JwtLifetime.IsValid(Now.AddSeconds(10), Now.AddSeconds(-10), Now, Skew));

    [Fact]
    public void Tokens_without_expiry_are_invalid() =>
        Assert.False(JwtLifetime.IsValid(Now, null, Now, Skew));
}

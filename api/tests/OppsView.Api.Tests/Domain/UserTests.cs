namespace OppsView.Api.Tests.Domain;

public class UserTests
{
    [Fact]
    public void Create_normalises_the_email()
    {
        var user = User.Create("  Sydney@Example.COM ", DateTime.UtcNow);
        Assert.Equal("sydney@example.com", user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_an_email(string email)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create(email, DateTime.UtcNow));
        Assert.Equal("Email", ex.Field);
    }
}

namespace OppsView.Api.Tests.Domain;

public class CompanyNameTests
{
    [Theory]
    [InlineData("Acme (Pty) Ltd", "Acme")]
    [InlineData("ACME LTD.", "acme")]
    [InlineData("  Acme   Inc ", "acme")]
    [InlineData("Ernst & Young", "Ernst and Young")]
    [InlineData("Nestlé", "Nestle")]
    [InlineData("Standard Bank Group", "Standard Bank")]
    public void Treats_spelling_and_legal_suffix_variants_as_the_same(string a, string b) =>
        Assert.True(CompanyName.AreSame(a, b));

    [Theory]
    [InlineData("Acme", "Acme Labs")]
    [InlineData("Discovery", "Discovery Health")]
    [InlineData("Capitec", "Absa")]
    public void Different_companies_do_not_match(string a, string b) =>
        Assert.False(CompanyName.AreSame(a, b));

    [Fact]
    public void A_name_made_only_of_a_suffix_word_is_kept()
    {
        Assert.Equal("group", CompanyName.Normalize("Group"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_names_never_match(string? name)
    {
        Assert.False(CompanyName.AreSame(name, name));
    }
}

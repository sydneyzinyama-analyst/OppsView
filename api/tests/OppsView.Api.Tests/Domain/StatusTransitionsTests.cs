namespace OppsView.Api.Tests.Domain;

public class StatusTransitionsTests
{
    [Theory]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Viewed)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Interview)] // skipping a stage is fine
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Rejected)]
    public void Forward_moves_are_allowed(ApplicationStatus from, ApplicationStatus to) =>
        Assert.True(StatusTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Viewed)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Applied)]
    public void Moving_backwards_is_not_allowed(ApplicationStatus from, ApplicationStatus to) =>
        Assert.False(StatusTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Rejected)]
    public void Offer_and_Rejected_are_final(ApplicationStatus final) =>
        Assert.Empty(StatusTransitions.AllowedFrom(final));

    [Fact]
    public void Every_status_has_a_rule()
    {
        // Guards against adding an enum member and forgetting the rules
        // (AllowedFrom would throw KeyNotFoundException at runtime).
        foreach (var status in Enum.GetValues<ApplicationStatus>())
            _ = StatusTransitions.AllowedFrom(status);
    }

    [Fact]
    public void No_status_can_transition_to_itself()
    {
        foreach (var status in Enum.GetValues<ApplicationStatus>())
            Assert.False(StatusTransitions.IsAllowed(status, status));
    }
}

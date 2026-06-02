using Frigorino.Features.Notifications;

namespace Frigorino.Test.Features;

public class MaintenanceKeyTests
{
    [Fact]
    public void Matches_WhenEqual()
    {
        Assert.True(MaintenanceKey.Matches("secret-abc", "secret-abc"));
    }

    [Fact]
    public void DoesNotMatch_WhenDifferent()
    {
        Assert.False(MaintenanceKey.Matches("secret-abc", "secret-xyz"));
    }

    [Fact]
    public void DoesNotMatch_WhenProvidedNullOrEmpty()
    {
        Assert.False(MaintenanceKey.Matches(null, "secret-abc"));
        Assert.False(MaintenanceKey.Matches("", "secret-abc"));
    }

    [Fact]
    public void DoesNotMatch_WhenExpectedUnconfigured()
    {
        // An unconfigured token must never accept any key.
        Assert.False(MaintenanceKey.Matches("anything", ""));
    }
}

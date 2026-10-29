using SecureVoting.Application.Configuration;
using Xunit;

namespace SecureVoting.UnitTests;

public sealed class SeedPolicyTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void DemoSeedingIsAllowedOnlyInLocalTestEnvironments(string environmentName)
    {
        SeedPolicy.EnsureAllowed(true, environmentName);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("PreProduction")]
    public void DemoSeedingIsRejectedElsewhere(string environmentName)
    {
        Assert.Throws<InvalidOperationException>(() => SeedPolicy.EnsureAllowed(true, environmentName));
    }

    [Fact]
    public void ProductionWithoutSeedEnabledIsAllowed()
    {
        SeedPolicy.EnsureAllowed(false, "Production");
    }
}

using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayEnvironmentTests
{
    [Fact]
    public void AddScalewayEnvironment_RegistersResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment("production");

        environment.Resource.Name.Should().Be("production");
        environment.Resource.CredentialConfig.DefaultRegion.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayEnvironment_AcceptsCustomRegionAndProject()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment(
            "staging",
            region: ScalewayRegion.NlAms,
            projectId: "my-project"
        );

        environment.Resource.CredentialConfig.DefaultRegion.Should().Be(ScalewayRegion.NlAms);
        environment.Resource.CredentialConfig.DefaultProjectId.Should().Be("my-project");
    }
}

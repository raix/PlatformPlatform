using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayDeploymentTests
{
    [Fact]
    public void AddScalewayEnvironment_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment("production");

        environment.Resource.Name.Should().Be("production");
        environment.Resource.CredentialConfig.DefaultRegion.Should().Be(ScalewayRegion.FrPar);
        environment.Resource.DefaultsProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddScalewayEnvironment_AcceptsCustomRegionAndProject()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment("staging", ScalewayRegion.NlAms, "my-project");

        environment.Resource.CredentialConfig.DefaultRegion.Should().Be(ScalewayRegion.NlAms);
        environment.Resource.CredentialConfig.DefaultProjectId.Should().Be("my-project");
    }

    [Fact]
    public void DefaultsProvider_PrivateNetwork_HasCorrectName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddScalewayEnvironment("staging");

        var privateNetwork = environment.Resource.DefaultsProvider.PrivateNetwork;

        privateNetwork.Name.Should().Be("staging-network");
        privateNetwork.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void DefaultsProvider_Registry_HasCorrectEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddScalewayEnvironment("production", ScalewayRegion.NlAms);

        var registry = environment.Resource.DefaultsProvider.Registry;

        registry.Name.Should().Be("production-registry");
        registry.Endpoint.Should().Be("rg.nl-ams.scw.cloud/production-registry");
    }

    [Fact]
    public void DefaultsProvider_ContainerNamespace_HasCorrectName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddScalewayEnvironment("production");

        var containerNamespace = environment.Resource.DefaultsProvider.ContainerNamespace;

        containerNamespace.Name.Should().Be("production-containers");
    }

    [Fact]
    public void DefaultsProvider_LazyInitializes_ReturnsSameInstance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddScalewayEnvironment("production");

        var network1 = environment.Resource.DefaultsProvider.PrivateNetwork;
        var network2 = environment.Resource.DefaultsProvider.PrivateNetwork;

        network1.Should().BeSameAs(network2);
    }
}

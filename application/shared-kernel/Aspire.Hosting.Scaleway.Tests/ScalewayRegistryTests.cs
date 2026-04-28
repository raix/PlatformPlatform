using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRegistryTests
{
    [Fact]
    public void AddScalewayRegistry_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistry("my-registry");

        registry.Resource.Name.Should().Be("my-registry");
        registry.Resource.Region.Should().Be(ScalewayRegion.FrPar);
        registry.Resource.IsPublic.Should().BeFalse();
        registry.Resource.Endpoint.Should().Be("rg.fr-par.scw.cloud/my-registry");
    }

    [Fact]
    public void AddScalewayRegistry_AcceptsCustomRegion()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistry("my-registry", region: ScalewayRegion.NlAms);

        registry.Resource.Region.Should().Be(ScalewayRegion.NlAms);
        registry.Resource.Endpoint.Should().Be("rg.nl-ams.scw.cloud/my-registry");
    }

    [Fact]
    public void WithPublicAccess_MakesRegistryPublic()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistry("my-registry")
            .WithPublicAccess();

        registry.Resource.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void WithDescription_SetsDescription()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistry("my-registry")
            .WithDescription("Production container images");

        registry.Resource.Description.Should().Be("Production container images");
    }
}

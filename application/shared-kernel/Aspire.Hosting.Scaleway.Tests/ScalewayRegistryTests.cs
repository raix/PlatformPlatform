using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRegistryTests
{
    [Fact]
    public void AddScalewayRegistryNamespace_CreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistryNamespace("my-registry");

        registry.Resource.Name.Should().Be("my-registry");
        registry.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayRegistryNamespace_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var registry = builder.AddScalewayRegistryNamespace("my-registry");
        registry.Resource.Description = "Production images";
        registry.Resource.IsPublic = true;
        registry.Resource.Region = ScalewayRegion.NlAms;

        registry.Resource.Description.Should().Be("Production images");
        registry.Resource.IsPublic.Should().BeTrue();
        registry.Resource.Region.Should().Be(ScalewayRegion.NlAms);
    }
}

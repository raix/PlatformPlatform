using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayServerlessContainerTests
{
    [Fact]
    public void AddScalewayContainerNamespace_CreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ns = builder.AddScalewayContainerNamespace("my-namespace");

        ns.Resource.Name.Should().Be("my-namespace");
        ns.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayContainerContainer_CreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var container = builder.AddScalewayContainerContainer("my-api");

        container.Resource.Name.Should().Be("my-api");
        container.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayContainerContainer_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var container = builder.AddScalewayContainerContainer("my-api");
        container.Resource.MemoryLimitBytes = 512 * 1024 * 1024;
        container.Resource.MinScale = 1;
        container.Resource.MaxScale = 10;
        container.Resource.Port = 8080;
        container.Resource.Image = "my-registry/my-api:latest";

        container.Resource.MemoryLimitBytes.Should().Be(512 * 1024 * 1024);
        container.Resource.MinScale.Should().Be(1);
        container.Resource.MaxScale.Should().Be(10);
        container.Resource.Port.Should().Be(8080);
        container.Resource.Image.Should().Be("my-registry/my-api:latest");
    }
}

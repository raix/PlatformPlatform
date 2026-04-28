using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayServerlessContainerTests
{
    [Fact]
    public void AddScalewayServerlessNamespace_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        ns.Resource.Name.Should().Be("my-namespace");
        ns.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayServerlessNamespace_AcceptsCustomRegion()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ns = builder.AddScalewayServerlessNamespace("my-namespace", region: ScalewayRegion.PlWaw);

        ns.Resource.Region.Should().Be(ScalewayRegion.PlWaw);
    }

    [Fact]
    public void AddContainer_CreatesContainerWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api");

        container.Resource.Name.Should().Be("my-api");
        container.Resource.Parent.Should().BeSameAs(ns.Resource);
        container.Resource.MemoryLimitMb.Should().Be(256);
        container.Resource.CpuLimitMillicores.Should().Be(140);
        container.Resource.MinScale.Should().Be(0);
        container.Resource.MaxScale.Should().Be(20);
        container.Resource.MaxConcurrency.Should().Be(50);
        container.Resource.TimeoutSeconds.Should().Be(300);
        container.Resource.Port.Should().Be(8080);
        container.Resource.Privacy.Should().Be(ScalewayContainerPrivacy.Public);
    }

    [Fact]
    public void AddContainer_AcceptsImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api", image: "rg.fr-par.scw.cloud/my-registry/my-api:latest");

        container.Resource.RegistryImage.Should().Be("rg.fr-par.scw.cloud/my-registry/my-api:latest");
    }

    [Fact]
    public void WithMemory_SetsMemoryLimit()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithMemory(512);

        container.Resource.MemoryLimitMb.Should().Be(512);
    }

    [Fact]
    public void WithScaling_SetsScalingParameters()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithScaling(minScale: 1, maxScale: 10, maxConcurrency: 100);

        container.Resource.MinScale.Should().Be(1);
        container.Resource.MaxScale.Should().Be(10);
        container.Resource.MaxConcurrency.Should().Be(100);
    }

    [Fact]
    public void WithPrivacy_SetsPrivacy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithPrivacy(ScalewayContainerPrivacy.Private);

        container.Resource.Privacy.Should().Be(ScalewayContainerPrivacy.Private);
    }

    [Fact]
    public void WithHealthCheck_SetsHealthCheckPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithHealthCheck("/health");

        container.Resource.HealthCheckPath.Should().Be("/health");
    }

    [Fact]
    public void WithTimeout_SetsTimeout()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithTimeout(60);

        container.Resource.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void FluentApi_ChainsCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ns = builder.AddScalewayServerlessNamespace("my-namespace");

        var container = ns.AddContainer("my-api")
            .WithMemory(1024)
            .WithScaling(minScale: 2, maxScale: 50, maxConcurrency: 200)
            .WithPrivacy(ScalewayContainerPrivacy.Private)
            .WithHealthCheck("/healthz")
            .WithTimeout(120);

        container.Resource.MemoryLimitMb.Should().Be(1024);
        container.Resource.MinScale.Should().Be(2);
        container.Resource.MaxScale.Should().Be(50);
        container.Resource.MaxConcurrency.Should().Be(200);
        container.Resource.Privacy.Should().Be(ScalewayContainerPrivacy.Private);
        container.Resource.HealthCheckPath.Should().Be("/healthz");
        container.Resource.TimeoutSeconds.Should().Be(120);
    }
}

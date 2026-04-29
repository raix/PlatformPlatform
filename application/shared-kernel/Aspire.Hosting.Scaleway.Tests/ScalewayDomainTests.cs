using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayDomainTests
{
    [Fact]
    public void WithCustomDomain_ShouldAttachAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("my-api", "my-image");

        container.WithCustomDomain("app.example.com");

        var annotation = container.Resource.Annotations.OfType<ScalewayCustomDomainAnnotation>().SingleOrDefault();
        annotation.Should().NotBeNull();
        annotation!.Domain.Should().Be("app.example.com");
        annotation.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void WithCustomDomain_ShouldAcceptCustomRegion()
    {
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("my-api", "my-image");

        container.WithCustomDomain("app.example.com", region: ScalewayRegion.NlAms);

        var annotation = container.Resource.Annotations.OfType<ScalewayCustomDomainAnnotation>().Single();
        annotation.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void WithCustomDomain_MultipleDomainsCanBeAttached()
    {
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("my-api", "my-image");

        container.WithCustomDomain("app.example.com");
        container.WithCustomDomain("staging.example.com");

        var annotations = container.Resource.Annotations.OfType<ScalewayCustomDomainAnnotation>().ToList();
        annotations.Should().HaveCount(2);
        annotations.Select(a => a.Domain).Should().Contain("app.example.com");
        annotations.Select(a => a.Domain).Should().Contain("staging.example.com");
    }
}

using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayTemTests
{
    [Fact]
    public void AddScalewayTemDomain_CreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tem = builder.AddScalewayTemDomain("my-email");

        tem.Resource.Name.Should().Be("my-email");
        tem.Resource.DomainName.Should().Be(string.Empty);
        tem.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayTemDomain_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tem = builder.AddScalewayTemDomain("my-email");
        tem.Resource.DomainName = "example.com";
        tem.Resource.AcceptTos = true;

        tem.Resource.DomainName.Should().Be("example.com");
        tem.Resource.AcceptTos.Should().BeTrue();
    }

    [Fact]
    public void RunAsMailpitContainer_ShouldNotThrow()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();

        // Act
        var act = () => builder.AddScalewayTemDomain("my-email")
            .RunAsMailpitContainer();

        // Assert
        act.Should().NotThrow();
    }
}

using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayCockpitTests
{
    [Fact]
    public void WithScalewayCockpit_ShouldApplyToContainerResource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("test-api", "my-image");

        // Act - should not throw
        container.WithScalewayCockpit(ScalewayRegion.FrPar, "my-data-source-id");

        // Assert
        container.Resource.Name.Should().Be("test-api");
    }

    [Fact]
    public void WithScalewayCockpit_ShouldAcceptDifferentRegions()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("test-api", "my-image");

        // Act - should not throw for any region
        container.WithScalewayCockpit(ScalewayRegion.NlAms, "ds-123");
        container.WithScalewayCockpit(ScalewayRegion.PlWaw, "ds-456");

        // Assert
        container.Resource.Name.Should().Be("test-api");
    }
}

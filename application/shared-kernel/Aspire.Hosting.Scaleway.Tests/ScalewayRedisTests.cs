using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRedisTests
{
    [Fact]
    public void AddScalewayRedisCluster_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache");

        redis.Resource.Name.Should().Be("my-cache");
        redis.Resource.Version.Should().Be(string.Empty);
        redis.Resource.NodeType.Should().Be(string.Empty);
        redis.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void AddScalewayRedisCluster_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache");
        redis.Resource.Version = "7.0";
        redis.Resource.NodeType = "RED1-MICRO";
        redis.Resource.ClusterSize = 3;
        redis.Resource.Region = ScalewayRegion.NlAms;

        redis.Resource.Version.Should().Be("7.0");
        redis.Resource.NodeType.Should().Be("RED1-MICRO");
        redis.Resource.ClusterSize.Should().Be(3);
        redis.Resource.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void AddScalewayRedisCluster_ImplementsIScalewayResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache");

        redis.Resource.Should().BeAssignableTo<IScalewayResource>();
    }

    [Fact]
    public void RunAsRedisContainer_ShouldAddConnectionStringRedirectAnnotation()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();

        // Act
        var redis = builder.AddScalewayRedisCluster("my-cache")
            .RunAsRedisContainer();

        // Assert
        redis.Resource.Annotations.OfType<ConnectionStringRedirectAnnotation>().Should().ContainSingle();
    }

    [Fact]
    public void RunAsRedisContainer_ConfiguresInnerContainer()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var configured = false;

        // Act
        builder.AddScalewayRedisCluster("my-cache")
            .RunAsRedisContainer(c =>
                {
                    configured = true;
                    c.WithLifetime(ContainerLifetime.Persistent);
                }
            );

        // Assert
        configured.Should().BeTrue();
    }
}

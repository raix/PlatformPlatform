using Aspire.Hosting.Scaleway;
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
        redis.Resource.Version.Should().Be("7.0");
        redis.Resource.NodeType.Should().Be("RED1-MICRO");
        redis.Resource.ClusterSize.Should().Be(1);
        redis.Resource.ClusterEnabled.Should().BeFalse();
        redis.Resource.Zone.Should().Be(ScalewayZone.FrPar1);
        redis.Resource.PasswordParameter.Should().NotBeNull();
    }

    [Fact]
    public void AddScalewayRedisCluster_AcceptsCustomVersionAndNodeType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache", version: "6.2", nodeType: "RED1-S");

        redis.Resource.Version.Should().Be("6.2");
        redis.Resource.NodeType.Should().Be("RED1-S");
    }

    [Fact]
    public void WithZone_SetsZone()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache")
            .WithZone(ScalewayZone.NlAms1);

        redis.Resource.Zone.Should().Be(ScalewayZone.NlAms1);
    }

    [Fact]
    public void WithClusterSize_SetsClusterSize()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache")
            .WithClusterSize(3);

        redis.Resource.ClusterSize.Should().Be(3);
    }

    [Fact]
    public void WithClusterEnabled_EnablesClusterMode()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache")
            .WithClusterEnabled();

        redis.Resource.ClusterEnabled.Should().BeTrue();
    }
}

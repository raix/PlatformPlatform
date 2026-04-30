using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayPublishTests
{
    [Fact]
    public void PublishAsScalewayRdb_AttachesAnnotationWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .PublishAsScalewayRdb();

        var annotation = rdb.Resource.Annotations.OfType<IScalewayPublishTargetAnnotation>().SingleOrDefault();
        annotation.Should().NotBeNull();
    }

    [Fact]
    public void PublishAsScalewayRdb_CallbackCustomizesConfig()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .PublishAsScalewayRdb(config =>
                {
                    config.Engine = "PostgreSQL-16";
                    config.NodeType = "DB-GP-XL";
                    config.IsHaCluster = true;
                    config.VolumeSizeInGb = 100;
                    config.Region = ScalewayRegion.NlAms;
                }
            );

        var annotation = rdb.Resource.Annotations.OfType<PublishAsScalewayRdbAnnotation>().Single();
        annotation.Config.Engine.Should().Be("PostgreSQL-16");
        annotation.Config.NodeType.Should().Be("DB-GP-XL");
        annotation.Config.IsHaCluster.Should().BeTrue();
        annotation.Config.VolumeSizeInGb.Should().Be(100);
        annotation.Config.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void PublishAsScalewayRedis_AttachesAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redis = builder.AddScalewayRedisCluster("my-cache")
            .PublishAsScalewayRedis(config =>
                {
                    config.NodeType = "RED1-M";
                    config.ClusterSize = 3;
                    config.Zone = ScalewayZone.FrPar2;
                }
            );

        var annotation = redis.Resource.Annotations.OfType<PublishAsScalewayRedisAnnotation>().Single();
        annotation.Config.NodeType.Should().Be("RED1-M");
        annotation.Config.ClusterSize.Should().Be(3);
        annotation.Config.Zone.Should().Be(ScalewayZone.FrPar2);
    }

    [Fact]
    public void PublishAsScalewayObjectStorage_AttachesAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var storage = builder.AddScalewayObjectStorage("my-storage")
            .PublishAsScalewayObjectStorage(config =>
                {
                    config.Acl = ScalewayBucketAcl.PublicRead;
                    config.Versioning = true;
                    config.Region = ScalewayRegion.PlWaw;
                }
            );

        var annotation = storage.Resource.Annotations.OfType<PublishAsScalewayObjectStorageAnnotation>().Single();
        annotation.Config.Acl.Should().Be(ScalewayBucketAcl.PublicRead);
        annotation.Config.Versioning.Should().BeTrue();
        annotation.Config.Region.Should().Be(ScalewayRegion.PlWaw);
    }

    [Fact]
    public void PublishAsScalewayRdb_ConfigHasSensibleDefaults()
    {
        var config = new ScalewayRdbPublishConfig();

        config.Engine.Should().Be("PostgreSQL-16");
        config.NodeType.Should().Be("DB-DEV-S");
        config.UserName.Should().Be("admin");
        config.IsHaCluster.Should().BeFalse();
        config.DisableBackup.Should().BeFalse();
        config.VolumeSizeInGb.Should().Be(5);
        config.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void PublishAsScalewayRedis_ConfigHasSensibleDefaults()
    {
        var config = new ScalewayRedisPublishConfig();

        config.Version.Should().Be("7.0");
        config.NodeType.Should().Be("RED1-MICRO");
        config.ClusterSize.Should().Be(1);
        config.TlsEnabled.Should().BeTrue();
        config.Zone.Should().Be(ScalewayZone.FrPar1);
    }

    [Fact]
    public void PublishAsScalewayContainer_ConfigHasSensibleDefaults()
    {
        var config = new ScalewayContainerPublishConfig();

        config.MemoryLimitMb.Should().Be(256);
        config.CpuLimitMillicores.Should().Be(140);
        config.MinScale.Should().Be(0);
        config.MaxScale.Should().Be(20);
        config.MaxConcurrency.Should().Be(50);
        config.TimeoutSeconds.Should().Be(300);
        config.Port.Should().Be(8080);
        config.Privacy.Should().Be("public");
        config.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void EnvironmentSpecificConfiguration_WorksWithCallbacks()
    {
        var builder = DistributedApplication.CreateBuilder();
        var isProduction = false; // Simulate environment check

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .PublishAsScalewayRdb(config =>
                {
                    if (isProduction)
                    {
                        config.NodeType = "DB-GP-XL";
                        config.IsHaCluster = true;
                        config.VolumeSizeInGb = 500;
                    }
                    else
                    {
                        config.NodeType = "DB-DEV-S";
                        config.VolumeSizeInGb = 5;
                    }
                }
            );

        var annotation = rdb.Resource.Annotations.OfType<PublishAsScalewayRdbAnnotation>().Single();
        annotation.Config.NodeType.Should().Be("DB-DEV-S");
        annotation.Config.IsHaCluster.Should().BeFalse();
        annotation.Config.VolumeSizeInGb.Should().Be(5);
    }
}

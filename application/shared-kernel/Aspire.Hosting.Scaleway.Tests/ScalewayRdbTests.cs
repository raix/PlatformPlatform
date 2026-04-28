using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRdbTests
{
    [Fact]
    public void AddScalewayRdbInstance_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");

        rdb.Resource.Name.Should().Be("my-db");
        rdb.Resource.Engine.Should().Be(string.Empty);
        rdb.Resource.NodeType.Should().Be(string.Empty);
        rdb.Resource.UserName.Should().Be(string.Empty);
        rdb.Resource.Region.Should().Be(ScalewayRegion.FrPar);
        rdb.Resource.IsHaCluster.Should().BeFalse();
        rdb.Resource.DisableBackup.Should().BeFalse();
    }

    [Fact]
    public void AddScalewayRdbInstance_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");
        rdb.Resource.Engine = "PostgreSQL-16";
        rdb.Resource.NodeType = "DB-DEV-S";
        rdb.Resource.UserName = "admin";
        rdb.Resource.IsHaCluster = true;
        rdb.Resource.Region = ScalewayRegion.NlAms;

        rdb.Resource.Engine.Should().Be("PostgreSQL-16");
        rdb.Resource.NodeType.Should().Be("DB-DEV-S");
        rdb.Resource.UserName.Should().Be("admin");
        rdb.Resource.IsHaCluster.Should().BeTrue();
        rdb.Resource.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void AddScalewayRdbDatabase_CreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var database = builder.AddScalewayRdbDatabase("app-db");

        database.Resource.Name.Should().Be("app-db");
    }

    [Fact]
    public void AddScalewayRdbInstance_ImplementsIScalewayResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");

        rdb.Resource.Should().BeAssignableTo<IScalewayResource>();
    }
}

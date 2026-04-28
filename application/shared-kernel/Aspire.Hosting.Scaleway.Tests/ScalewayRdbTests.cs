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
        rdb.Resource.Engine.Should().Be("PostgreSQL-16");
        rdb.Resource.NodeType.Should().Be("DB-DEV-S");
        rdb.Resource.UserName.Should().Be("admin");
        rdb.Resource.Region.Should().Be(ScalewayRegion.FrPar);
        rdb.Resource.IsHighAvailabilityCluster.Should().BeFalse();
        rdb.Resource.DisableBackup.Should().BeFalse();
        rdb.Resource.VolumeSizeInGb.Should().Be(5);
        rdb.Resource.PasswordParameter.Should().NotBeNull();
    }

    [Fact]
    public void AddScalewayRdbInstance_AcceptsCustomEngineAndNodeType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db", engine: "MySQL-8", nodeType: "DB-GP-S");

        rdb.Resource.Engine.Should().Be("MySQL-8");
        rdb.Resource.NodeType.Should().Be("DB-GP-S");
    }

    [Fact]
    public void WithRegion_SetsRegion()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithRegion(ScalewayRegion.NlAms);

        rdb.Resource.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void WithHighAvailability_EnablesHighAvailability()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithHighAvailability();

        rdb.Resource.IsHighAvailabilityCluster.Should().BeTrue();
    }

    [Fact]
    public void WithNodeType_SetsNodeType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithNodeType("DB-GP-M");

        rdb.Resource.NodeType.Should().Be("DB-GP-M");
    }

    [Fact]
    public void WithVolumeSize_SetsVolumeSize()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithVolumeSize(50);

        rdb.Resource.VolumeSizeInGb.Should().Be(50);
    }

    [Fact]
    public void WithBackupDisabled_DisablesBackup()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithBackupDisabled();

        rdb.Resource.DisableBackup.Should().BeTrue();
    }

    [Fact]
    public void AddDatabase_CreatesDatabaseResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rdb = builder.AddScalewayRdbInstance("my-db");

        var database = rdb.AddDatabase("app-db", "app_database");

        database.Resource.Name.Should().Be("app-db");
        database.Resource.DatabaseName.Should().Be("app_database");
        database.Resource.Parent.Should().BeSameAs(rdb.Resource);
    }

    [Fact]
    public void AddDatabase_TracksDatabaseInParent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rdb = builder.AddScalewayRdbInstance("my-db");

        rdb.AddDatabase("app-db", "app_database");

        rdb.Resource.Databases.Should().ContainKey("app-db");
        rdb.Resource.Databases["app-db"].Should().Be("app_database");
    }

    [Fact]
    public void AddDatabase_UsesNameAsDatabaseNameWhenNotSpecified()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rdb = builder.AddScalewayRdbInstance("my-db");

        var database = rdb.AddDatabase("app-db");

        database.Resource.DatabaseName.Should().Be("app-db");
    }

    [Fact]
    public void AddScalewayRdbInstance_AcceptsCustomPassword()
    {
        var builder = DistributedApplication.CreateBuilder();
        var password = builder.AddParameter("my-password", secret: true);

        var rdb = builder.AddScalewayRdbInstance("my-db", password: password);

        rdb.Resource.PasswordParameter.Should().BeSameAs(password.Resource);
    }

    [Fact]
    public void FluentApi_ChainsCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db")
            .WithRegion(ScalewayRegion.PlWaw)
            .WithNodeType("DB-GP-L")
            .WithHighAvailability()
            .WithVolumeSize(100)
            .WithBackupDisabled();

        rdb.Resource.Region.Should().Be(ScalewayRegion.PlWaw);
        rdb.Resource.NodeType.Should().Be("DB-GP-L");
        rdb.Resource.IsHighAvailabilityCluster.Should().BeTrue();
        rdb.Resource.VolumeSizeInGb.Should().Be(100);
        rdb.Resource.DisableBackup.Should().BeTrue();
    }
}
